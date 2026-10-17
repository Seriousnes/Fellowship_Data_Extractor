using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Fellowship.Codex.Endpoints;

public sealed class ParameterNumbers : IOpenApiSchemaTransformer
{
    private const JsonSchemaType Numeric = JsonSchemaType.Integer | JsonSchemaType.Number;

    public Task TransformAsync(
        OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellation)
    {
        if (context.ParameterDescription is null) return Task.CompletedTask;

        if (schema.Type is not { } type || (type & Numeric) is 0 || !type.HasFlag(JsonSchemaType.String))
        {
            return Task.CompletedTask;
        }

        schema.Type = type & ~JsonSchemaType.String;
        schema.Pattern = null;

        return Task.CompletedTask;
    }
}
