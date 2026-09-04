using Fellowship.Storage;

namespace Fellowship.Codex.Services;

public sealed class StorageExportSource(IStorage storage) : IExportSource
{
    public async Task<ExportHistory> LoadAsync(CancellationToken cancellationToken)
    {
        var carried = await CarriedAsync(cancellationToken);
        var builds = Builds(carried);

        if (builds.Count == 0) throw Absent(carried);

        var latest = builds[0];

        var settings = ExportReader.ReadSettings(
            await TextAsync(latest, ExportReader.SettingsName, cancellationToken));

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
            older);
    }

    public static IReadOnlyList<string> Builds(IReadOnlyDictionary<string, HashSet<string>> carried) =>
        [
            .. carried
                .Where(version => Whole(version.Value))
                .Select(version => version.Key)
                .OrderByDescending(ExportFolders.Build)
                .ThenByDescending(name => name, StringComparer.Ordinal)
                .Select(ExportFolders.Id),
        ];

    private async Task<Dictionary<string, HashSet<string>>> CarriedAsync(CancellationToken cancellationToken)
    {
        var carried = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        await foreach (var stored in storage.ListAsync(ExportFolders.Prefix, cancellationToken))
        {
            var separator = stored.Name.IndexOf('/');

            if (separator <= 0) continue;

            var folder = stored.Name[..separator];

            if (!carried.TryGetValue(folder, out var files))
            {
                carried[folder] = files = new HashSet<string>(StringComparer.Ordinal);
            }

            files.Add(stored.Name[(separator + 1)..]);
        }

        return carried;
    }

    private static bool Whole(HashSet<string> files) =>
        files.Contains(ExportReader.EntitiesName) &&
        files.Contains(ExportReader.SettingsName);

    private static InvalidOperationException Absent(IReadOnlyDictionary<string, HashSet<string>> carried) =>
        ExportReader.Missing(
            carried.Any(version => version.Value.Contains(ExportReader.EntitiesName))
                ? ExportReader.SettingsName
                : ExportReader.EntitiesName);

    private Task<string?> TextAsync(string build, string name, CancellationToken cancellationToken) =>
        storage.ReadTextAsync(ExportReader.Data(build, name), cancellationToken);
}
