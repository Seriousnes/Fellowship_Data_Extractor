using CUE4Parse.UE4.Assets.Exports;

namespace FellowshipExtractor.Tooltip;

/// <summary>A name a gameplay effect can inherit, and the entity it belongs to.</summary>
public sealed record EffectNameSource(string DisplayName, IconSource Origin);

/// <summary>
/// Where a gameplay effect can take a name from: the entities that apply it, and the one name they
/// agree on.
///
/// An effect is usually anonymous. 2,857 of the 3,261 effect tags carry no display name at all,
/// because the game never shows an effect under a name of its own: a player reads a debuff as the
/// ability that applied it, so Searing Blaze's damage-over-time is "Searing Blaze" only because the
/// ability that lands it says so. The reference that carries the name points from the applier to
/// the effect, so it is inverted here exactly as <see cref="EffectIconSources"/> inverts it for art,
/// over the same <see cref="GameplayEffects.ReferencedIn"/> predicate the resolver folds an effect's
/// numbers in with.
///
/// It is a separate walk from the icon one rather than a second field on it, because the two answer
/// for different sets of appliers: art is inheritable only from an applier that ships art and a name
/// only from one that ships a name, and a named but iconless ability never enters the icon map at
/// all. 1,288 effects are named from here, 1,183 of them by an ability, 74 by an augmentation, 16 by
/// another effect and 15 by an AI character.
///
/// Only an unambiguous answer is an answer. Where two appliers carrying different names both apply
/// one effect, that effect inherits nothing, because choosing between two shipped names is a guess
/// and a guess is indistinguishable from a reading once it is written to the export. 70 effects are
/// left anonymous that way, against 1,499 that nothing named applies at all.
/// </summary>
public sealed class EffectNameSources
{
    /// <summary>
    /// The name every naming source agrees on, keyed by effect name, or null where they disagree. An
    /// effect nothing named applies is absent rather than null: nothing to inherit and a contested
    /// inheritance are different facts, and the second is worth reporting.
    /// </summary>
    private readonly Dictionary<string, EffectNameSource?> _byEffect = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Effect names exactly one name answers for.</summary>
    public int Determined => _byEffect.Values.Count(source => source is not null);

    /// <summary>Effect names whose appliers are called different things.</summary>
    public int Contested => _byEffect.Values.Count(source => source is null);

    /// <param name="printedName">
    /// What a tag exports under, which is not the same question as what its own asset says. An
    /// augmentation carrying a stale draft of its name is exported under the talent tree's name
    /// instead, so reading the asset here would inherit a word that appears nowhere in the export
    /// and point the provenance at an entity that disagrees with it.
    /// </param>
    public EffectNameSources(
        IEnumerable<TagRecord> tags,
        IReadOnlySet<string> effectNames,
        Func<TagRecord, UObject?> load,
        Func<TagRecord, UObject, string?> printedName)
    {
        foreach (var tag in tags)
        {
            if (load(tag) is not { } cdo) continue;
            if (printedName(tag, cdo) is not { } name || string.IsNullOrWhiteSpace(name)) continue;

            foreach (var (applied, _) in GameplayEffects.ReferencedIn(cdo, effectNames))
            {
                // One effect in the build names itself. An asset is not a source for itself, and no
                // tag name is shared across two tables, so the name is enough to tell that case.
                if (tag.Name.Equals(applied, StringComparison.OrdinalIgnoreCase)) continue;

                if (!_byEffect.TryGetValue(applied, out var agreed))
                {
                    _byEffect[applied] = new EffectNameSource(name, new IconSource(tag.Table, tag.NetId, tag.Name));
                    continue;
                }

                // Compared exactly. Two appliers spelling one name differently are two names, and
                // taking the first of them would be the guess this whole class exists to refuse.
                if (agreed is null || agreed.DisplayName.Equals(name, StringComparison.Ordinal)) continue;

                _byEffect[applied] = null;
            }
        }
    }

    /// <summary>
    /// The name this effect inherits, or null when nothing named applies it and null again when
    /// several do and disagree. That second null is the whole inheritance rule.
    /// </summary>
    public EffectNameSource? For(string effectName) => _byEffect.GetValueOrDefault(effectName);

    /// <summary>
    /// Whether an effect is left anonymous because its appliers are called different things, as
    /// against having no named applier at all. The two need different work to close and are counted
    /// apart.
    /// </summary>
    public bool Disagrees(string effectName) =>
        _byEffect.TryGetValue(effectName, out var agreed) && agreed is null;
}
