using System.Text.Json;

namespace Fellowship.Codex.Usage;

/// <summary>The ranges the dashboard reads, in days.</summary>
public static class UsageRanges
{
    public static readonly int[] Days = [1, 7, 30, 90];

    public const int Default = 7;

    public const string ClientErrors = "4xx";

    public const string ServerErrors = "5xx";

    public const string Running = "Running";

    public const string Queued = "Queued";

    public const string PoolQueue = "Thread pool queue";

    public const string Processor = "Processor %";

    public const string WorkingSet = "Working set";

    public const string Heap = "Heap";

    private const long Minute = 60;

    private const long Hour = 3600;

    private const long Day = 86400;

    private const int LiveMinutes = 60;

    private const int DayMinutes = 1440;

    private const long Mebibyte = 1024 * 1024;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static int Pick(int? requested) => requested is { } days && Days.Contains(days) ? days : Default;

    public static string Label(int days) => days == 1 ? "24 hours" : $"{days} days";

    /// <summary>Seconds per chart bucket: an hour up to a week, a day past it.</summary>
    public static long Bucket(int days) => days <= 7 ? Hour : Day;

    public static string Unit(int days) => Bucket(days) == Hour ? "hour" : "day";

    public static string Head(int days) => $"{Label(days)}, per {Unit(days)}, by area";

    public static string ErrorsHead(int days) => $"{Label(days)}, errors per {Unit(days)}";

    // The ordinal of each series fixes its colour slot, so a series keeps its colour whatever else is drawn.
    private static readonly string[] Kinds = Enum.GetNames<ClientKind>();

    private static readonly string[] Reasons = Enum.GetNames<FailureReason>();

    public static readonly UsageArea[] ChartAreas = [UsageArea.Page, UsageArea.Api, UsageArea.Card, UsageArea.Circuit];

    public const string OtherAreas = "Other";

    public static string ChartArea(string area) =>
        Enum.TryParse<UsageArea>(area, out var parsed) && ChartAreas.Contains(parsed) ? area : OtherAreas;

    private static readonly string[] Areas = [.. ChartAreas.Select(area => area.ToString()), OtherAreas];

    public static string Charts(UsageStore store, int days, DateTimeOffset now)
    {
        var thisMinute = UsageCounter.Minute(now);
        var liveStart = thisMinute - (LiveMinutes - 1) * Minute;
        var live = Chart(
            store.Minutes(DateTimeOffset.FromUnixTimeSeconds(liveStart)),
            liveStart, thisMinute, Minute, Kinds, point => point.Series);

        var bucket = Bucket(days);
        var since = now - TimeSpan.FromDays(days);
        var rangeStart = since.ToUnixTimeSeconds() / bucket * bucket;
        var rangeEnd = now.ToUnixTimeSeconds() / bucket * bucket;
        var range = Chart(store.Series(since, bucket), rangeStart, rangeEnd, bucket, Areas, point => ChartArea(point.Series));
        var errors = Chart(
            store.ErrorSeries(since, bucket), rangeStart, rangeEnd, bucket, [ClientErrors, ServerErrors], point => point.Series);

        var dayStart = thisMinute - (DayMinutes - 1) * Minute;
        var day = DateTimeOffset.FromUnixTimeSeconds(dayStart);
        var failures = Chart(store.FailureMinutes(day), dayStart, thisMinute, Minute, Reasons, point => point.Series);
        var samples = store.Samples(day);
        var load = Chart(
            [.. samples.SelectMany(sample => new SeriesPoint[]
            {
                new(sample.Minute, Running, sample.Running),
                new(sample.Minute, Queued, sample.Queued),
                new(sample.Minute, PoolQueue, sample.PoolQueue),
                new(sample.Minute, Processor, (long)Math.Round(sample.Processor)),
            })],
            dayStart, thisMinute, Minute, [Running, Queued, PoolQueue, Processor], point => point.Series);
        var memory = Chart(
            [.. samples.SelectMany(sample => new SeriesPoint[]
            {
                new(sample.Minute, WorkingSet, sample.Memory / Mebibyte),
                new(sample.Minute, Heap, sample.Heap / Mebibyte),
            })],
            dayStart, thisMinute, Minute, [WorkingSet, Heap], point => point.Series);

        return JsonSerializer.Serialize(new
        {
            interval = (int)UsageFlush.Interval.TotalSeconds,
            generated = now.ToUnixTimeSeconds(),
            live,
            range = new { range.Labels, range.Series, unit = Unit(days), head = Head(days) },
            errors = new { errors.Labels, errors.Series, unit = Unit(days), head = ErrorsHead(days) },
            failures,
            load,
            memory,
        }, Json);
    }

    private sealed record ChartSeries(string Name, int Slot, long[] Values);

    private sealed record ChartData(long[] Labels, ChartSeries[] Series);

    private static ChartData Chart(
        IReadOnlyList<SeriesPoint> points, long start, long end, long step, string[] names, Func<SeriesPoint, string> series)
    {
        var labels = new List<long>();

        for (var at = start; at <= end; at += step) labels.Add(at);

        var values = names.ToDictionary(name => name, _ => new long[labels.Count]);

        foreach (var point in points)
        {
            var index = (int)((point.Bucket - start) / step);

            if (index < 0 || index >= labels.Count || !values.TryGetValue(series(point), out var row)) continue;

            row[index] += point.Requests;
        }

        return new ChartData(
            [.. labels],
            [.. names.Select((name, slot) => new ChartSeries(name, slot + 1, values[name])).Where(row => row.Values.Any(value => value > 0))]);
    }
}
