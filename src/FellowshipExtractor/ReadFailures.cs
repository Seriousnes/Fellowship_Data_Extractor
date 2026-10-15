using System.Text.Json;
using System.Text.RegularExpressions;

using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace FellowshipExtractor;

public sealed partial class ReadFailures
{
    private readonly List<(string Level, string Message, string? Exception)> _events = [];

    private ReadFailures()
    {
    }

    public static ReadFailures Collect()
    {
        var collected = new ReadFailures();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Warning()
            .WriteTo.Sink(new Sink(collected))
            .CreateLogger();

        return collected;
    }

    public int Count => _events.Count;

    public void Write(string path)
    {
        var byType = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var byPackage = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var byAssetName = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (_, message, exception) in _events)
        {
            var whole = exception is null ? message : $"{message}\n{exception}";

            foreach (Match match in MissingType().Matches(whole))
            {
                var type = match.Groups[1].Value;
                byType[type] = byType.GetValueOrDefault(type) + 1;
            }

            foreach (Match match in Truncated().Matches(whole))
            {
                byPackage[match.Groups[1].Value] = match.Groups[2].Value;
                byAssetName[Path.GetFileNameWithoutExtension(match.Groups[1].Value)] = match.Groups[2].Value;
            }
        }

        using var stream = File.Create(path);
        using var writer = new Utf8JsonWriter(stream, UeJson.WriterOptions);

        writer.WriteStartObject();
        writer.WriteNumber("eventCount", _events.Count);
        writer.WriteNumber("packagesTruncated", byPackage.Count);

        writer.WritePropertyName("truncatedByAssetName");
        writer.WriteStartObject();
        foreach (var (asset, percent) in byAssetName) writer.WriteString(asset, percent);
        writer.WriteEndObject();

        writer.WritePropertyName("missingSchemaCounts");
        writer.WriteStartObject();
        foreach (var (type, many) in byType.OrderByDescending(entry => entry.Value))
        {
            writer.WriteNumber(type, many);
        }
        writer.WriteEndObject();

        writer.WritePropertyName("packagesTruncatedAt");
        writer.WriteStartObject();
        foreach (var (package, percent) in byPackage) writer.WriteString(package, percent);
        writer.WriteEndObject();

        writer.WritePropertyName("events");
        writer.WriteStartArray();
        foreach (var (level, message, exception) in _events)
        {
            writer.WriteStartObject();
            writer.WriteString("level", level);
            writer.WriteString("message", message);
            writer.WriteString("exception", exception);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WriteEndObject();
        writer.Flush();
    }

    [GeneratedRegex("Missing prop mappings for type \"?([A-Za-z0-9_]+)\"?")]
    private static partial Regex MissingType();

    [GeneratedRegex(@"FAssetArchive Info: (\S+) \| Pos:\d+ Length:\d+ \(([\d.]+% done)\)")]
    private static partial Regex Truncated();

    private sealed class Sink(ReadFailures collected) : ILogEventSink
    {
        public void Emit(LogEvent logEvent)
        {
            collected._events.Add(
                (logEvent.Level.ToString(), logEvent.RenderMessage(), logEvent.Exception?.Message));

            Console.Error.WriteLine($"[{logEvent.Level}] {logEvent.RenderMessage()}");
            if (logEvent.Exception is { } broken) Console.Error.WriteLine(broken.Message);
        }
    }
}
