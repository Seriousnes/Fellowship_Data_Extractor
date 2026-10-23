using Fellowship.Codex.Services;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fellowship.Codex.Mcp;

public sealed class ExportWatcher(
    ExportLocation location,
    LiveExport export,
    ILogger<ExportWatcher> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    public static string? Signature(string output)
    {
        if (FolderExportSource.Newest(new DirectoryInfo(output)) is not { } folder) return null;

        var entities = new FileInfo(Path.Combine(folder, ExportReader.EntitiesName));
        var settings = new FileInfo(Path.Combine(folder, ExportReader.SettingsName));

        return $"{folder}|{entities.Length}|{entities.LastWriteTimeUtc.Ticks}|" +
               $"{settings.Length}|{settings.LastWriteTimeUtc.Ticks}";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (location.Output is not { } output) return;

        var loaded = Signature(output);
        var pending = loaded;

        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            string? seen;

            try
            {
                seen = Signature(output);
            }
            catch (IOException failure)
            {
                logger.LogWarning(failure, "Reading {Output} failed.", output);
                continue;
            }

            if (seen is null || seen == loaded) continue;

            if (seen != pending)
            {
                pending = seen;
                continue;
            }

            var folder = seen[..seen.IndexOf('|')];

            try
            {
                await export.LoadAsync(folder, stoppingToken);
                loaded = seen;
                logger.LogInformation("Loaded the export at {Folder}.", folder);
            }
            catch (Exception failure) when (failure is not OperationCanceledException)
            {
                logger.LogError(failure, "Loading the export at {Folder} failed. The previous export stays loaded.", folder);
                loaded = seen;
            }
        }
    }
}
