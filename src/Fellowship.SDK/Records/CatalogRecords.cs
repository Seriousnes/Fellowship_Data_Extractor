using System.Text.Json.Serialization;

namespace Fellowship.SDK.Records;

public sealed record Category(string Slug, string Label);

public sealed record FacetValue(string Value, string Label, int Count, string? Colour);

public sealed record FacetView(string Key, string Label, IReadOnlyList<FacetValue> Values);

public sealed record MenuSection(string? Head, IReadOnlyList<MenuLink> Links);

[method: JsonConstructor]
public sealed record MenuLink(string Label, string Href, string? Colour, IReadOnlyList<MenuSection> Sections)
{
    public MenuLink(string Label, string Href)
        : this(Label, Href, null, [])
    {
    }

    public MenuLink(string Label, string Href, params MenuSection[] Sections)
        : this(Label, Href, null, Sections)
    {
    }
}

public sealed record CatalogResponse(
    int Entities,
    IReadOnlyList<Category> Categories,
    IReadOnlyList<CatalogType> Types,
    IReadOnlyList<MenuLink> Menu,
    IReadOnlyList<FacetView> Facets,
    IReadOnlyList<CatalogFlag> Flags,
    IReadOnlyList<CatalogMeasure> Measures,
    IReadOnlyList<CatalogSorting> Sortings,
    IReadOnlyList<CatalogHero> Heroes,
    IReadOnlyList<CatalogRarity> Rarities);

public sealed record CatalogType(string Slug, string Label, string Category, int Count, int Position);

public sealed record CatalogFlag(string Key, string Label);

public sealed record CatalogMeasure(string Key, string Label, string? Unit);

public sealed record CatalogSorting(string Key, string Label);

public sealed record CatalogHero(string Name, string? Color);

public sealed record CatalogRarity(int Tier, string DisplayName, string GemDisplayName);
