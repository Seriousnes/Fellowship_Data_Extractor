using Azure.Storage.Blobs;

namespace FellowshipExtractor.Api.Services;

public sealed class BlobExportSource(BlobContainerClient container) : IExportSource
{
    public async Task<ExportSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        var settings = ExportReader.ReadSettings(
            await TextAsync(ExportReader.SettingsName, cancellationToken));

        var entities = await TextAsync(ExportReader.EntitiesName, cancellationToken)
            ?? throw ExportReader.Missing(ExportReader.EntitiesName);

        return new ExportSnapshot(settings, ExportReader.ReadEntities(ExportReader.Lines(entities)));
    }

    private async Task<string?> TextAsync(string name, CancellationToken cancellationToken)
    {
        var blob = container.GetBlobClient(ExportReader.Blob(name));

        if (!await blob.ExistsAsync(cancellationToken)) return null;

        var content = await blob.DownloadContentAsync(cancellationToken);

        return content.Value.Content.ToString();
    }
}
