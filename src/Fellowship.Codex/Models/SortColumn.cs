using Fellowship.Codex.Services;

namespace Fellowship.Codex.Models;

public readonly record struct SortColumn
{
    private SortColumn(SortingType? sorting, string? measure)
    {
        Sorting = sorting;
        Measure = measure;
    }

    public SortingType? Sorting { get; }

    /// <summary>Key of the sorting measure.</summary>
    public string? Measure { get; }

    public string Key => Sorting is { } sorting ? QueryKeys.Key(sorting) : Measure!;

    public static implicit operator SortColumn(SortingType sorting) => new(sorting, null);

    public static implicit operator SortColumn(MeasureType measure) => new(null, QueryKeys.Key(measure));

    public static SortColumn Of(Measure measure) => new(null, measure.Key);

    public static bool TryParse(string? key, ICodexService codex, out SortColumn sort)
    {
        sort = default;

        if (QueryKeys.TryParse<SortingType>(key, out var sorting)) sort = sorting;
        else if (codex.Measures.FirstOrDefault(measure => measure.Key.Equals(key, StringComparison.OrdinalIgnoreCase)) is { } measure) sort = Of(measure);
        else return false;

        return true;
    }
}
