using System.Security.Cryptography;

using Fellowship.Storage;

namespace Fellowship.Codex.Usage;

/// <summary>
/// Writes the counts to SQLite every ten seconds, deletes counts past the retention, and uploads a
/// snapshot of the database to storage each day.
/// </summary>
public sealed class UsageFlush(
    UsageCounter counter,
    UsageStore store,
    UsageSettings settings,
    TimeProvider time,
    ILogger<UsageFlush> logger,
    IServiceProvider services) : BackgroundService
{
    public const string SnapshotName = "usage/usage.db";

    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan SnapshotInterval = TimeSpan.FromDays(1);

    // Hourly counts are kept for the 24-hour and 7-day ranges, and the longer ranges read by day.
    private static readonly TimeSpan RollupAge = TimeSpan.FromDays(30);

    // Per-minute counts serve the last-hour chart alone.
    private static readonly TimeSpan MinuteAge = TimeSpan.FromDays(2);

    private DateTimeOffset snapshotted = DateTimeOffset.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, time);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken)) await TickAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        Flush();
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        Flush();

        var now = time.GetUtcNow();

        if (now - snapshotted < SnapshotInterval) return;

        snapshotted = now;

        try
        {
            store.Prune(now - TimeSpan.FromDays(settings.RetentionDays), now - RollupAge, now - MinuteAge);
            await UploadAsync(cancellationToken);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            logger.LogWarning(failure, "The usage snapshot failed.");
        }
    }

    private void Flush()
    {
        var batch = counter.Drain();

        try
        {
            store.Write(batch);
        }
        catch (Exception failure)
        {
            logger.LogWarning(failure, "Writing {Requests} usage rows failed.", batch.Requests.Count);
        }
    }

    private async Task UploadAsync(CancellationToken cancellationToken)
    {
        if (services.GetService<IStorage>() is not { } storage) return;

        var file = $"{store.Path}.snapshot";

        try
        {
            store.Snapshot(file);

            var content = await File.ReadAllBytesAsync(file, cancellationToken);

            await storage.WriteAsync(
                SnapshotName, content, "application/vnd.sqlite3", "no-store", MD5.HashData(content), cancellationToken);
        }
        finally
        {
            File.Delete(file);
        }
    }
}
