using System.Globalization;
using FellowshipExtractor.Tooltip;

namespace FellowshipExtractor.Api;

/// <summary>
/// Turns a description's {Token} placeholders into the text they print for one caller.
///
/// The export keeps a token in the form that is true for every caller: a coefficient, the attribute
/// it multiplies, and the width of the band the roll lands in. Whether that can become a number
/// depends on what the caller knows, so it is resolved here rather than there.
///
/// The attribute resolves in two stages. An item's primary stat slot is filled by whoever wears it,
/// so 361 tokens name PrimaryAttribute rather than a stat. Naming a hero, or naming a stat outright,
/// settles which of Strength, Intellect and Agility that is. Supplying a value for it then turns the
/// coefficient into a number. A token already naming a concrete attribute skips the first stage.
///
/// PrimaryAttribute is a placeholder and "Main Stat" is a phrase a designer typed. They do not
/// overlap: 13 descriptions contain the literal words and 76 records carry the placeholder, and no
/// record does both. Nothing here substitutes one for the other.
/// </summary>
public sealed class Placeholders(EntityStore store, RichTextRenderer renderer)
{
    /// <summary>The attribute name that stands in for whichever stat the wearer's hero uses.</summary>
    private const string Relative = "PrimaryAttribute";

    /// <summary>
    /// Every token of a description, resolved as far as the caller's parameters allow. A token that
    /// cannot become a number becomes the coefficient sentence instead, which is the honest reading
    /// and is what the export already says.
    ///
    /// Every token gets an answer, including the 47 the build settles nowhere. Leaving one out left
    /// the renderer nothing to write but the token's own key, so a sentence reading "smash the target
    /// for {HeavyStrikeDmg} physical damage" put the build's spelling of a number in the one place a
    /// tooltip is certain to be read. <see cref="Unresolved"/> is what stands there now.
    /// </summary>
    public Dictionary<string, Substitution> Resolve(
        IReadOnlyDictionary<string, Placeholder> placeholders,
        TooltipQuery query,
        HeroEntry? hero,
        IReadOnlyList<string>? tags = null)
    {
        var resolved = new Dictionary<string, Substitution>(StringComparer.Ordinal);
        var school = School(tags);

        foreach (var (name, placeholder) in placeholders)
        {
            resolved[name] = One(placeholder, query, hero, school);
        }

        return resolved;
    }

    /// <summary>
    /// One token on its own, for the callers holding a substitution rather than a description: a
    /// record listing what each number in its text is says the same reading beside the entry that
    /// the text itself spends.
    /// </summary>
    public Substitution Reading(
        Placeholder placeholder, TooltipQuery query, HeroEntry? hero, IReadOnlyList<string>? tags = null) =>
        One(placeholder, query, hero, School(tags));

    /// <summary>
    /// What a token the build settles nowhere reads as, in the two states the export leaves one in.
    ///
    /// 40 of the 47 say outright that the number comes from the entity's own tuning by way of native
    /// code, which the extractor reads bytecode rather than data to reach; the other 7 say nothing at
    /// all. The distinction is kept because it is the difference between a number the game has and a
    /// number the build does not, and the same two sentences are what the entry page's substitutions
    /// panel writes under the reading.
    /// </summary>
    public static Substitution Unresolved(Placeholder placeholder) =>
        placeholder.FromOwnConstants == true
            ? new Substitution(
                "[worked out in game]",
                "worked out by native code from this entity's own tuning",
                Resolved: false)
            : Substitution.Missing;

    /// <summary>
    /// Which damage school the owner's tags declare, or none where they declare neither. The
    /// shipped print formats come in a plain form and one per school, and the school is a fact
    /// about the ability rather than about the token: a token only says whether it wants the school
    /// spoken at all.
    ///
    /// Every one of the ten tokens in the build that asks is on a physical ability. Seven are tagged
    /// Physical or Physical.Bleed and Warbound is tagged with no school, which takes the plain form.
    /// Nothing here has ever selected a magic variant, so a default of magic would have been wrong
    /// in every shipped case.
    /// </summary>
    private static string? School(IReadOnlyList<string>? tags)
    {
        if (tags is null) return null;

        foreach (var tag in tags)
        {
            if (tag.StartsWith("Physical", StringComparison.Ordinal)) return "Physical";
            if (tag.StartsWith("Magic", StringComparison.Ordinal)) return "Magic";
        }

        return null;
    }

    private Substitution One(Placeholder placeholder, TooltipQuery query, HeroEntry? hero, string? school)
    {
        // A token standing in for a name has no arithmetic at all.
        if (placeholder.Text is { } text) return new Substitution(text);

        if (placeholder.Coefficient is { } coefficient)
        {
            var attribute = Attribute(placeholder.Attribute, query, hero);
            var value = attribute is null ? null : query.Total(attribute);

            return value is { } stat
                ? Banded(placeholder, coefficient, stat, attribute!, school)
                : Unscaled(placeholder, coefficient, attribute);
        }

        if (placeholder.Value is { } number) return Number(placeholder, number);

        return Unresolved(placeholder);
    }

    /// <summary>
    /// Which stat a token's coefficient multiplies. A token naming the relative attribute takes the
    /// hero's, or the one the caller named outright when they gave a stat rather than a hero.
    /// </summary>
    private string? Attribute(string? attribute, TooltipQuery query, HeroEntry? hero)
    {
        if (attribute is null) return null;
        if (!attribute.Equals(Relative, StringComparison.OrdinalIgnoreCase)) return attribute;

        if (hero is not null) return hero.PrimaryStat;

        // No hero, but a caller who sent exactly one of the three primary stats has named it.
        var named = Primary.Where(query.Stats.ContainsKey).ToArray();
        return named.Length == 1 ? named[0] : null;
    }

    private static readonly string[] Primary = ["Strength", "Intellect", "Agility"];

    /// <summary>
    /// The number, or the band around it. Spread is the total width rather than a plus or minus, so
    /// a spread of 0.2 is 90 to 110 percent of the mean and not 80 to 120. A spread the build does
    /// not state is not a band: the number is printed as it stands.
    /// </summary>
    private Substitution Banded(
        Placeholder placeholder, double coefficient, decimal stat, string attribute, string? school)
    {
        var mean = (decimal)coefficient * stat;
        var title = $"{Percent(coefficient)}% of {attribute}";

        if (placeholder.Spread is not { } spread || spread == 0)
        {
            return new Substitution(Round(mean), title, Format(placeholder, school, Round(mean)));
        }

        var half = (decimal)spread / 2m;
        var low = Round(mean * (1m - half));
        var high = Round(mean * (1m + half));

        return new Substitution(
            $"{low} - {high}",
            $"{title}, rolling in a band {Percent(spread)}% wide",
            Format(placeholder, school, low, high));
    }

    /// <summary>
    /// What a coefficient reads as with no stat behind it. This is the export's own form and is a
    /// real answer rather than a failure: "584.82% of Intellect" is what the game's own tooltip
    /// shows before a character is attached to it.
    /// </summary>
    private Substitution Unscaled(Placeholder placeholder, double coefficient, string? attribute)
    {
        var share = Percent(coefficient) + "%";
        var text = attribute is null ? share : $"{share} of {attribute}";

        return new Substitution(text, placeholder.Attribute is Relative
            ? "scales off whichever primary stat the hero uses"
            : null);
    }

    /// <summary>
    /// A token that prints a number outright. Seconds is the only unit the build ever asks for, and
    /// it asks through the shipped print formats rather than by spelling the word.
    /// </summary>
    private Substitution Number(Placeholder placeholder, double value)
    {
        var number = Round((decimal)value);

        if (placeholder.Percent == true || placeholder.Format == "percent")
        {
            return new Substitution(Print("UnitPercent", Percent(value)) ?? number + "%");
        }

        if (placeholder.Unit is null) return new Substitution(number);

        // The unit is spoken only where the sentence leaves the word out, and the shipped format
        // that speaks it carries the game's own emphasis. Rendered rather than stripped, so
        // "every 3 seconds" keeps the weight the game gives it, with the plain reading kept beside
        // it for the title and for anywhere the markup is not wanted.
        var name = value == 1 ? "UnitSecondSingular" : "UnitSeconds";
        var printed = Print(name, number);

        return new Substitution(
            Strip(printed) ?? $"{number} seconds",
            null,
            printed is null ? null : renderer.Render(printed));
    }

    /// <summary>
    /// The number wrapped in the sentence the game writes it in. The shipped formats carry the
    /// game's own markup, so the result is rendered rather than escaped, and a token that names no
    /// damage school takes the plain form so a sentence already saying "fire" does not read "fire
    /// damage magic damage".
    /// </summary>
    private string? Format(Placeholder placeholder, string? school, string low, string? high = null)
    {
        // A school variant applies only where the token asks for the school and there is a
        // coefficient behind it. Two of the ten that ask are a radius rather than damage.
        var spoken = placeholder.ShowDamageType == true && placeholder.Coefficient is not null
            ? school
            : null;

        var name = (high is not null ? "DamageRange" : "Damage") + spoken;

        if (!store.Vocabulary.PrintFormats.TryGetValue(name, out var format)) return null;

        // The numbers go in before the format is rendered rather than through the substitution
        // path, because the whole formatted run is already one substitution. Passing them as
        // substitutions of their own wraps each number in a second marker inside the first, which
        // draws the dotted underline twice and nests one help cursor in another.
        var filled = format
            .Replace("{0}", low, StringComparison.Ordinal)
            .Replace("{1}", high ?? string.Empty, StringComparison.Ordinal);

        // Rendered rather than pre-escaped: the renderer escapes the text between tags itself, and
        // encoding here as well would escape the escapes.
        return renderer.Render(filled);
    }

    private string? Print(string format, string value) =>
        store.Vocabulary.PrintFormats.TryGetValue(format, out var text)
            ? text.Replace("{0}", value, StringComparison.Ordinal)
            : null;

    /// <summary>The shipped formats carry markup that a plain-text reading has no use for.</summary>
    private static string? Strip(string? text)
    {
        if (text is null) return null;

        while (text.IndexOf('<') is var open and >= 0 && text.IndexOf('>', open) is var close and > 0)
        {
            text = text.Remove(open, close - open + 1);
        }

        return text;
    }

    /// <summary>
    /// A coefficient as a percentage. Read through decimal because 48 of the shipped coefficients
    /// are values a double cannot hold exactly, and multiplying those by 100 as a double prints
    /// digits the build never contained.
    /// </summary>
    private static string Percent(double value) =>
        ((decimal)value * 100m).Normalize().ToString(CultureInfo.InvariantCulture);

    /// <summary>Whole numbers print whole, and the rest keep one place.</summary>
    private static string Round(decimal value) =>
        Math.Round(value, 1).Normalize().ToString(CultureInfo.InvariantCulture);
}

internal static class DecimalExtensions
{
    /// <summary>
    /// Drops the trailing zeroes a decimal carries as scale, so 5.80 prints as 5.8 and 12.00 as 12.
    /// A decimal remembers how many places it was written to and prints them all, which turns a
    /// tidy coefficient into a number with a tail nobody typed.
    /// </summary>
    public static decimal Normalize(this decimal value) => value == 0m ? 0m : value / 1.000000000000000000000000000m;
}
