using System.Net;
using System.Text.Json;

namespace Fellowship.Codex.Tests;

public sealed class ResourceGainRecordTests : IClassFixture<RouteTests.Factory>
{
    private readonly HttpClient client;

    public ResourceGainRecordTests(RouteTests.Factory factory) => client = factory.CreateClient();

    private static IReadOnlyList<EntityDocument> Generating() =>
    [
        .. RouteTests.Factory.Store.Entities
            .Where(document => Models.FellowshipCodex.Generates(document).Count > 0),
    ];

    private async Task<JsonElement?> Record(EntityDocument document)
    {
        var address = $"/api/{EntityTypes.Slug(EntityTypes.TypeOf(document))}/{document.Id}";

        using var response = await client.GetAsync(address, Xunit.TestContext.Current.CancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound) return null;

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(Xunit.TestContext.Current.CancellationToken);

        return JsonDocument.Parse(body).RootElement.Clone();
    }

    [Fact]
    public async Task Every_generation_the_export_states_reaches_the_api_record_that_answers_for_it()
    {
        var answered = 0;

        foreach (var document in Generating())
        {
            if (await Record(document) is not { } record) continue;

            answered++;

            Assert.True(
                record.TryGetProperty("generates", out var stated),
                $"{EntityTypes.TypeOf(document)} {document.Id} states a generation in the export " +
                "and the api record carries none");

            Assert.Equal(Models.FellowshipCodex.Generates(document).Count, stated.GetArrayLength());
        }

        Assert.True(answered > 100, $"only {answered} generating documents were answered by the api");
    }

    [Fact]
    public async Task An_api_record_states_the_pool_the_amount_and_the_rows_the_export_named()
    {
        var document = Generating()
            .First(one => Models.FellowshipCodex.Generates(one).Any(gain => gain.Amount is not null));

        var gain = Models.FellowshipCodex.Generates(document).First(one => one.Amount is not null);

        var record = await Record(document);

        var stated = record!.Value
            .GetProperty("generates")
            .EnumerateArray()
            .First(one => one.TryGetProperty("amount", out _));

        Assert.Equal(gain.Resource, stated.GetProperty("resource").GetString());
        Assert.Equal(gain.ResourceName, stated.GetProperty("resourceName").GetString());
        Assert.Equal(gain.Amount, stated.GetProperty("amount").GetDouble());
        Assert.Equal(gain.Source, stated.GetProperty("source").GetString());
        Assert.Equal(gain.SourceKind, stated.GetProperty("sourceKind").GetString());
    }
}
