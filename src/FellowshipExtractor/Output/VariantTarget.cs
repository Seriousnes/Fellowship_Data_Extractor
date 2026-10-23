using FellowshipExtractor.Textures;

using Fellowship.SDK;
using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Output;

public sealed class VariantTarget(IExportTarget inner) : IExportTarget
{
    private readonly Queue<(string Name, Task<IReadOnlyList<TextureVariant>> Rendered)> _rendering = new();

    private readonly int _window = Environment.ProcessorCount * 4;

    private int _written;

    public Task WriteDocumentAsync(EntityDocument document, CancellationToken cancellationToken) =>
        inner.WriteDocumentAsync(document, cancellationToken);

    public Task WriteSettingsAsync(SettingsFile settings, CancellationToken cancellationToken) =>
        inner.WriteSettingsAsync(settings, cancellationToken);

    public async Task WriteImageAsync(string name, byte[] png, CancellationToken cancellationToken)
    {
        _rendering.Enqueue((name, Task.Run(() => TextureVariants.Cached(png), cancellationToken)));

        while (_rendering.Count > _window) await WriteNextAsync(cancellationToken);
    }

    private async Task WriteNextAsync(CancellationToken cancellationToken)
    {
        var (name, rendered) = _rendering.Dequeue();

        foreach (var variant in await rendered)
        {
            await inner.WriteImageAsync(TextureSizes.File(name, variant.Size), variant.Webp, cancellationToken);

            _written++;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            while (_rendering.Count > 0) await WriteNextAsync(default);

            Console.WriteLine(
                $"rendered {_written} texture variant(s) across {TextureSizes.All.Count} sizes, cached under " +
                TextureVariants.CacheFolder);
        }
        finally
        {
            await inner.DisposeAsync();
        }
    }
}
