using System.Globalization;

using Fellowship.Codex.Services;
using Fellowship.SDK.Documents;

namespace Fellowship.Codex.Models;

public sealed record PatchNoteEntry(PatchNote Note, EntityType Type, EntityDocument? Document, string Name);

public sealed record PatchNoteKind(string Label, IReadOnlyList<PatchNoteEntry> Entries);

public sealed record PatchNoteGroup(string Anchor, string Label, string? Colour, IReadOnlyList<PatchNoteKind> Kinds)
{
    public int Count => Kinds.Sum(kind => kind.Entries.Count);
}

public sealed record PatchNoteCategory(string Anchor, string Label, IReadOnlyList<PatchNoteGroup> Groups)
{
    public int Count => Groups.Sum(group => group.Count);
}

public static class PatchNoteSections
{
    private const string HeroesLabel = "Heroes";

    private readonly record struct Holder(EntityType Type, int Id, bool Own);

    public static IReadOnlyList<PatchNoteCategory> Of(IReadOnlyList<PatchNote> notes, ICodexViewService ui)
    {
        var byHolder = new Dictionary<Holder, List<PatchNoteEntry>>();

        foreach (var note in notes)
        {
            if (!EntityTypes.TryParse(note.Key.Slug, out var type)) continue;

            var document = ui.Store.All(type, note.Key.Id) is [var first, ..] ? first : null;

            if ((note.Name ?? (document is null ? null : PublicTypes.DisplayNameOf(document))) is not { } name) continue;

            var entry = new PatchNoteEntry(note, type, document, name);

            foreach (var holder in Holders(type, document, ui).Distinct())
            {
                if (!byHolder.TryGetValue(holder, out var held)) byHolder[holder] = held = [];

                held.Add(entry);
            }
        }

        var categories = new List<PatchNoteCategory>();

        var heroes = Groups(byHolder.Where(pair => pair.Key.Type == EntityType.Hero && pair.Key.Own), ui);

        if (heroes.Count > 0) categories.Add(new PatchNoteCategory("heroes", HeroesLabel, heroes));

        foreach (var category in Categories.All)
        {
            var groups = Groups(
                byHolder.Where(pair =>
                    !(pair.Key.Type == EntityType.Hero && pair.Key.Own) &&
                    PublicTypes.Category(pair.Key.Type) == category),
                ui);

            if (groups.Count > 0) categories.Add(new PatchNoteCategory(category.Slug, category.Label, groups));
        }

        return categories;
    }

    private static IEnumerable<Holder> Holders(EntityType type, EntityDocument? document, ICodexViewService ui)
    {
        switch (document)
        {
            case HeroDocument:
                yield return new Holder(EntityType.Hero, document.Id, true);
                yield break;

            case CreatureDocument when PublicTypes.DisplayNameOf(document) is not null:
                yield return new Holder(EntityType.Creature, document.Id, true);
                yield break;
        }

        var origins = document switch
        {
            AbilityDocument { Origins: { } held } => held,
            EffectDocument { Origins: { } held } => held,
            TalentDocument { Origins: { } held } => held,
            _ => [],
        };

        var found = false;

        foreach (var origin in origins)
        {
            var named = ui.Store.All(origin.Type, origin.Id) is [var first, ..] && PublicTypes.DisplayNameOf(first) is not null;

            found = true;

            yield return origin.Type is EntityType.Hero or EntityType.Creature && named
                ? new Holder(origin.Type, origin.Id, true)
                : new Holder(origin.Type, 0, false);
        }

        if (!found) yield return new Holder(type, 0, false);
    }

    private static IReadOnlyList<PatchNoteGroup> Groups(
        IEnumerable<KeyValuePair<Holder, List<PatchNoteEntry>>> held, ICodexViewService ui) =>
    [
        .. held
            .Select(pair => Group(pair.Key, pair.Value, ui))
            .OrderBy(group => group.Position)
            .ThenBy(group => group.Group.Label, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Group),
    ];

    private static (PatchNoteGroup Group, int Position) Group(Holder holder, List<PatchNoteEntry> entries, ICodexViewService ui)
    {
        var slug = EntityTypes.Slug(holder.Type);

        var kinds = entries
            .GroupBy(entry => entry.Type)
            .OrderBy(kind => holder.Own && kind.Key == holder.Type ? -1 : PublicTypes.Rank(kind.Key))
            .Select(kind => new PatchNoteKind(
                PublicTypes.Label(kind.Key),
                [
                    .. kind
                        .OrderBy(entry => entry.Note.Added ? 0 : 1)
                        .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(entry => entry.Note.Key.Id),
                ]))
            .ToList();

        if (!holder.Own)
        {
            return (new PatchNoteGroup($"pn-{slug}", PublicTypes.Label(holder.Type), null, kinds),
                PublicTypes.Rank(holder.Type));
        }

        var document = ui.Store.All(holder.Type, holder.Id)[0];

        return (new PatchNoteGroup(
                $"pn-{slug}-{holder.Id.ToString(CultureInfo.InvariantCulture)}",
                ui.Name(document),
                ui.LinkColour(document),
                kinds),
            0);
    }
}
