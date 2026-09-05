namespace Fellowship.Codex.Models;

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
