using System.Globalization;
using FitAi.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

// Números em HTML/SVG e em inputs type=number usam ponto; textos formatados usam pt-BR explicitamente (Format).
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<AppClock>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<BearerTokenHandler>();
builder.Services.AddHttpClient<ApiClient>(c =>
    {
        c.BaseAddress = new Uri((builder.Configuration["Api:BaseUrl"] ?? "http://localhost:8080").TrimEnd('/') + "/");
        c.Timeout = TimeSpan.FromSeconds(120); // o Coach AI pode demorar enquanto usa as tools
    })
    .AddHttpMessageHandler<BearerTokenHandler>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "fitai.session";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.LoginPath = "/login";
        o.AccessDeniedPath = "/";
    });
builder.Services.AddAuthorization();

builder.Services.AddAntiforgery(o => o.HeaderName = "RequestVerificationToken");
builder.Services.AddControllersWithViews(o =>
{
    o.Filters.Add<ApiExceptionFilter>();
    o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

// Login: limite por IP do cliente (a API vê só o IP deste servidor, então o limite por pessoa fica aqui).
builder.Services.AddMemoryCache();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("RateLimit:LoginPerMinute", 20),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
});

// X-Forwarded-* só é confiável atrás de um proxy reverso (ReverseProxy:TrustForwardedHeaders=true).
var trustForwardedHeaders = builder.Configuration.GetValue<bool>("ReverseProxy:TrustForwardedHeaders");
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    o.ForwardLimit = 1;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

FitAi.Web.ViewModels.Images.ApiPublicUrl =
    builder.Configuration["Api:PublicUrl"] ?? builder.Configuration["Api:BaseUrl"] ?? "http://localhost:8080";

if (trustForwardedHeaders) app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/erro");
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/erro/{0}");

app.UseRouting();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllerRoute("areas", "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}").WithStaticAssets();
app.MapControllers();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}").WithStaticAssets();

app.Run();
