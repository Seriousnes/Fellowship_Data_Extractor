using System.ComponentModel;
using System.Globalization;
using System.Reflection;

using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.OpenApi;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed class QueryOperations(IEntityStoreService store) : IOpenApiDocumentTransformer
{
    private const string SchemaId = "x-schema-id";

    public async Task TransformAsync(
        OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellation)
    {
        var note = new StatsNote(store);

        foreach (var description in context.DescriptionGroups.SelectMany(group => group.Items))
        {
            if (description.HttpMethod is not { } method || !HttpMethods.IsQuery(method)) continue;

            var operation = await Describe(document, context, description, cancellation);

            await note.TransformAsync(
                operation,
                new OpenApiOperationTransformerContext
                {
                    DocumentName = context.DocumentName,
                    Description = description,
                    ApplicationServices = context.ApplicationServices,
                    Document = document,
                },
                cancellation);

            Add(document, description, operation);
        }
    }

    private static async Task<OpenApiOperation> Describe(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        ApiDescription description,
        CancellationToken cancellation)
    {
        var metadata = description.ActionDescriptor.EndpointMetadata;

        return new OpenApiOperation
        {
            OperationId = metadata.OfType<IEndpointNameMetadata>().LastOrDefault()?.EndpointName,
            Summary = metadata.OfType<IEndpointSummaryMetadata>().LastOrDefault()?.Summary,
            Description = metadata.OfType<IEndpointDescriptionMetadata>().LastOrDefault()?.Description,
            Tags = Tags(document, metadata),
            Parameters = await Parameters(context, description, cancellation),
            RequestBody = await Body(document, context, description, cancellation),
            Responses = await Responses(document, context, description, cancellation),
        };
    }

    private static HashSet<OpenApiTagReference>? Tags(OpenApiDocument document, IList<object> metadata)
    {
        var names = metadata.OfType<ITagsMetadata>().SelectMany(tags => tags.Tags).ToArray();

        return names.Length is 0 ? null : [.. names.Select(name => new OpenApiTagReference(name, document))];
    }

    private static async Task<List<IOpenApiParameter>?> Parameters(
        OpenApiDocumentTransformerContext context,
        ApiDescription description,
        CancellationToken cancellation)
    {
        List<IOpenApiParameter>? parameters = null;

        foreach (var parameter in description.ParameterDescriptions)
        {
            if (Location(parameter.Source) is not { } location) continue;

            parameters ??= [];
            parameters.Add(new OpenApiParameter
            {
                Name = parameter.Name,
                In = location,
                Description = Stated(parameter),
                Required = location is ParameterLocation.Path || parameter.IsRequired,
                Schema = await context.GetOrCreateSchemaAsync(parameter.Type, parameter, cancellation),
            });
        }

        return parameters;
    }

    private static string? Stated(ApiParameterDescription parameter) =>
        parameter.ModelMetadata?.Description
        ?? (parameter.ParameterDescriptor as IParameterInfoParameterDescriptor)?.ParameterInfo
            .GetCustomAttribute<DescriptionAttribute>()?.Description;

    private static ParameterLocation? Location(BindingSource? source) =>
        source == BindingSource.Path ? ParameterLocation.Path
        : source == BindingSource.Query ? ParameterLocation.Query
        : source == BindingSource.Header ? ParameterLocation.Header
        : null;

    private static async Task<OpenApiRequestBody?> Body(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        ApiDescription description,
        CancellationToken cancellation)
    {
        var body = description.ParameterDescriptions
            .FirstOrDefault(parameter => parameter.Source == BindingSource.Body);

        if (body?.Type is not { } type) return null;

        var schema = Component(await context.GetOrCreateSchemaAsync(type, null, cancellation), document);

        var media = description.SupportedRequestFormats
            .Select(format => format.MediaType)
            .Where(name => name is { Length: > 0 })
            .DefaultIfEmpty("application/json");

        return new OpenApiRequestBody
        {
            Required = body.IsRequired,
            Content = media.Distinct(StringComparer.OrdinalIgnoreCase).ToDictionary(
                name => name,
                _ => new OpenApiMediaType { Schema = schema },
                StringComparer.OrdinalIgnoreCase),
        };
    }

    private static async Task<OpenApiResponses> Responses(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        ApiDescription description,
        CancellationToken cancellation)
    {
        var responses = new OpenApiResponses();

        foreach (var supported in description.SupportedResponseTypes)
        {
            var schema = supported.Type is { } type && type != typeof(void)
                ? Component(await context.GetOrCreateSchemaAsync(type, null, cancellation), document)
                : null;

            var response = new OpenApiResponse
            {
                Description = ReasonPhrases.GetReasonPhrase(supported.StatusCode),
                Content = supported.ApiResponseFormats.ToDictionary(
                    format => format.MediaType,
                    _ => new OpenApiMediaType { Schema = schema },
                    StringComparer.OrdinalIgnoreCase),
            };

            responses[supported.IsDefaultResponse
                ? "default"
                : supported.StatusCode.ToString(CultureInfo.InvariantCulture)] = response;
        }

        return responses;
    }

    private static IOpenApiSchema Component(IOpenApiSchema schema, OpenApiDocument document) =>
        schema is OpenApiSchema created
        && created.Metadata?.TryGetValue(SchemaId, out var id) is true
        && id is string { Length: > 0 } name
            ? new OpenApiSchemaReference(name, document)
            : schema;

    private static void Add(OpenApiDocument document, ApiDescription description, OpenApiOperation operation)
    {
        var route = Route(description);

        if (!document.Paths.TryGetValue(route, out var existing) || existing is not OpenApiPathItem item)
        {
            item = new OpenApiPathItem();
            document.Paths[route] = item;
        }

        item.Operations ??= [];
        item.Operations[HttpMethod.Query] = operation;
    }

    private static string Route(ApiDescription description)
    {
        var pattern = RoutePatternFactory.Parse(description.RelativePath ?? string.Empty);

        return "/" + string.Join('/', pattern.PathSegments.Select(segment =>
            string.Concat(segment.Parts.Select(part => part switch
            {
                RoutePatternLiteralPart literal => literal.Content,
                RoutePatternSeparatorPart separator => separator.Content,
                RoutePatternParameterPart parameter => "{" + parameter.Name + "}",
                _ => string.Empty,
            }))));
    }
}
