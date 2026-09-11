using CUE4Parse.FileProvider;
using CUE4Parse_Conversion.Textures;

using FellowshipExtractor.Output;
using FellowshipExtractor.Extraction;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FellowshipExtractor.Textures;

public sealed class IconImages(
    AbstractFileProvider provider, IReadOnlySet<string> renders, IReadOnlyList<RarityColour> rarities)
{
    private readonly Dictionary<string, string?> _names = new(StringComparer.OrdinalIgnoreCase);

    private readonly List<(string Name, byte[] Png)> _pending = [];

    public int Count { get; private set; }

    public int Composed { get; private set; }

    public int Undecoded { get; private set; }

    public IconWriter Writer => Encode;

    private string? Encode(string iconPath, string? rarity)
    {
        if (!_names.TryGetValue(iconPath, out var name)) name = _names[iconPath] = Decode(iconPath);

        if (name is null) return null;
        if (!renders.Contains(iconPath)) return name;

        return TextureSizes.Variant(name, rarity is null ? rarities[^1].Rarity : Check(rarity));
    }

    private string Check(string rarity)
    {
        foreach (var entry in rarities)
        {
            if (entry.Rarity.Equals(rarity, StringComparison.OrdinalIgnoreCase)) return entry.Rarity;
        }

        throw new InvalidOperationException(
            $"An icon was asked for at rarity {rarity}, which is no rarity the settings publish, so it " +
            "would name a variant no run writes.");
    }

    private string? Decode(string iconPath)
    {
        var texture = TextureExporter.Load(provider, iconPath);
        if (texture is null)
        {
            Undecoded++;
            return null;
        }

        var encoded = TextureExporter.Export(texture, ETextureFormat.Png);
        if (!encoded.HasContent)
        {
            Undecoded++;
            return null;
        }

        var name = texture.Name;

        if (!renders.Contains(iconPath))
        {
            _pending.Add((name, encoded.Content!));
            return name;
        }

        using var icon = Image.Load<Rgba32>(encoded.Content!);

        foreach (var entry in rarities)
        {
            _pending.Add((TextureSizes.Variant(name, entry.Rarity), RarityImages.Compose(icon, entry.Colour)));
            Composed++;
        }

        return name;
    }

    public async Task FlushAsync(IExportTarget target, CancellationToken cancellationToken)
    {
        foreach (var (name, png) in _pending)
        {
            await target.WriteImageAsync(name, png, cancellationToken);
            Count++;
        }

        _pending.Clear();
    }
}
