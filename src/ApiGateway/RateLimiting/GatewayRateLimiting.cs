using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace ApiGateway.RateLimiting;

/// <summary>
/// Rate limiting i gatewayen: et fast vindue pr. IP-adresse. Hver route i appsettings.json vælger en politik
/// med "RateLimiterPolicy". Kald over grænsen får 429 Too Many Requests og når aldrig frem til en service.
/// Grænserne står under "RateLimits" i appsettings.json, så de kan sænkes til en demonstration.
/// </summary>
public static class GatewayRateLimiting
{
    /// <summary>Siderne i Web. Én side henter mange billeder, scripts og (i admin) Blazor-filer, så grænsen er høj.</summary>
    public const string Web = "web";

    /// <summary>Alle /api-kald til services og PublicApi.</summary>
    public const string Api = "api";

    /// <summary>POST /api/authenticate. Lav grænse, så det er svært at gætte adgangskoder.</summary>
    public const string Login = "login";

    public static IServiceCollection AddGatewayRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var limits = configuration.GetSection("RateLimits");

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                // IP-adressen logges ikke (persondata); stien er nok til at se, hvad der blev afvist.
                context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger(typeof(GatewayRateLimiting))
                    .LogWarning("Rate limit reached for {Method} {Path}; answered 429",
                        context.HttpContext.Request.Method, context.HttpContext.Request.Path);
                return ValueTask.CompletedTask;
            };

            AddFixedWindowPerIp(options, Web, limits.GetSection("Web"), defaultPermitLimit: 1000);
            AddFixedWindowPerIp(options, Api, limits.GetSection("Api"), defaultPermitLimit: 100);
            AddFixedWindowPerIp(options, Login, limits.GetSection("Login"), defaultPermitLimit: 10);
        });

        return services;
    }

    private static void AddFixedWindowPerIp(RateLimiterOptions options, string policyName, IConfiguration limit, int defaultPermitLimit)
    {
        var permitLimit = limit.GetValue("PermitLimit", defaultPermitLimit);
        var window = TimeSpan.FromSeconds(limit.GetValue("WindowSeconds", 60));

        options.AddPolicy(policyName, httpContext => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                QueueLimit = 0
            }));
    }
}
