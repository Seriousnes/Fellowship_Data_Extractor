using System.Globalization;
using System.Net.Http.Json;
using System.Text;

using Fellowship.SDK.Records;

namespace Fellowship.SDK.Client;

public sealed class CodexClient
{
    public static Uri Origin { get; } = new("https://codex.fellowshipanalyzer.com/");

    private readonly HttpClient http;

    public CodexClient(HttpClient http)
    {
        this.http = http;
        this.http.BaseAddress ??= Origin;
    }

    public Task<TRecord> GetAsync<TRecord>(
        int id, string? hero = null, CancellationToken cancellation = default)
        where TRecord : EntityRecord =>
        Read<TRecord>(EntityRecords.TypeOf(typeof(TRecord)), id, hero, cancellation);

    public async Task<EntityRecord> GetAsync(
        EntityType type, int id, string? hero = null, CancellationToken cancellation = default) =>
        (EntityRecord)await Answered(
            EntityRecords.Record(type), Record(type, id, hero), cancellation).ConfigureAwait(false);

    public Task<EntityRecord> GetAsync(
        RecordReference reference, string? hero = null, CancellationToken cancellation = default) =>
        GetAsync(reference.Type, reference.Id, hero, cancellation);

    public Task<SearchResponse> SearchAsync(
        string query, EntityType? type = null, int? limit = null, CancellationToken cancellation = default) =>
        Answered<SearchResponse>(
            Address(
                "api/search",
                ("q", query),
                ("type", type is { } narrowed ? EntityTypes.Slug(narrowed) : null),
                ("limit", Number(limit))),
            cancellation);

    public Task<EntitiesResponse> EntitiesAsync(
        EntityFilter? filter = null, CancellationToken cancellation = default) =>
        Answered<EntitiesResponse>(Entities(filter), cancellation);

    public Task<CatalogResponse> CatalogAsync(CancellationToken cancellation = default) =>
        Answered<CatalogResponse>("api/catalog", cancellation);

    public async Task<string> TooltipAsync(
        EntityType type, int id, TooltipRequest? asked = null, CancellationToken cancellation = default)
    {
        using var answer = await http
            .GetAsync(Tooltip(type, id, asked), cancellation)
            .ConfigureAwait(false);

        await Raised(answer, cancellation).ConfigureAwait(false);

        return await answer.Content.ReadAsStringAsync(cancellation).ConfigureAwait(false);
    }

    private async Task<TRecord> Read<TRecord>(
        EntityType type, int id, string? hero, CancellationToken cancellation)
        where TRecord : EntityRecord =>
        (TRecord)await Answered(EntityRecords.Record(type), Record(type, id, hero), cancellation)
            .ConfigureAwait(false);

    private async Task<TAnswer> Answered<TAnswer>(string address, CancellationToken cancellation) =>
        (TAnswer)await Answered(typeof(TAnswer), address, cancellation).ConfigureAwait(false);

    private async Task<object> Answered(Type shape, string address, CancellationToken cancellation)
    {
        using var answer = await http.GetAsync(address, cancellation).ConfigureAwait(false);

        await Raised(answer, cancellation).ConfigureAwait(false);

        return await answer.Content
                .ReadFromJsonAsync(shape, CodexJson.Options, cancellation)
                .ConfigureAwait(false)
            ?? throw new CodexException(
                answer.StatusCode,
                new CodexFault { Title = "The codex answered nothing.", Detail = $"{address} answered null." });
    }

    private static async Task Raised(HttpResponseMessage answer, CancellationToken cancellation)
    {
        if (answer.IsSuccessStatusCode) return;

        CodexFault? fault = null;

        try
        {
            fault = await answer.Content
                .ReadFromJsonAsync<CodexFault>(CodexJson.Options, cancellation)
                .ConfigureAwait(false);
        }
        catch (Exception trouble) when (trouble is System.Text.Json.JsonException or NotSupportedException)
        {
            fault = null;
        }

        throw new CodexException(answer.StatusCode, fault ?? new CodexFault());
    }

    private static string Record(EntityType type, int id, string? hero) =>
        Address($"api/{EntityTypes.Slug(type)}/{Number(id)}", ("hero", hero));

    private static string Tooltip(EntityType type, int id, TooltipRequest? asked)
    {
        var address = new StringBuilder($"api/{EntityTypes.Slug(type)}/{Number(id)}/tooltip");

        var given = new List<(string Key, string? Value)>
        {
            ("showIcon", Flag(asked?.ShowIcon)),
            ("mobile", Flag(asked?.Mobile)),
            ("hero", asked?.Hero),
            ("difficulty", Number(asked?.Difficulty)),
        };

        if (asked?.Parameters is { Count: > 0 } parameters)
        {
            given.AddRange(parameters.Select(entry => (entry.Key, (string?)entry.Value)));
        }

        return Written(address, given);
    }

    private static string Entities(EntityFilter? filter)
    {
        var given = new List<(string Key, string? Value)>
        {
            ("q", filter?.Q),
            ("type", filter?.Type is { Count: > 0 } types
                ? string.Join(",", types.Select(EntityTypes.Slug))
                : null),
            ("sort", filter?.Sort),
            ("dir", filter?.Dir),
            ("group", filter?.Group),
            ("page", Number(filter?.Page)),
            ("size", Number(filter?.Size)),
        };

        if (filter?.Facets is { Count: > 0 } facets)
        {
            given.AddRange(facets.Select(entry => (entry.Key, (string?)entry.Value)));
        }

        return Written(new StringBuilder("api/entities"), given);
    }

    private static string Address(string path, params (string Key, string? Value)[] given) =>
        Written(new StringBuilder(path), given);

    private static string Written(
        StringBuilder address, IReadOnlyList<(string Key, string? Value)> given)
    {
        var first = true;

        foreach (var (key, value) in given)
        {
            if (string.IsNullOrEmpty(value)) continue;

            address.Append(first ? '?' : '&');
            address.Append(Uri.EscapeDataString(key));
            address.Append('=');
            address.Append(Uri.EscapeDataString(value));

            first = false;
        }

        return address.ToString();
    }

    private static string? Flag(bool? given) => given is true ? "true" : null;

    private static string? Number(int? given) =>
        given?.ToString(CultureInfo.InvariantCulture);

    private static string Number(int given) => given.ToString(CultureInfo.InvariantCulture);
}
