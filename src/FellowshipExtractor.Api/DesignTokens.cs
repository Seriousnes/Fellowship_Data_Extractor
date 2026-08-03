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
public sealed class DesignTokens(VocabularyFile vocabulary)
{
    private readonly IReadOnlyDictionary<string, string> colors = vocabulary.Colors;

    /// <summary>
    /// The colour keys that name their subject by a spelling the game does not print: one per hero
    /// whose codename is not their name, and two per rung whose filed word is not the word it prints,
    /// since the ladder is keyed under both prefixes. Counted off the roster and the ladder through
    /// the same key spellings the accessors look up by, so a hero renamed in the build leaves the
    /// stylesheet without this file being touched and a key can never be filtered under one spelling
    /// and read under another.
    /// </summary>
    private readonly IReadOnlySet<string> unshown = Unshown(vocabulary);

    /// <summary>The named colours, keyed as the shipped table keys them: core.text-primary.</summary>
    public IReadOnlyDictionary<string, string> Colors => colors;

    public IReadOnlyDictionary<string, TextStyle> TextStyles => vocabulary.TextStyles;

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
        Color(RarityKey(internalName)) ?? Color(LadderKey(internalName));

    public string? Hero(string codename) => Color(HeroKey(codename));

    public string? Gem(string type) => Color($"game.game.gem-{type.ToLowerInvariant()}");

    public string? Role(string role) => Color($"game.role-{role.ToLowerInvariant()}");

    /// <summary>
    /// The custom properties a payload's style block opens with: every named colour a reader may
    /// read the name of, under a CSS custom property of that name, so the stylesheet reads in the
    /// build's own vocabulary.
    ///
    /// Ten of the shipped rows name their subject by a spelling the game never prints. A hero's
    /// colour is filed under the codename their asset files carry, so Elarion's is
    /// game.game.hero-bowguy and eight of the twelve read that way, and the two top rungs are filed
    /// under words naming a different rung than they print: rarity-champion is the gold of the rung
    /// that prints Heroic. This block is served on every page and inside every tooltip fragment, so
    /// those keys published the roster's internal spellings on every surface at once.
    ///
    /// They are left out rather than renamed. A property name is a name, and one invented here would
    /// no longer trace to the row it came from, which is the whole worth of writing them in the
    /// build's vocabulary; renaming can also land two rows on one property and let one win in
    /// silence. Nothing is lost by dropping them: a hero, rarity or gem colour reaches a payload as
    /// a hex through the accessors above, which read the shipped dictionary and are untouched by
    /// what this writes.
    /// </summary>
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

    /// <summary>
    /// A colour name as a CSS custom property name. The shipped names carry dots and one group
    /// repeats its own prefix (game.game.gem-topaz), so the dots become hyphens and the repetition is
    /// left alone rather than tidied: the name has to stay traceable to its row.
    /// </summary>
    public static string Variable(string name) => name.Replace('.', '-');
}
