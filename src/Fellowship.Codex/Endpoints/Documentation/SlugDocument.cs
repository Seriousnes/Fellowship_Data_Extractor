using System.Text.Json.Nodes;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Fellowship.Codex.Endpoints;

public sealed class TypeSlugs : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellation)
    {
        if (context.JsonTypeInfo.Type != typeof(EntityType)) return Task.CompletedTask;

        schema.Type = JsonSchemaType.String;
        schema.Enum = [.. Enum.GetValues<EntityType>().Select(type => (JsonNode)EntityTypes.Slug(type))];

        return Task.CompletedTask;
    }
}
