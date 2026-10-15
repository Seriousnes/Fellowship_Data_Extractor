namespace Fellowship.SDK;

public sealed record TextStyle(string Color, string Weight, double Size)
{
    public bool Italic => Weight.EndsWith("Italic", StringComparison.OrdinalIgnoreCase);

    public string CssWeight => Weight.Split('-')[0];

    public static string CssClass(string name) =>
        "rt-" + name.Replace("rt.", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace('.', '-')
            .ToLowerInvariant();
}
