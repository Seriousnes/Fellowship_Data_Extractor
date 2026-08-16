using CUE4Parse.FileProvider;

namespace FellowshipExtractor.Extraction;

public sealed class SectionHeadingRows
{
    public const string Ability = "Ability";
    public const string Legendary = "Legendary";
    public const string SetBonus = "SetBonus";

    private const string ColorField = "NamedOverrideColor";
    private const string RowField = "RowName";

    private static readonly (string Section, string Widget, string Element)[] Headings =
    [
        (Ability, "UW_ItemEffectsBlock", "TextBLock"),
        (Legendary, "UW_LegendaryEffectBlock", "Text_ItemEffectName"),
        (SetBonus, "UW_SetBonusEntry", "UsageTxt"),
    ];

    public SectionHeadingRows(AbstractFileProvider provider)
    {
        var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (section, widget, element) in Headings) found[section] = Row(provider, widget, element);

        BySection = found;
    }

    public IReadOnlyDictionary<string, string> BySection { get; }

    public string Source => string.Join(", ", Headings.Select(heading => heading.Widget));

    private static string Row(AbstractFileProvider provider, string widget, string element)
    {
        var file = provider.Files.Values.FirstOrDefault(candidate =>
            candidate.Path.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase) &&
            Path.GetFileNameWithoutExtension(candidate.Path).Equals(widget, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"No {widget} package in the build. Its {element} is the only thing that says which named colour " +
                "the section heading it draws prints in.");

        var drawn = provider.LoadPackage(file).GetExports()
            .FirstOrDefault(export => export.Name.Equals(element, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"{widget} holds no {element}, so nothing says which named colour its section heading prints in.");

        if (PropertyWalk.GetStruct(drawn, ColorField) is not { } colour ||
            PropertyWalk.GetString(colour, RowField) is not { } row)
        {
            throw new InvalidOperationException(
                $"{widget}'s {element} carries no {ColorField}.{RowField}, so its section heading would print in " +
                "the plain body colour.");
        }

        return row;
    }
}
