using System.Text;

namespace FellowshipExtractor.Api;

/// <summary>
/// The game's own colours and text styles, as the extractor read them out of the design system and
/// wrote them beside the entities.
///
/// Nothing here is chosen. The game ships a design system of named colours and a rich text style
/// set, and a tooltip that picks its own gold or its own rarity red is a tooltip that stops
/// matching the game the moment an artist changes one. Every colour a payload draws with is looked
/// up by the name the build files it under.
/// </summary>
public sealed class DesignTokens(
    IReadOnlyDictionary<string, string> colors,
    IReadOnlyDictionary<string, TextStyle> textStyles)
{
    /// <summary>The named colours, keyed as the shipped table keys them: core.text-primary.</summary>
    public IReadOnlyDictionary<string, string> Colors => colors;

    public IReadOnlyDictionary<string, TextStyle> TextStyles => textStyles;

    /// <summary>
    /// One named colour as a CSS hex, or null when the build has no colour of that name. A caller
    /// that needs a colour to exist reads it through one of the named accessors below, which say
    /// which key they are asking for, so a renamed row fails where it can be read rather than
    /// silently drawing the wrong thing.
    /// </summary>
    public string? Color(string name) => colors.TryGetValue(name, out var value) ? '#' + value : null;

    /// <summary>
    /// The colour the game draws a rarity's name in, by the rarity's internal name rather than the
    /// word it prints. The two diverge at the top of the ladder: ordinal 4 is filed as Champion and
    /// prints as "Heroic", and its colour is the gold under rarity-champion, while the red under
    /// rarity-heroic belongs to ordinal 5, which prints as "Regal".
    /// </summary>
    public string? Rarity(string internalName) =>
        Color($"game.game.rarity-{internalName.ToLowerInvariant()}")
        ?? Color($"game.rarity-{internalName.ToLowerInvariant()}");

    public string? Hero(string codename) => Color($"game.game.hero-{codename.ToLowerInvariant()}");

    public string? Gem(string type) => Color($"game.game.gem-{type.ToLowerInvariant()}");

    public string? Role(string role) => Color($"game.role-{role.ToLowerInvariant()}");

    /// <summary>
    /// The custom properties a payload's style block opens with: every named colour under a CSS
    /// custom property of the same name, so the stylesheet reads in the build's own vocabulary.
    /// </summary>
    public string CssVariables()
    {
        var css = new StringBuilder();

        foreach (var (name, value) in colors.OrderBy(c => c.Key, StringComparer.Ordinal))
        {
            css.Append("--").Append(Variable(name)).Append(":#").Append(value).Append(';');
        }

        return css.ToString();
    }

    /// <summary>
    /// A colour name as a CSS custom property name. The shipped names carry dots and one group
    /// repeats its own prefix (game.game.hero-firemage), so the dots become hyphens and the
    /// repetition is left alone rather than tidied: the name has to stay traceable to its row.
    /// </summary>
    public static string Variable(string name) => name.Replace('.', '-');
}
