using System.Net.Http.Json;

using Fellowship.SDK.Records;

namespace Fellowship.SDK.Client;

public sealed class CodexClient
{
    public static Uri Origin => CodexAddresses.Origin;

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
            EntityRecords.Record(type), CodexAddresses.Record(type, id, hero), cancellation).ConfigureAwait(false);

    public Task<EntityRecord> GetAsync(
        RecordReference reference, string? hero = null, CancellationToken cancellation = default) =>
        GetAsync(reference.Type, reference.Id, hero, cancellation);

    public Task<SearchResponse> SearchAsync(
        string query, EntityType? type = null, int? limit = null, CancellationToken cancellation = default) =>
        Answered<SearchResponse>(CodexAddresses.Search(query, type, limit), cancellation);

    public Task<EntitiesResponse> EntitiesAsync(
        EntityFilter? filter = null, CancellationToken cancellation = default) =>
        Answered<EntitiesResponse>(CodexAddresses.Entities(filter), cancellation);

    public Task<CatalogResponse> CatalogAsync(CancellationToken cancellation = default) =>
        Answered<CatalogResponse>(CodexAddresses.Catalog, cancellation);

    public async Task<string> TooltipAsync(
        EntityType type, int id, TooltipRequest? asked = null, CancellationToken cancellation = default)
    {
        using var answer = await http
            .GetAsync(CodexAddresses.Tooltip(type, id, asked), cancellation)
            .ConfigureAwait(false);

        await Raised(answer, cancellation).ConfigureAwait(false);

        return await answer.Content.ReadAsStringAsync(cancellation).ConfigureAwait(false);
    }

    private async Task<TRecord> Read<TRecord>(
        EntityType type, int id, string? hero, CancellationToken cancellation)
        where TRecord : EntityRecord =>
        (TRecord)await Answered(
                EntityRecords.Record(type), CodexAddresses.Record(type, id, hero), cancellation)
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
}
