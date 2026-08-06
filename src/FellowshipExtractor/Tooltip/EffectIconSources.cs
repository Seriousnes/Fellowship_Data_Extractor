using CUE4Parse.UE4.Assets.Exports;

namespace FellowshipExtractor.Tooltip;

public sealed record EffectIconSource(string IconPath, IconSource Origin);

public sealed class EffectIconSources
{
    private readonly Dictionary<string, EffectIconSource?> _byEffect = new(StringComparer.OrdinalIgnoreCase);

    public int Determined => _byEffect.Values.Count(source => source is not null);

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

    public EffectIconSource? For(string effectName) => _byEffect.GetValueOrDefault(effectName);

    public bool Disagrees(string effectName) =>
        _byEffect.TryGetValue(effectName, out var agreed) && agreed is null;
}
