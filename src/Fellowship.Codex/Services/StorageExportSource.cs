using System.Text;

using Fellowship.Storage;

namespace Fellowship.Codex.Services;

public sealed class StorageExportSource(IStorage storage) : IExportSource
{
    public async Task<ExportHistory> LoadAsync(CancellationToken cancellationToken)
    {
        var folders = await FoldersAsync(cancellationToken);
        var builds = Builds(folders);

        if (builds.Count == 0) throw Absent(folders);

        var latest = builds[0];

        var settingsText = await TextAsync(latest, ExportReader.SettingsName, cancellationToken)
            ?? throw ExportReader.Missing(ExportReader.SettingsName);

        var settings = ExportReader.ReadSettings(settingsText);

        var entities = await TextAsync(latest, ExportReader.EntitiesName, cancellationToken)
            ?? throw ExportReader.Missing(ExportReader.EntitiesName);

        var older = new Dictionary<string, ExportVersion>(StringComparer.Ordinal);

        foreach (var build in builds.Skip(1))
        {
            older[build] = new ExportVersion(
                build,
                ExportReader.Group(ExportReader.Lines(
                    await TextAsync(build, ExportReader.EntitiesName, cancellationToken) ?? string.Empty)),
                await TextAsync(build, ExportReader.SettingsName, cancellationToken) ?? string.Empty);
        }

        return new ExportHistory(
            latest,
            new ExportSnapshot(settings, ExportReader.ReadEntities(ExportReader.Lines(entities))),
            new ExportFiles(Encoding.UTF8.GetBytes(entities), Encoding.UTF8.GetBytes(settingsText)),
            older);
    }

    public static IReadOnlyList<string> Builds(IReadOnlyDictionary<string, HashSet<string>> byFolder) =>
        [
            .. byFolder
                .Where(version => ExportFolders.Build(version.Key) >= 0 && Whole(version.Value))
                .Select(version => version.Key)
                .OrderByDescending(ExportFolders.Build)
                .ThenByDescending(name => name, StringComparer.Ordinal)
                .Select(ExportFolders.Id),
        ];

    private async Task<Dictionary<string, HashSet<string>>> FoldersAsync(CancellationToken cancellationToken)
    {
        var byFolder = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        await foreach (var stored in storage.ListAsync(Beneath, cancellationToken))
        {
            if (!stored.Name.StartsWith(Beneath, StringComparison.Ordinal)) continue;

            var beneath = stored.Name[Beneath.Length..];

            if (beneath.Length == 0) continue;

            var separator = beneath.IndexOf('/');
            var folder = separator > 0 ? beneath[..separator] : ExportReader.DataFolder;

            if (!byFolder.TryGetValue(folder, out var files))
            {
                byFolder[folder] = files = new HashSet<string>(StringComparer.Ordinal);
            }

            files.Add(separator > 0 ? beneath[(separator + 1)..] : beneath);
        }

        return byFolder;
    }

    private static bool Whole(HashSet<string> files) =>
        files.Contains(ExportReader.EntitiesName) && files.Contains(ExportReader.SettingsName);

    private const string Beneath = $"{ExportReader.DataFolder}/";

    private const int MaxNames = 4;

    private static InvalidOperationException Absent(IReadOnlyDictionary<string, HashSet<string>> byFolder)
    {
        if (byFolder.Count == 0) return ExportReader.Missing(ExportReader.EntitiesName);

        var folders = string.Join("; ", byFolder
            .OrderByDescending(version => ExportFolders.Build(version.Key))
            .ThenByDescending(version => version.Key, StringComparer.Ordinal)
            .Select(version => $"{version.Key} holds {Holds(version.Value)}"));

        return new InvalidOperationException(
            $"No {ExportReader.Data(ExportFolders.Pattern)} folder in the store carries both " +
            $"{ExportReader.EntitiesName} and " +
            $"{ExportReader.SettingsName}, which is where an object store's export is " +
            $"read from. The store holds {folders}. A store filled by an extractor that laid the pair out " +
            "at another depth reads this way until it is filled again in the shape it is read in: 'dotnet " +
            "run --project src/FellowshipExtractor -- --connection-string <value>' for an Azure Blob " +
            "container, '-- --bucket <name>' for an S3 bucket.");
    }

    private static string Holds(HashSet<string> files)
    {
        if (files.Count == 0) return "nothing";

        var named = files.Order(StringComparer.Ordinal).Take(MaxNames).ToArray();
        var beyond = files.Count - named.Length;

        return beyond > 0
            ? $"{string.Join(", ", named)} and {beyond} more"
            : string.Join(", ", named);
    }

    private Task<string?> TextAsync(string build, string name, CancellationToken cancellationToken) =>
        storage.ReadTextAsync(ExportReader.Data(build, name), cancellationToken);
}
