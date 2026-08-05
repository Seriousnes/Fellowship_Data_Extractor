namespace Fellowship.SDK;

/// <summary>
/// One row of the game's rich text style set: the colour, weight and size the game draws a tagged
/// run in. Weight arrives as the typeface name the font asset declares, which is a number for the
/// upright faces and a number with an Italic suffix for the slanted ones.
/// </summary>
public sealed record TextStyle(string Color, string Weight, double Size)
{
    public bool Italic => Weight.EndsWith("Italic", StringComparison.OrdinalIgnoreCase);

    /// <summary>The numeric half of the typeface name, which is what CSS font-weight takes.</summary>
    public string CssWeight => Weight.Split('-')[0];

    /// <summary>
    /// The CSS class a run in a named style is drawn with. The name is the tag a description writes
    /// and is the key the style is filed under, so it is passed in rather than carried on the row.
    /// </summary>
    public static string CssClass(string name) =>
        "rt-" + name.Replace("rt.", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace('.', '-')
            .ToLowerInvariant();
}
