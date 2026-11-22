using System.Diagnostics;

namespace ApiGateway.Logging;

/// <summary>
/// Én linje pr. kald: metode, YARP-route, statuskode og varighed – også for kald, der afvises med 401 eller 429.
/// Stien logges ikke, fordi den kan indeholde en e-mail (/api/baskets/{buyerId}). Routens navn siger,
/// hvor kaldet blev sendt hen, og det er nok til at følge trafikken i Kibana.
/// </summary>
public static class RequestSummaryLogging
{
    public static IApplicationBuilder UseRequestSummaryLogging(this IApplicationBuilder app)
    {
        var logger = app.ApplicationServices.GetRequiredService<ILoggerFactory>().CreateLogger("ApiGateway.Requests");

        return app.Use(async (context, next) =>
        {
            var started = Stopwatch.GetTimestamp();
            try
            {
                await next(context);
            }
            finally
            {
                logger.LogInformation("{Method} via route {Route} answered {StatusCode} in {ElapsedMs:0} ms",
                    context.Request.Method,
                    context.GetEndpoint()?.DisplayName ?? "(no route)",
                    context.Response.StatusCode,
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }
        });
    }
}
