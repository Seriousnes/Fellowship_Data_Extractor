namespace FellowshipExtractor.Tests;

public sealed class TypeResolverTests
{
    [Fact]
    public void TheTypeSetIsTheOneTheSpecNames() => Assert.Equal(24, Enum.GetValues<EntityType>().Length);

    [Theory]
    [InlineData(EntityType.Item, "item")]
    [InlineData(EntityType.Gem, "gem")]
    [InlineData(EntityType.LegendaryPower, "legendary-power")]
    [InlineData(EntityType.TalentSlot, "talent-slot")]
    public void ASlugIsTheTypeNameHyphenated(EntityType type, string slug) =>
        Assert.Equal(slug, EntityTypes.Slug(type));

    [Theory]
    [InlineData(EntityType.Item, 5735)]
    [InlineData(EntityType.Effect, 3363)]
    [InlineData(EntityType.Ability, 2380)]
    [InlineData(EntityType.Creature, 244)]
    [InlineData(EntityType.Dungeon, 20)]
    [InlineData(EntityType.Talent, 216)]
    [InlineData(EntityType.TalentSlot, 216)]
    [InlineData(EntityType.Mount, 111)]
    [InlineData(EntityType.Trait, 68)]
    [InlineData(EntityType.GemTrait, 60)]
    [InlineData(EntityType.MountPower, 54)]
    [InlineData(EntityType.TraitPower, 46)]
    [InlineData(EntityType.LegendaryPower, 36)]
    [InlineData(EntityType.Currency, 16)]
    [InlineData(EntityType.Modifier, 26)]
    [InlineData(EntityType.Gem, 24)]
    [InlineData(EntityType.Blessing, 8)]
    [InlineData(EntityType.WeaponPower, 12)]
    [InlineData(EntityType.Hero, 12)]
    [InlineData(EntityType.RelicPower, 9)]
    [InlineData(EntityType.RelicPassive, 5)]
    [InlineData(EntityType.CompanionPet, 14)]
    [InlineData(EntityType.GemType, 6)]
    [InlineData(EntityType.LegendaryStat, 2)]
    public void TheCorpusHoldsTheseCounts(EntityType type, int expected) =>
        Assert.Equal(expected, Export.Loaded.Documents.Count(d => d.Type == type));

    [Fact]
    public void EveryHeroHasEighteenSlots()
    {
        var perHero = Export.Loaded.Documents
            .Where(d => d.Type == EntityType.TalentSlot)
            .GroupBy(d => d.Body["hero"]!.GetValue<string>())
            .ToArray();

        Assert.Equal(12, perHero.Length);
        Assert.All(perHero, hero => Assert.Equal(18, hero.Count()));
    }

    [Fact]
    public void SixtyGemTraitsTenPerType()
    {
        var traits = Export.Loaded.Documents.Where(d => d.Type == EntityType.GemTrait).ToArray();

        Assert.Equal(60, traits.Length);
        Assert.All(traits.GroupBy(t => t.Body["gem"]!.GetValue<string>()), type => Assert.Equal(10, type.Count()));
    }

    [Fact]
    public void TheSlotBorneTalentIsTheTwoHundredAndSixteenth()
    {
        var talent = Export.Loaded.Documents.Single(
            d => d.Type == EntityType.Talent && d.Name == "Macabre Stratagem");

        Assert.Equal(8, talent.Id);
    }

    [Fact]
    public void AnItemPointsAtAugmentationsThatExist()
    {
        var ids = Export.Loaded.Documents.Select(d => d.Id).ToHashSet();

        var legendary = Export.Loaded.Documents
            .Where(d => d.Type == EntityType.Item && d.Body["legendaryStats"] is not null)
            .ToArray();

        Assert.Equal(36, legendary.Length);
        Assert.All(legendary, item => Assert.All(
            item.Body["legendaryStats"]!.AsArray(),
            stat => Assert.Contains(stat!["id"]!.GetValue<int>(), ids)));

        Assert.All(legendary, item => Assert.Equal(2, item.Body["legendaryStats"]!.AsArray().Count));
    }

    [Fact]
    public void AnItemStatesARarityOnlyWhereTheBuildFixesIt()
    {
        var items = Export.Loaded.Documents.Where(d => d.Type == EntityType.Item).ToArray();
        var stated = items.Where(d => d.Body["rarity"] is not null).ToArray();

        var legendary = stated.Where(d => d.Body["legendaryStats"] is not null).ToArray();

        Assert.Equal(36, legendary.Length);
        Assert.All(legendary, item => Assert.Equal("Legendary", item.Body["rarity"]!.GetValue<string>()));

        var rows = stated.Except(legendary).ToArray();

        Assert.Equal(226, rows.Length);
        Assert.All(rows, item => Assert.Null(item.Body["ruleSet"]));

        Assert.Equal(262, stated.Length);
    }
}
