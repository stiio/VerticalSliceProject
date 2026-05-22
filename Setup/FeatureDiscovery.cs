using System.Reflection;
using VerticalSliceProject.Shared.Endpoints;

namespace VerticalSliceProject.Setup;

/// <summary>
/// Reflection-based feature discovery. Walks the assembly across the lifecycle of the host:
/// <list type="bullet">
/// <item><see cref="AddFeatureServices"/> — before <c>builder.Build()</c>, finds all
///   <see cref="IFeatureServices"/> implementers and invokes their static
///   <see cref="IFeatureServices.ConfigureServices"/> so features register their own services
///   into the DI container.</item>
/// <item><see cref="MapAllEndpoints"/> — after <c>builder.Build()</c>, runs in two phases:
///   <list type="number">
///   <item>Materialize every <see cref="IEndpointGroup"/> implementation once,
///     building its <see cref="RouteGroupBuilder"/>.</item>
///   <item>For every <see cref="IEndpoint"/>, look up its target group via
///     <see cref="EndpointGroupAttribute{TGroup}"/> (or fall back to the root route builder if no
///     attribute is present) and invoke <see cref="IEndpoint.Map"/>.</item>
///   </list></item>
/// </list>
/// All passes honour <see cref="DevOnlyAttribute"/> — Development-only types are skipped when the
/// host environment is not Development. An endpoint targeting a skipped Dev-only group is skipped
/// as well (group absence cascades to its endpoints).
/// </summary>
/// <remarks>
/// Cost: ~ms once at startup (reflection over typically &lt;100 types). Zero per-request cost.
/// Trade-off: incompatible with native AOT (<c>PublishAot</c>) due to trimming.
/// </remarks>
public static class FeatureDiscovery
{
    private static readonly Assembly AppAssembly = typeof(FeatureDiscovery).Assembly;

    /// <summary>
    /// Registers feature-internal services in the DI container. Invokes
    /// <see cref="IFeatureServices.ConfigureServices"/> on every type in the assembly that
    /// implements <see cref="IFeatureServices"/>. Call before <c>builder.Build()</c>.
    /// </summary>
    public static IServiceCollection AddFeatureServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment env)
    {
        var isDev = env.IsDevelopment();

        var types = AppAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } &&
                        typeof(IFeatureServices).IsAssignableFrom(t));

        foreach (var type in types)
        {
            if (!isDev && type.GetCustomAttribute<DevOnlyAttribute>() is not null)
            {
                continue;
            }

            var method = type.GetMethod(nameof(IFeatureServices.ConfigureServices), BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException(
                    $"Type {type.FullName} implements {nameof(IFeatureServices)} but has no public static ConfigureServices(IServiceCollection).");

            method.Invoke(null, [services]);
        }

        return services;
    }

    /// <summary>
    /// Maps every feature's endpoints onto the live application. First materialises all
    /// <see cref="IEndpointGroup"/> implementations, then walks every <see cref="IEndpoint"/> and
    /// hands it either its target group's <see cref="RouteGroupBuilder"/> or the root
    /// <see cref="IEndpointRouteBuilder"/> if no <see cref="EndpointGroupAttribute{TGroup}"/> is
    /// present. Call after <c>builder.Build()</c>.
    /// </summary>
    public static IEndpointRouteBuilder MapAllEndpoints(this IEndpointRouteBuilder app)
    {
        var env = app.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var isDev = env.IsDevelopment();

        var groups = MaterialiseGroups(app, isDev);
        MapEndpoints(app, isDev, groups);

        return app;
    }

    private static Dictionary<Type, RouteGroupBuilder> MaterialiseGroups(IEndpointRouteBuilder app, bool isDev)
    {
        var groups = new Dictionary<Type, RouteGroupBuilder>();

        var groupTypes = AppAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } &&
                        typeof(IEndpointGroup).IsAssignableFrom(t));

        foreach (var type in groupTypes)
        {
            if (!isDev && type.GetCustomAttribute<DevOnlyAttribute>() is not null)
            {
                continue;
            }

            var method = type.GetMethod(nameof(IEndpointGroup.MapGroup), BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException(
                    $"Type {type.FullName} implements {nameof(IEndpointGroup)} but has no public static MapGroup(IEndpointRouteBuilder).");

            var group = (RouteGroupBuilder)method.Invoke(null, [app])!;
            groups[type] = group;
        }

        return groups;
    }

    private static void MapEndpoints(IEndpointRouteBuilder app, bool isDev, Dictionary<Type, RouteGroupBuilder> groups)
    {
        var endpointTypes = AppAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } &&
                        typeof(IEndpoint).IsAssignableFrom(t));

        foreach (var type in endpointTypes)
        {
            if (!isDev && type.GetCustomAttribute<DevOnlyAttribute>() is not null)
            {
                continue;
            }

            var mapMethod = type.GetMethod(nameof(IEndpoint.Map), BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException(
                    $"Type {type.FullName} implements {nameof(IEndpoint)} but has no public static Map(IEndpointRouteBuilder).");

            IEndpointRouteBuilder target = app;

            if (TryGetEndpointGroupType(type, out var groupType))
            {
                if (!groups.TryGetValue(groupType, out var group))
                {
                    // Group was filtered out (e.g. dev-only in non-dev) — skip the endpoint too.
                    continue;
                }

                target = group;
            }

            mapMethod.Invoke(null, [target]);
        }
    }

    private static bool TryGetEndpointGroupType(Type endpointType, out Type groupType)
    {
        foreach (var attr in endpointType.GetCustomAttributes(inherit: false))
        {
            var attrType = attr.GetType();
            if (attrType.IsGenericType && attrType.GetGenericTypeDefinition() == typeof(EndpointGroupAttribute<>))
            {
                groupType = attrType.GetGenericArguments()[0];
                return true;
            }
        }

        groupType = null!;
        return false;
    }
}
