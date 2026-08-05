namespace Fellowship.SDK;

/// <summary>
/// Everything the build states about turning an item level into stat magnitudes, as the sidecar
/// carries it. Every number here is shipped; the arithmetic that puts them together is
/// <c>StatMagnitudes</c>.
/// </summary>
public sealed record StatCalculationFile
{
    public PrimaryStatPool? PrimaryPool { get; init; }
    public SecondaryStatPool? SecondaryPool { get; init; }

    /// <summary>
    /// One, in this build. A rarity does not scale a stat's magnitude; it grants extra slots.
    /// </summary>
    public double StatRarityBase { get; init; }

    public SlotStatSettings BaseSlot { get; init; } = new();
    public SlotStatSettings FixedSlot { get; init; } = new();
    public SlotStatSettings DynamicSlot { get; init; } = new();

    /// <summary>What each equipment position is worth, keyed by the slot name the game prints.</summary>
    public IReadOnlyDictionary<string, SlotWeight> SlotWeights { get; init; } =
        new Dictionary<string, SlotWeight>(StringComparer.OrdinalIgnoreCase);

    /// <summary>What each armour type is worth, keyed by the word the game prints.</summary>
    public IReadOnlyDictionary<string, ArmorTypeWeight> ArmorWeights { get; init; } =
        new Dictionary<string, ArmorTypeWeight>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, AttributeDisplay> Attributes { get; init; } =
        new Dictionary<string, AttributeDisplay>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The stats a slot rolled at random can land on.</summary>
    public IReadOnlyList<string> RandomStatPool { get; init; } = [];

    /// <summary>The slots each rarity adds, keyed by the rule set an item names and then by tier.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<int, RaritySlots>> RarityAdditions { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<int, RaritySlots>>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// The sidecar the extractor writes beside the entity stream, as it deserialises. The entry records
/// are the extractor's own, so a column added there arrives here without being restated; only the
/// envelope is declared, because the extractor's Vocabulary class builds itself from cooked game
/// files and cannot read its own output back.
/// </summary>
public sealed record VocabularyFile
{
    public IReadOnlyList<HeroEntry> Heroes { get; init; } = [];

    public IReadOnlyList<RarityEntry> Rarities { get; init; } = [];

    public IReadOnlyDictionary<string, string> PrintFormats { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>The game's named colours, keyed as the shipped table keys them.</summary>
    public IReadOnlyDictionary<string, string> Colors { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>The rich text style rows, keyed by the tag a description writes: rt.warning.</summary>
    public IReadOnlyDictionary<string, TextStyle> TextStyles { get; init; } =
        new Dictionary<string, TextStyle>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The gem types the build ships, under the words it prints for them.</summary>
    public IReadOnlyList<string> GemTypes { get; init; } = [];

    public StatCalculationFile StatCalculation { get; init; } = new();
}
