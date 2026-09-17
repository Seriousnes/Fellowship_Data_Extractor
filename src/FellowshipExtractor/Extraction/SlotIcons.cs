using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Extraction;

public sealed class SlotIcons
{
    public const string Widget = "UW_ItemTooltipSubheader";

    private const string MapField = "GearTypes";
    private const string EnumSeparator = "::";
    private const string DefaultObjectPrefix = "Default__";

    private readonly Dictionary<string, string> _bySlot = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _bySlot.Count;

    public SlotIcons(AbstractFileProvider provider, ItemSlotNames slots)
    {
        foreach (var (type, texture) in Textures(provider))
        {
            var named = slots.Find(type)
                ?? (int.TryParse(type, out var ordinal) ? slots.ByOrdinal(ordinal) : null);

            if (named is not { } slot)
            {
                throw new InvalidOperationException(
                    $"{Widget} draws {texture} for the {type} slot, and no shipped slot name is called {type}. " +
                    "The icon on a slot line is keyed on that name, so every item would draw the wrong one.");
            }

            _bySlot[slot.Shipped] = texture;
        }
    }

    public IReadOnlyDictionary<string, string> BySlot => _bySlot;

    public static IReadOnlyDictionary<string, string> Textures(AbstractFileProvider provider)
    {
        var file = provider.Files.Values.FirstOrDefault(f =>
            f.Path.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase) &&
            Path.GetFileNameWithoutExtension(f.Path).Equals(Widget, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"No {Widget} package in the build. Its {MapField} map is the only thing that says which icon " +
                "belongs to which equipment slot.");

        var defaults = provider.LoadPackage(file).GetExports()
            .FirstOrDefault(export => export.Name.StartsWith(DefaultObjectPrefix, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"{Widget} serializes no default object, so its {MapField} map cannot be read.");

        if (PropertyWalk.Get(defaults, MapField)?.GenericValue is not UScriptMap map)
        {
            throw new InvalidOperationException(
                $"{Widget} no longer holds a {MapField} map, so no slot line would have its own icon.");
        }

        var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in map.Properties)
        {
            if (PropertyWalk.AsString(pair.Key?.GenericValue) is not { } key) continue;
            if (PropertyWalk.AsString(pair.Value?.GenericValue) is not { } path) continue;

            var separator = key.LastIndexOf(EnumSeparator, StringComparison.Ordinal);

            found[separator < 0 ? key : key[(separator + EnumSeparator.Length)..]] =
                AbilityPresentationIndex.AssetName(path);
        }

        if (found.Count == 0)
        {
            throw new InvalidOperationException(
                $"{Widget}'s {MapField} map read empty, so every slot line would fall back to the generic glyph.");
        }

        return found;
    }
}
