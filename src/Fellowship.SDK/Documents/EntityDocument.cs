using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fellowship.SDK.Documents;

/// <summary>
/// One row as a reader meets it. The runtime class is the row's type, so nothing here restates it.
///
/// Two fields, because two are what every type has. A row the extractor cannot read yet carries the
/// id and nothing else, which is what makes the extraction worklist countable.
/// </summary>
public abstract class EntityDocument
{
    public required int Id { get; init; }

    /// <summary>The name a reader is shown, and null where the build prints none they would read.</summary>
    public string? Name { get; init; }
}

/// <summary>
/// The line a reader meets, joined to the class its own <c>type</c> field names. Not attached to
/// <see cref="EntityDocument"/> by attribute: that attribute is inherited by every document class, so
/// the write this converter does against a document's own runtime type - Ability, Item and the rest -
/// would resolve to this same converter and re-enter itself. A caller adds it to
/// <see cref="JsonSerializerOptions.Converters"/> instead, which only <see cref="EntityDocument"/>
/// itself matches; a concrete document serialises through the ordinary reflection-based converter, the
/// same one <c>JsonlOutputFormat</c> already writes with.
/// </summary>
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
        // Every document class is named exactly its EntityType member, so the type this line carries
        // is read straight off the runtime type rather than through a second table.
        var type = Enum.Parse<EntityType>(value.GetType().Name);
        var body = JsonSerializer.SerializeToNode(value, value.GetType(), options)!.AsObject();

        // The type leads the line, matching the shape JsonlOutputFormat writes, so a line the
        // extractor writes and a line the SDK writes are the same line.
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
