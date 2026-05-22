using System.Reflection;
using VerticalSliceProject.Shared.Endpoints;

namespace VerticalSliceProject.Features.Info.Endpoints;

[EndpointGroup<InfoGroup>]
public sealed class GetInfo : IEndpoint
{
    public sealed record Response
    {
        /// <summary>UTC timestamp when the assembly was built.</summary>
        public DateTime? BuildDate { get; init; }

        /// <summary>Informational assembly version.</summary>
        public string? Version { get; init; }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/", Handle)
            .WithName(nameof(GetInfo))
            .WithSummary("Get build / version info");

    public static Response Handle()
    {
        var entry = Assembly.GetEntryAssembly()!;
        return new Response
        {
            BuildDate = entry.GetCustomAttribute<AssemblyBuildDateAttribute>()?.BuildDate,
            Version = entry.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? entry.GetName().Version?.ToString(),
        };
    }
}
