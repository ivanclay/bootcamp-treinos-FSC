using System.Text.Json.Serialization;
using FitAi.Api.Ai;
using FitAi.Api.Auth;
using FitAi.Api.Controllers;
using FitAi.Api.Data;
using FitAi.Api.Errors;
using FitAi.Api.Options;
using FitAi.Api.UseCases.Coach;
using FitAi.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ---------- Opções ----------
builder.Services.AddOptions<AuthOptions>().Bind(config.GetSection(AuthOptions.Section))
    .Validate(o => o.JwtSecret.Length >= 32, "Auth:JwtSecret must have at least 32 characters")
    .ValidateOnStart();
builder.Services.Configure<AiOptions>(config.GetSection(AiOptions.Section));
builder.Services.Configure<YouTubeOptions>(config.GetSection(YouTubeOptions.Section));
builder.Services.Configure<PlanOptions>(config.GetSection(PlanOptions.Section));
builder.Services.Configure<PaymentsOptions>(config.GetSection(PaymentsOptions.Section));
builder.Services.Configure<AsaasOptions>(config.GetSection(AsaasOptions.Section));
var rateLimitOptions = config.GetSection(RateLimitOptions.Section).Get<RateLimitOptions>() ?? new RateLimitOptions();
var authOptions = config.GetSection(AuthOptions.Section).Get<AuthOptions>() ?? new AuthOptions();

// ---------- Banco ----------
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured")));

// ---------- Autenticação ----------
var authentication = builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = authOptions.JwtIssuer,
            ValidAudience = authOptions.JwtAudience,
            IssuerSigningKey = JwtTokenService.CreateKey(authOptions.JwtSecret.PadRight(32)),
            ClockSkew = TimeSpan.FromMinutes(1),
        };
        o.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new ErrorResponse("Unauthorized", ErrorCodes.Unauthorized));
            },
        };
    })
    // Cookie temporário que só guarda a identidade do Google entre o callback do provedor e a emissão do código.
    .AddCookie(AuthController.ExternalScheme, o =>
    {
        o.Cookie.Name = "fitai.external";
        o.ExpireTimeSpan = TimeSpan.FromMinutes(10);
    });

if (authOptions.Google.IsConfigured)
{
    authentication.AddGoogle(o =>
    {
        o.SignInScheme = AuthController.ExternalScheme;
        o.ClientId = authOptions.Google.ClientId;
        o.ClientSecret = authOptions.Google.ClientSecret;
        o.CallbackPath = "/auth/google/signin";
        o.ClaimActions.MapJsonKey("picture", "picture");
        o.Events.OnRedirectToAuthorizationEndpoint = context =>
        {
            // Sempre pede para escolher a conta, como no projeto original.
            context.Response.Redirect(context.RedirectUri + "&prompt=select_account");
            return Task.CompletedTask;
        };
        o.Events.OnRemoteFailure = context =>
        {
            // Cancelamento/erro no Google: volta para o callback, que redireciona o cliente com ?error=.
            context.HandleResponse();
            context.Response.Redirect(context.Properties?.RedirectUri ?? "/");
            return Task.CompletedTask;
        };
    });
}

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<JwtTokenService>();

// ---------- Use cases e IA ----------
foreach (var type in typeof(Program).Assembly.GetTypes()
             .Where(t => t is { IsClass: true, IsAbstract: false, Namespace: not null }
                         && t.Namespace.StartsWith("FitAi.Api.UseCases")
                         && t.GetMethod("ExecuteAsync") is not null))
{
    builder.Services.AddScoped(type);
}
builder.Services.AddHttpClient<SearchExerciseVideos>(c => c.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddScoped<AiSettingsStore>();
builder.Services.AddScoped<FitAi.Api.Billing.PlanService>();
builder.Services.AddScoped<FitAi.Api.Billing.PendingPaymentLoader>();

// Provedor de pagamento: Fake (em memória) por padrão; Asaas com Payments:Provider=Asaas.
var paymentsOptions = config.GetSection(PaymentsOptions.Section).Get<PaymentsOptions>() ?? new PaymentsOptions();
if (paymentsOptions.IsFake)
{
    builder.Services.AddSingleton<FitAi.Api.Payments.IPaymentGateway, FitAi.Api.Payments.FakePaymentGateway>();
}
else
{
    builder.Services.AddHttpClient<FitAi.Api.Payments.IPaymentGateway, FitAi.Api.Payments.AsaasPaymentGateway>();
}
builder.Services.AddSingleton<ChatClientFactory>();

// ---------- MVC, erros e OpenAPI ----------
builder.Services.AddControllers(o =>
    {
        o.Filters.Add<ActiveUserFilter>();
        // Respostas nulas (ex.: GET /me sem dados) saem como 200 "null", não 204.
        o.OutputFormatters.RemoveType<Microsoft.AspNetCore.Mvc.Formatters.HttpNoContentOutputFormatter>();
    })
    .AddJsonOptions(o => o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)
    .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = context =>
    {
        var message = string.Join("; ", context.ModelState
            .Where(e => e.Value?.Errors.Count > 0)
            .Select(e => $"{e.Key}: {e.Value!.Errors[0].ErrorMessage}"));
        return new BadRequestObjectResult(new ErrorResponse(message, ErrorCodes.Validation));
    });
builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(o => o.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

builder.Services.AddFitAiRateLimiting(rateLimitOptions);

// X-Forwarded-* só é confiável atrás de um proxy reverso (ReverseProxy:TrustForwardedHeaders=true).
// Sem proxy, aceitar esses headers deixaria qualquer cliente forjar IP (e burlar o rate limit) ou esquema.
var trustForwardedHeaders = config.GetValue<bool>("ReverseProxy:TrustForwardedHeaders");
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    o.ForwardLimit = 1;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

// Segurança: o segredo JWT de desenvolvimento é público (está no repositório); fora de Development ele é recusado.
if (!app.Environment.IsDevelopment() && authOptions.JwtSecret == AuthOptions.DevelopmentJwtSecret)
{
    throw new InvalidOperationException(
        "Auth:JwtSecret está com o valor de desenvolvimento. Defina um segredo próprio (32+ caracteres) fora de Development.");
}
if (!app.Environment.IsDevelopment() && paymentsOptions.IsFake)
{
    app.Logger.LogWarning("Payments:Provider=Fake fora de Development: nenhuma cobrança real será feita.");
}
if (app.Environment.IsDevelopment())
{
    app.Logger.LogWarning("Rodando em Development: login de desenvolvimento e dados de exemplo podem estar ligados. Não exponha esta instância.");
}

if (config.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

// Dados de exemplo (com usuários conhecidos) só em Development.
if (app.Environment.IsDevelopment() && config.GetValue<bool>("Database:SeedDemoData"))
{
    using var scope = app.Services.CreateScope();
    await DemoDataSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), TimeProvider.System);
}

if (trustForwardedHeaders) app.UseForwardedHeaders();
// Fotos de capa (wwwroot/covers), usadas pela Web e pelo App.
app.UseStaticFiles();
app.UseExceptionHandler();
app.UseStatusCodePages(async context =>
{
    var response = context.HttpContext.Response;
    if (response.StatusCode == StatusCodes.Status404NotFound && !response.HasStarted)
    {
        await response.WriteAsJsonAsync(new ErrorResponse("Not found", ErrorCodes.NotFound));
    }
});

// Referência da API só em Development ou quando habilitada explicitamente (Api:ExposeDocs).
if (app.Environment.IsDevelopment() || config.GetValue<bool>("Api:ExposeDocs"))
{
    app.MapOpenApi("/swagger.json");
    app.MapScalarApiReference("/docs", o => o
        .WithTitle("FIT.AI API")
        .WithOpenApiRoutePattern("/swagger.json"));
}

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { message = "FIT.AI API" })).ExcludeFromDescription();
app.MapControllers();

app.Run();

public partial class Program;
