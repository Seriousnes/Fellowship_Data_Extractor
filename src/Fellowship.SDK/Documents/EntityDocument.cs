using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fellowship.SDK.Documents;

public abstract class EntityDocument
{
    public required int Id { get; init; }

    public string? Name { get; init; }
}

public sealed class EntityDocumentConverter : JsonConverter<EntityDocument>
{
    public override EntityDocument Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        var slug = TypeProperty(root, options).GetString()
            ?? throw new JsonException("A document's 'type' property is not a string.");

        var documentType = EntityTypes.TryParse(slug, out var type)
            ? EntityTypes.Document(type)
            : throw new JsonException($"'{slug}' names no {nameof(EntityType)}.");

        return (EntityDocument?)root.Deserialize(documentType, options)
            ?? throw new JsonException($"A {documentType.Name} line deserialised to nothing.");
    }

    public override void Write(Utf8JsonWriter writer, EntityDocument value, JsonSerializerOptions options)
    {
        var type = EntityTypes.TypeOf(value);
        var body = JsonSerializer.SerializeToNode(value, value.GetType(), options)!.AsObject();

        body.Insert(0, "type", EntityTypes.Slug(type));

        body.WriteTo(writer, options);
    }

    private static JsonElement TypeProperty(JsonElement root, JsonSerializerOptions options)
    {
        if (root.TryGetProperty("type", out var exact)) return exact;

        if (options.PropertyNameCaseInsensitive)
        {
            foreach (var property in root.EnumerateObject())
            {
                if (string.Equals(property.Name, "type", StringComparison.OrdinalIgnoreCase)) return property.Value;
            }
        }

        throw new JsonException("A document line carries no 'type' property.");
    }
}
