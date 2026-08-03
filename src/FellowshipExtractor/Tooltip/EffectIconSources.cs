using CUE4Parse.UE4.Assets.Exports;

namespace FellowshipExtractor.Tooltip;

/// <summary>An icon a gameplay effect can inherit, and the entity it belongs to.</summary>
public sealed record EffectIconSource(string IconPath, IconSource Origin);

/// <summary>
/// Where a gameplay effect can take an icon from: the entities that apply it, and the one image
/// they agree on.
///
/// An effect ships no art of its own. Exactly 1 of the 3,261 effect tags carries an icon, while
/// the ability, talent or augmentation that applies it usually does, so the only record of what an
/// effect looks like is held by the thing that references it. That reference points the wrong way
/// to be read from the effect, so it is inverted once here: every tag that reaches an asset is
/// walked with <see cref="GameplayEffects.ReferencedIn"/>, the same predicate the resolver uses to
/// fold an effect's numbers in, and each effect a walked entity names records that entity's icon.
///
/// Only an unambiguous answer is an answer. 1,360 effect names are applied by something carrying
/// art; 65 of them are applied by two or more entities whose art differs, and those answer nothing,
/// because choosing between two shipped icons is a guess and a guess is indistinguishable from a
/// reading once it is written to the export.
/// </summary>
public sealed class EffectIconSources
{
    /// <summary>
    /// The icon every icon-carrying source agrees on, keyed by effect name, or null where they
    /// disagree. An effect nothing icon-carrying applies is absent rather than null: nothing to
    /// inherit and a contested inheritance are different facts, and the second is worth reporting.
    /// </summary>
    private readonly Dictionary<string, EffectIconSource?> _byEffect = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Effect names exactly one icon answers for.</summary>
    public int Determined => _byEffect.Values.Count(source => source is not null);

    /// <summary>Effect names whose sources ship different art.</summary>
    public int Contested => _byEffect.Values.Count(source => source is null);

    public EffectIconSources(
        IEnumerable<TagRecord> tags,
        IReadOnlySet<string> effectNames,
        AbilityPresentationIndex presentation,
        Func<TagRecord, UObject?> load)
    {
        foreach (var tag in tags)
        {
            if (load(tag) is not { } cdo) continue;
            if (Display.From(presentation.Find(tag.Name)?.Row, cdo).Icon is not { } icon) continue;

            foreach (var (name, _) in GameplayEffects.ReferencedIn(cdo, effectNames))
            {
                // One effect in the build names itself. An asset is not a source for itself, and no
                // tag name is shared across two tables, so the name is enough to tell that case.
                if (tag.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;

                if (!_byEffect.TryGetValue(name, out var agreed))
                {
                    _byEffect[name] = new EffectIconSource(icon, new IconSource(tag.Table, tag.NetId, tag.Name));
                    continue;
                }

                if (agreed is null || agreed.IconPath.Equals(icon, StringComparison.Ordinal)) continue;

                _byEffect[name] = null;
            }
        }
    }

    /// <summary>
    /// The icon this effect inherits, or null when nothing icon-carrying applies it and null again
    /// when several do and disagree. That second null is the whole inheritance rule.
    /// </summary>
    public EffectIconSource? For(string effectName) => _byEffect.GetValueOrDefault(effectName);

    /// <summary>
    /// Whether an effect is left without art because its sources ship different art, as against
    /// having no source at all. The two need different work to close and are counted apart.
    /// </summary>
    public bool Disagrees(string effectName) =>
        _byEffect.TryGetValue(effectName, out var agreed) && agreed is null;
}
