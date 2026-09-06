using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using Fellowship.SDK.Records;

namespace Fellowship.SDK.Client;

public sealed class CodexClient
{
    private const string TrimWarning =
        "A record is read into a type named at run time, so trimming can take that type away.";

    public static Uri Origin => CodexAddresses.Origin;

    private readonly HttpClient http;

    public CodexClient(HttpClient http)
    {
        this.http = http;
        this.http.BaseAddress ??= Origin;
    }

    [RequiresUnreferencedCode(TrimWarning)]
    public Task<TRecord> GetAsync<TRecord>(
        int id, string? hero = null, CancellationToken cancellation = default)
        where TRecord : EntityRecord =>
        Read<TRecord>(EntityRecords.TypeOf(typeof(TRecord)), id, hero, cancellation);

    [RequiresUnreferencedCode(TrimWarning)]
    public async Task<EntityRecord> GetAsync(
        EntityType type, int id, string? hero = null, CancellationToken cancellation = default) =>
        (EntityRecord)await ReadAsync(
            EntityRecords.Record(type), CodexAddresses.Record(type, id, hero), cancellation).ConfigureAwait(false);

    [RequiresUnreferencedCode(TrimWarning)]
    public Task<EntityRecord> GetAsync(
        RecordReference reference, string? hero = null, CancellationToken cancellation = default) =>
        GetAsync(reference.Type, reference.Id, hero, cancellation);

    [RequiresUnreferencedCode(TrimWarning)]
    public Task<SearchResponse> SearchAsync(
        string query, EntityType? type = null, int? limit = null, CancellationToken cancellation = default) =>
        ReadAsync<SearchResponse>(CodexAddresses.Search(query, type, limit), cancellation);

    [RequiresUnreferencedCode(TrimWarning)]
    public Task<EntitiesResponse> EntitiesAsync(
        EntityFilter? filter = null, CancellationToken cancellation = default) =>
        ReadAsync<EntitiesResponse>(CodexAddresses.Entities(filter), cancellation);

    [RequiresUnreferencedCode(TrimWarning)]
    public Task<CatalogResponse> CatalogAsync(CancellationToken cancellation = default) =>
        ReadAsync<CatalogResponse>(CodexAddresses.Catalog, cancellation);

    [RequiresUnreferencedCode(TrimWarning)]
    public async Task<string> TooltipAsync<TRequest>(
        int id, TRequest? request = null, CancellationToken cancellation = default)
        where TRequest : class, ITooltipRequest
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Query, CodexAddresses.Tooltip(TRequest.Type, id));

        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

        if (request is not null)
        {
            message.Content = JsonContent.Create(request, options: CodexJson.Options);
        }

        using var response = await http.SendAsync(message, cancellation).ConfigureAwait(false);

        await ThrowIfFailed(response, cancellation).ConfigureAwait(false);

        return await response.Content.ReadAsStringAsync(cancellation).ConfigureAwait(false);
    }

    [RequiresUnreferencedCode(TrimWarning)]
    private async Task<TRecord> Read<TRecord>(
        EntityType type, int id, string? hero, CancellationToken cancellation)
        where TRecord : EntityRecord =>
        (TRecord)await ReadAsync(
                EntityRecords.Record(type), CodexAddresses.Record(type, id, hero), cancellation)
            .ConfigureAwait(false);

    [RequiresUnreferencedCode(TrimWarning)]
    private async Task<TResponse> ReadAsync<TResponse>(string address, CancellationToken cancellation) =>
        (TResponse)await ReadAsync(typeof(TResponse), address, cancellation).ConfigureAwait(false);

    [RequiresUnreferencedCode(TrimWarning)]
    private async Task<object> ReadAsync(Type recordType, string address, CancellationToken cancellation)
    {
        using var response = await http.GetAsync(address, cancellation).ConfigureAwait(false);

        await ThrowIfFailed(response, cancellation).ConfigureAwait(false);

        return await response.Content
                .ReadFromJsonAsync(recordType, CodexJson.Options, cancellation)
                .ConfigureAwait(false)
            ?? throw new CodexException(
                response.StatusCode,
                new CodexFault { Title = "The codex answered nothing.", Detail = $"{address} answered null." });
    }

    [RequiresUnreferencedCode(TrimWarning)]
    private static async Task ThrowIfFailed(HttpResponseMessage response, CancellationToken cancellation)
    {
        if (response.IsSuccessStatusCode) return;

        CodexFault? fault = null;

        try
        {
            fault = await response.Content
                .ReadFromJsonAsync<CodexFault>(CodexJson.Options, cancellation)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is System.Text.Json.JsonException or NotSupportedException)
        {
            fault = null;
        }

        throw new CodexException(response.StatusCode, fault ?? new CodexFault());
    }
}
