using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Fellowship.SDK.Documents;

public abstract class EntityDocument
{
    public required int Id { get; init; }

    public string? Name { get; init; }

    public bool? EnabledInGame { get; init; }

    /// <summary>Reached only from an item trait whose category places it on no item.</summary>
    public bool? NoKnownSource { get; init; }

    public IReadOnlyDictionary<string, Models.TuningReference>? Tuning { get; init; }

    /// <summary>Rows each scalar field is set from, keyed by the field's name in the document.</summary>
    public IReadOnlyDictionary<string, Models.FieldSource>? FieldSources { get; init; }
}

public static class EntityDocuments
{
    public const string Discriminator = "$type";

    private const string TrimWarning =
        "A document is read into a type named at run time, so trimming can take that type away.";

    private static JsonSerializerOptions? options;

    public static JsonNamingPolicy NamingPolicy => JsonNamingPolicy.CamelCase;

    public static JsonSerializerOptions Options
    {
        [RequiresUnreferencedCode(TrimWarning)]
        get => options ??= new()
        {
            WriteIndented = false,
            PropertyNamingPolicy = NamingPolicy,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            AllowOutOfOrderMetadataProperties = true,
            Converters = { new JsonStringEnumConverter() },
            TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { Polymorphism } },
        };
    }

    [RequiresUnreferencedCode(TrimWarning)]
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
