using CUE4Parse.UE4.Assets.Exports;

namespace FellowshipExtractor.Extraction;

public sealed record AssetTags(IReadOnlySet<string> Named, IReadOnlySet<string> Added)
{
    public static readonly AssetTags None = new(new HashSet<string>(), new HashSet<string>());
}

public sealed record HeldTags(IReadOnlySet<string> Asset, IReadOnlySet<string> Held)
{
    public IReadOnlySet<string> Ongoing { get; init; } = new HashSet<string>();
}

public static class TagNames
{
    private static readonly string[] AddingFields = ["Added", "CombinedTags"];
    private static readonly string[] AssetContainers = ["AssetTags", "GameplayEffectTags"];
    private static readonly string[] HeldContainers = ["OwnedTags", "GrantedTags"];
    private const string RequireField = "RequireTags";
    private const string OngoingContainer = "OngoingTagRequirements";

    private const string TalentsPrefix = "Talents.";
    private const string GrantedPrefix = "CustomGrantedTags.";
    private const string TalentSegment = ".Talent.";

    public static bool IsTalent(string tag) =>
        tag.StartsWith(TalentsPrefix, StringComparison.Ordinal) ||
        (tag.StartsWith(GrantedPrefix, StringComparison.Ordinal) &&
         tag.Contains(TalentSegment, StringComparison.Ordinal));

    public static AssetTags Of(UObject? asset)
    {
        if (asset?.Owner is not { } package) return AssetTags.None;

        var added = new HashSet<string>(StringComparer.Ordinal);

        foreach (var export in package.GetExports())
        {
            foreach (var property in PropertyWalk.Walk(export))
            {
                if (!AddingFields.Contains(property.Name, StringComparer.Ordinal)) continue;

                added.UnionWith(PropertyWalk.AsTags(property.Value) ?? []);
            }
        }

        var named = package.NameMap
            .Select(entry => entry.Name)
            .OfType<string>()
            .Where(name => name.Contains('.') && !name.Contains('/') && !added.Contains(name))
            .ToHashSet(StringComparer.Ordinal);

        return new AssetTags(named, added);
    }

    public static HeldTags Split(UObject? asset)
    {
        var owned = new HashSet<string>(StringComparer.Ordinal);
        var held = new HashSet<string>(StringComparer.Ordinal);
        var ongoing = new HashSet<string>(StringComparer.Ordinal);

        foreach (var export in asset?.Owner?.GetExports() ?? [])
        {
            foreach (var property in PropertyWalk.Walk(export))
            {
                if (property.Name.Equals(RequireField, StringComparison.Ordinal) &&
                    property.Path.Contains(OngoingContainer, StringComparison.Ordinal) &&
                    PropertyWalk.AsTags(property.Value) is { Length: > 0 } required)
                {
                    ongoing.UnionWith(required);
                    continue;
                }

                if (!AddingFields.Contains(property.Name, StringComparer.Ordinal)) continue;
                if (PropertyWalk.AsTags(property.Value) is not { Length: > 0 } tags) continue;

                if (AssetContainers.Any(container => property.Path.Contains(container, StringComparison.Ordinal)))
                {
                    owned.UnionWith(tags);
                }
                else if (HeldContainers.Any(container => property.Path.Contains(container, StringComparison.Ordinal)))
                {
                    held.UnionWith(tags);
                }
            }
        }

        return new HeldTags(owned, held) { Ongoing = ongoing };
    }
}
