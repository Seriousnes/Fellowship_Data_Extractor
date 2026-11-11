using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed class BrowsingParameters(ICodexService codex) : IOpenApiOperationTransformer
{
    private static readonly string[] EndpointNames =
        [EntitiesEndpoint.Name, .. EntitiesEndpoint.Downloads.Select(EntitiesEndpoint.DownloadName)];

    public Task TransformAsync(
        OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellation)
    {
        var endpointName = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<IEndpointNameMetadata>()
            .Select(metadata => metadata.EndpointName)
            .FirstOrDefault();

        if (!EndpointNames.Contains(endpointName, StringComparer.Ordinal)) return Task.CompletedTask;

        foreach (var facet in codex.Facets)
        {
            Add(operation, facet.Key, JsonSchemaType.String, $"{facet.Label}, comma separated for several.");
        }

        foreach (var flag in codex.Flags)
        {
            Add(operation, flag.Key, JsonSchemaType.Boolean, flag.Label + ".");
        }

        var note =
            Comparisons.Note + " The measures are " + string.Join(", ", codex.Measures.Select(measure => measure.Key)) + ". " +
            "`sort` takes name, type or a measure, and `group` takes a facet.";

        operation.Description = operation.Description is { Length: > 0 } existing
            ? existing + "\n\n" + note
            : note;

        return Task.CompletedTask;
    }

    private static void Add(OpenApiOperation operation, string name, JsonSchemaType shape, string description)
    {
        operation.Parameters ??= [];

        if (operation.Parameters.Any(existing =>
            string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase)))
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
