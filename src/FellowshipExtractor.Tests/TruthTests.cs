using System.Text.Json.Nodes;
using FellowshipExtractor.Extraction;
namespace FellowshipExtractor.Tests;
public sealed class TruthTests
{
    private static JsonObject Record(EntityType type, int id) =>
        Export.Loaded.Documents
            .First(document => document.Type == type && document.Id == id)
            .Body;
    [Theory]
    [InlineData(EntityType.Effect, 2009, 4)]
    [InlineData(EntityType.Ability, 1398, 4)]
    [InlineData(EntityType.Effect, 1321, 12)]
    [InlineData(EntityType.Effect, 1491, 20)]
    [InlineData(EntityType.Effect, 1820, 30)]
    [InlineData(EntityType.Effect, 1752, 4)]
    [InlineData(EntityType.Ability, 976, 4)]
    public void ADurationIsWhatTheCalculationReturnsBare(EntityType type, int id, double expected) =>
        Assert.Equal(expected, (double?)Record(type, id)["duration"]);
    [Theory]
    [InlineData(996, 1)]
    [InlineData(995, 1)]
    [InlineData(994, 1)]
    [InlineData(1887, 9)]
    [InlineData(1859, 218)]
    [InlineData(1110, 100)]
    [InlineData(1242, 50)]
    public void ACostIsTheCastCostNotATalentRow(int id, double expected) =>
        Assert.Equal(expected, (double?)Record(EntityType.Ability, id)["cost"]);
    [Theory]
    [InlineData(1229, 25)]
    [InlineData(1242, 50)]
    [InlineData(1970, 30)]
    [InlineData(1964, 30)]
    [InlineData(1973, 50)]
    public void AFractionOfTheResourceIsWrittenAsTheAmountItSpends(int id, double expected) =>
        Assert.Equal(expected, (double?)Record(EntityType.Ability, id)["cost"]);
    [Fact]
    public void NoRecordSaysItsCostIsAFraction() =>
        Assert.All(Export.Loaded.Documents, document => Assert.Null(document.Body["costIsFraction"]));
    [Theory]
    [InlineData(2280, "range", 1000)]
    [InlineData(3, "castTime", 1.5)]
    [InlineData(981, "range", 800)]
    [InlineData(1312, "channelTime", 2)]
    [InlineData(1027, "channelTime", 2)]
    public void TheTooltipHookArgumentIsTheDisplayedValue(int id, string field, double expected) =>
        Assert.Equal(expected, (double?)Record(EntityType.Ability, id)[field]);
    [Theory]
    [InlineData(1892, "Steady Hands")]
    [InlineData(3189, "The Philosopher")]
    [InlineData(2854, "The Dragon's Embrace")]
    public void AnEffectShipsItsOwnUiDataName(int id, string expected) =>
        Assert.Equal(expected, (string?)Record(EntityType.Effect, id)["name"]);
    private static JsonObject Token(EntityType type, int id, string key) =>
        Record(type, id)["placeholders"]?.AsArray()
            .OfType<JsonObject>()
            .FirstOrDefault(token => (string?)token["key"] == key)
        ?? throw new InvalidOperationException($"{type} {id} carries no {{{key}}} placeholder.");
    [Theory]
    [InlineData(155, "VoidTouchDmg", 42.5, "PrimaryAttribute", true)]
    [InlineData(1241, "IntimidateDmgBreak", 30, "Strength", false)]
    [InlineData(1848, "SolarShldAbsrb", 22.998, "Strength", true)]
    public void ATokenTheBodyMultipliesByAnAttributeIsACoefficientNotAValue(
        int id, string key, double coefficient, string attribute, bool expertise)
    {
        var token = Token(EntityType.Ability, id, key);
        Assert.Equal(coefficient, (double?)token["coefficient"]);
        Assert.Equal(attribute, (string?)token["attribute"]);
        Assert.Null(token["value"]);
        Assert.Equal(expertise ? true : null, (bool?)token["scalesWithExpertise"]);
    }
    [Fact]
    public void ABodyCombiningReadsAcrossAttributesShipsItsBracket()
    {
        var token = Token(EntityType.Ability, 1122, "StoneShieldHealth");
        Assert.Equal(8.624, (double?)token["coefficient"]);
        Assert.Equal("Agility", (string?)token["attribute"]);
        var bracket = token["bracket"]?.AsObject();
        Assert.NotNull(bracket);
        Assert.Equal(-1, (double?)bracket!["addend"]);
        var terms = bracket["terms"]?.AsArray().OfType<JsonObject>()
            .ToDictionary(term => (string)term["attribute"]!, term => (double)term["coefficient"]!);
        Assert.NotNull(terms);
        Assert.Equal(
            new Dictionary<string, double>
            {
                ["CritChance"] = 2, ["Expertise"] = 2, ["Haste"] = 2, ["Spirit"] = 2,
            },
            terms);
    }
    [Theory]
    [InlineData(1926, "TemporBarrDmg", 1.91, "Intellect")]
    [InlineData(1926, "TemporBarrHeal", 5.22, "Intellect")]
    [InlineData(156, "TwilSkyDmg", 9.552, "Intellect")]
    [InlineData(156, "TwilSkyHeal", 19.104, "Intellect")]
    public void ARangeBodyTheReaderUsedToRefuseNowCarriesItsCoefficient(
        int id, string key, double coefficient, string attribute)
    {
        var token = Token(EntityType.Ability, id, key);
        Assert.Equal(coefficient, (double?)token["coefficient"]);
        Assert.Equal(attribute, (string?)token["attribute"]);
        Assert.True((bool?)token["scalesWithExpertise"]);
    }
    [Fact]
    public void ARoundedRangeStillReadsItsExpertiseScaling()
    {
        var token = Token(EntityType.Ability, 1028, "GlacialBlast");
        Assert.Equal(10.24, (double?)token["coefficient"]);
        Assert.Equal("Intellect", (string?)token["attribute"]);
        Assert.True((bool?)token["scalesWithExpertise"]);
    }
    [Fact]
    public void AStackCountIsNoneAtRestSoTheBaseHealRemains()
    {
        var token = Token(EntityType.Ability, 1119, "SerenityHeal");
        Assert.Equal(2.2, (double?)token["coefficient"]);
        Assert.Equal("Agility", (string?)token["attribute"]);
        Assert.Equal(0.2, (double?)token["spread"]);
        Assert.True((bool?)token["scalesWithExpertise"]);
        Assert.Null(token["bracket"]);
    }
    [Theory]
    [InlineData(1100, "EarthFistDmg", 0.1)]
    [InlineData(1229, "SkullCrushDmg", 0.2)]
    [InlineData(1229, "SkullCrushLightnDmg", 0.2)]
    public void ATokenBandsInWhatItsEffectOverridesWithOrTheDefaultItsPresetDeclares(
        int id, string key, double spread) =>
        Assert.Equal(spread, (double?)Token(EntityType.Ability, id, key)["spread"]);
    [Theory]
    [InlineData(46, 0.4)]
    [InlineData(448, 0.2)]
    public void TwoAbilitiesOfOneBossPartOnWhetherTheirEffectBindsASpreadRow(int id, double spread)
    {
        var coefficient = Record(EntityType.Ability, id)["coefficients"]?.AsArray().OfType<JsonObject>().Single();
        Assert.NotNull(coefficient);
        Assert.Equal(spread, (double?)coefficient!["spread"]);
    }
    [Fact]
    public void ACreatureCoefficientBandsInTheEffectOnlyItsBytecodeNames()
    {
        var coefficient = Record(EntityType.Ability, 92)["coefficients"]?.AsArray()
            .OfType<JsonObject>()
            .Single();
        Assert.NotNull(coefficient);
        Assert.Equal("Strength", (string?)coefficient!["attribute"]);
        Assert.Equal(2.89, (double?)coefficient["value"]);
        Assert.Equal(0.2, (double?)coefficient["spread"]);
    }
    [Fact]
    public void AnEffectBorrowsTheNameOfTheAbilityOnlyItsBytecodeNames()
    {
        var effect = Record(EntityType.Effect, 1212);
        Assert.Equal("Frost Bolt", (string?)effect["name"]);
        Assert.Equal(958, (int?)effect["nameBorrowedFrom"]?["id"]);
    }
    [Theory]
    [InlineData(1868, "Shoot")]
    [InlineData(1089, "Pounce")]
    public void AnEffectAnAbilityOnlyComparesOrFiltersOnKeepsItsApplierName(int id, string expected) =>
        Assert.Equal(expected, (string?)Record(EntityType.Effect, id)["name"]);
    [Theory]
    [InlineData(1226, 4)]
    [InlineData(1244, 4)]
    public void ARowAnAppliedEffectBindsToItsDeclaredValueStillReadsAsTheAbilityDuration(
        int id, double expected) =>
        Assert.Equal(expected, (double?)Record(EntityType.Ability, id)["duration"]);
    [Theory]
    [InlineData(2223, 4.096, true)]
    [InlineData(2224, 2.114, false)]
    public void ACoefficientOnTheAbilityAReferenceNamesIsRead(int id, double coefficient, bool expertise)
    {
        var token = Token(EntityType.Ability, id, "Talons");
        Assert.Equal(coefficient, (double?)token["coefficient"]);
        Assert.Equal("Intellect", (string?)token["attribute"]);
        Assert.Equal(0.2, (double?)token["spread"]);
        Assert.Equal(expertise ? true : null, (bool?)token["scalesWithExpertise"]);
    }
    private static JsonObject Modifier(EntityType type, int id, string attribute) =>
        Record(type, id)["modifiers"]?.AsArray()
            .OfType<JsonObject>()
            .FirstOrDefault(modifier => (string?)modifier["attribute"] == attribute)
        ?? throw new InvalidOperationException($"{type} {id} carries no modifier on {attribute}.");
    [Fact]
    public void AnAbsorbConsumptionScalerIsWhatItsCalculationClampsTo()
    {
        var modifier = Modifier(EntityType.Effect, 1321, "AbsorbConsumptionScaler");
        Assert.Equal(0.5, (double?)modifier["value"]);
        Assert.Null(modifier["op"]);
    }
    [Fact]
    public void ABlockMultiplierIsTheRowItsCalculationReads()
    {
        var modifier = Modifier(EntityType.Effect, 6, "BlockDamageReductionMultiplier");
        Assert.Equal(0.8, (double?)modifier["value"]);
        Assert.Equal("Multiplicitive", (string?)modifier["op"]);
    }
    [Fact]
    public void AMagnitudeMultiplyingACapturedAttributeIsACoefficient()
    {
        var modifier = Modifier(EntityType.Effect, 1735, "MaxResources");
        Assert.Equal(200, (double?)modifier["coefficient"]);
        Assert.Null(modifier["value"]);
        Assert.Equal(["Strength"], modifier["scalesOff"]?.AsArray().Select(scale => (string?)scale));
    }
    [Theory]
    [InlineData(1321, "IncreasedDuration", 12, 15)]
    [InlineData(1315, "IncreasedDuration", 24, 30)]
    [InlineData(1316, "IncreasedDuration", 30, 37.5)]
    [InlineData(1334, "DurationAndCost", 8, 14)]
    [InlineData(1491, "IncreasedHealthAndDuration", 20, 30)]
    [InlineData(1379, "DurationBoostAndHealFromDamage", 4, 8)]
    [InlineData(1752, "IncreasedDurationAndTaunt", 4, 8)]
    [InlineData(1448, "IncreasedHotDuration", 15, 24)]
    [InlineData(2325, "ExtendedDuration", 9, 12)]
    [InlineData(2624, "AbilityDamageIncrease", 20, 15)]
    [InlineData(2009, "ChargesCooldownAndDuration", 4, 15)]
    public void ATalentTheCalculationGatesOnCarriesItsOwnDuration(
        int id, string talent, double bare, double held)
    {
        var record = Record(EntityType.Effect, id);
        Assert.Equal(bare, (double?)record["duration"]);
        Assert.Equal(held, (double?)record["talents"]?[talent]?["duration"]);
    }
    [Fact]
    public void ATokenTheUiDataDoesNotDeclareComesFromTheSoleModifier()
    {
        var token = Token(EntityType.Effect, 2710, "Increase");
        Assert.Equal(480, (double?)token["value"]);
        Assert.Equal(480, (double?)Modifier(EntityType.Effect, 2710, "AmethystPower")["value"]);
    }
    [Fact]
    public void TheOneCalculationAnsweringARangeStatesBothEnds()
    {
        var modifier = Modifier(EntityType.Effect, 1731, "DurationMagnitude");
        Assert.Equal(6, (double?)modifier["valueLow"]);
        Assert.Equal(8, (double?)modifier["valueHigh"]);
        Assert.Null(modifier["value"]);
    }
    [Fact]
    public void AnApplierWithAnAssignmentTheWalkCannotReadPublishesNoDuration() =>
        Assert.Null(Record(EntityType.Effect, 2461)["duration"]);
    [Theory]
    [InlineData(1583, 4)]
    [InlineData(1756, 0.6)]
    [InlineData(2446, 20)]
    [InlineData(2907, 0.4)]
    public void ADurationTheApplierSetsByCallerIsRecoveredFromItsBytecode(int id, double expected) =>
        Assert.Equal(expected, (double?)Record(EntityType.Effect, id)["duration"]);
    [Fact]
    public void APerkWritesEachHeroTheSentenceTheirOwnDynamicTextTableCarries()
    {
        var record = Record(EntityType.TraitPower, 689);
        var elarion = (string?)record["descriptionsByHero"]?["Elarion"];
        Assert.Equal("{uniqueHeroDescription}", (string?)record["description"]);
        Assert.NotNull(elarion);
        Assert.Contains("duration is increased", elarion, StringComparison.Ordinal);
    }
    [Fact]
    public void APerkSubstitutesTheConstantTheHerosOwnTokenNames()
    {
        var elarion = (string?)Record(EntityType.TraitPower, 688)["descriptionsByHero"]?["Elarion"];
        Assert.NotNull(elarion);
        Assert.Contains("20%", elarion, StringComparison.Ordinal);
    }
    [Fact]
    public void EveryPerkWhoseDescriptionIsTheSharedTokenCarriesTwelveHeroSentences()
    {
        var filled = Export.Loaded.Documents
            .Where(document => document.Body["descriptionsByHero"] is not null)
            .ToArray();
        Assert.Equal(7, filled.Length);
        Assert.All(filled, document => Assert.Equal(12, document.Body["descriptionsByHero"]!.AsObject().Count));
    }
    [Theory]
    [InlineData(2, "Sahril's Madness", "Ascension", 2)]
    [InlineData(14, "Meteor Rain", "Dungeon", 3)]
    [InlineData(18, "Promoted Enemies", "Implicit", 0)]
    public void AModifierRowNamesItsKindAndTheRankItAsks(
        int id, string name, string kind, int minimumHeroRank)
    {
        var record = Record(EntityType.Modifier, id);
        Assert.Equal(name, (string?)record["name"]);
        Assert.Equal(kind, (string?)record["kind"]);
        Assert.Equal(minimumHeroRank, (int?)record["minimumHeroRank"]);
    }
    [Fact]
    public void AModifierRowCarriesTheDescriptionAndIconTheTableStates()
    {
        var record = Record(EntityType.Modifier, 2);
        Assert.Contains("Increased Threat", (string?)record["description"], StringComparison.Ordinal);
        Assert.NotNull((string?)record["icon"]);
    }
    [Theory]
    [InlineData(26)]
    [InlineData(28)]
    public void AModifierTagNoRowStatesExportsAsAStub(int id)
    {
        var record = Record(EntityType.Modifier, id);
        Assert.Null(record["name"]);
        Assert.Null(record["kind"]);
        Assert.Null(record["description"]);
    }
    [Fact]
    public void EveryModifierTagButTheThreeBranchesIsWritten()
    {
        var written = Export.Loaded.Documents.Where(document => document.Type == EntityType.Modifier).ToArray();
        Assert.Equal(26, written.Length);
        Assert.Equal(24, written.Count(document => document.Body["kind"] is not null));
    }
    [Theory]
    [InlineData(1630, "Talent Points", "Epic")]
    [InlineData(1629, "Mastery Points", "Epic")]
    [InlineData(1742, "Max number of Equippable Legendaries", "Legendary")]
    [InlineData(2110, "League Level", "Epic")]
    public void AProgressionItemCarriesTheNameAndRarityItsRowStates(int id, string name, string rarity)
    {
        var record = Record(EntityType.Item, id);
        Assert.Equal(name, (string?)record["name"]);
        Assert.Equal(rarity, (string?)record["rarity"]);
        Assert.NotNull((string?)record["icon"]);
    }
    [Fact]
    public void AProgressionItemCarriesTheSentenceItsRowStates() =>
        Assert.Contains(
            "allocated within the Hero Sheet",
            (string?)Record(EntityType.Item, 1630)["description"],
            StringComparison.Ordinal);
    [Fact]
    public void ATitleTakesBothItsNameAndItsTitleTextFromTheOneProperty()
    {
        var record = Record(EntityType.Item, 5597);
        Assert.Equal("Fellow", (string?)record["name"]);
        Assert.Equal("Fellow", (string?)record["titleText"]);
        Assert.Equal("Unlocked by reaching Adept League with Any Hero.", (string?)record["acquire"]);
        Assert.Contains("Fellowship community", (string?)record["description"], StringComparison.Ordinal);
        Assert.Equal("Uncommon", (string?)record["rarity"]);
    }
    [Theory]
    [InlineData(4959, "Chief Rebel")]
    [InlineData(4977, "Chief Rebel")]
    [InlineData(6024, "Gold")]
    [InlineData(4986, "Chief Rebel Name Color")]
    public void TheOtherFourCustomizationTablesNameTheirRowsWithDisplayName(int id, string name)
    {
        var record = Record(EntityType.Item, id);
        Assert.Equal(name, (string?)record["name"]);
        Assert.Null(record["titleText"]);
        Assert.Equal("Legendary", (string?)record["rarity"]);
    }
    [Theory]
    [InlineData(4986)]
    public void ACustomizationRowStatesHowItIsAcquired(int id) =>
        Assert.Equal("Become an employee at Chief Rebel", (string?)Record(EntityType.Item, id)["acquire"]);
    [Theory]
    [InlineData(5103, "Complete Paragon league as a Tank hero.")]
    [InlineData(5110, "This can be purchased in the Store.")]
    public void ACompanionPetStatesHowItIsAcquired(int id, string acquire) =>
        Assert.Equal(acquire, (string?)Record(EntityType.CompanionPet, id)["acquire"]);
    [Fact]
    public void TheTenPetsWithAnAssetStateHowTheyAreAcquired()
    {
        var pets = Export.Loaded.Documents.Where(document => document.Type == EntityType.CompanionPet).ToArray();
        Assert.Equal(14, pets.Length);
        Assert.Equal(10, pets.Count(pet => pet.Body["acquire"] is not null));
    }
    [Fact]
    public void AMountWhoseLoreOnlyTheLocalizationTableCarriesShipsIt() =>
        Assert.Contains(
            "spirit-bear",
            (string?)Record(EntityType.Mount, 4082)["lore"],
            StringComparison.Ordinal);
    [Theory]
    [InlineData(9, "Gunde", "The Northlander", "Strength")]
    [InlineData(7, "Ardeos", "The Pyromancer", "Intellect")]
    [InlineData(14, "Sylvie", "The Daughter of the Forest", "Intellect")]
    [InlineData(2, "Elarion", "The Skystrider", "Agility")]
    public void AHeroCarriesTheNameTitleAndMainStatTheBuildStates(
        int id, string name, string title, string mainStat)
    {
        var record = Record(EntityType.Hero, id);
        Assert.Equal(name, (string?)record["name"]);
        Assert.Equal(title, (string?)record["title"]);
        Assert.Equal(mainStat, (string?)record["mainStat"]);
    }
    [Fact]
    public void TheHeroWhoseProseSitsUnderCleanKeysCarriesAllThreePassages()
    {
        var record = Record(EntityType.Hero, 9);
        Assert.Contains("blood-magic", (string?)record["biography"], StringComparison.Ordinal);
        Assert.Contains("Rend is Gunde", (string?)record["playstyle"], StringComparison.Ordinal);
        Assert.Contains("Spirit Refund", (string?)record["spiritAbility"], StringComparison.Ordinal);
    }
    [Theory]
    [InlineData(7, "too reckless", "Burning Embers", "costs Burning Embers")]
    [InlineData(17, null, null, "Spirit Refund")]
    public void AHeroCarriesTheProseItsMetadataAssetStates(
        int id, string? biography, string? playstyle, string spiritAbility)
    {
        var record = Record(EntityType.Hero, id);
        Assert.Contains(biography ?? string.Empty, (string?)record["biography"], StringComparison.Ordinal);
        Assert.Contains(playstyle ?? string.Empty, (string?)record["playstyle"], StringComparison.Ordinal);
        Assert.Contains(spiritAbility, (string?)record["spiritAbility"], StringComparison.Ordinal);
    }

    [Fact]
    public void EveryHeroCarriesItsBiographyPlaystyleAndSpiritAbility() =>
        Assert.All(
            Export.Loaded.Documents.Where(document => document.Type == EntityType.Hero),
            hero =>
            {
                Assert.NotNull((string?)hero.Body["biography"]);
                Assert.NotNull((string?)hero.Body["playstyle"]);
                Assert.NotNull((string?)hero.Body["spiritAbility"]);
            });

    [Theory]
    [InlineData(2, "Dps", 1687)]
    [InlineData(13, "Tank", 2216)]
    [InlineData(14, "Healer", 1643)]
    public void AHeroCarriesTheRoleAndBaseHealthItsAssetsState(int id, string role, double baseHealth)
    {
        var record = Record(EntityType.Hero, id);
        Assert.Equal(role, (string?)record["role"]);
        Assert.Equal(baseHealth, (double?)record["baseAttributes"]?["BaseHealth"]);
    }
    [Fact]
    public void EveryHeroColourIsTheHexTheSettingsPublishForTheSameName()
    {
        var heroes = Export.Loaded.Documents.Where(document => document.Type == EntityType.Hero).ToArray();
        var published = Export.Loaded.Settings.Heroes.ToDictionary(
            hero => hero.Name, hero => hero.Color, StringComparer.OrdinalIgnoreCase);
        Assert.All(
            heroes,
            hero =>
            {
                Assert.NotNull((string?)hero.Body["color"]);
                Assert.Equal(published[(string)hero.Body["name"]!], (string?)hero.Body["color"]);
            });
        Assert.Equal("5A1F20", (string?)Record(EntityType.Hero, 9)["color"]);
    }
    [Fact]
    public void TheTwelveRosterHeroesAreWrittenAndNoVariantOrUnbuiltCodenameIs()
    {
        var written = Export.Loaded.Documents.Where(document => document.Type == EntityType.Hero).ToArray();
        Assert.Equal(12, written.Length);
        Assert.All(written, document => Assert.NotNull((string?)document.Body["title"]));
    }
    [Theory]
    [InlineData(2740, 1964, "Heal")]
    [InlineData(2741, 1964, "Cost")]
    [InlineData(2826, 1964, "RemoveStagger")]
    [InlineData(1703, 1200, "Lockout")]
    [InlineData(1704, 1200, "Interrupt")]
    public void AnEffectNamedAfterAnAbilityCarriesTheAbilityAndTheRoleItPlays(int id, int ability, string role)
    {
        var record = Record(EntityType.Effect, id);
        Assert.Equal("ability", (string?)record["partOf"]!["type"]);
        Assert.Equal(ability, (int?)record["partOf"]!["id"]);
        Assert.Equal(role, (string?)record["role"]);
    }
    [Theory]
    [InlineData(3172, 1249, "DamageOld")]
    [InlineData(2196, 1047, "TurretCounter")]
    [InlineData(1836, 1275, "CustomGlobalCooldown")]
    public void AMultiWordTailIsWrittenAsTheRole(int id, int ability, string role)
    {
        var record = Record(EntityType.Effect, id);
        Assert.Equal(ability, (int?)record["partOf"]!["id"]);
        Assert.Equal(role, (string?)record["role"]);
    }
    [Fact]
    public void EveryEffectNamedAsPartOfAnAbilityCarriesARole() =>
        Assert.All(
            Export.Loaded.Documents.Where(document => document.Type == EntityType.Effect),
            document => Assert.True(document.Body["partOf"] is null || document.Body["role"] is not null));
    [Fact]
    public void AnEffectNoAbilityTagNamesCarriesNeitherField()
    {
        var record = Record(EntityType.Effect, 2382);
        Assert.Null(record["partOf"]);
        Assert.Null(record["role"]);
    }
    [Fact]
    public void ARoleIsOnlyWrittenBesideTheAbilityItIsPartOf() =>
        Assert.All(
            Export.Loaded.Documents.Where(document => document.Type == EntityType.Effect),
            document => Assert.True(document.Body["role"] is null || document.Body["partOf"] is not null));

    [Fact]
    public void AMountWithNoAugmentationTagStatesItsRarityAndHowItIsAcquired()
    {
        var record = Record(EntityType.Mount, 2564);
        Assert.Equal("Ael'dethi Prowler", (string?)record["name"]);
        Assert.Equal("Champion", (string?)record["rarity"]);
        Assert.Equal("Unlocked during Steam Next Fest 2025", (string?)record["acquire"]);
    }

    [Fact]
    public void ACreatureAbilityCarriesTheReuseCooldownItsBehaviourComponentStates()
    {
        var attack = Record(EntityType.Creature, 249)["abilities"]!.AsArray()
            .First(slot => (string?)slot!["ability"]!["name"] == "Attack")!;

        Assert.Equal("RandomRange", (string?)attack["cooldown"]!["type"]);
        Assert.Equal(3.5, (double?)attack["cooldown"]!["minSeconds"]);
        Assert.Equal(3.5, (double?)attack["cooldown"]!["maxSeconds"]);
    }

    [Fact]
    public void AWeaponNamesTheTraitTreeTheSettingsLayOut()
    {
        Assert.Equal("ItemTraitTrees.EASeason4Weapon", (string?)Record(EntityType.Item, 5981)["traitTree"]);

        var tree = Export.Loaded.Settings.TraitTrees.Single(entry => entry.Tag == "ItemTraitTrees.EASeason4Weapon");
        Assert.Equal(13, tree.Nodes.Count);
        Assert.Equal(6, tree.Nodes.Select(node => node.TraitRowIndex).Distinct().Count());
        Assert.Equal([1, 2], tree.Nodes.Single(node => node.UITreeIndex == 4).ParentNodes);
        Assert.Equal(["ItemTrait.TraitCategory.WeaponAbility"], tree.Nodes.Single(node => node.UITreeIndex == 0).TraitCategories);
    }

    [Theory]
    [InlineData("ItemTraitTrees.EASeason4Weapon", "UW_StaticWeaponTree", 4, 0d, 30d)]
    [InlineData("ItemTraitTrees.EASeason4Necklace", "UW_StaticNeclaceTree_EASeason4", 3, -180d, -25.5)]
    public void ATraitTreeIsLaidOutOnTheCanvasItsWidgetPlacesItsNodesOn(
        string tag, string widget, int node, double x, double y)
    {
        var layout = Export.Loaded.Settings.TraitTrees.Single(entry => entry.Tag == tag).Layout!;

        Assert.Equal(widget, layout.Widget);
        Assert.Equal((586d, 500d), (layout.Width, layout.Height));
        Assert.Equal(new TraitTreePoint(0, 220), layout.Root);
        Assert.Equal(new TraitTreePoint(x, y), layout.Nodes[node]);
        Assert.Equal((71d, 82d), (layout.NodeWidth, layout.NodeHeight));
        Assert.Contains(layout.RootLayers, layer => layer.Item);
    }

    [Fact]
    public void ATraitNodeIsColouredByItsCategoryAndGreyOtherwise()
    {
        var rows = Export.Loaded.Settings.TraitNodeColorRows!;

        Assert.Equal("game.rarity-rare", rows.ByCategory["ItemTrait.TraitCategory.Heroic"]);
        Assert.Equal("game.rarity-uncommon", rows.ByCategory["ItemTrait.TraitCategory.Defensive"]);
        Assert.Equal("game.rarity-epic", rows.ByCategory["ItemTrait.TraitCategory.Major"]);
        Assert.Equal("core.disabled-900", rows.Otherwise);
    }

    [Fact]
    public void ATraitNodeUpgradeCostsTheCurveInTheCurrencyItsRowNames() =>
        Assert.Equal(
            [(1, 1000d), (2, 2000d), (3, 3000d), (4, 3000d)],
            Export.Loaded.Settings.TraitNodeUpgradeCosts
                .Where(cost => cost.Currency == 4 && cost.Rank <= 4)
                .Select(cost => (cost.Rank, cost.Cost)));

    [Fact]
    public void ADungeonPlacesEveryCreatureItsAlwaysLoadedLevelsHoldOnAFloor()
    {
        var dungeon = Record(EntityType.Dungeon, 5);
        Assert.Equal(4, dungeon["floors"]!.AsArray().Count);
        Assert.Equal(323, dungeon["placements"]!.AsArray().Count);
        Assert.All(dungeon["placements"]!.AsArray(), placed => Assert.NotNull(placed!["floor"]));
    }

    [Fact]
    public void TheMaxDungeonScoreLadderInterpolatesItsCurve()
    {
        var scores = Export.Loaded.Settings.DifficultyScaling.MaxDungeonScore!;
        Assert.Equal(140, scores[1]);
        Assert.Equal(455, scores[21]);
        Assert.Equal(920, scores[52]);
        Assert.Equal(19, Export.Loaded.Settings.DifficultyScaling.ModifierAbilityUnlock);
    }
}
