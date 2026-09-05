using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fellowship.SDK.Client;

public static class CodexJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };
}
