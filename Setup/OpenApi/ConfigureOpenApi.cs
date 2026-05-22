using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using VerticalSliceProject.Setup.OpenApi.Transformers;

namespace VerticalSliceProject.Setup.OpenApi;

public static class ConfigureOpenApi
{
    /// <summary>
    /// Registers two OpenAPI documents:
    /// <list type="bullet">
    /// <item><c>v1</c> — production endpoints in <c>/api/v1/</c></item>
    /// <item><c>dev</c> — dev-only endpoints in <c>/api/dev/</c> (only visible in Development)</item>
    /// </list>
    /// Documents filter by <c>WithGroupName(...)</c> set on each feature's <c>_Group.cs</c>.
    /// </summary>
    public static IServiceCollection AddAppOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi("v1", opts => ConfigureVersion(opts, "v1"));
        services.AddOpenApi("dev", opts => ConfigureVersion(opts, "dev"));

        return services;
    }

    public static WebApplication UseAppOpenApi(this WebApplication app)
    {
        app.MapOpenApi("api/openapi/{documentName}.json");

        app.MapScalarApiReference("api/docs", options =>
        {
            options.Title = "Api Documentation";
            options.OpenApiRoutePattern = "api/openapi/{documentName}.json";
            options.Agent = new ScalarAgentOptions { Disabled = true };
            options.Mcp = new ScalarMcpOptions { Disabled = true };
            options.ShowOperationId = true;
            options.HiddenClients = false;
            options.DotNetFlag = true;
            options.Theme = ScalarTheme.Purple;
            options.HideClientButton = true;
        });

        return app;
    }

    private static void ConfigureVersion(OpenApiOptions opts, string versionTag)
    {
        ApplyCommonTransformers(opts);
        opts.ShouldInclude = description => description.GroupName == versionTag;
        opts.AddDocumentTransformer((doc, _, _) =>
        {
            doc.Info = new OpenApiInfo
            {
                Title = $"Api - {versionTag}",
                Version = versionTag,
            };
            return Task.CompletedTask;
        });
    }

    private static void ApplyCommonTransformers(OpenApiOptions opts)
    {
        // Schema reference id strategy: nested types get parent name prepended so
        // `CreateFoo.Request` becomes `CreateFooRequest`, avoiding $ref collisions.
        opts.CreateSchemaReferenceId = ctx =>
        {
            var t = ctx.Type;
            if (t.IsNested && t.DeclaringType is { } parent)
            {
                return parent.Name + t.Name;
            }

            return OpenApiOptions.CreateDefaultSchemaReferenceId(ctx);
        };

        opts.AddDocumentTransformer<ErrorResultDocumentTransformer>();
        opts.AddDocumentTransformer<PolymorphicDerivedRequiredTransformer>();

        opts.AddOperationTransformer<DefaultApplicationResponsesTransformer>();
        opts.AddOperationTransformer<CamelCaseParametersTransformer>();
        opts.AddOperationTransformer<AsParametersRequiredFixer>();
        // Multipart body normalization runs LAST — flattens Minimal API's allOf-of-singletons
        // composition and camelCases property names / required entries / encoding keys after the
        // earlier transformers have finished mutating the schema.
        opts.AddOperationTransformer<MultipartBodyFlattenTransformer>();

        opts.AddSchemaTransformer<FormFileSchemaTransformer>();
        opts.AddSchemaTransformer<StringEnumSchemaTransformer>();
        opts.AddSchemaTransformer<DateTimeSchemaTransformer>();
        opts.AddSchemaTransformer<PolymorphismOneOfTransformer>();
        opts.AddSchemaTransformer<FileResultSchemaTransformer>();
    }
}
