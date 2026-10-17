using CUE4Parse.UE4.Assets.Exports;

namespace FellowshipExtractor.Extraction;

public sealed record EffectContext(string Class, IReadOnlySet<string> AssetTags, Func<string, double?>? Captured)
{
    public static EffectContext? For(UObject? effect, Func<string, double?>? captured) =>
        effect?.Class?.GetPathName() is { } path
            ? new EffectContext(path, CooldownScaling.AssetTags(effect), captured)
            : null;
}
