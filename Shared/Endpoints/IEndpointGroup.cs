namespace VerticalSliceProject.Shared.Endpoints;

/// <summary>
/// Declares a route group (prefix + shared metadata like tags, OpenAPI version, authorization).
/// Endpoints opt into a group via <see cref="EndpointGroupAttribute{TGroup}"/>; discovery
/// materializes each <see cref="IEndpointGroup"/> implementation exactly once and reuses the
/// resulting <see cref="RouteGroupBuilder"/> for every endpoint that targets it.
/// </summary>
/// <remarks>
/// <para>One feature can declare multiple groups when its surface area splits across different
/// audiences — e.g. <c>UsersGroup</c> at <c>/api/v1/users</c> for end users plus
/// <c>UsersAdminGroup</c> at <c>/api/v1/admin/users</c> with a tighter auth policy.</para>
/// <para>Endpoints with no <see cref="EndpointGroupAttribute{TGroup}"/> register against the root
/// <see cref="IEndpointRouteBuilder"/> directly — useful for ungrouped one-offs like
/// <c>/health</c>.</para>
/// </remarks>
public interface IEndpointGroup
{
    static abstract RouteGroupBuilder MapGroup(IEndpointRouteBuilder app);
}
