using Serilog;

namespace VerticalSliceProject.Setup;

public static class ConfigureSerilog
{
    /// <summary>
    /// Adds Serilog request logging with referer enrichment. Auth claims / IP-extraction enrichers
    /// are intentionally absent — add them when (if) the project introduces auth and an HTTP-helper
    /// for extracting the client IP behind proxies.
    /// </summary>
    public static IApplicationBuilder UseAppRequestLogging(this IApplicationBuilder app) =>
        app.UseSerilogRequestLogging(opts =>
        {
            opts.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms.";
            opts.IncludeQueryInRequestPath = true;

            opts.EnrichDiagnosticContext = (context, httpContext) =>
            {
                context.Set("Referer", httpContext.Request.Headers.Referer.ToString());
            };
        });
}
