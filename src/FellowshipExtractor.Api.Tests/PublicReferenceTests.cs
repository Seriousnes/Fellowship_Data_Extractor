using System.Text.Json;

namespace FellowshipExtractor.Api.Tests;

public sealed class PublicReferenceTests : IClassFixture<RouteTests.Factory>
{
    private readonly HttpClient client;

    private readonly ICodexViewService view;

    public PublicReferenceTests(RouteTests.Factory factory)
    {
        client = factory.CreateClient();
        view = factory.Services.GetRequiredService<ICodexViewService>();
    }

    public static TheoryData<string> Shapes() =>
    [
        "owner", "appliedBy", "nameBorrowedFrom", "artBorrowedFrom", "setBonus", "grants",
        "legendaryStats", "power", "replaces", "foundIn", "abilities", "passives",
    ];

    private static EntityDocument Carrying(string shape) =>
        RouteTests.Factory.Store.Entities
            .Where(document =>
                RouteTests.Factory.Store.All(EntityTypes.TypeOf(document), document.Id).Count == 1)
            .FirstOrDefault(document => shape switch
        {
            "owner" => document is AbilityDocument { Owner: not null } or EffectDocument { Owner: not null },
            "appliedBy" => document is EffectDocument { AppliedBy: not null },
            "nameBorrowedFrom" => document is EffectDocument { NameBorrowedFrom: not null },
            "artBorrowedFrom" => document is EffectDocument { ArtBorrowedFrom: not null },
            "setBonus" => document is ItemDocument { SetBonus.Count: > 0 },
            "grants" => document is ItemDocument { Grants: not null },
            "legendaryStats" => document is ItemDocument { LegendaryStats.Count: > 0 },
            "power" => document is TraitDocument { Power: not null } or SetBonusDocument { Power: not null },
            "replaces" => document is GemTraitDocument { Replaces: not null },
            "foundIn" => document is CreatureDocument { FoundIn.Count: > 0 },
            "abilities" => document is CreatureDocument { Abilities.Count: > 0 },
            "passives" => document is CreatureDocument { Passives.Count: > 0 },
            _ => false,
        })
        ?? throw new InvalidOperationException($"No document in the export carries a {shape} reference.");

    [Theory]
    [MemberData(nameof(Shapes))]
    public async Task Every_reference_shape_carries_a_resource_link_the_api_answers_at(string shape)
    {
        var document = Carrying(shape);
        var slug = EntityTypes.Slug(EntityTypes.TypeOf(document));

        var body = await client.GetStringAsync(
            $"/api/{slug}/{document.Id}", Xunit.TestContext.Current.CancellationToken);

        using var reading = JsonDocument.Parse(body);

        Assert.True(reading.RootElement.TryGetProperty(shape, out var carried), $"{slug} emitted no {shape}.");

        var references = References(shape, carried).ToArray();

        Assert.NotEmpty(references);

        foreach (var reference in references) Verify(reference);
    }

    private static IEnumerable<JsonElement> References(string shape, JsonElement carried)
    {
        if (carried.ValueKind == JsonValueKind.Object)
        {
            yield return carried;
            yield break;
        }

        if (carried.ValueKind != JsonValueKind.Array) yield break;

        foreach (var entry in carried.EnumerateArray())
        {
            yield return shape is "abilities" or "passives" ? entry.GetProperty("ability") : entry;
        }
    }

    private static void Verify(JsonElement reference)
    {
        var slug = reference.GetProperty("type").GetString();
        var id = reference.GetProperty("id").GetInt32();
        var resource = reference.GetProperty("resource").GetString();

        Assert.False(string.IsNullOrWhiteSpace(slug));
        Assert.True(EntityTypes.TryParse(slug!, out _), $"'{slug}' names no type.");

        Assert.NotNull(resource);
        Assert.True(Uri.TryCreate(resource, UriKind.Absolute, out var absolute), $"{resource} is not absolute.");
        Assert.Equal($"/api/{slug}/{id}", absolute!.AbsolutePath);
    }

    [Fact]
    public void A_talent_slot_carries_the_talent_it_bears_as_a_reference()
    {
        var slot = RouteTests.Factory.Store.Entities.OfType<TalentSlotDocument>().First();

        var reference = PublicEntity.From(slot, view, "https://host").Talent;

        Assert.NotNull(reference);
        Assert.Equal(EntityTypes.Slug(EntityType.Talent), reference.Type);
        Assert.Equal($"https://host/api/talent/{reference.Id}", reference.Resource);
    }

    [Fact]
    public async Task A_resource_link_answers_the_record_it_names()
    {
        var creature = RouteTests.Factory.Store.Entities
            .OfType<CreatureDocument>()
            .First(row => row.FoundIn is { Count: > 0 });

        var body = await client.GetStringAsync(
            $"/api/creature/{creature.Id}", Xunit.TestContext.Current.CancellationToken);

        using var reading = JsonDocument.Parse(body);

        var resource = reading.RootElement.GetProperty("foundIn")[0].GetProperty("resource").GetString()!;

        var followed = await client.GetStringAsync(
            new Uri(resource).AbsolutePath, Xunit.TestContext.Current.CancellationToken);

        using var dungeon = JsonDocument.Parse(followed);

        Assert.Equal("dungeon", dungeon.RootElement.GetProperty("type").GetString());
    }
}
