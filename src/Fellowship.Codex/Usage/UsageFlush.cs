namespace Fellowship.Codex.Usage;

/// <summary>
/// Writes the counts to SQLite every ten seconds, and deletes counts past the retention each day.
/// </summary>
public sealed class UsageFlush(
    UsageCounter counter,
    UsageStore store,
    UsageSettings settings,
    TimeProvider time,
    ILogger<UsageFlush> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan PruneInterval = TimeSpan.FromDays(1);

    // Hourly counts are kept for the 24-hour and 7-day ranges, and the longer ranges read by day.
    private static readonly TimeSpan RollupAge = TimeSpan.FromDays(30);

    // Per-minute counts serve the last-hour chart alone.
    private static readonly TimeSpan MinuteAge = TimeSpan.FromDays(2);

    private DateTimeOffset pruned = DateTimeOffset.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, time);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken)) Tick();
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

    private void Tick()
    {
        Flush();

        var now = time.GetUtcNow();

        if (now - pruned < PruneInterval) return;

        pruned = now;

        try
        {
            store.Prune(now - TimeSpan.FromDays(settings.RetentionDays), now - RollupAge, now - MinuteAge);
        }
        catch (Exception failure)
        {
            logger.LogWarning(failure, "Pruning the usage counts failed.");
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
}
