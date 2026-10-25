using CUE4Parse.UE4.Assets.Exports;

namespace FellowshipExtractor.Extraction;

public sealed record EffectNameSource(string DisplayName, IconSource Origin);

public sealed class EffectNameSources
{
    private readonly Dictionary<string, EffectNameSource?> _byEffect = new(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _lentByProperty = new(StringComparer.OrdinalIgnoreCase);

    public int Widened => _byEffect.Keys.Count(effect => !_lentByProperty.Contains(effect));

    public int Unpackaged { get; }

    public EffectNameSources(
        IEnumerable<TagRecord> tags,
        IReadOnlySet<string> effectNames,
        Func<TagRecord, UObject?> load,
        Func<TagRecord, UObject, string?> printedName,
        Func<string, UObject?> loadClass)
    {
        var unpackaged = 0;
        var bySpawnedActor = new Dictionary<string, EffectNameSource?>(StringComparer.OrdinalIgnoreCase);

        foreach (var tag in tags)
        {
            if (load(tag) is not { } cdo) continue;
            if (printedName(tag, cdo) is not { } name || string.IsNullOrWhiteSpace(name)) continue;

            if (cdo.Owner is null) unpackaged++;

            var source = new EffectNameSource(name, new IconSource(tag.Table, tag.NetId, tag.Name));

            foreach (var (applied, _, widened) in GameplayEffects.Applying(cdo, effectNames))
            {
                if (tag.Name.Equals(applied, StringComparison.OrdinalIgnoreCase)) continue;

                if (!widened) _lentByProperty.Add(applied);

                Lend(_byEffect, applied, source);
            }

            foreach (var actor in RowSinks.SpawnedPaths(cdo).Select(loadClass).OfType<UObject>())
            {
                foreach (var (applied, _, _) in GameplayEffects.Applying(actor, effectNames))
                {
                    if (!tag.Name.Equals(applied, StringComparison.OrdinalIgnoreCase)) Lend(bySpawnedActor, applied, source);
                }
            }
        }

        foreach (var (applied, source) in bySpawnedActor) _byEffect.TryAdd(applied, source);

        Unpackaged = unpackaged;
    }

    private static void Lend(Dictionary<string, EffectNameSource?> byEffect, string applied, EffectNameSource source)
    {
        if (!byEffect.TryGetValue(applied, out var agreed))
        {
            byEffect[applied] = source;
            return;
        }

        if (agreed is null || agreed.DisplayName.Equals(source.DisplayName, StringComparison.Ordinal)) return;

        byEffect[applied] = null;
    }

    public EffectNameSource? For(string effectName) => _byEffect.GetValueOrDefault(effectName);

    public bool Disagrees(string effectName) =>
        _byEffect.TryGetValue(effectName, out var agreed) && agreed is null;
}
