using FellowshipExtractor.Api.Services;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FellowshipExtractor.Api;

public static class ApiDocument
{
    public const string Name = "v1";

    public const string Route = "/openapi/v1.json";

    public const string Title = "Fellowship Codex";

    public const string Ui = "swagger";

    public static void Describe(OpenApiDocument document, string entities)
    {
        document.Info = new OpenApiInfo
        {
            Title = Title,
            Version = Name,
            Description =
                $"Every ability, item, talent, trait and gem the shipped build declares: {entities} of them, " +
                "each answered as a JSON record or as the self-contained HTML tooltip the game draws. " +
                "A tooltip fragment carries its own stylesheet, so it renders the same on a page this " +
                "project has never seen.",
        };
    }
}

public sealed class ApiInfo(ICodexService codex) : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellation)
    {
        ApiDocument.Describe(document, codex.Count.ToString("N0", System.Globalization.CultureInfo.InvariantCulture));

        return Task.CompletedTask;
    }
}
