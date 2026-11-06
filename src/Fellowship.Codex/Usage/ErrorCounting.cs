namespace Fellowship.Codex.Usage;

/// <summary>
/// Counts Error and Critical log events by category and exception type. An exception inside an
/// interactive component is in the circuit and has no HTTP status, so this count is the only one it
/// reaches. The database has the count and never the message. <see cref="RecentLogs" /> holds the
/// message of each Warning and higher event in memory.
/// </summary>
public sealed class ErrorCounting(UsageCounter counter, RecentLogs logs) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new Counted(counter, logs, categoryName);

    public void Dispose()
    {
    }

    private sealed class Counted(UsageCounter counter, RecentLogs logs, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning && logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var name = eventId.Name ?? eventId.Id.ToString();
            var message = formatter(state, exception);

            logs.Add(logLevel, category, name, Template(state) ?? message, message, exception);

            if (logLevel >= LogLevel.Error) counter.Error(category, exception?.GetType().FullName ?? name);
        }

        private static string? Template<TState>(TState state) =>
            state is IReadOnlyList<KeyValuePair<string, object?>> values
                ? values.FirstOrDefault(value => value.Key == RecentLogs.TemplateKey).Value as string
                : null;
    }
}
