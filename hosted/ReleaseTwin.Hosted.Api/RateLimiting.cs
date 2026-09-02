using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using ReleaseTwin.Hosted.Api.Auth;

namespace ReleaseTwin.Hosted.Api;

/// <summary>
/// security-hardening-pre-pilot D7 (abuse-rate-limiting): per-caller request ceilings on the surface
/// that a hostile or misconfigured client could otherwise flood — the token-authenticated ingest
/// path, the anonymous share-link routes, and the billing webhook. In-process and per-instance:
/// `(warm instances × ceiling)` is still far below what a flood needs to cost real money, it adds no
/// latency and no infra, and CloudFront + WAF stays the documented escalation if abuse outgrows it.
///
/// Sizing (7.3): ceilings are set well above honest use.
///  - Ingest: a CI build uploads ~one report per case. A large suite (~500 cases) run by ~10 parallel
///    jobs with a retry is ~10k uploads in a burst. A token-bucket with a 5,000 burst that refills at
///    500 / 10s (50/s sustained) absorbs that with no 429 and only throttles a single token that
///    stays above 50/s after draining its burst — abuse territory.
///  - Share links: opening a shared page plus its (≤20) screenshots is ~21 requests; 120 / minute
///    per client address is generous for a human and cheap to shed for a scanner.
///  - Billing webhook: Polar sends a handful; 60 / minute per address, rejected before the endpoint
///    body runs (so signature verification is never reached under a flood).
///
/// Config (`RateLimiting:*`) is read lazily per partition from the request's <see cref="IConfiguration"/>
/// — not captured at registration — so an environment override (or a test's in-memory override) is
/// always the fully-composed value. `RateLimiting:Enabled=false` disables every policy.
/// </summary>
public static class RateLimiting
{
    public const string IngestPolicy = "ingest";
    public const string ShareLinkPolicy = "share-links";
    public const string BillingWebhookPolicy = "billing-webhook";

    public static IServiceCollection AddReleaseTwinRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // 7.2: the caller gets 429 + Retry-After; a failure of the limiter mechanism itself must
            // never turn into blocking all traffic (fail open for the platform) — the framework
            // already lets an in-ceiling request through, and OnRejected only adds a header + log.
            options.OnRejected = (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(NumberFormatInfo.InvariantInfo);
                }

                context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("ReleaseTwin.Hosted.Api.RateLimiting")
                    .LogWarning("rate_limit_rejected policy={Policy} path={Path}",
                        context.HttpContext.GetEndpoint()?.DisplayName, context.HttpContext.Request.Path);

                return ValueTask.CompletedTask;
            };

            options.AddPolicy(IngestPolicy, httpContext =>
            {
                var cfg = Config(httpContext);
                return Enabled(cfg)
                    ? RateLimitPartition.GetTokenBucketLimiter(IngestPartitionKey(httpContext), _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = Limit(cfg, "Ingest:TokenLimit", 5_000),
                        TokensPerPeriod = Limit(cfg, "Ingest:TokensPerPeriod", 500),
                        // `ReplenishmentSeconds` is a knob mainly so a test can set it far larger than
                        // the test's own runtime and get a deterministic burst (no mid-test refill on
                        // a slow, contended CI runner).
                        ReplenishmentPeriod = TimeSpan.FromSeconds(Limit(cfg, "Ingest:ReplenishmentSeconds", 10)),
                        AutoReplenishment = true,
                        QueueLimit = 0,
                    })
                    : NoLimiter();
            });

            options.AddPolicy(ShareLinkPolicy, httpContext =>
            {
                var cfg = Config(httpContext);
                return Enabled(cfg)
                    ? RateLimitPartition.GetFixedWindowLimiter(ClientAddress(httpContext), _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = Limit(cfg, "ShareLinks:PermitLimit", 120),
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    })
                    : NoLimiter();
            });

            options.AddPolicy(BillingWebhookPolicy, httpContext =>
            {
                var cfg = Config(httpContext);
                return Enabled(cfg)
                    ? RateLimitPartition.GetFixedWindowLimiter(ClientAddress(httpContext), _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = Limit(cfg, "BillingWebhook:PermitLimit", 60),
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    })
                    : NoLimiter();
            });
        });

        return services;
    }

    private static IConfiguration Config(HttpContext httpContext) =>
        httpContext.RequestServices.GetRequiredService<IConfiguration>();

    private static bool Enabled(IConfiguration cfg) =>
        cfg["RateLimiting:Enabled"] is not { } v || (bool.TryParse(v, out var b) && b);

    private static int Limit(IConfiguration cfg, string key, int fallback) =>
        int.TryParse(cfg[$"RateLimiting:{key}"], out var n) && n > 0 ? n : fallback;

    private static RateLimitPartition<string> NoLimiter() => RateLimitPartition.GetNoLimiter("disabled");

    /// <summary>The token's own hash claim (stamped by <see cref="ApiTokenAuthenticationHandler"/>) —
    /// one bucket per API token. Falls back to the connection address for an unauthenticated request
    /// that somehow reaches an ingest endpoint (it would be 401'd anyway).</summary>
    private static string IngestPartitionKey(HttpContext httpContext) =>
        httpContext.User.FindFirstValue(ApiTokenDefaults.TokenHashClaim) is { Length: > 0 } hash
            ? $"token:{hash}"
            : $"addr:{ClientAddress(httpContext)}";

    /// <summary>
    /// 7.4: the AWS Lambda Function URL populates <c>X-Forwarded-For</c> from the real edge
    /// connection; take the left-most entry. If abuse via a forged header appears, switch these
    /// address-partitioned policies to <c>HttpContext.Connection.Id</c> instead (the ingest policy is
    /// token-partitioned, so it is unaffected either way).
    /// </summary>
    private static string ClientAddress(HttpContext httpContext)
    {
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            var first = forwardedFor.Split(',', 2)[0].Trim();
            if (first.Length > 0)
            {
                return first;
            }
        }

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
