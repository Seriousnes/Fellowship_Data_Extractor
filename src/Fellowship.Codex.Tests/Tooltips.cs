using System.Net.Http.Headers;
using System.Net.Http.Json;

using Fellowship.SDK.Client;

namespace Fellowship.Codex.Tests;

public static class Tooltips
{
    public static Task<HttpResponseMessage> TooltipAsync(
        this HttpClient client, EntityType type, int id, TooltipRequest? request = null) =>
        client.TooltipAsync($"/{CodexAddresses.Tooltip(type, id)}", request);

    public static async Task<HttpResponseMessage> TooltipAsync(
        this HttpClient client, string address, TooltipRequest? request = null)
    {
        using var message = new HttpRequestMessage(HttpMethod.Query, address);

        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

        if (request is not null)
        {
            message.Content = JsonContent.Create(request, options: CodexJson.Options);
        }

        return await client.SendAsync(message, Xunit.TestContext.Current.CancellationToken);
    }

    public static Task<string> BodyAsync(this HttpResponseMessage response) =>
        response.Content.ReadAsStringAsync(Xunit.TestContext.Current.CancellationToken);

    public static async Task<string> TooltipHtmlAsync(
        this HttpClient client, EntityType type, int id, TooltipRequest? request = null)
    {
        using var response = await client.TooltipAsync(type, id, request);

        var body = await response.BodyAsync();

        Assert.True(response.IsSuccessStatusCode, body);

        return body;
    }

    public static TooltipRequest Rated(this TooltipRequest request, string stat, params decimal[] values) =>
        request.WithStat(stat, rolls => rolls with { Rating = values });

    public static TooltipRequest Percent(this TooltipRequest request, string stat, params decimal[] values) =>
        request.WithStat(stat, rolls => rolls with { Percentage = values });

    private static TooltipRequest WithStat(
        this TooltipRequest request, string stat, Func<StatRolls, StatRolls> change)
    {
        var stats = new Dictionary<string, StatRolls>(StringComparer.OrdinalIgnoreCase);

        if (request.Stats is not null)
        {
            foreach (var (name, rolls) in request.Stats) stats[name] = rolls;
        }

        stats[stat] = change(stats.GetValueOrDefault(stat) ?? new StatRolls());

        return request with { Stats = stats };
    }
}
