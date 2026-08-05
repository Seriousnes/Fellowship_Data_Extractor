using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;

namespace FellowshipExtractor.Tooltip;

/// <summary>
/// Everything the build states about turning an item level into stat magnitudes.
///
/// The magnitudes themselves are not in the build: an item declares which stats it has and the
/// share of the budget each slot is worth, and the number a player reads is worked out from the
/// item's level when the item is rolled. Every input to that arithmetic is shipped, so it is read
/// here and published rather than restated by whoever draws the tooltip.
///
/// The two pool curves are the exception to the project's usual "read the data table" shape,
/// because they are not a data table: they are the compiled bodies of
/// HeroItemStatCalculation's two Calc*PoolFromItemLevel functions. The coefficients are read out
/// of the Kismet bytecode and the shape of the expression is asserted, so a build that changes
/// the numbers is picked up and a build that changes the formula fails loudly instead of
/// publishing coefficients that no longer mean what they are named.
/// </summary>
public sealed class StatCalculation
{
    private const string SettingsClass = "CRDynamicItemStatSettings";
    private const string WeightsRowStructure = "/Script/DynamicLootSystemRuntime.StatWeightsDefinition";
    private const string AttributeRowStructure = "/Script/fellowship.CRArmoryItemAttributeTemplate";
    private const string RarityRowStructure =
        "/Script/DynamicLootSystemRuntime.CRDynamicHeroItemLootRarityRuleSet";

    private const string CalculationBlueprint = "HeroItemStatCalculation";
    private const string PrimaryFunction = "CalcPrimaryStatPoolFromItemLevel";
    private const string SecondaryFunction = "CalcSecondaryStatPoolFromItemLevel";
    private const string RuleSetPrefix = "DynamicHeroItemRuleSet.";

    public PrimaryStatPool Primary { get; }
    public SecondaryStatPool Secondary { get; }

    /// <summary>
    /// The three settings blocks, keyed by the slot kind ECRDynamicItemSlotType names: the base
    /// slot every item's primary attribute sits in, the slots an item spells out, and the ones a
    /// rarity adds.
    /// </summary>
    public SlotStatSettings BaseSlot { get; }
    public SlotStatSettings FixedSlot { get; }
    public SlotStatSettings DynamicSlot { get; }

    /// <summary>The growth base, primary-and-stamina multiplier and rarity base, as shipped.</summary>
    public double PrimaryPowerBase { get; }
    public double PrimaryAndStaminaMultiplier { get; }

    /// <summary>
    /// One, in this build, which is the build stating that a rarity does not scale a stat's
    /// magnitude. A rarity's effect on an item's stats is entirely the slots it adds.
    /// </summary>
    public double StatRarityBase { get; }

    /// <summary>The weight rows an item names, by the row name: "40.17.27" holds 40, 17 and 27.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<int>> Weights { get; }

    /// <summary>What each equipment position is worth, keyed by the slot name the game prints.</summary>
    public IReadOnlyDictionary<string, SlotWeight> SlotWeights { get; }

    public IReadOnlyDictionary<string, ArmorTypeWeight> ArmorWeights { get; }

    public IReadOnlyDictionary<string, AttributeDisplay> Attributes { get; }

    /// <summary>
    /// The slots each rarity adds, keyed by the rule set an item names and then by the tier. A
    /// rule set is spelled without its tag prefix, as the item's own exported rule set is.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<int, RaritySlots>> RarityAdditions { get; }

    public StatCalculation(
        AbstractFileProvider provider,
        IReadOnlyList<FAssetData> assets,
        ItemSlotNames slots,
        ArmorTypes armor)
    {
        var settings = Settings(provider, assets);

        PrimaryPowerBase = Number(settings, "PrimaryPowerBase");
        PrimaryAndStaminaMultiplier = Number(settings, "PrimaryAndStaminaMultiplier");
        StatRarityBase = Number(settings, "StatRarityBase");

        BaseSlot = Block(settings, "BaseItemSlotCalculationSettings");
        FixedSlot = Block(settings, "FixedSlotCalculationSettings");
        DynamicSlot = Block(settings, "DynamicSlotCalculationSettings");

        (Primary, Secondary) = Pools(provider);

        Weights = WeightRows(provider, assets);
        SlotWeights = SlotWeightRows(provider, assets, slots);
        ArmorWeights = ArmorWeightRows(provider, assets, armor);
        Attributes = AttributeRows(provider, assets);
        RarityAdditions = RarityRows(provider, assets);

        if (Weights.Count == 0 || SlotWeights.Count == 0 || ArmorWeights.Count == 0 ||
            Attributes.Count == 0 || RarityAdditions.Count == 0)
        {
            throw new InvalidOperationException(
                $"The stat calculation is missing a whole table: {Weights.Count} weight rows, " +
                $"{SlotWeights.Count} slots, {ArmorWeights.Count} armour types, {Attributes.Count} attributes, " +
                $"{RarityAdditions.Count} rule sets. Every item's stat magnitudes would come out wrong.");
        }
    }

    private static UObject Settings(AbstractFileProvider provider, IReadOnlyList<FAssetData> assets)
    {
        foreach (var asset in assets)
        {
            if (!asset.AssetClass.Text.Equals(SettingsClass, StringComparison.OrdinalIgnoreCase)) continue;

            if (provider.TryLoadPackageObject<UObject>(
                AbilityPresentationIndex.PackagePath(asset.ObjectPath), out var loaded)) return loaded;
        }

        throw new InvalidOperationException(
            $"No {SettingsClass} asset in the build. The multipliers every item stat is scaled by live on it, " +
            "and the plugin's own config names it as the stat calculation settings.");
    }

    private static double Number(IPropertyHolder holder, string field) =>
        PropertyWalk.AsNumber(PropertyWalk.Get(holder, field)?.GenericValue)
        ?? throw new InvalidOperationException($"The stat calculation settings have no {field}.");

    private static SlotStatSettings Block(IPropertyHolder settings, string field)
    {
        if (PropertyWalk.GetStruct(settings, field) is not { } block)
        {
            throw new InvalidOperationException($"The stat calculation settings have no {field} block.");
        }

        return new SlotStatSettings
        {
            PrimaryMultiplier = Optional(block, "PrimaryMultiplier"),
            SecondaryMultiplier = Optional(block, "SecondaryMultiplier"),
            StaminaMultiplierAtThreshold = Optional(block, "StaminaMultiplierAtThreshold"),
            WeightMultiplier = Optional(block, "WeightMultiplier"),
            StaminaWeight = Optional(block, "StaminaWeight"),
            SecondaryWeight = Optional(block, "SecondaryWeight"),
        };
    }

    private static double? Optional(IPropertyHolder holder, string field) =>
        PropertyWalk.AsNumber(PropertyWalk.Get(holder, field)?.GenericValue);

    /// <summary>
    /// The two pool curves, read out of the blueprint that computes them.
    ///
    /// Each function compiles to a straight run of assignments with no branching past the
    /// minimum-level guard, so the calls it makes in order are the formula written out. The
    /// sequence is checked against the shape the coefficients are named for; the numbers are
    /// whatever the build holds.
    /// </summary>
    private static (PrimaryStatPool, SecondaryStatPool) Pools(AbstractFileProvider provider)
    {
        var file = provider.Files.Values.FirstOrDefault(f =>
            f.Path.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase) &&
            f.Path.Contains(CalculationBlueprint, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"No {CalculationBlueprint} package in the build; the stat pools have nowhere to come from.");

        var bodies = new Dictionary<string, List<MathCall>>(StringComparer.Ordinal);

        foreach (var export in provider.LoadPackage(file).GetExports())
        {
            if (export is not UStruct { ScriptBytecode: { Length: > 0 } bytecode }) continue;
            bodies[export.Name] = [.. Calls(bytecode)];
        }

        var primary = Expect(bodies, PrimaryFunction,
            ["Less_IntInt", "Conv_IntToDouble", "Divide_DoubleDouble", "Sqrt",
             "MultiplyMultiply_FloatFloat", "Multiply_DoubleDouble", "Multiply_DoubleDouble"]);

        var secondary = Expect(bodies, SecondaryFunction,
            ["Less_IntInt", "Conv_IntToDouble", "Multiply_DoubleDouble", "Sqrt", "Multiply_DoubleDouble"]);

        return (
            new PrimaryStatPool(
                MinimumItemLevel: (int)Constant(primary, 0, PrimaryFunction),
                LevelDivisor: Constant(primary, 2, PrimaryFunction),
                GrowthBase: Constant(primary, 4, PrimaryFunction),
                Multiplier: Constant(primary, 6, PrimaryFunction)),
            new SecondaryStatPool(
                MinimumItemLevel: (int)Constant(secondary, 0, SecondaryFunction),
                LevelMultiplier: Constant(secondary, 2, SecondaryFunction),
                Multiplier: Constant(secondary, 4, SecondaryFunction)));
    }

    /// <summary>One math call the compiled body makes, and the literal it was handed.</summary>
    private readonly record struct MathCall(string Function, double? Constant);

    private static IEnumerable<MathCall> Calls(KismetExpression[] bytecode)
    {
        foreach (var statement in bytecode)
        {
            var assignment = statement switch
            {
                EX_Let let => let.Assignment,
                EX_LetBool let => let.Assignment,
                _ => null,
            };

            if (assignment is not EX_CallMath call) continue;

            yield return new MathCall(call.StackNode.Name, call.Parameters.Select(Literal).OfType<double>().Cast<double?>().FirstOrDefault());
        }
    }

    private static double? Literal(KismetExpression expression) => expression switch
    {
        EX_IntConst value => value.Value,
        EX_FloatConst value => value.Value,
        EX_DoubleConst value => value.Value,
        _ => null,
    };

    private static List<MathCall> Expect(
        IReadOnlyDictionary<string, List<MathCall>> bodies, string function, string[] shape)
    {
        if (!bodies.TryGetValue(function, out var calls))
        {
            throw new InvalidOperationException($"{CalculationBlueprint} no longer has a {function} function.");
        }

        var found = calls.Select(c => c.Function).ToArray();

        if (!found.SequenceEqual(shape, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"{function} no longer computes what its coefficients are named for. Expected the calls " +
                $"{string.Join(", ", shape)} and found {string.Join(", ", found)}.");
        }

        return calls;
    }

    private static double Constant(List<MathCall> calls, int index, string function) =>
        calls[index].Constant
        ?? throw new InvalidOperationException(
            $"{function}'s {calls[index].Function} call no longer takes a literal, so there is no coefficient " +
            "to read off it.");

    private static Dictionary<string, IReadOnlyList<int>> WeightRows(
        AbstractFileProvider provider, IReadOnlyList<FAssetData> assets)
    {
        var found = new Dictionary<string, IReadOnlyList<int>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (rowName, row) in Rows(provider, assets, WeightsRowStructure))
        {
            if (PropertyWalk.Get(row, "Weights")?.GenericValue is not UScriptArray weights) continue;

            found.TryAdd(rowName, [.. weights.Properties
                .Select(p => PropertyWalk.AsNumber(p?.GenericValue))
                .OfType<double>()
                .Select(value => (int)value)]);
        }

        return found;
    }

    private static Dictionary<string, SlotWeight> SlotWeightRows(
        AbstractFileProvider provider, IReadOnlyList<FAssetData> assets, ItemSlotNames slots)
    {
        var found = new Dictionary<string, SlotWeight>(StringComparer.OrdinalIgnoreCase);

        foreach (var (_, row) in Rows(provider, assets, ItemSlotNames.SlotWeightRowStructure))
        {
            // Keyed by the ordinal rather than by the row label, because the labels are a
            // designer's - the table calls ordinal 6 Gloves and ordinal 1 Necklace - and the
            // export spells slots the way the game prints them.
            if (PropertyWalk.AsOrdinal(PropertyWalk.Get(row, "ItemType")?.GenericValue) is not { } ordinal) continue;
            if (slots.ByOrdinal(ordinal) is not { } slot) continue;

            found.TryAdd(slot.Shipped, new SlotWeight(
                StatWeight: Number(row, "StatWeight"),
                ArmorWeight: Number(row, "ArmorWeight")));
        }

        return found;
    }

    private static Dictionary<string, ArmorTypeWeight> ArmorWeightRows(
        AbstractFileProvider provider, IReadOnlyList<FAssetData> assets, ArmorTypes armor)
    {
        var found = new Dictionary<string, ArmorTypeWeight>(StringComparer.OrdinalIgnoreCase);

        foreach (var (_, row) in Rows(provider, assets, ArmorTypes.RowStructure))
        {
            if (armor.Label(PropertyWalk.Get(row, "ArmorType")?.GenericValue) is not { } label) continue;

            found.TryAdd(label, new ArmorTypeWeight(
                ArmorWeight: Number(row, "ArmorWeight"),
                FlatArmor: Number(row, "FlatArmor")));
        }

        return found;
    }

    private static Dictionary<string, AttributeDisplay> AttributeRows(
        AbstractFileProvider provider, IReadOnlyList<FAssetData> assets)
    {
        var found = new Dictionary<string, AttributeDisplay>(StringComparer.OrdinalIgnoreCase);

        foreach (var (rowName, row) in Rows(provider, assets, AttributeRowStructure))
        {
            if (PropertyWalk.GetText(row, "DisplayName") is not { } displayName) continue;

            found.TryAdd(rowName, new AttributeDisplay(
                DisplayName: displayName,
                RoundToDecimals: (int)(PropertyWalk.AsNumber(
                    PropertyWalk.Get(row, "RoundToDecimals")?.GenericValue) ?? 0),
                IsPercent: PropertyWalk.Get(row, "bIsPercent")?.GenericValue is true,
                Attribute: PropertyWalk.GetStruct(row, "Attribute") is { } attribute
                    ? PropertyWalk.GetString(attribute, "AttributeName")
                    : null,
                BaseStatMultiplier: PropertyWalk.AsNumber(
                    PropertyWalk.Get(row, "BaseStatMultiplier")?.GenericValue) ?? 1,
                Brackets: Brackets(row),
                FinalPenaltyPercentage: PropertyWalk.AsNumber(
                    PropertyWalk.Get(row, "FinalPenaltyPercentage")?.GenericValue) ?? 1));
        }

        return found;
    }

    /// <summary>
    /// A rating's diminishing-return bands, in the order the row states them. Kept in that order
    /// rather than sorted: the bands are read as a run from the bottom up, and the shipped
    /// CooldownReduction row states three of them at the same ceiling, which sorting would hide.
    /// </summary>
    private static IReadOnlyList<RatingBracket>? Brackets(IPropertyHolder row)
    {
        if (PropertyWalk.Get(row, "Brackets")?.GenericValue is not UScriptArray array) return null;

        var bands = new List<RatingBracket>(array.Properties.Count);

        foreach (var entry in array.Properties)
        {
            if (entry?.GenericValue is not FScriptStruct { StructType: IPropertyHolder bracket }) continue;

            bands.Add(new RatingBracket(
                MaxBracketValue: PropertyWalk.AsNumber(
                    PropertyWalk.Get(bracket, "MaxBracketValue")?.GenericValue) ?? 0,
                PenaltyPercentage: PropertyWalk.AsNumber(
                    PropertyWalk.Get(bracket, "PenaltyPercentage")?.GenericValue) ?? 1));
        }

        return bands.Count == 0 ? null : bands;
    }

    private static Dictionary<string, IReadOnlyDictionary<int, RaritySlots>> RarityRows(
        AbstractFileProvider provider, IReadOnlyList<FAssetData> assets)
    {
        var found = new Dictionary<string, Dictionary<int, RaritySlots>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (_, row) in Rows(provider, assets, RarityRowStructure))
        {
            if (PropertyWalk.AsOrdinal(PropertyWalk.Get(row, "RarityTier")?.GenericValue) is not { } tier) continue;

            var slots = new RaritySlots(
                Tier: tier,
                GuaranteedSlots: Names(row, "AdditionalGuaranteedSlots"),
                RandomRolls: (int)(PropertyWalk.AsNumber(
                    PropertyWalk.Get(row, "NumberOfRandomRolls")?.GenericValue) ?? 0),
                RandomizedSlots: Names(row, "AdditionalRandomizedSlots"))
            {
                SpecialSlots = Special(row),
            };

            // An array of tags rather than a tag container, so each entry is read on its own.
            foreach (var ruleSet in Names(row, "RuleSets"))
            {
                var name = ruleSet.StartsWith(RuleSetPrefix, StringComparison.OrdinalIgnoreCase)
                    ? ruleSet[RuleSetPrefix.Length..]
                    : ruleSet;

                if (!found.TryGetValue(name, out var byTier))
                {
                    found[name] = byTier = [];
                }

                byTier.TryAdd(tier, slots);
            }
        }

        return found.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyDictionary<int, RaritySlots>)kv.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The conditional slots a row grants, each under the name and condition it states.</summary>
    private static List<SpecialSlot> Special(IPropertyHolder row)
    {
        if (PropertyWalk.Get(row, "SpecialRandomizedSlots")?.GenericValue is not UScriptArray array) return [];

        return [.. array.Properties
            .Select(p => p?.GenericValue is FScriptStruct { StructType: IPropertyHolder slot } ? slot : null)
            .OfType<IPropertyHolder>()
            .Select(slot => (Name: PropertyWalk.GetString(slot, "SlotName"), Condition: PropertyWalk.GetString(slot, "Condition")))
            .Where(entry => entry.Name is not null)
            .Select(entry => new SpecialSlot(entry.Name!, entry.Condition))];
    }

    private static List<string> Names(IPropertyHolder row, string field) =>
        PropertyWalk.Get(row, field)?.GenericValue is UScriptArray array
            ? [.. array.Properties.Select(p => PropertyWalk.AsString(p?.GenericValue)).OfType<string>()]
            : [];

    private static IEnumerable<(string RowName, FStructFallback Row)> Rows(
        AbstractFileProvider provider, IReadOnlyList<FAssetData> assets, string rowStructure)
    {
        foreach (var objectPath in TableFinder.ByRowStructure(assets, rowStructure))
        {
            if (!provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            foreach (var (rowName, row) in table.RowMap) yield return (rowName.Text, row);
        }
    }
}
