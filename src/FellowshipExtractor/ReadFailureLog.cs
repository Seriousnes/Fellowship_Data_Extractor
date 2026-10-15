using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace FellowshipExtractor;

public static class ReadFailureLog
{
    public static void EnableIfRequested()
    {
        if (Environment.GetEnvironmentVariable("FELLOWSHIP_LOG") is not { Length: > 0 }) return;

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Warning()
            .WriteTo.Sink(new StandardErrorSink())
            .CreateLogger();
    }

    private sealed class StandardErrorSink : ILogEventSink
    {
        public void Emit(LogEvent logEvent)
        {
            Console.Error.WriteLine($"[{logEvent.Level}] {logEvent.RenderMessage()}");
            if (logEvent.Exception is { } ex) Console.Error.WriteLine(ex.Message);
        }
    }
}
