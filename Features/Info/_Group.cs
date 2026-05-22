using VerticalSliceProject.Shared.Endpoints;

namespace VerticalSliceProject.Features.Info;

public sealed class InfoGroup : IEndpointGroup
{
    public static RouteGroupBuilder MapGroup(IEndpointRouteBuilder app) =>
        app.MapGroup("/api/v1/info")
            .WithTags("Info")
            .WithGroupName("v1");
}
