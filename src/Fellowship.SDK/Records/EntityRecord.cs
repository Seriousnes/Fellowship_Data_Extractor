using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using Fellowship.SDK.Models;

namespace Fellowship.SDK.Records;

public abstract record EntityRecord
{
    [JsonConverter(typeof(EntityTypeSlugConverter))]
    public required EntityType Type { get; init; }

    public required int Id { get; init; }

    public required string Name { get; init; }
}

public static class EntityRecords
{
    private const string Suffix = "Record";

    private const string Named = "The record class is named at run time, so trimming can take it away.";

    private static readonly ConcurrentDictionary<EntityType, Type> ByMember = new();

    [RequiresUnreferencedCode(Named)]
    public static Type Record(EntityType type) => ByMember.GetOrAdd(type, Resolve);

    [RequiresUnreferencedCode(Named)]
    private static Type Resolve(EntityType type) =>
        typeof(EntityRecord).Assembly.GetType($"{typeof(EntityRecord).Namespace}.{type}{Suffix}")
        ?? throw new InvalidOperationException(
            $"{type} names no record class in {typeof(EntityRecord).Assembly.GetName().Name}.");

    public static EntityType TypeOf(Type record)
    {
        var name = record.Name;
        var member = name.EndsWith(Suffix, StringComparison.Ordinal) ? name[..^Suffix.Length] : name;

        return Enum.TryParse<EntityType>(member, out var type)
            ? type
            : throw new InvalidOperationException($"{name} names no {nameof(EntityType)}.");
    }

    public static EntityType TypeOf(EntityRecord record) => TypeOf(record.GetType());
}

public sealed record RecordReference(
    [property: JsonConverter(typeof(EntityTypeSlugConverter))] EntityType Type,
    int Id,
    string Name,
    string Resource);

public sealed record PlaceholderRecord
{
    public int Ordinal { get; init; }

    public required string Reading { get; init; }

    public string? Attribute { get; init; }
    public double? Coefficient { get; init; }
    public double? Spread { get; init; }
    public double? Value { get; init; }
    public string? Text { get; init; }
    public bool? Percent { get; init; }
    public string? Format { get; init; }
    public string? Unit { get; init; }
    public bool? ShowDamageType { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }
}

public sealed record ResourceGainRecord(string Resource)
{
    public string? ResourceName { get; init; }
    public double? Amount { get; init; }
    public double? AmountOnCritical { get; init; }
    public double? ShareOfMaximum { get; init; }
    public string? Per { get; init; }
    public double? Chance { get; init; }
    public string? RowGroup { get; init; }
    public string? Source { get; init; }
    public string? SourceOnCritical { get; init; }
    public string? SourceKind { get; init; }
}

public sealed record RankRunRecord(string Literal, IReadOnlyList<string> Printed)
{
    public IReadOnlyList<double?>? Values { get; init; }
    public string? Transform { get; init; }
    public int? Ticks { get; init; }
    public bool? Disagrees { get; init; }
}

public sealed record CreatureAbilityRecord(RecordReference Ability, string Phase, bool Shown);

public sealed record GemOverflowRecord(string Description)
{
    public IReadOnlyList<PlaceholderRecord>? Placeholders { get; init; }

    public string? Icon { get; init; }
}

public sealed record ItemSourceRecord(SourceKind Kind, string Name)
{
    public RecordReference? Dungeon { get; init; }
}

public sealed record StatSlotRecord(string Stat, int Weight, string Kind);
