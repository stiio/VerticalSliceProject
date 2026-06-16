using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;
using Serilog.Exceptions;
using Serilog.Exceptions.Core;
using Serilog.Formatting.Json;
using VerticalSliceProject.Setup;
using VerticalSliceProject.Setup.Json;
using VerticalSliceProject.Setup.OpenApi;

// ----- Bootstrap Serilog before host construction so startup errors are captured. -----
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new JsonFormatter())
    .Enrich.WithExceptionDetails(new DestructuringOptionsBuilder())
    .Enrich.FromLogContext()
    .CreateLogger();

Log.Information("Starting up");

try
{
    // Special-case for `dotnet GetDocument.Insider` (Microsoft.Extensions.ApiDescription.Server) —
    // it spins up the host to generate OpenAPI documents at build time. Everything endpoints
    // resolve from DI must be visible here, but no real infra (DB, etc.) is needed.
    if (Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider")
    {
        var minimalBuilder = WebApplication.CreateBuilder(args);
        ConfigureSerilog(minimalBuilder.Host);
        ConfigureJson(minimalBuilder.Services);
        minimalBuilder.Services.AddAppOpenApi();
        minimalBuilder.Services.AddEndpointsApiExplorer();
        minimalBuilder.Services.AddDocGenStubs();
        var minimalApp = minimalBuilder.Build();
        minimalApp.UseAppOpenApi();
        // Endpoints must still be in the route table so doc generation can describe them, but
        // they won't actually serve — GetDocument.Insider only walks the description provider.
        minimalApp.MapAllEndpoints();
        minimalApp.Run();
    }
    else
    {
        var builder = WebApplication.CreateBuilder(args);

        ConfigureSerilog(builder.Host);
        ConfigureConfiguration(builder.Configuration, builder.Environment);
        ConfigureServices(builder.Services, builder.Configuration, builder.Environment);

        var webApplication = builder.Build();

        ConfigureMiddleware(webApplication, webApplication.Environment);
        ConfigureEndpoints(webApplication);

        webApplication.Run();
    }
}
catch (Exception e)
{
    Log.Fatal(e, "Unhandled exception");
}
finally
{
    Log.Information("Shut down complete");
    Log.CloseAndFlush();
}

return;

void ConfigureConfiguration(ConfigurationManager configuration, IWebHostEnvironment env)
{
    configuration
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
        .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables()
        .AddEnvironmentVariablesFromJsonVariables();
}

void ConfigureServices(IServiceCollection services, IConfiguration configuration, IWebHostEnvironment env)
{
    ConfigureJson(services);

    services.AddEndpointsApiExplorer();
    services.AddAppOpenApi();
    services.AddAppProblemDetails();
    services.AddHealthChecks();

    services.AddHttpContextAccessor();

    services.Configure<HostOptions>(options =>
    {
        options.ShutdownTimeout = TimeSpan.FromMinutes(1);
    });

    services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.All;
        options.ForwardLimit = 2;
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
    });

    // Walk the assembly for IFeatureServices implementers and register feature-internal services.
    // Called LAST so feature registrations may override anything registered above.
    services.AddFeatureServices(configuration, env);
}

void ConfigureMiddleware(WebApplication app, IWebHostEnvironment env)
{
    app.UseForwardedHeaders();

    if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
    {
        app.UseAppOpenApi();
    }

    app.UseAppRequestLogging();
    app.UseExceptionHandler();
}

void ConfigureEndpoints(IEndpointRouteBuilder app)
{
    app.MapHealthChecks("/health");
    app.MapAllEndpoints();
}

void ConfigureJson(IServiceCollection services)
{
    services.Configure<JsonOptions>(opts =>
    {
        opts.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        opts.SerializerOptions.Converters.Add(new DateTimeJsonConverter());
        opts.SerializerOptions.Converters.Add(new DateOnlyJsonConverter());
        opts.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        opts.SerializerOptions.WriteIndented = false;
        opts.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });
}

void ConfigureSerilog(IHostBuilder host)
{
    host.UseSerilog((context, services, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.WithExceptionDetails(new DestructuringOptionsBuilder())
            .Enrich.FromLogContext();
    });
}

