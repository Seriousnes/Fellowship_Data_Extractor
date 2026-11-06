using System.Text.Json.Nodes;

using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;
using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

/// <summary>Talent-slot ids repeat for each hero, so a talent's origin hero comes from the slots naming it.</summary>
public sealed class TalentOriginTests
{
    private const int Invictus = 604;
    private const int FlowerPower = 31;
    private const int Xavian = 25;
    private const int SlotsPerHero = 18;

    private static IEnumerable<(EntityType Type, int Id, string? Name, JsonObject Body)> Of(EntityType type) =>
        Export.Loaded.Documents.Where(document => document.Type == type);

    private static JsonObject Talent(int id) => Of(EntityType.Talent).Single(talent => talent.Id == id).Body;

    private static string[] OriginHeroes(JsonObject document) =>
    [
        .. (document["origins"]?.AsArray() ?? [])
            .Where(origin => origin!["type"]!.GetValue<string>() == "hero")
            .Select(origin => origin!["name"]!.GetValue<string>()),
    ];

    private static TalentSlotDocument Slot(int id, Hero hero, int talent, bool? enabled) => new()
    {
        Id = id,
        EnabledInGame = enabled,
        Hero = hero,
        Row = 1,
        Column = 1,
        Cost = 1,
        Talent = new EntityReference(EntityType.Talent, talent, null),
    };

    [Fact]
    public void InvictusBelongsToXavian()
    {
        var origin = Assert.Single(Talent(Invictus)["origins"]!.AsArray())!;

        Assert.Equal("hero", origin["type"]!.GetValue<string>());
        Assert.Equal(Xavian, origin["id"]!.GetValue<int>());
    }

    [Fact]
    public void FlowerPowerBelongsToTheHeroOfItsSlot()
    {
        var slot = Assert.Single(
            Of(EntityType.TalentSlot),
            slot => slot.Body["talent"]!["id"]!.GetValue<int>() == FlowerPower);

        Assert.Equal([slot.Body["hero"]!.GetValue<string>()], OriginHeroes(Talent(FlowerPower)));
    }

    [Fact]
    public void EveryTalentHasTheHeroOfEachSlotNamingIt()
    {
        var slots = Of(EntityType.TalentSlot).ToArray();

        Assert.Equal(216, slots.Length);
        Assert.All(slots.GroupBy(slot => slot.Body["hero"]!.GetValue<string>()), hero => Assert.Equal(SlotsPerHero, hero.Count()));

        Assert.All(slots, slot => Assert.Contains(
            slot.Body["hero"]!.GetValue<string>(),
            OriginHeroes(Talent(slot.Body["talent"]!["id"]!.GetValue<int>()))));
    }

    [Fact]
    public void TheTwelveHeroesEachHoldEighteenTalents()
    {
        var byHero = Of(EntityType.Talent).SelectMany(talent => OriginHeroes(talent.Body)).GroupBy(hero => hero).ToArray();

        Assert.Equal(12, byHero.Length);
        Assert.All(byHero, hero => Assert.Equal(SlotsPerHero, hero.Count()));
    }

    [Fact]
    public void SlotsSharingAnIdAreJudgedTheSameInEitherOrder()
    {
        var first = Slot(1, Hero.Xavian, 100, enabled: false);
        var second = Slot(1, Hero.Aeona, 200, enabled: null);

        Assert.Equal(
            Reachability.Unreachable([first, second]).Order(),
            Reachability.Unreachable([second, first]).Order());
    }

    [Fact]
    public void ATalentReadsTheHeroOfItsOwnSlotWhenSlotIdsRepeat()
    {
        var heroes = new[] { Hero.Xavian, Hero.Aeona }
            .Select((hero, index) => (EntityDocument)new HeroDocument { Id = 20 + index, Name = hero.ToString() });

        EntityDocument[] documents =
        [
            .. heroes,
            Slot(1, Hero.Aeona, 100, enabled: null),
            Slot(1, Hero.Xavian, 200, enabled: null),
            new TalentDocument { Id = 100 },
            new TalentDocument { Id = 200 },
        ];

        var origins = Origins.Of(documents);

        Assert.Equal([(EntityType.Hero, 21)], origins[(EntityType.Talent, 100)]);
        Assert.Equal([(EntityType.Hero, 20)], origins[(EntityType.Talent, 200)]);
    }
}
