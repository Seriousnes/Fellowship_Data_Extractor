namespace Fellowship.Codex.Models;

public sealed record FacetCount(Facet Facet, IReadOnlyList<FacetValue> Values)
{
    public FacetType Type => Facet.Type;

    public string Key => Facet.Key;

    public string Label => Facet.Label;
}
