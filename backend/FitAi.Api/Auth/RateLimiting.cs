using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using FitAi.Api.Options;
using FitAi.Contracts;
using Microsoft.AspNetCore.RateLimiting;

namespace FitAi.Api.Auth;

/// <summary>Limites de requisição: login (por IP), Coach AI (por usuário, minuto e dia) e códigos de convite.</summary>
public static class RateLimiting
{
    public const string Auth = "auth";
    public const string Coach = "coach";
    public const string InviteCode = "invite-code";

    public static IServiceCollection AddFitAiRateLimiting(this IServiceCollection services, RateLimitOptions limits)
    {
        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
                }
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new ErrorResponse("Muitas requisições. Aguarde um pouco e tente de novo.", ErrorCodes.RateLimited), ct);
            };

            // Teto geral: por usuário autenticado (a Web chama a API de um único IP) ou por IP para anônimos.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(UserOrIp(context), _ => Window(limits.GlobalPerMinute, TimeSpan.FromMinutes(1))));

            options.AddPolicy(Auth, context =>
                RateLimitPartition.GetFixedWindowLimiter(ClientIp(context), _ => Window(limits.AuthPerMinute, TimeSpan.FromMinutes(1))));

            options.AddPolicy(InviteCode, context =>
                RateLimitPartition.GetFixedWindowLimiter(UserOrIp(context), _ => Window(limits.InviteCodePer10Minutes, TimeSpan.FromMinutes(10))));

            options.AddPolicy(Coach, new CoachPolicy(limits));
        });
    }

    private static FixedWindowRateLimiterOptions Window(int permits, TimeSpan window) => new()
    {
        PermitLimit = Math.Max(1, permits),
        Window = window,
        QueueLimit = 0,
    };

    private static string ClientIp(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? IPAddress.None.ToString();

    private static string UserOrIp(HttpContext context) =>
        context.User.FindFirstValue(JwtTokenService.SubjectClaim) is { } userId ? "user:" + userId : "ip:" + ClientIp(context);

    /// <summary>Coach AI: limite por minuto e por dia, ambos por usuário.</summary>
    private sealed class CoachPolicy(RateLimitOptions limits) : IRateLimiterPolicy<string>
    {
        public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => null;

        public RateLimitPartition<string> GetPartition(HttpContext httpContext) =>
            RateLimitPartition.Get(UserOrIp(httpContext), _ => new ChainedLimiter(
                new FixedWindowRateLimiter(Window(limits.CoachPerMinute, TimeSpan.FromMinutes(1))),
                new FixedWindowRateLimiter(Window(limits.CoachPerDay, TimeSpan.FromDays(1)))));
    }

    /// <summary>Concede a requisição só se todos os limitadores concederem.</summary>
    private sealed class ChainedLimiter(params RateLimiter[] limiters) : RateLimiter
    {
        public override TimeSpan? IdleDuration => limiters.Select(l => l.IdleDuration).Min();

        public override RateLimiterStatistics? GetStatistics() => limiters[0].GetStatistics();

        protected override RateLimitLease AttemptAcquireCore(int permitCount)
        {
            var leases = new List<RateLimitLease>();
            foreach (var limiter in limiters)
            {
                var lease = limiter.AttemptAcquire(permitCount);
                if (!lease.IsAcquired)
                {
                    foreach (var acquired in leases) acquired.Dispose();
                    return lease;
                }
                leases.Add(lease);
            }
            return new CombinedLease(leases);
        }

        protected override ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken) =>
            ValueTask.FromResult(AttemptAcquireCore(permitCount));

        protected override void Dispose(bool disposing)
        {
            if (disposing) foreach (var limiter in limiters) limiter.Dispose();
            base.Dispose(disposing);
        }
    }

    private sealed class CombinedLease(List<RateLimitLease> leases) : RateLimitLease
    {
        public override bool IsAcquired => true;

        public override IEnumerable<string> MetadataNames => leases.SelectMany(l => l.MetadataNames).Distinct();

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            foreach (var lease in leases)
            {
                if (lease.TryGetMetadata(metadataName, out metadata)) return true;
            }
            metadata = null;
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) foreach (var lease in leases) lease.Dispose();
            base.Dispose(disposing);
        }
    }
}
