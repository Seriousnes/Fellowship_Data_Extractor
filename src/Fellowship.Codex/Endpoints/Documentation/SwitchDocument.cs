using System.Reflection;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

using Fellowship.Codex.Models;

namespace Fellowship.Codex.Endpoints;

public sealed class SwitchParameters : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellation)
    {
        if (operation.Parameters is not { Count: > 0 }) return Task.CompletedTask;

        var switched = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<MethodInfo>()
            .SelectMany(method => method.GetParameters())
            .Where(parameter => parameter.GetCustomAttribute<AsParametersAttribute>() is not null)
            .SelectMany(parameter => parameter.ParameterType.GetProperties())
            .Where(property =>
                property.PropertyType == typeof(Switch) ||
                Nullable.GetUnderlyingType(property.PropertyType) == typeof(Switch))
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (switched.Count == 0) return Task.CompletedTask;

        foreach (var parameter in operation.Parameters)
        {
            if (parameter.Name is not { } name || !switched.Contains(name)) continue;

            if (parameter is not OpenApiParameter given || given.Schema is not OpenApiSchema schema) continue;

            schema.Type = JsonSchemaType.Boolean;
            schema.Properties?.Clear();
            schema.Required?.Clear();
            given.AllowEmptyValue = true;
        }

        return Task.CompletedTask;
    }
}
