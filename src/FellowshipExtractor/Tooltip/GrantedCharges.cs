using System.Text.RegularExpressions;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;

namespace FellowshipExtractor.Tooltip;

public sealed class GrantedCharges
{
    private const string AugmentationClass = "CRCharacterAugmentationAsset";
    private const string GrantedTagsField = "GrantedGameplayTags";
    private const string TalentTagPrefix = "Talents.";
    private static readonly string[] TalentSegments = ["Talent", "Talents"];

    private static readonly Regex ChargeCount = new(
        @"\b\d+\s+charges?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex Markup = new(@"<[^>]*>", RegexOptions.Compiled);

    private readonly HashSet<string> _tags = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _abilities = new(StringComparer.OrdinalIgnoreCase);

    public int NamedAugmentations { get; }

    public GrantedCharges(
        AbstractFileProvider provider, IReadOnlyList<FAssetData> assets, AbilityPresentationIndex presentation)
    {
        var names = presentation.AbilityNames;

        foreach (var asset in assets)
        {
            if (!asset.AssetClass.Text.Equals(AugmentationClass, StringComparison.OrdinalIgnoreCase)) continue;
            if (Load(provider, asset.ObjectPath) is not { } cdo) continue;

            var own = Display.From(null, cdo);
            var granted = own is { Name: not null, Description: not null } ? null : Granted(presentation, cdo);

            var name = own.Name ?? granted?.Name;
            if (name is null) continue;

            NamedAugmentations++;

            foreach (var tag in PropertyWalk.GetTags(cdo, GrantedTagsField) ?? [])
            {
                if (tag.StartsWith(TalentTagPrefix, StringComparison.Ordinal)) _tags.Add(tag);
            }

            var description = own.Description ?? granted?.Description;
            if (description is not null) Sweep(description, names);
        }
    }

    public bool Supplies(string rowName, string? abilityName) =>
        (TalentTag(rowName) is { } tag && _tags.Contains(tag)) ||
        (abilityName is not null && _abilities.Contains(abilityName));

    private static string? TalentTag(string rowName)
    {
        var segments = rowName.Split('.');
        var at = Array.FindIndex(segments, s => TalentSegments.Contains(s, StringComparer.Ordinal));

        if (at < 1 || at + 1 >= segments.Length) return null;
        return $"{TalentTagPrefix}{string.Join('.', segments[..at])}.{segments[at + 1]}";
    }

    private void Sweep(string description, IReadOnlyCollection<string> names)
    {
        var text = Markup.Replace(description, " ");

        foreach (Match count in ChargeCount.Matches(text))
        {
            string? closest = null;
            var at = -1;

            foreach (var name in names)
            {
                var found = LastMention(text, name, count.Index);
                if (found > at)
                {
                    at = found;
                    closest = name;
                }
            }

            if (closest is not null) _abilities.Add(closest);
        }
    }

    private static int LastMention(string text, string name, int before)
    {
        var at = -1;

        for (var from = 0; from + name.Length <= before;)
        {
            var found = text.IndexOf(name, from, before - from, StringComparison.OrdinalIgnoreCase);
            if (found < 0) break;

            if (!Inside(text, found - 1) && !Inside(text, found + name.Length)) at = found;
            from = found + 1;
        }

        return at;
    }

    private static bool Inside(string text, int index) =>
        index >= 0 && index < text.Length && (char.IsLetterOrDigit(text[index]) || text[index] == '\'');

    private static Display? Granted(AbilityPresentationIndex presentation, UObject augmentation)
    {
        foreach (var property in PropertyWalk.Walk(augmentation))
        {
            if (!property.Path.StartsWith("GrantedAbilities[", StringComparison.Ordinal)) continue;
            if (PropertyWalk.AsString(property.Value) is not { } path) continue;

            var row = presentation.Find(AbilityPresentationIndex.AssetName(path));
            if (row is null) continue;

            var display = Display.From(row.Row, null);
            if (display is { Name: null, Description: null }) continue;

            return display;
        }

        return null;
    }

    private static UObject? Load(AbstractFileProvider provider, string objectPath)
    {
        if (!provider.TryLoadPackage(AbilityPresentationIndex.PackagePath(objectPath), out var package)) return null;

        var exports = package.GetExports().ToArray();
        return exports.FirstOrDefault(e => e.Name.StartsWith("Default__", StringComparison.Ordinal))
            ?? exports.FirstOrDefault();
    }
}
