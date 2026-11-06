using System.Collections.Concurrent;

namespace Fellowship.Codex.Usage;

public static class Latency
{
    /// <summary>Upper bound in milliseconds of each duration bucket. A duration above the last is in one more bucket.</summary>
    public static readonly double[] Bounds = [10, 50, 200, 1000];

    public static int Count => Bounds.Length + 1;

    public static int Bucket(double milliseconds)
    {
        for (var bucket = 0; bucket < Bounds.Length; bucket++)
        {
            if (milliseconds <= Bounds[bucket]) return bucket;
        }

        return Bounds.Length;
    }

    public static string Label(int bucket) =>
        bucket < Bounds.Length ? $"≤ {Bounds[bucket]:0} ms" : $"> {Bounds[^1]:0} ms";

    /// <summary>The bucket holding the given fraction of the counted requests.</summary>
    public static int? Percentile(IReadOnlyList<long> buckets, double fraction)
    {
        var total = buckets.Sum();

        if (total == 0) return null;

        var target = Math.Ceiling(total * fraction);
        var running = 0L;

        for (var bucket = 0; bucket < buckets.Count; bucket++)
        {
            running += buckets[bucket];

            if (running >= target) return bucket;
        }

        return buckets.Count - 1;
    }
}

public readonly record struct RequestKey(
    long Hour, UsageArea Area, string Method, string Route, string Entity, int Status, bool Cached);

public readonly record struct AgentKey(long Day, Agent Agent, UsageArea Area, int StatusClass);

public readonly record struct ErrorKey(long Hour, string Category, string Exception);

public readonly record struct MinuteKey(long Minute, ClientKind Kind, UsageArea Area);

public readonly record struct FailureKey(
    long Hour, FailureReason Reason, int Status, UsageArea Area, string Method, string Route, string Entity, string Browser, string Os);

public readonly record struct FailureMinuteKey(long Minute, FailureReason Reason);

public sealed class RequestTally
{
    public long Count;

    public double SumMilliseconds;

    public double MaxMilliseconds;

    public readonly long[] Buckets = new long[Latency.Count];
}

public sealed record UsageBatch(
    IReadOnlyDictionary<RequestKey, RequestTally> Requests,
    IReadOnlyDictionary<AgentKey, long> Agents,
    IReadOnlyDictionary<ErrorKey, long> Errors,
    IReadOnlyDictionary<MinuteKey, long> Minutes,
    IReadOnlyDictionary<FailureKey, long> Failures,
    IReadOnlyDictionary<FailureMinuteKey, long> FailureMinutes)
{
    public bool Empty =>
        Requests.Count == 0 && Agents.Count == 0 && Errors.Count == 0 && Minutes.Count == 0 && Failures.Count == 0 && FailureMinutes.Count == 0;
}

/// <summary>Request, agent and error counts held in memory until the next flush.</summary>
public sealed class UsageCounter(TimeProvider time)
{
    private readonly Lock gate = new();

    private Dictionary<RequestKey, RequestTally> requests = [];

    private Dictionary<AgentKey, long> agents = [];

    private Dictionary<MinuteKey, long> minutes = [];

    private Dictionary<FailureKey, long> failures = [];

    private Dictionary<FailureMinuteKey, long> failureMinutes = [];

    private ConcurrentDictionary<ErrorKey, long> errors = new();

    public static long Minute(DateTimeOffset at) => at.ToUnixTimeSeconds() / 60 * 60;

    public static long Hour(DateTimeOffset at) => at.ToUnixTimeSeconds() / 3600 * 3600;

    public static long Day(DateTimeOffset at) => at.ToUnixTimeSeconds() / 86400 * 86400;

    public void Request(UsageRoute route, string method, int status, bool cached, Agent agent, double? milliseconds)
    {
        var now = time.GetUtcNow();
        var key = new RequestKey(Hour(now), route.Area, method, route.Route, route.Entity, status, cached);
        var agentKey = new AgentKey(Day(now), agent, route.Area, status / 100);
        var minuteKey = new MinuteKey(Minute(now), agent.Kind, route.Area);

        lock (gate)
        {
            if (!requests.TryGetValue(key, out var tally)) requests[key] = tally = new RequestTally();

            tally.Count++;

            if (milliseconds is { } duration)
            {
                tally.SumMilliseconds += duration;
                tally.MaxMilliseconds = Math.Max(tally.MaxMilliseconds, duration);
                tally.Buckets[Latency.Bucket(duration)]++;
            }

            agents[agentKey] = agents.GetValueOrDefault(agentKey) + 1;
            minutes[minuteKey] = minutes.GetValueOrDefault(minuteKey) + 1;
        }
    }

    public void Failure(UsageRoute route, string method, int status, FailureReason reason, Agent agent)
    {
        var now = time.GetUtcNow();
        var key = new FailureKey(Hour(now), reason, status, route.Area, method, route.Route, route.Entity, agent.Browser, agent.Os);
        var minuteKey = new FailureMinuteKey(Minute(now), reason);

        lock (gate)
        {
            failures[key] = failures.GetValueOrDefault(key) + 1;
            failureMinutes[minuteKey] = failureMinutes.GetValueOrDefault(minuteKey) + 1;
        }
    }

    public void Error(string category, string exception) =>
        errors.AddOrUpdate(new ErrorKey(Hour(time.GetUtcNow()), category, exception), 1, (_, count) => count + 1);

    public UsageBatch Drain()
    {
        Dictionary<RequestKey, RequestTally> drainedRequests;
        Dictionary<AgentKey, long> drainedAgents;
        Dictionary<MinuteKey, long> drainedMinutes;
        Dictionary<FailureKey, long> drainedFailures;
        Dictionary<FailureMinuteKey, long> drainedFailureMinutes;

        lock (gate)
        {
            (drainedRequests, requests) = (requests, []);
            (drainedAgents, agents) = (agents, []);
            (drainedMinutes, minutes) = (minutes, []);
            (drainedFailures, failures) = (failures, []);
            (drainedFailureMinutes, failureMinutes) = (failureMinutes, []);
        }

        var drainedErrors = Interlocked.Exchange(ref errors, new ConcurrentDictionary<ErrorKey, long>());

        return new UsageBatch(drainedRequests, drainedAgents, drainedErrors, drainedMinutes, drainedFailures, drainedFailureMinutes);
    }
}
