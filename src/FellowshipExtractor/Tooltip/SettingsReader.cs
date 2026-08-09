using System.Text.Json;
using System.Text.Json.Serialization;

namespace FellowshipExtractor.Tooltip;

public static class SettingsReader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static SettingsFile Read(string path) =>
        JsonSerializer.Deserialize<SettingsFile>(File.ReadAllText(path), Options)
        ?? throw new InvalidOperationException($"{path} deserialised to nothing.");
}
