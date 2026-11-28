using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;

namespace FellowshipExtractor.Extraction;

/// <summary>
/// The <c>RelevantAttributesToCapture</c> of each native damage and healing calculation class. No shipped file has a
/// native class's default object, so the list is the one its constructor builds. Shipping exe 26320967.
/// <para>
/// Three statics structs have the definitions. Each definition is 0x40 bytes after the pointer of its attribute's
/// property: <c>AttributeToCapture</c>, then <c>AttributeSource</c> at +0x38 and <c>bSnapshot</c> at +0x39. A statics
/// constructor stores the two bytes as one word: 0x0100 is <c>Source</c> with <c>bSnapshot</c>, 0x0001 is
/// <c>Target</c> without it, and 0x0101 is <c>Target</c> with it. <c>FindCaptureSpecByDefinition</c> 0x144A73C10 reads
/// +0x38 as the source, and <c>CaptureAttributeForGameplayEffect</c> 0x144A6F7A0 reads +0x39 as <c>bSnapshot</c>.
/// </para>
/// <para>
/// The capture of a definition with <c>bSnapshot</c> is a copy of the attribute's aggregator (0x144AB4A30). The
/// capture of one without it is a reference to the live aggregator (0x144A65E90). A spec captures its source
/// definitions at <c>FGameplayEffectSpec::Initialize</c> (0x144A801B0), before each execution of an effect with
/// <c>RecaptureSourceAttributesBeforeEveryExecution</c> (0x144A808C4, 0x1449FD735), and at
/// <c>CRBlueprintLibrary::SetEffectContext</c> (0x145243334). It captures its target definitions at each application
/// (0x144A6ACEB).
/// </para>
/// </summary>
public static class CalculationCaptures
{
    private const string SourceEnum = "EGameplayEffectAttributeCaptureSource";
    private const string BasicSet = "CRBasicAttributeSet";
    private const string HeroSet = "CRHeroAttributeSet";

    /// <summary>The word a statics constructor stores for a <c>Source</c> definition with <c>bSnapshot</c>.</summary>
    private const ushort SourceSnapshot = 0x0100;

    /// <summary>The word a statics constructor stores for a <c>Target</c> definition without <c>bSnapshot</c>.</summary>
    private const ushort TargetLive = 0x0001;

    private sealed record Definition(string Attribute, string AttributeSet, ushort Stored);

    // Shared statics: accessor 0x1450E5720, constructor 0x1450D6E10. The comment has the definition's offset and the
    // address of its store.
    private static readonly Definition Strength = new("Strength", BasicSet, SourceSnapshot);                  // +0x08, 0x1450D6FF7
    private static readonly Definition Agility = new("Agility", BasicSet, SourceSnapshot);                    // +0x50, 0x1450D7089
    private static readonly Definition Intellect = new("Intellect", BasicSet, SourceSnapshot);                // +0x98, 0x1450D711E
    private static readonly Definition PrimaryAttribute = new("PrimaryAttribute", BasicSet, SourceSnapshot);  // +0xE0, 0x1450D71B2
    private static readonly Definition CritChance = new("CritChance", BasicSet, SourceSnapshot);              // +0x128, 0x1450D7246
    private static readonly Definition CritMultiplier = new("CritMultiplier", BasicSet, SourceSnapshot);      // +0x170, 0x1450D72DA

    // Damage statics: accessor 0x1450DCBE0, constructor 0x1450D5390.
    private static readonly Definition DamageScale = new("DamageScale", BasicSet, SourceSnapshot);                        // +0x50, 0x1450D58ED
    private static readonly Definition DifficultyDamageScale = new("DifficultyDamageScale", BasicSet, SourceSnapshot);    // +0x320, 0x1450D5982
    private static readonly Definition DamageLifeSteal = new("LifeSteal", BasicSet, SourceSnapshot);                      // +0x08, 0x1450D5A12
    private static readonly Definition SourceDamageReflect = new("SourceDamageReflect", BasicSet, SourceSnapshot);        // +0x98, 0x1450D5AA7
    private static readonly Definition DamageThreatMultiplier = new("DamageThreatMultiplier", BasicSet, SourceSnapshot);  // +0x368, 0x1450D5B3B
    private static readonly Definition DamageExpertise = new("Expertise", HeroSet, SourceSnapshot);                       // +0x560, 0x1450D5BCF
    private static readonly Definition BalancingDamageAura = new("BalancingDamageAura", HeroSet, SourceSnapshot);         // +0x5F0, 0x1450D5C67
    private static readonly Definition DodgeChance = new("DodgeChance", BasicSet, TargetLive);                            // +0x128, 0x1450D5CFF
    private static readonly Definition ParryChance = new("ParryChance", BasicSet, TargetLive);                            // +0x170, 0x1450D5D97
    private static readonly Definition BlockChance = new("BlockChance", BasicSet, TargetLive);                            // +0x1B8, 0x1450D5E2F
    private static readonly Definition BlockDamageReductionFlat = new("BlockDamageReductionFlat", BasicSet, TargetLive);  // +0x200, 0x1450D5EC7
    private static readonly Definition BlockDamageReductionMultiplier =
        new("BlockDamageReductionMultiplier", BasicSet, TargetLive);                                                      // +0x248, 0x1450D5F5F
    private static readonly Definition IncomingDamageMultiplier = new("IncomingDamageMultiplier", BasicSet, TargetLive);  // +0x290, 0x1450D5FF7
    private static readonly Definition PhysicalDamageReduction = new("PhysicalDamageReduction", BasicSet, TargetLive);    // +0x3F8, 0x1450D608F
    private static readonly Definition MagicDamageReduction = new("MagicDamageReduction", BasicSet, TargetLive);          // +0x440, 0x1450D6127
    private static readonly Definition PhysicalIncomingDamageMultiplier =
        new("PhysicalIncomingDamageMultiplier", BasicSet, TargetLive);                                                    // +0x488, 0x1450D61BF
    private static readonly Definition MagicalIncomingDamageMultiplier =
        new("MagicalIncomingDamageMultiplier", BasicSet, TargetLive);                                                     // +0x4D0, 0x1450D6257
    private static readonly Definition AoeIncomingDamageMultiplier =
        new("AoeIncomingDamageMultiplier", BasicSet, TargetLive);                                                         // +0x518, 0x1450D62EF
    private static readonly Definition DamageMitigationScale = new("DamageMitigationScale", BasicSet, TargetLive);        // +0x5A8, 0x1450D6387
    private static readonly Definition TargetDamageReflect = new("TargetDamageReflect", BasicSet, TargetLive);            // +0xE0, 0x1450D641F
    private static readonly Definition Armor = new("Armor", BasicSet, TargetLive);                                        // +0x2D8, 0x1450D64B7
    private static readonly Definition Avoidance = new("Avoidance", HeroSet, TargetLive);                                 // +0x3B0, 0x1450D654F

    // Heal statics: accessor 0x1450E27E0, constructor 0x1450D65C0.
    private static readonly Definition HealLifeSteal = new("LifeSteal", BasicSet, SourceSnapshot);                    // +0x08, 0x1450D6859
    private static readonly Definition HealScale = new("HealScale", BasicSet, SourceSnapshot);                        // +0x170, 0x1450D68EE
    private static readonly Definition HealThreatMultiplier = new("HealThreatMultiplier", BasicSet, SourceSnapshot);  // +0x128, 0x1450D6982
    private static readonly Definition HealExpertise = new("Expertise", HeroSet, SourceSnapshot);                     // +0x1B8, 0x1450D6A16
    private static readonly Definition BalancingHealAura = new("BalancingHealAura", HeroSet, SourceSnapshot);         // +0x290, 0x1450D6AAA
    private static readonly Definition IncomingHealMultiplier = new("IncomingHealMultiplier", BasicSet, TargetLive);  // +0x50, 0x1450D6B3B
    private static readonly Definition IncomingHealSign = new("IncomingHealSign", BasicSet, TargetLive);              // +0x98, 0x1450D6BD0
    private static readonly Definition Health = new("Health", BasicSet, TargetLive);                                  // +0x200, 0x1450D6D00
    private static readonly Definition MaxHealth = new("MaxHealth", BasicSet, TargetLive);                            // +0x248, 0x1450D6D98

    /// <summary><c>CRDamageCalculationBase</c>'s constructor 0x1450D7510, in the order of its 24 additions.</summary>
    private static readonly Definition[] DamageBase =
    [
        CritChance, CritMultiplier, DamageLifeSteal, SourceDamageReflect, TargetDamageReflect, DodgeChance, ParryChance,
        BlockChance, BlockDamageReductionFlat, BlockDamageReductionMultiplier, DamageScale, DifficultyDamageScale,
        IncomingDamageMultiplier, DamageThreatMultiplier, DamageMitigationScale, Avoidance, PhysicalDamageReduction,
        MagicDamageReduction, PhysicalIncomingDamageMultiplier, MagicalIncomingDamageMultiplier,
        AoeIncomingDamageMultiplier, DamageExpertise, BalancingDamageAura, Armor,
    ];

    /// <summary><c>CRHealCalculationBase</c>'s constructor 0x1450D80C0, in the order of its 12 additions.</summary>
    private static readonly Definition[] HealBase =
    [
        CritChance, CritMultiplier, HealThreatMultiplier, IncomingHealMultiplier, IncomingHealSign, HealScale,
        HealLifeSteal, HealExpertise, BalancingHealAura, Health, MaxHealth, DifficultyDamageScale,
    ];

    /// <summary>
    /// Each class's list as its constructor builds it: the list of its base class's constructor, then its own
    /// additions. The comment has the constructor.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, Definition[]> Constructors =
        new Dictionary<string, Definition[]>(StringComparer.Ordinal)
        {
            ["CRDamageCalculationBase"] = DamageBase,                                 // 0x1450D7510
            ["CRStrengthDamageCalculation"] = [.. DamageBase, Strength],              // 0x1450D8910
            ["CRAgilityDamageCalculation"] = [.. DamageBase, Agility],                // 0x1450D73F0
            ["CRIntellectDamageCalculation"] = [.. DamageBase, Intellect],            // 0x1450D84C0
            ["CRPrimaryAttributeDamageCalculation"] = [.. DamageBase, PrimaryAttribute],  // 0x1450D8700
            ["CRUserDefinedDamageCalculation"] = DamageBase,                          // 0x1450D8A30
            ["CRHealCalculationBase"] = HealBase,                                     // 0x1450D80C0
            ["CRStrengthHealCalculation"] = [.. HealBase, Strength],                  // 0x1450D89A0
            ["CRAgilityHealCalculation"] = [.. HealBase, Agility],                    // 0x1450D7480
            ["CRIntellectHealCalculation"] = [.. HealBase, Intellect],                // 0x1450D8550
            ["CRPrimaryAttributeHealCalculation"] = [.. HealBase, PrimaryAttribute],  // 0x1450D8790
            ["CRUserDefinedHealCalculation"] = HealBase,                              // 0x1450D8A60
            ["CRDamageReflectDamageCalculation"] = [DamageScale],                     // 0x1450D7CC0
            ["CRLifeStealHealCalculation"] = [DamageScale, DamageExpertise],          // 0x1450D85E0
            ["CRReflectHealCalculation"] = [DamageScale, HealThreatMultiplier],       // 0x1450D8820
            ["CRManaGainCalculation"] = [],                                           // 0x1450D86D0
        };

    private static IReadOnlyDictionary<string, IReadOnlyList<AttributeCapture>>? read;

    /// <summary>
    /// Each class's captures with the member names of <c>EGameplayEffectAttributeCaptureSource</c>. Each attribute is
    /// checked against the properties of its attribute set class in the mappings.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<AttributeCapture>> Read(AbstractFileProvider provider)
    {
        if (read is not null) return read;

        var mappings = provider.MappingsContainer?.MappingsForGame
            ?? throw new InvalidOperationException(
                $"No mappings are loaded, so the members of {SourceEnum} and the attributes of {BasicSet} cannot be read.");
        var sources = EnumOrdinals.Members(mappings, SourceEnum);
        var built = new SortedDictionary<string, IReadOnlyList<AttributeCapture>>(StringComparer.Ordinal);

        foreach (var (calculation, definitions) in Constructors)
        {
            built[calculation] = [.. definitions.Select(definition => Named(definition, sources, mappings, calculation))];
        }

        return read = built;
    }

    /// <summary>
    /// Checks a scoped modifier's <c>CapturedAttribute</c> against its calculation class's list. The engine finds the
    /// capture of a scoped modifier by the whole definition, so a cooked definition equals an entry of the list.
    /// </summary>
    public static void Check(
        string calculation, string attribute, string? source, bool snapshot, string effect, AbstractFileProvider provider)
    {
        if (!Read(provider).TryGetValue(calculation, out var captures))
        {
            throw new InvalidOperationException(
                $"{effect} has a scoped modifier on {attribute} of {calculation}, and {nameof(CalculationCaptures)} has " +
                "no constructor recorded for that class. Decode the class's constructor in the shipping exe and add " +
                "the definitions it adds to RelevantAttributesToCapture.");
        }

        // A definition with no stated AttributeSource has the struct's zero value, the member with the ordinal 0.
        var stated = new AttributeCapture(
            attribute, source ?? EnumOrdinals.Members(provider.MappingsContainer!.MappingsForGame!, SourceEnum)[0], snapshot);

        if (captures.Contains(stated)) return;

        var recorded = captures.Where(capture => capture.Attribute == attribute).ToArray();

        throw new InvalidOperationException(
            $"{effect} states the captured attribute {attribute} of {calculation} as {stated.AttributeSource} with " +
            $"bSnapshot {stated.Snapshot}. {nameof(CalculationCaptures)} has " +
            (recorded.Length == 0
                ? "no such attribute for that class"
                : string.Join(", ", recorded.Select(capture => $"{capture.AttributeSource} with bSnapshot {capture.Snapshot}"))) +
            ". Decode the class's constructor and its statics constructor in the shipping exe again.");
    }

    private static AttributeCapture Named(
        Definition definition, IReadOnlyDictionary<long, string> sources, TypeMappings mappings, string calculation)
    {
        if (!mappings.Types.TryGetValue(definition.AttributeSet, out var set) ||
            !set.Properties.Values.Any(property => property.Name == definition.Attribute))
        {
            throw new InvalidOperationException(
                $"{nameof(CalculationCaptures)} records {definition.Attribute} of {definition.AttributeSet} for " +
                $"{calculation}, and the mappings have no such property. Decode the statics constructor in the " +
                "shipping exe again.");
        }

        var ordinal = definition.Stored & 0xFF;

        return sources.TryGetValue(ordinal, out var source)
            ? new AttributeCapture(definition.Attribute, source, definition.Stored >> 8 != 0)
            : throw new InvalidOperationException($"{SourceEnum} has no member with the ordinal {ordinal}.");
    }
}
