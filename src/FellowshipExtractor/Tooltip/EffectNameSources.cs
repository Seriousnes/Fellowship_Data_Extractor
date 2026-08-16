using CUE4Parse.UE4.Assets.Exports;

namespace FellowshipExtractor.Tooltip;

public sealed record EffectNameSource(string DisplayName, IconSource Origin);

public sealed class EffectNameSources
{
    private readonly Dictionary<string, EffectNameSource?> _byEffect = new(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _lentByProperty = new(StringComparer.OrdinalIgnoreCase);

    public int Determined => _byEffect.Values.Count(source => source is not null);

    public int Contested => _byEffect.Values.Count(source => source is null);

    public int Widened => _byEffect.Keys.Count(effect => !_lentByProperty.Contains(effect));

    public int Unpackaged { get; }

    public EffectNameSources(
        IEnumerable<TagRecord> tags,
        IReadOnlySet<string> effectNames,
        Func<TagRecord, UObject?> load,
        Func<TagRecord, UObject, string?> printedName)
    {
        var unpackaged = 0;

        foreach (var tag in tags)
        {
            if (load(tag) is not { } cdo) continue;
            if (printedName(tag, cdo) is not { } name || string.IsNullOrWhiteSpace(name)) continue;

            if (cdo.Owner is null) unpackaged++;

            foreach (var (applied, _, widened) in GameplayEffects.Applying(cdo, effectNames))
            {
                if (tag.Name.Equals(applied, StringComparison.OrdinalIgnoreCase)) continue;

                if (!widened) _lentByProperty.Add(applied);

                if (!_byEffect.TryGetValue(applied, out var agreed))
                {
                    _byEffect[applied] = new EffectNameSource(name, new IconSource(tag.Table, tag.NetId, tag.Name));
                    continue;
                }

                if (agreed is null || agreed.DisplayName.Equals(name, StringComparison.Ordinal)) continue;

                _byEffect[applied] = null;
            }
        }

        Unpackaged = unpackaged;
    }

    public EffectNameSource? For(string effectName) => _byEffect.GetValueOrDefault(effectName);

    public bool Disagrees(string effectName) =>
        _byEffect.TryGetValue(effectName, out var agreed) && agreed is null;
}
