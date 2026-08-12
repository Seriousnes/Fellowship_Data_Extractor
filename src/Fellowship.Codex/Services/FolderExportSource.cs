namespace Fellowship.Codex.Services;

public sealed class FolderExportSource(string folder) : IExportSource
{
    public const string Variable = "FELLOWSHIP_EXPORT";

    public string Folder { get; } = folder;

    public static bool Carries(string folder) =>
        File.Exists(Path.Combine(folder, ExportReader.Data, ExportReader.EntitiesName));

    public static string? Above(string start)
    {
        for (var here = new DirectoryInfo(start); here is not null; here = here.Parent)
        {
            var export = Path.Combine(here.FullName, "output", "export");

            if (Carries(export)) return export;
        }

        return null;
    }

    public async Task<ExportSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        var settings = ExportReader.ReadSettings(
            await TextAsync(ExportReader.SettingsName, cancellationToken));

        var lines = await LinesAsync(ExportReader.EntitiesName, cancellationToken);

        return new ExportSnapshot(settings, ExportReader.ReadEntities(lines));
    }

    private async Task<string?> TextAsync(string name, CancellationToken cancellationToken)
    {
        var file = Path.Combine(Folder, ExportReader.Data, name);

        return File.Exists(file) ? await File.ReadAllTextAsync(file, cancellationToken) : null;
    }

    private async Task<string[]> LinesAsync(string name, CancellationToken cancellationToken)
    {
        var file = Path.Combine(Folder, ExportReader.Data, name);

        if (!File.Exists(file)) throw ExportReader.Missing(name);

        return await File.ReadAllLinesAsync(file, cancellationToken);
    }
}
