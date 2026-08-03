using FellowshipExtractor.Tooltip;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FellowshipExtractor.Api.Pages;

/// <summary>
/// The front page: what the build contains, and the ways in.
///
/// The tiles are the menu the masthead already carries, laid out rather than hidden behind a hover,
/// so the shape of the corpus is the first thing on screen. The showcase beneath them is drawn from
/// the export rather than chosen: the named entities carrying the most art, which is what a reader
/// clicking blind is most likely to find worth looking at.
/// </summary>
public sealed class IndexModel(Codex codex, EntityStore store) : PageModel
{
    public Codex Codex => codex;

    public EntityStore Store => store;

    /// <summary>
    /// A handful of entities with a name, art and a description, spread across tables so the row is
    /// a sample of the corpus rather than eight helmets. Deterministic: the same export always shows
    /// the same eight, so the page is stable to reload and to screenshot.
    /// </summary>
    public IReadOnlyList<Entity> Showcase =>
    [
        .. store.Entities
            .Where(e => e.DisplayName is not null && e.Icon is not null && e.Description is not null)
            .GroupBy(e => e.Table!, StringComparer.Ordinal)
            .SelectMany(group => group
                .OrderByDescending(e => (e.Description?.Length ?? 0) + (e.Constants?.Count ?? 0) * 20)
                .Take(2))
            .OrderBy(e => e.Table, StringComparer.Ordinal)
            .ThenBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(8),
    ];
}
