using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

using Fellowship.SDK.Client;

namespace Fellowship.Codex.Tests;

public static class Tooltips
{
    public static Task<HttpResponseMessage> TooltipAsync<TRequest>(
        this HttpClient client, int id, TRequest? request = null)
        where TRequest : class, ITooltipRequest =>
        client.TooltipAsync($"/{CodexAddresses.Tooltip(TRequest.Type, id)}", request);

    public static async Task<HttpResponseMessage> TooltipAsync<TRequest>(
        this HttpClient client, string address, TRequest? request = null)
        where TRequest : class, ITooltipRequest
    {
        using var message = new HttpRequestMessage(HttpMethod.Query, address);

        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

        if (request is not null)
        {
            message.Content = JsonContent.Create(request, options: CodexJson.Options);
        }

        return await client.SendAsync(message, Xunit.TestContext.Current.CancellationToken);
    }

    public static async Task<HttpResponseMessage> TooltipAsync(
        this HttpClient client, string address, string? json = null)
    {
        using var message = new HttpRequestMessage(HttpMethod.Query, address);

        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

        if (json is not null)
        {
            message.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return await client.SendAsync(message, Xunit.TestContext.Current.CancellationToken);
    }

    public static Task<HttpResponseMessage> TooltipAsync(
        this HttpClient client, EntityType type, int id, bool? showIcon = null, bool? mobile = null,
        string? hero = null)
    {
        Task<HttpResponseMessage> Send<TRequest>(TRequest request)
            where TRequest : class, ITooltipRequest => client.TooltipAsync(id, request);

        return type switch
        {
            EntityType.Ability =>
                Send(new AbilityTooltipRequest { ShowIcon = showIcon, Mobile = mobile, Hero = hero }),
            EntityType.Blessing => Send(new BlessingTooltipRequest { ShowIcon = showIcon, Mobile = mobile }),
            EntityType.CompanionPet =>
                Send(new CompanionPetTooltipRequest { ShowIcon = showIcon, Mobile = mobile }),
            EntityType.Creature => Send(new CreatureTooltipRequest { ShowIcon = showIcon, Mobile = mobile }),
            EntityType.Currency => Send(new CurrencyTooltipRequest { ShowIcon = showIcon, Mobile = mobile }),
            EntityType.Dungeon => Send(new DungeonTooltipRequest { ShowIcon = showIcon, Mobile = mobile }),
            EntityType.Effect => Send(new EffectTooltipRequest { ShowIcon = showIcon, Mobile = mobile }),
            EntityType.Gem => Send(new GemTooltipRequest { ShowIcon = showIcon, Mobile = mobile }),
            EntityType.GemTrait => Send(new GemTraitTooltipRequest { ShowIcon = showIcon, Mobile = mobile }),
            EntityType.Item =>
                Send(new ItemTooltipRequest { ShowIcon = showIcon, Mobile = mobile, Hero = hero }),
            EntityType.LegendaryPower =>
                Send(new LegendaryPowerTooltipRequest { ShowIcon = showIcon, Mobile = mobile }),
            EntityType.Mount => Send(new MountTooltipRequest { ShowIcon = showIcon, Mobile = mobile }),
            EntityType.RelicPower =>
                Send(new RelicPowerTooltipRequest { ShowIcon = showIcon, Mobile = mobile }),
            EntityType.SetBonus => Send(new SetBonusTooltipRequest { ShowIcon = showIcon, Mobile = mobile }),
            EntityType.SetBonusPower =>
                Send(new SetBonusPowerTooltipRequest { ShowIcon = showIcon, Mobile = mobile }),
            EntityType.Talent => Send(new TalentTooltipRequest { ShowIcon = showIcon, Mobile = mobile }),
            EntityType.Trait => Send(new TraitTooltipRequest { ShowIcon = showIcon, Mobile = mobile }),
            EntityType.TraitPower =>
                Send(new TraitPowerTooltipRequest { ShowIcon = showIcon, Mobile = mobile }),
            EntityType.WeaponPower =>
                Send(new WeaponPowerTooltipRequest { ShowIcon = showIcon, Mobile = mobile, Hero = hero }),
            _ => throw new ArgumentOutOfRangeException(
                nameof(type), type, $"{type} carries no tooltip request of its own."),
        };
    }

    public static Task<string> BodyAsync(this HttpResponseMessage response) =>
        response.Content.ReadAsStringAsync(Xunit.TestContext.Current.CancellationToken);

    public static async Task<string> TooltipHtmlAsync<TRequest>(
        this HttpClient client, int id, TRequest? request = null)
        where TRequest : class, ITooltipRequest
    {
        using var response = await client.TooltipAsync(id, request);

        var body = await response.BodyAsync();

        Assert.True(response.IsSuccessStatusCode, body);

        return body;
    }
}

public static class Stats
{
    public static IReadOnlyDictionary<string, StatRolls> Rated(string stat, params decimal[] values) =>
        Rated(new Dictionary<string, StatRolls>(StringComparer.OrdinalIgnoreCase), stat, values);

    public static IReadOnlyDictionary<string, StatRolls> Rated(
        this IReadOnlyDictionary<string, StatRolls> stats, string stat, params decimal[] values) =>
        With(stats, stat, rolls => rolls with { Rating = values });

    public static IReadOnlyDictionary<string, StatRolls> Percent(string stat, params decimal[] values) =>
        Percent(new Dictionary<string, StatRolls>(StringComparer.OrdinalIgnoreCase), stat, values);

    public static IReadOnlyDictionary<string, StatRolls> Percent(
        this IReadOnlyDictionary<string, StatRolls> stats, string stat, params decimal[] values) =>
        With(stats, stat, rolls => rolls with { Percentage = values });

    private static IReadOnlyDictionary<string, StatRolls> With(
        IReadOnlyDictionary<string, StatRolls> stats, string stat, Func<StatRolls, StatRolls> change)
    {
        var written = new Dictionary<string, StatRolls>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, rolls) in stats) written[name] = rolls;

        written[stat] = change(written.GetValueOrDefault(stat) ?? new StatRolls());

        return written;
    }
}
