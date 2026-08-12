using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed class BrowsingParameters(ICodexService codex) : IOpenApiOperationTransformer
{
    private const string Named = "GetEntities";

    public Task TransformAsync(
        OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellation)
    {
        var named = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<IEndpointNameMetadata>()
            .Select(metadata => metadata.EndpointName)
            .FirstOrDefault();

        if (!string.Equals(named, Named, StringComparison.Ordinal)) return Task.CompletedTask;

        foreach (var facet in codex.Facets)
        {
            Add(operation, facet.Key, JsonSchemaType.String, $"{facet.Label}, comma separated for several.");
        }

        foreach (var flag in codex.Flags)
        {
            Add(operation, flag.Key, JsonSchemaType.Boolean, flag.Label + ".");
        }

        var note =
            "Each measure bounds a range through a `Min` and a `Max` parameter, as `?cooldownMin=3`. " +
            "The measures are " + string.Join(", ", codex.Measures.Select(measure => measure.Key)) + ". " +
            "`sort` takes name, type or a measure, and `group` takes a facet.";

        operation.Description = operation.Description is { Length: > 0 } already
            ? already + "\n\n" + note
            : note;

        return Task.CompletedTask;
    }

    private static void Add(OpenApiOperation operation, string name, JsonSchemaType shape, string description)
    {
        operation.Parameters ??= [];

        if (operation.Parameters.Any(already =>
            string.Equals(already.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = name,
            In = ParameterLocation.Query,
            Required = false,
            Description = description,
            Schema = new OpenApiSchema { Type = shape },
        });
    }
}
