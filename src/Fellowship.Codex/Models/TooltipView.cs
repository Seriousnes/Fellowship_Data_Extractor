using System.Globalization;

using Fellowship.SDK.Documents;

namespace Fellowship.Codex.Models;

public static class TooltipView
{
    public const string Root = "fx-tip";

    public const string AbilityCard = "tip-ability";

    public const EntityType TraitType = EntityType.Trait;

    public const int SlotWidth = 48;

    public const string Unresolved = "unresolved";

    public const string RandomModifierLabel = "[Random Modifier]";

    public const string GemSocketLabel = "Gem Socket";

    public static EntityType TypeOf(ModifierKind kind) => kind switch
    {
        ModifierKind.Trait => TraitType,
        ModifierKind.Blessing => EntityType.Blessing,
        ModifierKind.Essence => EntityType.GemType,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, $"{kind} names no entity type."),
    };

    public static string Label(ModifierKind kind) => kind switch
    {
        ModifierKind.Trait => "Trait",
        ModifierKind.Blessing => "Blessing",
        ModifierKind.Essence => "Imbued Essence",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, $"{kind} names no label."),
    };

    public static string Token(ModifierKind? kind) => kind?.ToString().ToLowerInvariant() ?? Unresolved;

    public static string? Wearer(ItemDocument item, TooltipQuery query) => query.Hero ?? item.Hero?.ToString();

    public static string? PrintedFor(EntityDocument document, string? wearer) =>
        document is TraitDocument { NameByHero: { } names } && wearer is not null &&
        names.TryGetValue(wearer, out var printed)
            ? printed
            : null;

    public static string Magnitude(StatLine line) =>
        (line.Value < 0 ? string.Empty : "+") +
        line.Value.Normalize().ToString(CultureInfo.InvariantCulture);

    public sealed record Fact(string Label, string Value, string? Title);

    public const string InstantCast = "Instant";

    public const string CastLabel = "cast";

    public const string RangeLabel = "range";

    public const string CooldownLabel = "cooldown";

    public const string SecondsUnit = "s";

    public sealed record Reading(string? Value, string? Unit, string? Label)
    {
        public string? Title { get; init; }

        public bool Spent { get; init; }
    }

    public sealed record Overview(
        IReadOnlyList<Reading> CastAndRange, IReadOnlyList<Reading> CostAndCooldown)
    {
        public static readonly Overview None = new([], []);

        public bool Stated => CastAndRange.Count > 0 || CostAndCooldown.Count > 0;
    }

    public const string GlyphRowValue = "+1";

    public static string Plus(int value) => "+" + value.ToString(CultureInfo.InvariantCulture);

    public sealed record ModifierLine(ModifierKind? Kind, string Name, string? Value)
    {
        public static readonly ModifierLine Random = new(null, RandomModifierLabel, null);

        public bool Unresolved => Kind is null;
    }
}
