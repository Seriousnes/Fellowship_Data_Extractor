using System.Text.Json.Nodes;

using Fellowship.SDK.Documents;

namespace Fellowship.Codex.Services;

public static class DocumentVariants
{
    private static readonly string[] Fields =
    [
        JsonName(nameof(ItemDocument.ArmorType)),
        JsonName(nameof(ItemDocument.PrimaryStat)),
        JsonName(nameof(ItemDocument.Hero)),
        JsonName(nameof(ItemDocument.Heroes)),
    ];

    public static bool Same(JsonNode? one, JsonNode? other) =>
        Fields.All(field => JsonNode.DeepEquals(Field(one, field), Field(other, field)));

    private static JsonNode? Field(JsonNode? node, string name) =>
        node is JsonObject fields && fields.TryGetPropertyValue(name, out var value) ? value : null;

    private static string JsonName(string property) =>
        EntityDocuments.Options.PropertyNamingPolicy!.ConvertName(property);
}
