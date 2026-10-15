namespace Fellowship.SDK;

public enum TextureSize
{
    Small,
    Medium,
    Large,
    Full,
}

public static class TextureSizes
{
    public const string Folder = "ui";

    public const string Extension = ".webp";

    public static IReadOnlyList<TextureSize> All { get; } = Enum.GetValues<TextureSize>();

    public static string Segment(TextureSize size) => size.ToString().ToLowerInvariant();

    public static int? Pixels(TextureSize size) => size switch
    {
        TextureSize.Small => 64,
        TextureSize.Medium => 128,
        TextureSize.Large => 256,
        TextureSize.Full => null,
        _ => throw new ArgumentOutOfRangeException(nameof(size), size, $"{size} names no {nameof(TextureSize)}."),
    };

    public static string Variant(string texture, string rarity) => $"{texture}-{rarity.ToLowerInvariant()}";

    public static string File(string texture, TextureSize size) => $"{texture}_{Segment(size)}{Extension}";

    public static string Key(string texture, TextureSize size) => $"{Folder}/{File(texture, size)}";

    public static bool Fits(int width, int height, TextureSize size) =>
        Pixels(size) is not { } pixels || Math.Min(width, height) >= pixels;
}
