using CUE4Parse.FileProvider;
using CUE4Parse_Conversion.Textures;

using FellowshipExtractor.Output;
using FellowshipExtractor.Tooltip;

namespace FellowshipExtractor.Textures;

public sealed class IconImages(AbstractFileProvider provider, IReadOnlySet<string> renders)
{
    private readonly Dictionary<string, string?> _names = new(StringComparer.OrdinalIgnoreCase);

    private readonly List<(string Name, byte[] Png)> _pending = [];

    public int Written { get; private set; }

    public int Undecoded { get; private set; }

    public IconWriter Writer => Encode;

    private string? Encode(string iconPath)
    {
        if (_names.TryGetValue(iconPath, out var already)) return already;

        var texture = TextureExporter.Load(provider, iconPath);
        if (texture is null)
        {
            Undecoded++;
            return _names[iconPath] = null;
        }

        var encoded = TextureExporter.Export(
            texture, ETextureFormat.Png, discardAlpha: renders.Contains(iconPath));

        if (!encoded.Written)
        {
            Undecoded++;
            return _names[iconPath] = null;
        }

        var name = $"{texture.Name}.{encoded.Extension}";
        _pending.Add((name, encoded.Content!));

        return _names[iconPath] = name;
    }

    public async Task FlushAsync(IExportTarget target, CancellationToken cancellationToken)
    {
        foreach (var (name, png) in _pending)
        {
            await target.WriteImageAsync(name, png, cancellationToken);
            Written++;
        }

        _pending.Clear();
    }
}
