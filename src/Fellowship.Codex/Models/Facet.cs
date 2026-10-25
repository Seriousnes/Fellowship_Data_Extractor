using Fellowship.SDK.Documents;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Models;

public sealed record Facet(FacetType Type, Func<EntityDocument, IEnumerable<string>> Values)
{
    public string Key => QueryKeys.Key(Type);

    public string Label => CodexKeys.Label(Type);

    public Func<IDesignTokenService, string, string?>? Colour { get; init; }

    public bool Primary { get; init; }

    public bool Matches(EntityDocument document, IReadOnlyCollection<string> wanted) =>
        Values(document).Any(value => wanted.Contains(value, StringComparer.OrdinalIgnoreCase));
}
