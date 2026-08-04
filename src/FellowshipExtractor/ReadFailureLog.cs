using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace FellowshipExtractor;

/// <summary>
/// Routes CUE4Parse's own log to stderr when FELLOWSHIP_LOG is set.
///
/// CUE4Parse catches every export it fails to deserialize and reports the reason through Serilog,
/// which an application with no sink discards. The export then arrives with the properties it read
/// before the failure and nothing to say the rest is missing, so a wrong schema reads as an asset
/// that simply has less on it. Turning this on is what turns "the curve table has no rows" into
/// "SimpleCurve: Unknown property with value 2", which names the broken schema outright.
///
/// Off by default because the build has standing read failures in areas nothing here reads, and a
/// thousand lines of them on every command would bury the output a caller asked for.
/// </summary>
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
