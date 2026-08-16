using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports;

namespace FellowshipExtractor.Extraction;

public sealed class AbilityCategories
{
    public const string Field = "AbilityCategory";

    private const string Scope = "::";

    private readonly HashSet<string> _members;

    public string EnumName { get; }

    public IReadOnlyList<AbilityCategoryEntry> Members { get; }

    public AbilityCategories(AbstractFileProvider provider)
    {
        var mappings = provider.MappingsContainer?.MappingsForGame
            ?? throw new InvalidOperationException(
                $"No mappings are loaded, so nothing states which enum {Field} is written in.");

        EnumName = Declaring(mappings);

        var byOrdinal = EnumOrdinals.Members(mappings, EnumName);

        Members =
        [
            .. byOrdinal.OrderBy(member => member.Key)
                .Select(member => new AbilityCategoryEntry(member.Value, (int)member.Key)),
        ];

        _members = [.. Members.Select(member => member.Name)];
    }

    private static string Declaring(TypeMappings mappings)
    {
        var named = new HashSet<string>(StringComparer.Ordinal);

        foreach (var schema in mappings.Types.Values)
        {
            for (var at = 0; at < schema.PropertyCount; at++)
            {
                if (!schema.TryGetValue(at, out var property) || property is null) continue;
                if (!property.Name.Equals(Field, StringComparison.Ordinal)) continue;
                if (property.MappingType.EnumName is { Length: > 0 } enumName) named.Add(enumName);
            }
        }

        if (named.Count == 1) return named.First();

        throw new InvalidOperationException(
            named.Count == 0
                ? $"No schema names the enum behind {Field}, so an ability's category reads back as a bare " +
                  $"ordinal. Rewrite mappings/{ExtractorOptions.MappingsName} with " +
                  "'dotnet run --project src/FellowshipExtractor -- usmap', which is what carries the enum table."
                : $"{named.Count} enums are written in a property named {Field} ({string.Join(", ", named)}), so " +
                  "which one an ability states its category in is no longer decided by the mappings.");
    }

    public string? Of(IPropertyHolder? cdo)
    {
        if (cdo is null) return null;
        if (PropertyWalk.GetString(cdo, Field) is not { } written) return null;

        var scoped = written.IndexOf(Scope, StringComparison.Ordinal);
        if (scoped < 0 || !written[..scoped].Equals(EnumName, StringComparison.Ordinal)) return null;

        var member = written[(scoped + Scope.Length)..];
        return _members.Contains(member) ? member : null;
    }
}
