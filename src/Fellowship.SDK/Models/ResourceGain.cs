using System.Text.Json.Serialization;

namespace Fellowship.SDK.Models;

public sealed record ResourceGain(string Resource)
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

    [JsonIgnore]
    public string? ClaimedBy { get; init; }

    [JsonIgnore]
    public bool Unread { get; init; }
}
