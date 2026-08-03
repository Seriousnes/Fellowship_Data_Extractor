using System.Text.RegularExpressions;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;

namespace FellowshipExtractor.Tooltip;

/// <summary>
/// The charge counts that only a named legendary supplies.
///
/// An ability's charge count is often kept in a row named for a talent, and a row named for a
/// talent is usually unconditional - the talent was folded into the ability and left its name in
/// the row. Five of them are not: a named augmentation, worn as a legendary, is the only thing that
/// adds the charge, so the ability's baseline is one cast and the second charge belongs to the item.
///
/// Two tests find those five and neither finds them alone.
///
/// The first joins on the tag. A talent row is X.Y.Talent.Z.Field and the tag for that talent is
/// Talents.X.Y.Z, so a named augmentation granting that exact tag is the granter. This is the only
/// test that catches The Motherload, which grants Talents.Ink.BouncyProjectile.Charged and writes
/// no prose at all, and it catches Iron Sentinel, Bleeding Hearts and Mass Entropy as well.
///
/// The second reads the prose. Unrelenting Thicket says "Your Ironleaf Ward has 2 charges" and
/// Explosivo says "Your Apocalypse has 2 Charges", and neither joins on a tag: Unrelenting Thicket
/// grants Talents.Mosse.CastedSingleTargetDamageReduction.AddCharges while the tuning rows sit
/// under Mosse.SingleTargetCastedDamageReduction, and Explosivo's charge is not among the two tags
/// it grants at all. The ability the count belongs to is the one named closest in front of it:
/// Explosivo's sentence goes on to name Fire Ball, whose two charges are its own and must survive.
/// </summary>
public sealed class GrantedCharges
{
    private const string AugmentationClass = "CRCharacterAugmentationAsset";
    private const string GrantedTagsField = "GrantedGameplayTags";
    private const string TalentTagPrefix = "Talents.";
    private static readonly string[] TalentSegments = ["Talent", "Talents"];

    /// <summary>A count of charges written out, as "2 charges" or "1 charge".</summary>
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

            // Explosivo carries no text of its own and Loop of the Thundering Vortex carries no
            // name; a wrapper asset's presentation is on the ability it grants, which is where the
            // rest of the export reads it from too.
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

    /// <summary>Whether a named augmentation is what supplies this talent row's charge count.</summary>
    public bool Supplies(string rowName, string? abilityName) =>
        (TalentTag(rowName) is { } tag && _tags.Contains(tag)) ||
        (abilityName is not null && _abilities.Contains(abilityName));

    /// <summary>
    /// The gameplay tag of the talent a tuning row is named for.
    /// Gunde.HeavyMeleeDotBased.Talent.Charges.NumOfCharges is Talents.Gunde.HeavyMeleeDotBased.Charges.
    /// </summary>
    private static string? TalentTag(string rowName)
    {
        var segments = rowName.Split('.');
        var at = Array.FindIndex(segments, s => TalentSegments.Contains(s, StringComparer.Ordinal));

        if (at < 1 || at + 1 >= segments.Length) return null;
        return $"{TalentTagPrefix}{string.Join('.', segments[..at])}.{segments[at + 1]}";
    }

    /// <summary>
    /// The ability named closest in front of each charge count in a description. Every other name in
    /// the text belongs to a different clause: Explosivo names Apocalypse before its count and Fire
    /// Ball after it, and only the first is the ability the count is about.
    /// </summary>
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

    /// <summary>Where a name last appears as a whole word before a position, or -1.</summary>
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

    /// <summary>The presentation of the first ability an augmentation grants that has any.</summary>
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
