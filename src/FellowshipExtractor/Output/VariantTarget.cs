using FellowshipExtractor.Textures;

using Fellowship.SDK;
using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Output;

public sealed class VariantTarget(IExportTarget inner) : IExportTarget
{
    private int _written;

    public Task WriteDocumentAsync(EntityDocument document, CancellationToken cancellationToken) =>
        inner.WriteDocumentAsync(document, cancellationToken);

    public Task WriteSettingsAsync(SettingsFile settings, CancellationToken cancellationToken) =>
        inner.WriteSettingsAsync(settings, cancellationToken);

    public async Task WriteImageAsync(string name, byte[] png, CancellationToken cancellationToken)
    {
        foreach (var variant in TextureVariants.Render(png))
        {
            await inner.WriteImageAsync(
                TextureSizes.File(name, variant.Size), variant.Webp, cancellationToken);

            _written++;
        }
    }

    public async ValueTask DisposeAsync()
    {
        Console.WriteLine($"rendered {_written} texture variant(s) across {TextureSizes.All.Count} sizes");

        await inner.DisposeAsync();
    }
}
