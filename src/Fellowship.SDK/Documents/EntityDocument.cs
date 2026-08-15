using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Fellowship.SDK.Documents;

public abstract class EntityDocument
{
    public required int Id { get; init; }

    public string? Name { get; init; }

    public string? Tag { get; init; }
}

public static class EntityDocuments
{
    public const string Discriminator = "$type";

    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        AllowOutOfOrderMetadataProperties = true,
        Converters = { new JsonStringEnumConverter() },
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { Polymorphism } },
    };

    private static void Polymorphism(JsonTypeInfo info)
    {
        if (info.Type != typeof(EntityDocument)) return;

        var polymorphism = new JsonPolymorphismOptions { TypeDiscriminatorPropertyName = Discriminator };

        foreach (var type in Enum.GetValues<EntityType>())
        {
            polymorphism.DerivedTypes.Add(new JsonDerivedType(EntityTypes.Document(type), EntityTypes.Slug(type)));
        }

        info.PolymorphismOptions = polymorphism;
    }
}
