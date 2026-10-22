using System.Text.Json.Nodes;

using CUE4Parse.UE4.Assets.Exports;

using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class VerifiedTokenTests
{
    private const int BloodboundSpirit = 2296;
    private const int RazorShrapnel = 1613;
    private const int UndulatingSpiritAbility = 2226;
    private const int UndulatingSpiritPower = 398;
    private const int DragonsEmbrace = 2074;
    private const int TheDragon = 686;

    private const string UndulatingPackage =
        "fellowship/Content/Abilities/Heroes/Rime/Talents/DamageAsAoeHealingBuff_GuaranteedSpiritProcBuff/GA_Rime_Passive_Talent_DamageAsAoeHealingBuff_GuaranteedSpiritProcBuff";

    private static JsonObject Document(EntityType type, int id) =>
        Export.Loaded.Documents.First(row => row.Type == type && row.Id == id).Body;

    private static double Constant(string row) =>
        new ConstantsTable(Export.Provider).Lookup(row).Record!.Value.Value!.Value;

    [Fact]
    public void ATokenTheAbilityResolvesReadsItsWholeExpression()
    {
        var rend = Document(EntityType.Ability, BloodboundSpirit)["placeholders"]!.AsArray()
            .Single(placeholder => placeholder!["key"]!.GetValue<string>() == "Rend")!;

        var expected = Constant("Gunde.DurationalAoeDotAndBuff.DotScaler") *
                       Constant("Gunde.PerTargetAccumulatedBleed.DamageToDotScaler");

        Assert.Equal(1.0, expected);
        Assert.Equal(expected, rend["value"]!.GetValue<double>());
    }

    [Fact]
    public void ATargetCapInABlueprintVariableIsTheAbilitysMaxTargets() =>
        Assert.Equal(8, Document(EntityType.Ability, RazorShrapnel)["maxTargets"]!.GetValue<double>());

    [Fact]
    public void AGrantOnlyAnUnboundEventReachesIsNotGenerated()
    {
        var exports = Export.Provider.LoadPackage(UndulatingPackage).GetExports().ToArray();

        Assert.Contains("Dungeon Started", BytecodeGates.Unbound(exports));
        Assert.Null(Document(EntityType.Ability, UndulatingSpiritAbility)["generates"]);
        Assert.Null(Document(EntityType.LegendaryPower, UndulatingSpiritPower)["generates"]);
    }

    [Fact]
    public void AHeroSentenceReadsItsAbsorbThroughTheHeroToDataMap()
    {
        foreach (var body in new[] { Document(EntityType.TraitPower, TheDragon), Document(EntityType.Ability, DragonsEmbrace) })
        {
            var sentences = body["descriptionsByHero"]!.AsObject();

            Assert.Contains("10% of your maximum health", sentences["Elarion"]!.GetValue<string>());
            Assert.Contains("6% of your maximum health", sentences["Aeona"]!.GetValue<string>());
            Assert.Contains("6% of your maximum health", sentences["Helena"]!.GetValue<string>());
            Assert.All(sentences, sentence => Assert.DoesNotContain("{absorb}", sentence.Value!.GetValue<string>()));
        }

        Assert.Equal(0.1, Constant("Perk.MaxHealthBasedAbsorb.DPS.MaxHealthPercentage"));
        Assert.Equal(0.06, Constant("Perk.MaxHealthBasedAbsorb.Lisa.MaxHealthPercentage"));

        var token = Document(EntityType.Ability, DragonsEmbrace)["placeholders"]!.AsArray()
            .Single(placeholder => placeholder!["key"]!.GetValue<string>() == "uniqueHeroDescription")!;

        Assert.Null(token["constants"]);
    }

    [Fact]
    public void ARarityPrintsTheNameTheSubheaderWidgetSwitchesTo()
    {
        var rarities = Export.Loaded.Settings.Rarities.ToDictionary(rarity => rarity.Name, rarity => rarity.DisplayName);

        Assert.Equal("Heroic", rarities["Champion"]);
        Assert.Equal("Regal", rarities["Heroic"]);
        Assert.Equal("Legendary", rarities["Legendary"]);
    }

    [Fact]
    public void TheAbilityTooltipReadsItsNumbersAndWordsFromTheWidget()
    {
        var text = Export.Loaded.Settings.AbilityTooltip!;

        Assert.Equal(100, text.UnitsPerYard);
        Assert.Equal(250, text.MeleeMaximum);
        Assert.Equal("range", text.Range);
        Assert.Equal("Melee Range", text.MeleeRange);
        Assert.Equal("Self", text.Self);
        Assert.Equal("yd", text.RangeUnit);

        Assert.Contains(
            Export.Loaded.Documents,
            row => row.Type == EntityType.Ability && row.Body["range"]?.GetValue<double>() == 0);
    }
}
