namespace Fellowship.Codex.Usage;

/// <summary>
/// Counts Error and Critical log events by category and exception type. An exception inside an
/// interactive component is in the circuit and has no HTTP status, so this count is the only one it
/// reaches. The message is never stored.
/// </summary>
public sealed class ErrorCounting(UsageCounter counter) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new Counted(counter, categoryName);

    public void Dispose()
    {
    }

    private sealed class Counted(UsageCounter counter, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            counter.Error(category, exception?.GetType().FullName ?? eventId.Name ?? eventId.Id.ToString());
        }
    }
}
