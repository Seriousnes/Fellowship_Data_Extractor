using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FellowshipExtractor.Api;

public enum ApiValue { Text, Whole, Number, Flag }

public enum ApiQuerySet { Tooltip, Catalogue, Search }

public sealed record ApiParameter(string Name, ApiValue Value, string Description);

public static class ApiDocument
{
    public const string Name = "v1";

    public const string Route = "/openapi/v1.json";

    public const string Title = "Fellowship Codex";

    public const string Ui = "swagger";

    public const string Tooltips = "Tooltips";

    public const string Records = "Records";

    public const string Browsing = "Browsing";

    public static RouteHandlerBuilder WithQuery(this RouteHandlerBuilder route, ApiQuerySet set) =>
        route.WithMetadata(set);

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

public sealed class ApiParameters(IEntityStoreService store, ICodexService codex) : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellation)
    {
        var asked = context.Description.ActionDescriptor.EndpointMetadata.OfType<ApiQuerySet>().ToArray();

        if (asked.Length == 0) return Task.CompletedTask;

        foreach (var parameter in Declared(asked[^1]))
        {
            Add(operation, parameter);
        }

        if (Note(asked[^1]) is { } note)
        {
            operation.Description = operation.Description is { Length: > 0 } already
                ? already + "\n\n" + note
                : note;
        }

        return Task.CompletedTask;
    }

    private IEnumerable<ApiParameter> Declared(ApiQuerySet set) => set switch
    {
        ApiQuerySet.Tooltip => TooltipQuery.Parameters,
        _ => [],
    };

    private string? Note(ApiQuerySet set) => set switch
    {
        ApiQuerySet.Tooltip =>
            "Any stat the build names may also be given, as `?Stamina=40`, to state a magnitude the tooltip " +
            $"prints rather than computes, and a rating may be given as a percentage with the " +
            $"`{StatConversion.PercentageSuffix}` suffix. " +
            "The stats are " + string.Join(", ", store.Stats.Names) + ".",

        _ => null,
    };

    private static void Add(OpenApiOperation operation, ApiParameter parameter)
    {
        operation.Parameters ??= [];

        if (operation.Parameters.Any(already =>
            string.Equals(already.Name, parameter.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = parameter.Name,
            In = ParameterLocation.Query,
            Required = false,
            Description = parameter.Description,
            Schema = new OpenApiSchema { Type = Shape(parameter.Value) },
        });
    }

    private static JsonSchemaType Shape(ApiValue value) => value switch
    {
        ApiValue.Whole => JsonSchemaType.Integer,
        ApiValue.Number => JsonSchemaType.Number,
        ApiValue.Flag => JsonSchemaType.Boolean,
        _ => JsonSchemaType.String,
    };
}
