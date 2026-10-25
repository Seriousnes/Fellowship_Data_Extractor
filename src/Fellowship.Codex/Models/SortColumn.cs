namespace Fellowship.Codex.Models;

public readonly record struct SortColumn
{
    private SortColumn(SortingType? sorting, MeasureType? measure)
    {
        Sorting = sorting;
        Measure = measure;
    }

    public SortingType? Sorting { get; }

    public MeasureType? Measure { get; }

    public string Key => Sorting is { } sorting ? CodexKeys.Key(sorting) : CodexKeys.Key(Measure!.Value);

    public static implicit operator SortColumn(SortingType sorting) => new(sorting, null);

    public static implicit operator SortColumn(MeasureType measure) => new(null, measure);

    public static bool TryParse(string? key, out SortColumn sort)
    {
        sort = default;

        if (CodexKeys.TryParse<SortingType>(key, out var sorting)) sort = sorting;
        else if (CodexKeys.TryParse<MeasureType>(key, out var measure)) sort = measure;
        else return false;

        return true;
    }
}
