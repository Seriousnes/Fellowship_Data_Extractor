using System.Text.Json.Serialization;

namespace Fellowship.SDK.Models;

public sealed record EntityReference(
    [property: JsonConverter(typeof(EntityTypeSlugConverter))] EntityType Type,
    int Id,
    string? Name);
