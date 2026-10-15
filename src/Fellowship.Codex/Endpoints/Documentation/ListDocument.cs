using System.Text.Json.Nodes;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

using Fellowship.Codex.Models;

namespace Fellowship.Codex.Endpoints;

public sealed class ListParameters : IOpenApiOperationTransformer
{
    private const string EndpointName = "GetEntities";

    private static readonly IReadOnlyDictionary<string, string> Examples =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(EntitiesRequest.Type)] = string.Join(
                ',', PublicTypes.All.Where(type => type.Listed).Take(2).Select(type => type.Slug)),
        };

    public Task TransformAsync(
        OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellation)
    {
        if (operation.Parameters is not { Count: > 0 }) return Task.CompletedTask;

        var endpointName = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<IEndpointNameMetadata>()
            .Select(metadata => metadata.EndpointName)
            .FirstOrDefault();

        if (!string.Equals(endpointName, EndpointName, StringComparison.Ordinal)) return Task.CompletedTask;

        foreach (var parameter in operation.Parameters)
        {
            if (parameter.Name is not { } name || !Examples.TryGetValue(name, out var example)) continue;

            if (parameter.Schema is not OpenApiSchema schema) continue;

            schema.Example = JsonValue.Create(example);
        }

        return Task.CompletedTask;
    }
}
