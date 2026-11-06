using System.Diagnostics;

namespace Fellowship.Codex.Usage;

/// <summary>Peak of each measure over one minute. Memory is in bytes and processor time in percent.</summary>
public sealed record ServerSample(
    long Minute, int Running, int Queued, int PoolQueue, int Circuits, double Processor, long Memory, long Heap);

/// <summary>The process at this moment.</summary>
public sealed record ServerReading(
    DateTimeOffset Started,
    long Memory,
    long MemoryLimit,
    long Heap,
    int FullCollections,
    int Threads,
    int PoolThreads,
    int PoolQueue,
    int Running,
    int Queued,
    int Circuits,
    double Processor);

/// <summary>Measures the process, and keeps the peaks of each minute for the database.</summary>
public sealed class ServerWatch(RequestLoad load, TimeProvider time)
{
    private const string ContainerLimitFile = "/sys/fs/cgroup/memory.max";

    private readonly Lock gate = new();

    private readonly DateTimeOffset started = new(Process.GetCurrentProcess().StartTime.ToUniversalTime());

    private int circuits;

    private ServerSample? peak;

    private double processor;

    private TimeSpan spent = Environment.CpuUsage.TotalTime;

    private DateTimeOffset measured = time.GetUtcNow();

    public void CircuitOpened() => Interlocked.Increment(ref circuits);

    public void CircuitClosed() => Interlocked.Decrement(ref circuits);

    public ServerReading Read()
    {
        var (running, queued) = load.Now();
        var memory = GC.GetGCMemoryInfo();

        return new ServerReading(
            started,
            Environment.WorkingSet,
            ContainerLimit() ?? memory.TotalAvailableMemoryBytes,
            GC.GetTotalMemory(false),
            GC.CollectionCount(GC.MaxGeneration),
            Process.GetCurrentProcess().Threads.Count,
            ThreadPool.ThreadCount,
            (int)ThreadPool.PendingWorkItemCount,
            running,
            queued,
            Volatile.Read(ref circuits),
            Volatile.Read(ref processor));
    }

    // In a container the runtime reports its heap limit, a fraction of the limit the kernel enforces.
    private static long? ContainerLimit() =>
        File.Exists(ContainerLimitFile) && long.TryParse(File.ReadAllText(ContainerLimitFile), out var limit) ? limit : null;

    /// <summary>Takes a measure. Returns the finished minute's peaks when this measure opens a new minute.</summary>
    public ServerSample? Tick()
    {
        var now = time.GetUtcNow();
        var total = Environment.CpuUsage.TotalTime;
        var elapsed = now - measured;

        lock (gate)
        {
            if (elapsed > TimeSpan.Zero)
            {
                Volatile.Write(ref processor, (total - spent) / elapsed / Environment.ProcessorCount * 100);
            }

            spent = total;
            measured = now;

            var reading = Read();
            var minute = UsageCounter.Minute(now);
            var finished = peak is { } open && open.Minute != minute ? open : null;
            var current = finished is null ? peak : null;

            peak = new ServerSample(
                minute,
                Math.Max(current?.Running ?? 0, reading.Running),
                Math.Max(current?.Queued ?? 0, reading.Queued),
                Math.Max(current?.PoolQueue ?? 0, reading.PoolQueue),
                Math.Max(current?.Circuits ?? 0, reading.Circuits),
                Math.Max(current?.Processor ?? 0, reading.Processor),
                Math.Max(current?.Memory ?? 0, reading.Memory),
                Math.Max(current?.Heap ?? 0, reading.Heap));

            return finished;
        }
    }
}
