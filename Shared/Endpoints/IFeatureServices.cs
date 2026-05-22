namespace VerticalSliceProject.Shared.Endpoints;

/// <summary>
/// Symmetric to <see cref="IEndpoint"/> but for DI registration: implement on a feature's
/// <c>_Group.cs</c> (or any class living inside the feature folder) to register feature-internal
/// services in the container. <c>FeatureDiscovery.AddFeatureServices</c> finds all implementers
/// in the assembly and invokes <see cref="ConfigureServices"/> on each.
/// </summary>
/// <remarks>
/// <para>Use for services whose scope is a single feature. Cross-cutting infrastructure goes to
/// <c>Shared/Infrastructure/</c> (add as needed) and is registered directly in <c>Program.cs</c>.</para>
/// <para>Called <b>before</b> <c>builder.Build()</c>, so registrations participate in the DI
/// container that endpoints later resolve from. Don't perform side-effects beyond
/// <c>services.AddXxx</c> calls.</para>
/// </remarks>
public interface IFeatureServices
{
    static abstract void ConfigureServices(IServiceCollection services);
}
