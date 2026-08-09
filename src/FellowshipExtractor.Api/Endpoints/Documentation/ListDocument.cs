using System.Reflection;
using System.Text.Json.Nodes;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public sealed class ListParameters : IOpenApiOperationTransformer
{
    private static readonly IReadOnlyDictionary<Type, string> Examples = new Dictionary<Type, string>
    {
        [typeof(ImbuedTraits)] = "7:2,12:1",
        [typeof(GemPowers)] = "3:40,5:25",
    };

    public Task TransformAsync(
        OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellation)
    {
        if (operation.Parameters is not { Count: > 0 }) return Task.CompletedTask;

        var listed = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<MethodInfo>()
            .SelectMany(method => method.GetParameters())
            .Where(parameter => parameter.GetCustomAttribute<AsParametersAttribute>() is not null)
            .SelectMany(parameter => parameter.ParameterType.GetProperties())
            .Where(property => Examples.ContainsKey(property.PropertyType))
            .ToDictionary(property => property.Name, property => Examples[property.PropertyType], StringComparer.OrdinalIgnoreCase);

        if (listed.Count == 0) return Task.CompletedTask;

        foreach (var parameter in operation.Parameters)
        {
            if (parameter.Name is not { } name || !listed.TryGetValue(name, out var example)) continue;

            if (parameter.Schema is not OpenApiSchema schema) continue;

            schema.Type = JsonSchemaType.String;
            schema.Properties?.Clear();
            schema.Example = JsonValue.Create(example);
        }

        return Task.CompletedTask;
    }
}
