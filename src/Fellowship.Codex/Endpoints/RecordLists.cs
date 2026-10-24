using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

using Fellowship.SDK.Client;
using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public record RecordListRequest
{
    [Description("Free text matched against every name.")]
    public string? Q { get; init; }

    [Description("The column to order by: name, a sorting, a measure or a record field. Defaults to name.")]
    public string? Sort { get; init; }

    [Description("Given as desc to order the other way.")]
    public string? Dir { get; init; }

    [Description("Which page to answer, from 1.")]
    public int? Page { get; init; }

    [Description("How many rows a page holds, up to 200. Defaults to 50.")]
    public int? Size { get; init; }

    public virtual RecordReading ToReading() => new();
}

public sealed record RecordListMetadata(EntityType Type, Type Record, Type Request);

internal sealed class Row<TRecord>(EntityDocument document, Func<TRecord> project)
    where TRecord : EntityRecord
{
    private TRecord? record;

    public EntityDocument Document => document;

    public TRecord Record => record ??= project();
}

public static class RecordLists
{
    private static readonly IReadOnlySet<string> Controls = RequestProperties.Of(typeof(RecordListRequest));

    public static RouteHandlerBuilder MapRecordList<TRecord, TRequest>(this IEndpointRouteBuilder group)
        where TRecord : EntityRecord
        where TRequest : RecordListRequest
    {
        var type = EntityRecords.TypeOf(typeof(TRecord));

        return group.MapGet(string.Empty, Handle<TRecord, TRequest>)
            .WithName("List" + PublicTypes.Label(type).Replace(" ", string.Empty, StringComparison.Ordinal))
            .WithSummary($"Every {PublicTypes.Noun(type)} a filter leaves, paged.")
            .WithMetadata(new RecordListMetadata(type, typeof(TRecord), typeof(TRequest)))
            .Produces<RecordPage<TRecord>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }

    private static IResult Handle<TRecord, TRequest>(
        [AsParameters] TRequest request,
        HttpContext http,
        IEntityStoreService store,
        ICodexService codex,
        ICodexSearchService search,
        ICodexViewService view,
        ITooltipViewService tooltips)
        where TRecord : EntityRecord
        where TRequest : RecordListRequest
    {
        var type = EntityRecords.TypeOf(typeof(TRecord));
        var reading = request.ToReading();

        if (RecordReadings.Refused(reading, store) is { } refused) return refused;

        var readingKeys = RequestProperties.Of(typeof(TRequest));
        var browsing = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
        var filters = new List<RecordFieldFilter>();
        RecordField? sortField = null;

        foreach (var (key, values) in http.Request.Query)
        {
            if (Controls.Contains(key))
            {
                if (string.Equals(key, "sort", StringComparison.OrdinalIgnoreCase) &&
                    values.LastOrDefault() is { Length: > 0 } sort &&
                    codex.Sorting(sort) is null && codex.Measure(sort) is null &&
                    RecordFields.Find(typeof(TRecord), sort) is { } field)
                {
                    sortField = field;
                    continue;
                }

                browsing[key] = values;
                continue;
            }

            if (readingKeys.Contains(key)) continue;

            if (codex.Facet(key) is not null || codex.Flag(key) is not null || Measured(codex, key))
            {
                browsing[key] = values;
                continue;
            }

            if (Filter(typeof(TRecord), key, values, out var error) is { } filter)
            {
                filters.Add(filter);
                continue;
            }

            return Unreadable(error ?? Unknown(key, codex, typeof(TRecord), readingKeys));
        }

        var query = CodexQuery.Parse(new QueryCollection(browsing), codex, Controls, null, out var refusal);

        if (refusal is not null) return Unreadable(refusal);

        var everything = query with { Types = [type], Page = 1, PageSize = Math.Max(1, store.Entities.Count) };
        var origin = Origins.Of(http);

        var rows = search.Run(everything).Rows
            .Where(document => RecordReadings.Keeps(document, reading, store))
            .Select(document => new Row<TRecord>(document, () => Projected<TRecord>(document, view, origin)))
            .Where(row => filters.All(filter => filter.Matches(row.Record)))
            .ToArray();

        if (sortField is not null) rows = Sorted(rows, sortField, query.Descending);

        var pages = Math.Max(1, (rows.Length + query.PageSize - 1) / query.PageSize);
        var page = Math.Min(query.Page, pages);

        return Results.Ok(new RecordPage<TRecord>(
            rows.Length,
            page,
            pages,
            [
                .. rows.Skip((page - 1) * query.PageSize).Take(query.PageSize)
                    .Select(row => RecordReadings.Read(row.Record, row.Document, reading, store, tooltips)),
            ]));
    }

    private static readonly ConditionalWeakTable<EntityDocument, ConcurrentDictionary<string, EntityRecord>> Projections = new();

    private static TRecord Projected<TRecord>(EntityDocument document, ICodexViewService view, string origin)
        where TRecord : EntityRecord =>
        (TRecord)Projections.GetOrCreateValue(document)
            .GetOrAdd(origin, at => RecordProjection.Of<TRecord>(document, view, at));

    private static bool Measured(ICodexService codex, string key) =>
        (key.EndsWith("Min", StringComparison.OrdinalIgnoreCase) ||
            key.EndsWith("Max", StringComparison.OrdinalIgnoreCase)) &&
        codex.Measure(key[..^3]) is not null;

    private static RecordFieldFilter? Filter(Type record, string key, StringValues values, out string? error)
    {
        error = null;

        var wanted = values
            .SelectMany(value => (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .ToArray();

        if (RecordFields.Find(record, key) is { } field) return new RecordFieldFilter(field) { Wanted = wanted };

        var isMax = key.EndsWith("Max", StringComparison.OrdinalIgnoreCase);

        if (!isMax && !key.EndsWith("Min", StringComparison.OrdinalIgnoreCase)) return null;

        if (RecordFields.Find(record, key[..^3]) is not { Numeric: true } bounded) return null;

        if (wanted.Length == 0) return new RecordFieldFilter(bounded);

        if (!double.TryParse(wanted[^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var bound))
        {
            error = $"'{wanted[^1]}' is not a number.";
            return null;
        }

        return isMax ? new RecordFieldFilter(bounded) { Max = bound } : new RecordFieldFilter(bounded) { Min = bound };
    }

    private static Row<TRecord>[] Sorted<TRecord>(Row<TRecord>[] rows, RecordField field, bool descending)
        where TRecord : EntityRecord
    {
        var ordered = rows.OrderBy(row => !field.Values(row.Record).Any());

        ordered = field.Numeric
            ? descending
                ? ordered.ThenByDescending(row => field.Numbers(row.Record).DefaultIfEmpty().First())
                : ordered.ThenBy(row => field.Numbers(row.Record).DefaultIfEmpty().First())
            : descending
                ? ordered.ThenByDescending(row => field.Texts(row.Record).FirstOrDefault(), StringComparer.OrdinalIgnoreCase)
                : ordered.ThenBy(row => field.Texts(row.Record).FirstOrDefault(), StringComparer.OrdinalIgnoreCase);

        return [.. ordered.ThenBy(row => row.Record.Name, StringComparer.OrdinalIgnoreCase).ThenBy(row => row.Record.Id)];
    }

    private static string Unknown(string key, ICodexService codex, Type record, IReadOnlySet<string> reading) =>
        $"Unknown parameter '{key}'. The parameters are " +
        string.Join(", ", reading.Select(JsonNamingPolicy.CamelCase.ConvertName).Order(StringComparer.Ordinal)
            .Concat(codex.Facets.Select(facet => facet.Key))
            .Concat(codex.Flags.Select(flag => flag.Key))
            .Concat(codex.Measures.SelectMany(measure => new[] { measure.Key + "Min", measure.Key + "Max" }))
            .Concat(RecordFields.Of(record).Select(field => field.Key))
            .Distinct(StringComparer.OrdinalIgnoreCase)) + ".";

    private static IResult Unreadable(string detail) => Results.Problem(
        title: "The filter could not be read.", detail: detail, statusCode: StatusCodes.Status400BadRequest);
}
