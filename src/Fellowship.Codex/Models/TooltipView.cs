using System.Globalization;

using Fellowship.SDK.Documents;

namespace Fellowship.Codex.Models;

public static class TooltipView
{
    public const string Root = "fx-tip";

    public const EntityType TraitType = EntityType.Trait;

    public const int SlotWidth = 48;

    public const string Unresolved = "unresolved";

    public const string RandomModifierLabel = "[Random Modifier]";

    public const string GemSocketLabel = "Gem Socket";

    public static readonly IReadOnlyDictionary<string, int> ImbuedTraitCapacities =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Weapon"] = 5,
            ["Neck"] = 2,
        };

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

    public static string Magnitude(StatLine line) =>
        (line.Value < 0 ? string.Empty : "+") +
        line.Value.Normalize().ToString(CultureInfo.InvariantCulture);

    public sealed record Fact(string Label, string Value, string? Title);

    public sealed record ModifierLine(ModifierKind? Kind, string Name, int Amount)
    {
        public static readonly ModifierLine Random = new(null, RandomModifierLabel, 0);

        public bool Unresolved => Kind is null;
    }
}
