using Fellowship.SDK.Documents;

namespace Fellowship.Codex.Models;

public sealed record Measure(MeasureType Type, Func<EntityDocument, double?> Value)
{
    public string Key => CodexKeys.Key(Type);

    public string Label => CodexKeys.Label(Type);

    public string? Unit { get; init; }
}
