namespace Fellowship.SDK;

public sealed record StatCalculationFile
{
    public PrimaryStatPool? PrimaryPool { get; init; }
    public SecondaryStatPool? SecondaryPool { get; init; }

    public double StatRarityBase { get; init; }

    public SlotStatSettings BaseSlot { get; init; } = new();
    public SlotStatSettings FixedSlot { get; init; } = new();
    public SlotStatSettings DynamicSlot { get; init; } = new();

    public IReadOnlyDictionary<string, SlotWeight> SlotWeights { get; init; } =
        new Dictionary<string, SlotWeight>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, ArmorTypeWeight> ArmorWeights { get; init; } =
        new Dictionary<string, ArmorTypeWeight>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, AttributeDisplay> Attributes { get; init; } =
        new Dictionary<string, AttributeDisplay>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> RandomStatPool { get; init; } = [];

    public IReadOnlyDictionary<string, IReadOnlyDictionary<int, RaritySlots>> RarityAdditions { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<int, RaritySlots>>(StringComparer.OrdinalIgnoreCase);
}

public sealed record VocabularyFile
{
    public IReadOnlyList<HeroEntry> Heroes { get; init; } = [];

    public IReadOnlyList<RarityEntry> Rarities { get; init; } = [];

    public IReadOnlyDictionary<string, string> PrintFormats { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, string> Colors { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, TextStyle> TextStyles { get; init; } =
        new Dictionary<string, TextStyle>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> GemTypes { get; init; } = [];

    public IReadOnlyDictionary<string, string> SlotIcons { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, IReadOnlyList<TemperCeiling>> MaxTempers { get; init; } =
        new Dictionary<string, IReadOnlyList<TemperCeiling>>(StringComparer.OrdinalIgnoreCase);

    public StatCalculationFile StatCalculation { get; init; } = new();
}
