using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Kismet;

namespace FellowshipExtractor.Extraction;

public sealed class RoleIcons
{
    public const string Widget = "UW_PlayerRoleBadgeIcon";
    public const string Function = "SetRole";
    public const string RoleEnum = "EPlayerRole";

    private const string Absence =
        "Its select over the player roles is the only thing that says which badge a role draws.";

    public RoleIcons(AbstractFileProvider provider, TypeMappings mappings)
    {
        var roles = EnumOrdinals.Members(mappings, RoleEnum);
        var byRole = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (ordinal, path) in EnumSelect.Read(provider, Widget, Function, Texture, Absence))
        {
            if (!roles.TryGetValue(ordinal, out var member))
            {
                throw new InvalidOperationException(
                    $"{Widget}.{Function} answers {RoleEnum} {ordinal}, and the mappings declare no such member.");
            }

            byRole[Titled(member)] = path;
        }

        if (byRole.Count == 0) throw new InvalidOperationException($"{Widget}.{Function} selects nothing. {Absence}");

        ByRole = byRole;
    }

    public IReadOnlyDictionary<string, string> ByRole { get; }

    public static string Titled(string value)
    {
        var leaf = value[(value.LastIndexOf('_') + 1)..];
        return leaf.Length == 0 ? value : char.ToUpperInvariant(leaf[0]) + leaf[1..].ToLowerInvariant();
    }

    private static string? Texture(KismetExpression term) =>
        term is EX_ObjectConst drawn ? PropertyWalk.AsString(drawn.Value) : null;
}
