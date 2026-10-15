using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Fellowship.Codex;

public static class ApiDocument
{
    public const string Name = "v1";

    public const string Route = "/openapi/v1.json";

    public const string Title = "Fellowship Codex";

    public const string Ui = "swagger";

    public static void Describe(OpenApiDocument document)
    {
        document.Info = new OpenApiInfo
        {
            Title = Title,
            Version = Name,            
        };
    }
}

public sealed class ApiInfo : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellation)
    {
        ApiDocument.Describe(document);

        return Task.CompletedTask;
    }
}
