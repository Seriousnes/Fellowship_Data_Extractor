namespace Fellowship.Codex.Services;

public sealed class FolderExportSource(string folder) : IExportSource
{
    public const string Variable = "FELLOWSHIP_EXPORT";

    public string Folder { get; } = folder;

    public string Root => Directory.GetParent(Path.GetFullPath(Folder))?.FullName ?? Folder;

    public static string Located(string? option = null) =>
        Environment.GetEnvironmentVariable(Variable) is { Length: > 0 } named
            ? Carried(named, $"{Variable} names")
            : Above(Directory.GetCurrentDirectory())
                ?? Above(AppContext.BaseDirectory)
                ?? throw Unlocated(option);

    public static string Carried(string folder, string source)
    {
        var full = Path.GetFullPath(folder);

        return Carries(full)
            ? full
            : throw new InvalidOperationException(
                $"{source} {full}. That folder carries no {ExportReader.EntitiesName}.");
    }

    public static InvalidOperationException Unlocated(string? option = null) =>
        new($"No output/{ExportFolders.Pattern} carrying {ExportReader.EntitiesName} and " +
            $"{ExportReader.SettingsName} above {Directory.GetCurrentDirectory()} or " +
            $"{AppContext.BaseDirectory}, and " +
            (option is null ? $"{Variable} names none" : $"neither {option} nor {Variable} names one") +
            ". Write it with 'dotnet run --project src/FellowshipExtractor'.");

    public static bool Carries(string folder) =>
        File.Exists(Path.Combine(folder, ExportReader.EntitiesName)) &&
        File.Exists(Path.Combine(folder, ExportReader.SettingsName));

    public static string? Above(string start)
    {
        for (var here = new DirectoryInfo(start); here is not null; here = here.Parent)
        {
            var output = new DirectoryInfo(Path.Combine(here.FullName, "output"));

            if (!output.Exists) continue;

            if (Newest(output) is { } export) return export;
        }

        return null;
    }

    public static string? Newest(DirectoryInfo output) =>
        output.EnumerateDirectories(ExportFolders.Pattern)
            .Where(folder => Carries(folder.FullName))
            .OrderByDescending(folder => ExportFolders.Build(folder.Name))
            .ThenByDescending(folder => folder.Name, StringComparer.Ordinal)
            .FirstOrDefault()
            ?.FullName;

    public async Task<ExportHistory> LoadAsync(CancellationToken cancellationToken)
    {
        var settings = ExportReader.ReadSettings(
            await TextAsync(ExportReader.SettingsName, cancellationToken));

        var lines = await LinesAsync(ExportReader.EntitiesName, cancellationToken);

        var here = new DirectoryInfo(Path.GetFullPath(Folder));

        return new ExportHistory(
            ExportFolders.Id(here.Name),
            new ExportSnapshot(settings, ExportReader.ReadEntities(lines)),
            await OlderAsync(here, cancellationToken));
    }

    private static async Task<IReadOnlyDictionary<string, ExportVersion>> OlderAsync(
        DirectoryInfo here, CancellationToken cancellationToken)
    {
        var older = new Dictionary<string, ExportVersion>(StringComparer.Ordinal);
        var build = ExportFolders.Build(here.Name);

        if (build < 0 || here.Parent is not { Exists: true } parent) return older;

        foreach (var sibling in parent.EnumerateDirectories(ExportFolders.Pattern))
        {
            var beneath = ExportFolders.Build(sibling.Name);

            if (beneath < 0 || beneath >= build) continue;
            if (!Carries(sibling.FullName)) continue;

            var id = ExportFolders.Id(sibling.Name);

            older[id] = new ExportVersion(
                id,
                ExportReader.Group(await File.ReadAllLinesAsync(
                    Path.Combine(sibling.FullName, ExportReader.EntitiesName),
                    cancellationToken)),
                await File.ReadAllTextAsync(
                    Path.Combine(sibling.FullName, ExportReader.SettingsName),
                    cancellationToken));
        }

        return older;
    }

    private async Task<string?> TextAsync(string name, CancellationToken cancellationToken)
    {
        var file = Path.Combine(Folder, name);

        return File.Exists(file) ? await File.ReadAllTextAsync(file, cancellationToken) : null;
    }

    private async Task<string[]> LinesAsync(string name, CancellationToken cancellationToken)
    {
        var file = Path.Combine(Folder, name);

        if (!File.Exists(file)) throw ExportReader.Missing(name);

        return await File.ReadAllLinesAsync(file, cancellationToken);
    }
}
