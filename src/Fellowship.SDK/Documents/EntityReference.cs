using System.Text.Json.Serialization;

namespace Fellowship.SDK.Documents;

/// <summary>
/// Another row, addressed the way a reader would ask for it again. The type is written as its slug,
/// which is what a line carries as its own type, so the two join.
/// </summary>
public sealed record EntityReference(
    [property: JsonConverter(typeof(EntityTypeSlugConverter))] EntityType Type,
    int Id,
    string? Name);
