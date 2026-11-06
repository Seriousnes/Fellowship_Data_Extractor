namespace Fellowship.Codex.Usage;

/// <summary>Occurrences of one log event, with the text of the latest.</summary>
public sealed record LogEvent(
    LogLevel Level,
    string Category,
    string Event,
    DateTimeOffset First,
    DateTimeOffset Last,
    long Count,
    string Message,
    string? Exception);

/// <summary>
/// Warning and higher log events, in memory alone. A repeat of an event adds to its count, so a flood of
/// one message leaves the others in place.
/// </summary>
public sealed class RecentLogs(TimeProvider time)
{
    public const int Capacity = 500;

    public const string TemplateKey = "{OriginalFormat}";

    private const int MessageLength = 2000;

    private const int ExceptionLength = 8000;

    private readonly Lock gate = new();

    private readonly Dictionary<(LogLevel, string, string, string?), LogEvent> events = [];

    public void Add(LogLevel level, string category, string name, string template, string message, Exception? exception)
    {
        var now = time.GetUtcNow();
        var key = (level, category, template, exception?.GetType().FullName);
        var text = Clip(message, MessageLength);
        var detail = exception is null ? null : Clip(exception.ToString(), ExceptionLength);

        lock (gate)
        {
            events[key] = events.TryGetValue(key, out var seen)
                ? seen with { Last = now, Count = seen.Count + 1, Message = text, Exception = detail }
                : new LogEvent(level, category, name, now, now, 1, text, detail);

            if (events.Count > Capacity) events.Remove(events.MinBy(entry => entry.Value.Last).Key);
        }
    }

    /// <summary>The events, latest first.</summary>
    public IReadOnlyList<LogEvent> Newest()
    {
        lock (gate) return [.. events.Values.OrderByDescending(entry => entry.Last)];
    }

    private static string Clip(string text, int length) => text.Length <= length ? text : text[..length];
}
