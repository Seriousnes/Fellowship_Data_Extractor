using CUE4Parse.MappingsProvider;

namespace FellowshipExtractor;

public static class EnumOrdinals
{
    private const string Scope = "::";

    private static IReadOnlyDictionary<string, long> _byMember =
        new Dictionary<string, long>(StringComparer.Ordinal);

    public static int Count => _byMember.Count;

    public static void Read(TypeMappings? mappings)
    {
        if (mappings is null) return;

        var byMember = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var (name, members) in mappings.Enums)
        {
            foreach (var (ordinal, member) in members) byMember[$"{name}{Scope}{member}"] = ordinal;
        }

        _byMember = byMember;
    }

    public static long? For(string value)
    {
        if (_byMember.TryGetValue(value, out var ordinal)) return ordinal;

        var scoped = value.IndexOf(Scope, StringComparison.Ordinal);
        if (scoped < 0) return null;

        return long.TryParse(value[(scoped + Scope.Length)..], out var unnamed) ? unnamed : null;
    }

    public static IReadOnlyDictionary<long, string> Members(TypeMappings mappings, string name) =>
        mappings.Enums.TryGetValue(name, out var members)
            ? members
            : throw new InvalidOperationException(
                $"The mappings carry no enum named {name}, so its members cannot be read back. Rewrite " +
                $"output/{ExtractorOptions.MappingsName} with " +
                "'dotnet run --project src/FellowshipExtractor -- usmap'.");
}
