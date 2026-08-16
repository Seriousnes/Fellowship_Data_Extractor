using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Fellowship.Codex.Services;

public sealed class BlobExportSource(BlobContainerClient container) : IExportSource
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

        await foreach (var blob in container.GetBlobsAsync(
            BlobTraits.None, BlobStates.None, ExportFolders.Prefix, cancellationToken))
        {
            var separator = blob.Name.IndexOf('/');

            if (separator <= 0) continue;

            var folder = blob.Name[..separator];

            if (!carried.TryGetValue(folder, out var files))
            {
                carried[folder] = files = new HashSet<string>(StringComparer.Ordinal);
            }

            files.Add(blob.Name[(separator + 1)..]);
        }

        return carried;
    }

    private static bool Whole(HashSet<string> files) =>
        files.Contains(ExportReader.Blob(ExportReader.EntitiesName)) &&
        files.Contains(ExportReader.Blob(ExportReader.SettingsName));

    private static InvalidOperationException Absent(IReadOnlyDictionary<string, HashSet<string>> carried) =>
        ExportReader.Missing(
            carried.Any(version => version.Value.Contains(ExportReader.Blob(ExportReader.EntitiesName)))
                ? ExportReader.SettingsName
                : ExportReader.EntitiesName);

    private async Task<string?> TextAsync(string build, string name, CancellationToken cancellationToken)
    {
        var blob = container.GetBlobClient(ExportReader.Blob(build, name));

        if (!await blob.ExistsAsync(cancellationToken)) return null;

        var content = await blob.DownloadContentAsync(cancellationToken);

        return content.Value.Content.ToString();
    }
}
