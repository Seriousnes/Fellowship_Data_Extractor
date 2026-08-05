using System.Text.Json;
using System.Text.Json.Serialization;

namespace FellowshipExtractor.Tooltip;

/// <summary>Reads a vocabulary sidecar back. The extractor writes one and cannot otherwise read one.</summary>
public static class VocabularyReader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static VocabularyFile Read(string path) =>
        JsonSerializer.Deserialize<VocabularyFile>(File.ReadAllText(path), Options)
        ?? throw new InvalidOperationException($"{path} deserialised to nothing.");
}
