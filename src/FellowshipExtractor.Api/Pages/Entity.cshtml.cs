using FellowshipExtractor.Tooltip;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FellowshipExtractor.Api.Pages;

/// <summary>
/// One entity, in full.
///
/// The tooltip at the top is not drawn here. It is fetched from /api/tooltip by the page's own
/// script, so what a reader sees is the same fragment any other caller of the API would get, and a
/// reader changing the rung or the hero re-renders it through the API rather than through a second
/// implementation that could disagree with it.
///
/// Everything below the tooltip is the opposite: the record itself, every field of it, drawn from
/// the export. The tooltip is what the game shows; the page is what the build says.
/// </summary>
public sealed class EntityModel(EntityStore store, Codex codex) : PageModel
{
    public string Table { get; private set; } = string.Empty;

    public int NetId { get; private set; }

    /// <summary>Every reading of the id. One for most, up to twelve for a shared item.</summary>
    public IReadOnlyList<Entity> Readings { get; private set; } = [];

    /// <summary>The reading the page is about.</summary>
    public Entity Entity { get; private set; } = new();

    /// <summary>The hero whose reading is on screen, where the id needed one to settle it.</summary>
    public string? Hero { get; private set; }

    /// <summary>
    /// The hero the tooltip has to be asked for so it draws the reading this page is showing. An id
    /// with several readings is answered by the API only when a hero is named, and the readings are
    /// often told apart by armour type alone, so the hero is worked back out of the reading rather
    /// than left to whichever name a select happened to list first.
    /// </summary>
    public string? Resolved =>
        Hero ?? (Readings.Count == 1
            ? null
            : store.Vocabulary.Heroes
                .FirstOrDefault(h => Reads(Entity, h.Name, h.ArmorType, h.PrimaryStat))?.Name);

    public Codex Codex => codex;

    public EntityStore Store => store;

    /// <summary>
    /// Entities worth going to next: the other readings of this id, then the rows sharing its most
    /// specific trait. Kept short, and never a guess - each is a filter the reader could have run.
    /// </summary>
    public IReadOnlyList<(string Label, string Href, int Count)> Related { get; private set; } = [];

    public IActionResult OnGet(string table, int netId, string? hero)
    {
        Table = table;
        NetId = netId;
        Readings = store.All(table, netId);

        if (Readings.Count == 0) return NotFound();

        Hero = hero;

        Entity = hero is not null && store.Hero(hero) is { } named
            ? Readings.FirstOrDefault(e => Reads(e, named.Name, named.ArmorType, named.PrimaryStat)) ?? Readings[0]
            : Readings[0];

        Related = Nearby();

        return Page();
    }

    private static bool Reads(Entity entity, string hero, string? armor, string? stat) =>
        (entity.Heroes is null || entity.Heroes.Contains(hero)) &&
        (entity.ArmorType is null || entity.ArmorType == armor) &&
        (entity.PrimaryStat is null || entity.PrimaryStat == stat);

    private IReadOnlyList<(string, string, int)> Nearby()
    {
        var nearby = new List<(string, string, int)>();

        void Add(string facet, string? value)
        {
            if (value is null) return;
            if (codex.Facet(facet) is not { } definition) return;

            var count = store.Entities.Count(row =>
                row.Table == Entity.Table && definition.Matches(row, [value]));

            if (count < 2) return;

            nearby.Add((
                $"{definition.Label}: {Codex.Spaced(value)}",
                $"/browse?type={Uri.EscapeDataString(Table)}&{facet}={Uri.EscapeDataString(value)}",
                count));
        }

        Add("slot", Entity.Slot);
        Add("set", Entity.SetBonus?.FirstOrDefault());
        Add("ruleset", Entity.RuleSet);
        Add("gem", Entity.GemType);
        Add("trait", Entity.TraitCategory);
        Add("hero", Entity.Heroes?.Count == 1 ? Entity.Heroes[0] : null);
        Add("school", Entity.Tags?.FirstOrDefault(t =>
            t.StartsWith("Magic", StringComparison.Ordinal) || t.StartsWith("Physical", StringComparison.Ordinal)));

        return nearby;
    }
}
