namespace Fellowship.SDK.Models;

public sealed record CountRead(IReadOnlyList<string> Tags, int Skipped)
{
    public bool Stacks { get; init; }

    public string? Attribute { get; init; }

    public string? Call { get; init; }

    public string? Inverts { get; init; }
}
