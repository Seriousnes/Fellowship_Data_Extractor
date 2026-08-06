using System.Text;

namespace FellowshipExtractor.Api;

public sealed class DesignTokens(VocabularyFile vocabulary)
{
    private readonly IReadOnlyDictionary<string, string> colors = vocabulary.Colors;

    private readonly IReadOnlySet<string> unshown = Unshown(vocabulary);

    public IReadOnlyDictionary<string, string> Colors => colors;

    public IReadOnlyDictionary<string, TextStyle> TextStyles => vocabulary.TextStyles;

    public string? Color(string name) => colors.TryGetValue(name, out var value) ? '#' + value : null;

    public string? Rarity(string internalName) =>
        Color(RarityKey(internalName)) ?? Color(LadderKey(internalName));

    public string? Hero(string codename) => Color(HeroKey(codename));

    public string? Gem(string type) => Color($"game.game.gem-{type.ToLowerInvariant()}");

    public string? Role(string role) => Color($"game.role-{role.ToLowerInvariant()}");

    public string CssVariables()
    {
        var css = new StringBuilder();

        foreach (var (name, value) in colors.OrderBy(c => c.Key, StringComparer.Ordinal))
        {
            if (unshown.Contains(name)) continue;

            css.Append("--").Append(Variable(name)).Append(":#").Append(value).Append(';');
        }

        return css.ToString();
    }

    private static IReadOnlySet<string> Unshown(VocabularyFile vocabulary)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var hero in vocabulary.Heroes)
        {
            if (!string.Equals(hero.Codename, hero.Name, StringComparison.OrdinalIgnoreCase))
            {
                keys.Add(HeroKey(hero.Codename));
            }
        }

        foreach (var rung in vocabulary.Rarities)
        {
            if (string.Equals(rung.Name, rung.DisplayName, StringComparison.OrdinalIgnoreCase)) continue;

            keys.Add(RarityKey(rung.Name));
            keys.Add(LadderKey(rung.Name));
        }

        return keys;
    }

    private static string HeroKey(string codename) => $"game.game.hero-{codename.ToLowerInvariant()}";

    private static string RarityKey(string internalName) =>
        $"game.game.rarity-{internalName.ToLowerInvariant()}";

    private static string LadderKey(string internalName) =>
        $"game.rarity-{internalName.ToLowerInvariant()}";

    public static string Variable(string name) => name.Replace('.', '-');
}
