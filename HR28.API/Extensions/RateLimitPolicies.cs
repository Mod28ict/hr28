using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;

namespace HR28.API.Extensions;

/// <summary>
/// Request limits that stop guessing codes and bulk-downloading data.
/// Sign-in limits are per client IP (the web app forwards the visitor's IP; it is
/// trusted only from known proxies). Data limits are per signed-in user.
/// Per-account sign-in limits (cooldown, hourly cap, lockout) live in AuthService.
/// </summary>
public static class RateLimitPolicies
{
    public const string OtpRequest = "otp-request";
    public const string OtpVerify = "otp-verify";
    public const string Export = "export";
    public const string Search = "search";
    public const string Import = "import";
    public const string PhotoChange = "photo-change";

    public static IServiceCollection AddHr28RateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Trust X-Forwarded-For only from known proxies (loopback by default, so the
        // web app on the same machine works in development). In Azure, list the web
        // app's outbound addresses under ReverseProxy:KnownProxies.
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
            options.ForwardLimit = 1;

            foreach (var proxy in configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
            {
                if (System.Net.IPAddress.TryParse(proxy, out var ip))
                    options.KnownProxies.Add(ip);
            }
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                }

                await context.HttpContext.Response.WriteAsJsonAsync(
                    new { message = "Too many attempts. Please wait a few minutes and try again." },
                    cancellationToken);
            };

            options.AddPolicy(OtpRequest, http =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    ClientIp(http),
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(5),
                        SegmentsPerWindow = 5,
                        QueueLimit = 0
                    }));

            options.AddPolicy(OtpVerify, http =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    ClientIp(http),
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(5),
                        SegmentsPerWindow = 5,
                        QueueLimit = 0
                    }));

            options.AddPolicy(Export, http =>
                RateLimitPartition.GetFixedWindowLimiter(
                    UserOrIp(http),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromHours(1),
                        QueueLimit = 0
                    }));

            options.AddPolicy(Import, http =>
                RateLimitPartition.GetFixedWindowLimiter(
                    UserOrIp(http),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromHours(1),
                        QueueLimit = 0
                    }));

            // Adding, replacing or removing voter photos.
            options.AddPolicy(PhotoChange, http =>
                RateLimitPartition.GetFixedWindowLimiter(
                    UserOrIp(http),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 60,
                        Window = TimeSpan.FromHours(1),
                        QueueLimit = 0
                    }));

            options.AddPolicy(Search, http =>
                RateLimitPartition.GetTokenBucketLimiter(
                    UserOrIp(http),
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 120,
                        TokensPerPeriod = 120,
                        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));
        });

        return services;
    }

    private static string ClientIp(HttpContext http) =>
        "ip:" + (http.Connection.RemoteIpAddress?.ToString() ?? "unknown");

    private static string UserOrIp(HttpContext http) =>
        http.User.GetUserId() is Guid id ? "user:" + id : ClientIp(http);
}
