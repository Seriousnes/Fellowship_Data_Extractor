using System.Globalization;

using Fellowship.SDK.Documents;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Models;

public static class TooltipView
{
    public const string Root = "fx-tip";

    public const string AbilityCard = "tip-ability";

    public const int SlotWidth = 48;

    public const string Unresolved = "unresolved";

    public const string GemSocketLabel = "Gem Socket";

    public static EntityType TypeOf(ModifierKind kind, SettingsFile settings) =>
        ItemCraft.Definition(kind.ToString(), settings) switch
        {
            { SlotType: CraftSlots.Augmentation, AugmentationPool: [var category] }
                when AugmentationTypes.ByCategory.TryGetValue(category, out var type) => type,
            { SlotType: CraftSlots.AbilityRank } => EntityType.Glyph,
            { SlotType: CraftSlots.GemPower } => EntityType.Radiance,
            var definition => throw new ArgumentOutOfRangeException(
                nameof(kind), kind, $"{kind} is a {definition.SlotType} slot, which names no entity type."),
        };

    public static ModifierTypeEntry Type(ModifierKind kind, SettingsFile settings) =>
        settings.ModifierTypes.FirstOrDefault(type => type.Name == kind.ToString())
        ?? throw new InvalidOperationException(
            $"The settings state no modifier type {kind}, so its row has no header and its slot no cap.");

    public static string Header(ModifierKind kind, SettingsFile settings) =>
        Type(kind, settings).Header ?? Type(kind, settings).Printed ?? kind.ToString();

    public static string Plural(ModifierKind kind, SettingsFile settings) =>
        Type(kind, settings).Plural is { Length: > 0 } plural ? plural : Type(kind, settings).Printed ?? kind.ToString();

    public static string TreeHeader(SettingsFile settings) =>
        settings.AttunementTreeHeader
        ?? throw new InvalidOperationException("The settings state no attunement tree tooltip header.");

    public static string TreeName(SettingsFile settings) => TreeHeader(settings).TrimEnd(':', ' ');

    public static ModifierLine Empty(SettingsFile settings) =>
        settings.EmptyModifierSlot is { } empty
            ? new ModifierLine(null, null, empty.Sign, empty.Text, null)
            : throw new InvalidOperationException("The settings state no empty modifier slot text.");

    public static string Token(ModifierKind? kind) => kind?.ToString().ToLowerInvariant() ?? Unresolved;

    public static string? Wearer(ItemDocument item, TooltipQuery query) => query.Hero ?? item.Hero?.ToString();

    public sealed record LegendaryEntry(EntityDocument? Roll, Fellowship.SDK.Models.LegendaryStatText? Untagged);

    public static IReadOnlyList<LegendaryEntry> Legendary(IReadOnlyList<EntityDocument> rolls, EntityDocument entity)
    {
        var entries = rolls.Select(roll => new LegendaryEntry(roll, null)).ToList();

        foreach (var untagged in ((entity as ItemDocument)?.UntaggedLegendaryStats ?? []).OrderBy(stat => stat.Position))
        {
            entries.Insert(Math.Min(untagged.Position, entries.Count), new LegendaryEntry(null, untagged));
        }

        return entries;
    }

    public static AugmentationDocument? Augmentation(IEntityStoreService store, int id, string? wearer)
    {
        AugmentationDocument[] readings =
        [
            .. AugmentationTypes.All.SelectMany(type => store.All(type, id)).OfType<AugmentationDocument>(),
        ];

        var hero = wearer is null ? null : store.Hero(wearer);

        return (hero is null ? null : readings.FirstOrDefault(reading => EntityStore.Reads(reading, hero)))
            ?? readings.FirstOrDefault();
    }

    public static string Magnitude(StatLine line) =>
        (line.Value < 0 ? string.Empty : "+") +
        line.Value.Normalize().ToString(CultureInfo.InvariantCulture);

    public sealed record Fact(string Label, string Value, string? Title);

    public static AbilityTooltipText AbilityText(SettingsFile settings) =>
        settings.AbilityTooltip ?? throw new InvalidOperationException(
            "The settings carry no abilityTooltip, so no ability tooltip reads its cast, range or cooldown words.");

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

    public sealed record ModifierLine(ModifierKind? Kind, string? Slot, string Header, string Name, string? Value)
    {
        public string? Tone { get; init; }

        public bool Unresolved => Kind is null;
    }
}
