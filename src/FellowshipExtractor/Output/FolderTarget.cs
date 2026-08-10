using System.Text.Json;

using FellowshipExtractor.Tooltip;

using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Output;

public sealed class FolderTarget(string root) : IExportTarget
{
    private readonly WrittenNames _images = new();

    private StreamWriter? _entities;

    private string Data()
    {
        var data = Path.Combine(root, ExportLayout.DataFolder);
        Directory.CreateDirectory(data);
        return data;
    }

    private StreamWriter Entities() =>
        _entities ??= new StreamWriter(Path.Combine(Data(), ExportLayout.EntitiesFile), append: false);

    public async Task WriteDocumentAsync(EntityDocument document, CancellationToken cancellationToken) =>
        await Entities().WriteLineAsync(JsonSerializer.Serialize<EntityDocument>(document, EntityDocuments.Options));

    public async Task WriteSettingsAsync(SettingsFile settings, CancellationToken cancellationToken) =>
        await File.WriteAllTextAsync(
            Path.Combine(Data(), Settings.FileName),
            JsonSerializer.Serialize(settings, TooltipCommand.JsonOptions),
            cancellationToken);

    public async Task WriteImageAsync(string name, byte[] png, CancellationToken cancellationToken)
    {
        if (_images.Repeats(name, png, out _)) return;

        var ui = Path.Combine(root, ExportLayout.UiFolder);
        Directory.CreateDirectory(ui);

        await File.WriteAllBytesAsync(Path.Combine(ui, name), png, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_entities is null) return;

        await _entities.FlushAsync();
        await _entities.DisposeAsync();
    }
}
