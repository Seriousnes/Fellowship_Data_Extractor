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

    public static string? Member(TypeMappings mappings, string name, string? text)
    {
        if (text is null || text.Length == 0) return null;

        var members = Members(mappings, name);

        var scoped = text.IndexOf(Scope, StringComparison.Ordinal);
        var member = scoped < 0 ? text : text[(scoped + Scope.Length)..];

        if (members.Values.Contains(member, StringComparer.Ordinal)) return member;

        return long.TryParse(member, out var ordinal) && members.TryGetValue(ordinal, out var declared) ? declared : null;
    }

    public static IReadOnlyDictionary<long, string> Members(TypeMappings mappings, string name) =>
        mappings.Enums.TryGetValue(name, out var members)
            ? members
            : throw new InvalidOperationException(
                $"The mappings carry no enum named {name}, so its members cannot be read back. Rewrite " +
                $"mappings/{ExtractorOptions.MappingsPattern} with " +
                "'dotnet run --project src/FellowshipExtractor -- usmap'.");
}
