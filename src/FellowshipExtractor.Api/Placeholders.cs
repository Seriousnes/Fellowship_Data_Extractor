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
    /// </summary>
    public Dictionary<string, Substitution> Resolve(
        IReadOnlyDictionary<string, Placeholder> placeholders,
        TooltipQuery query,
        HeroEntry? hero,
        IReadOnlyList<string>? tags = null)
    {
        var resolved = new Dictionary<string, Substitution>(StringComparer.Ordinal);
        var school = School(tags);

        // A tickrate is haste's to change, and the ability's ScaleTimeWithHaste is not consulted for
        // it. The two are separate axes: that flag governs the ability's own cast and channel, while
        // an effect's period follows the tickrate default and the only thing that overrides it is
        // the effect opting out. Gating one on the other withheld haste from 22 of the 42 tickrates
        // in the build, most of them on items and augmentations, which carry no such flag to begin
        // with because they are not abilities.
        var haste = query.Total(Haste);

        foreach (var (name, placeholder) in placeholders)
        {
            if (One(placeholder, query, hero, school, haste) is { } substitution) resolved[name] = substitution;
        }

        return resolved;
    }

    /// <summary>
    /// The attribute a haste value arrives under. It is the wearer's Haste as the build's own
    /// constants express it, a fraction: Hero.Spirit.Buff.HasteIncrease is 0.3 and the ability
    /// granting it says "+30% Haste". A gear slot's haste is a rating rather than this, and the
    /// build states no conversion between the two, so a caller sends the attribute.
    /// </summary>
    private const string Haste = "Haste";

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

    private Substitution? One(
        Placeholder placeholder, TooltipQuery query, HeroEntry? hero, string? school, decimal? haste)
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

        if (placeholder.Value is { } number) return Number(placeholder, number, haste);

        return null;
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
    private Substitution Number(Placeholder placeholder, double value, decimal? haste)
    {
        if (placeholder.Percent == true || placeholder.Format == "percent")
        {
            return new Substitution(Print("UnitPercent", Percent(value)) ?? Round((decimal)value) + "%");
        }

        var (quickened, title) = Quickened(placeholder, (decimal)value, haste);

        // A quickened tickrate keeps a second place. One is enough for a damage number and loses
        // too much here: Incinerate's 0.5s interval at 30% haste is 0.3846, and rounding it to 0.4
        // is the difference between six ticks in the channel and five.
        var number = title is null ? Round(quickened) : Round(quickened, 2);

        if (placeholder.Unit is null) return new Substitution(number, title);

        // The unit is spoken only where the sentence leaves the word out, and the shipped format
        // that speaks it carries the game's own emphasis. Rendered rather than stripped, so
        // "every 3 seconds" keeps the weight the game gives it, with the plain reading kept beside
        // it for the title and for anywhere the markup is not wanted.
        var name = quickened == 1m ? "UnitSecondSingular" : "UnitSeconds";
        var printed = Print(name, number);

        return new Substitution(
            Strip(printed) ?? $"{number} seconds",
            title,
            printed is null ? null : renderer.Render(printed));
    }

    /// <summary>
    /// A period shortened by the caller's haste, with the sentence that explains it.
    ///
    /// Haste raises the rate an effect ticks at rather than shortening the window it ticks in: a
    /// channelled ability runs for the time it states and lands more ticks inside it. So this
    /// applies to a period and to nothing else - not to the channel, not to the cast, not to a
    /// duration, which are lengths rather than rates.
    ///
    /// The build declares which abilities haste reaches and never the arithmetic. The cooldown
    /// magnitude that reads Haste, GEC_CooldownReductionAndHasteMagnitude, computes in Blueprint
    /// bytecode this project does not recover. Treating haste as a rate, so an interval divides by
    /// 1 + haste, is this reader's reading and not the build's, which is why the title always states
    /// the number before it and the haste applied: a figure nobody can derive from the export is
    /// never presented as one that came out of it.
    /// </summary>
    private static (decimal Value, string? Title) Quickened(
        Placeholder placeholder, decimal value, decimal? haste)
    {
        // An effect that declares its tickrate out of haste keeps its period whatever the wearer
        // holds, which is the one case the build states outright rather than by default.
        if (placeholder.Tickrate is not true || placeholder.IgnoresHaste is true) return (value, null);
        if (haste is not { } fraction || fraction <= 0) return (value, null);

        return (
            value / (1m + fraction),
            $"{Round(value)}s at no haste, divided by {Round(1m + fraction)} for {Share(fraction)}% haste");
    }

    /// <summary>A fraction as the percentage a reader recognises, kept in decimal throughout.</summary>
    private static string Share(decimal fraction) =>
        (fraction * 100m).Normalize().ToString(CultureInfo.InvariantCulture);

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

    /// <summary>Whole numbers print whole, and the rest keep one place unless asked for more.</summary>
    private static string Round(decimal value, int places = 1) =>
        Math.Round(value, places).Normalize().ToString(CultureInfo.InvariantCulture);
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
