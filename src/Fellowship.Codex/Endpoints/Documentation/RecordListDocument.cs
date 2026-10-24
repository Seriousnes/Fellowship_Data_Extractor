using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed class RecordListParameters(ICodexService codex, IEntityStoreService store) : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellation)
    {
        if (context.Description.ActionDescriptor.EndpointMetadata.OfType<RecordListMetadata>().FirstOrDefault()
            is not { } list)
        {
            return Task.CompletedTask;
        }

        var documents = store.Entities.Where(document => EntityTypes.TypeOf(document) == list.Type).ToArray();

        foreach (var facet in codex.Facets.Where(facet => documents.Any(document => facet.Values(document).Any())))
        {
            Add(operation, facet.Key, JsonSchemaType.String, $"{facet.Label}, comma separated for several.");
        }

        foreach (var flag in codex.Flags.Where(flag => documents.Any(flag.Predicate)))
        {
            Add(operation, flag.Key, JsonSchemaType.Boolean, flag.Label + ".");
        }

        foreach (var measure in codex.Measures.Where(measure => documents.Any(document => measure.Value(document) is not null)))
        {
            Add(operation, measure.Key + "Min", JsonSchemaType.Number, $"Lowest {measure.Label}.");
            Add(operation, measure.Key + "Max", JsonSchemaType.Number, $"Highest {measure.Label}.");
        }

        foreach (var field in RecordFields.Of(list.Record))
        {
            Add(
                operation,
                field.Key,
                field.Boolean ? JsonSchemaType.Boolean : JsonSchemaType.String,
                field.Boolean ? $"The record's {field.Key}." : $"The record's {field.Key}, comma separated for several.");

            if (!field.Numeric) continue;

            Add(operation, field.Key + "Min", JsonSchemaType.Number, $"Lowest {field.Key}.");
            Add(operation, field.Key + "Max", JsonSchemaType.Number, $"Highest {field.Key}.");
        }

        var note =
            $"Each row is a whole {PublicTypes.Noun(list.Type)} record. A filter names a facet, a flag, a " +
            "measure bound or a record field. A record field matches its value, or any value of a list field, " +
            "case insensitively, and a numeric field bounds a range through a `Min` and a `Max` parameter, as " +
            "`?idMin=100`. A name taken by a reading parameter or a facet reads as that, not as a record field. " +
            "`sort` takes name, a sorting, a measure or a record field.";

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
