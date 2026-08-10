using System.Globalization;

using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;

namespace FellowshipExtractor.Api.Services;

public interface IPlaceholderService
{
    Dictionary<string, Substitution> Resolve(
        IReadOnlyDictionary<string, Placeholder> placeholders,
        TooltipQuery query,
        HeroEntry? hero,
        IReadOnlyList<string>? tags = null);

    Substitution Reading(
        Placeholder placeholder, TooltipQuery query, HeroEntry? hero, IReadOnlyList<string>? tags = null);
}
public sealed class PlaceholderService(IEntityStoreService store, IRichTextService renderer) : IPlaceholderService
{
    private const string Relative = "PrimaryAttribute";

    public Dictionary<string, Substitution> Resolve(
        IReadOnlyDictionary<string, Placeholder> placeholders,
        TooltipQuery query,
        HeroEntry? hero,
        IReadOnlyList<string>? tags = null)
    {
        var resolved = new Dictionary<string, Substitution>(StringComparer.Ordinal);
        var school = School(tags);

        var haste = query.Haste;

        foreach (var (name, placeholder) in placeholders)
        {
            resolved[name] = One(placeholder, query, hero, school, haste);
        }

        return resolved;
    }

    public Substitution Reading(
        Placeholder placeholder, TooltipQuery query, HeroEntry? hero, IReadOnlyList<string>? tags = null) =>
        One(placeholder, query, hero, School(tags), query.Haste);

    public static Substitution Unresolved(Placeholder placeholder) =>
        placeholder.FromOwnConstants == true
            ? new Substitution(
                "[worked out in game]",
                "worked out by native code from this entity's own tuning",
                Resolved: false)
            : Substitution.Missing;

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

    private Substitution One(
        Placeholder placeholder, TooltipQuery query, HeroEntry? hero, string? school, decimal? haste)
    {
        if (placeholder.Text is { } text) return new Substitution(text);

        if (placeholder.Coefficient is { } coefficient)
        {
            var attribute = Attribute(placeholder.Attribute, query, hero);
            var value = attribute is null ? null : query.Total(attribute);

            return value is { } stat
                ? Banded(placeholder, coefficient, stat, attribute!, school, query.Expertise)
                : Unscaled(placeholder, coefficient, attribute);
        }

        if (placeholder.Value is { } number) return Number(placeholder, number, haste);

        return Unresolved(placeholder);
    }

    private string? Attribute(string? attribute, TooltipQuery query, HeroEntry? hero)
    {
        if (attribute is null) return null;
        if (!attribute.Equals(Relative, StringComparison.OrdinalIgnoreCase)) return attribute;

        if (hero is not null) return hero.PrimaryStat;

        var named = Primary.Where(query.Stats.ContainsKey).ToArray();
        return named.Length == 1 ? named[0] : null;
    }

    private static readonly string[] Primary = ["Strength", "Intellect", "Agility"];

    private Substitution Banded(
        Placeholder placeholder,
        double coefficient,
        decimal stat,
        string attribute,
        string? school,
        decimal? expertise)
    {
        var (mean, sharpening) = Sharpened(placeholder, (decimal)coefficient * stat, expertise);

        var title = $"{Percent(coefficient)}% of {attribute}{sharpening}";

        if (placeholder.Spread is not { } spread || spread == 0)
        {
            return new Substitution(Magnitude(mean), title, Format(placeholder, school, Magnitude(mean)));
        }

        var half = (decimal)spread / 2m;
        var low = Magnitude(mean * (1m - half));
        var high = Magnitude(mean * (1m + half));

        return new Substitution(
            $"{low} - {high}",
            $"{title}, rolling in a band {Percent(spread)}% wide",
            Format(placeholder, school, low, high));
    }

    private Substitution Unscaled(Placeholder placeholder, double coefficient, string? attribute)
    {
        var share = Percent(coefficient) + "%";
        var text = attribute is null ? share : $"{share} of {attribute}";

        return new Substitution(text, placeholder.Attribute is Relative
            ? "scales off whichever primary stat the hero uses"
            : null);
    }

    private Substitution Number(Placeholder placeholder, double value, decimal? haste)
    {
        if (placeholder.Format == "delta")
        {
            var excess = (decimal)value - 1m;
            var share = Share(excess);
            var percent = Print("UnitPercent", share) ?? share + "%";

            return new Substitution(excess < 0 ? percent : "+" + percent);
        }

        if (placeholder.Percent == true || placeholder.Format == "percent")
        {
            var share = placeholder.Multiplies == true ? (decimal)value - 1m : (decimal)value;

            return new Substitution(Print("UnitPercent", Share(share)) ?? Round(share) + "%");
        }

        var (quickened, title) = Quickened(placeholder, (decimal)value, haste);

        var number = title is null ? Round(quickened) : Round(quickened, 2);

        if (placeholder.Unit is null) return new Substitution(number, title);

        var name = quickened == 1m ? "UnitSecondSingular" : "UnitSeconds";
        var printed = Print(name, number);

        return new Substitution(
            Strip(printed) ?? $"{number} seconds",
            title,
            printed is null ? null : renderer.Render(printed));
    }

    private static (decimal Value, string Title) Sharpened(
        Placeholder placeholder, decimal mean, decimal? expertise)
    {
        if (placeholder.ScalesWithExpertise is not true) return (mean, string.Empty);
        if (expertise is not { } fraction || fraction <= 0) return (mean, string.Empty);

        return (mean * (1m + fraction), $", raised by {Share(fraction)}% Expertise");
    }

    private static (decimal Value, string? Title) Quickened(
        Placeholder placeholder, decimal value, decimal? haste)
    {
        if (placeholder.Tickrate is not true || placeholder.IgnoresHaste is true) return (value, null);
        if (haste is not { } fraction || fraction <= 0) return (value, null);

        return (
            value / (1m + fraction),
            $"{Round(value)}s at no haste, divided by {Round(1m + fraction, 4)} for {Share(fraction)}% haste");
    }

    private static string Share(decimal fraction) =>
        (fraction * 100m).Normalize().ToString(CultureInfo.InvariantCulture);

    private string? Format(Placeholder placeholder, string? school, string low, string? high = null)
    {
        var spoken = placeholder.ShowDamageType == true && placeholder.Coefficient is not null
            ? school
            : null;

        var name = (high is not null ? "DamageRange" : "Damage") + spoken;

        if (!store.Settings.PrintFormats.TryGetValue(name, out var format)) return null;

        var filled = format
            .Replace("{0}", low, StringComparison.Ordinal)
            .Replace("{1}", high ?? string.Empty, StringComparison.Ordinal);

        return renderer.Render(filled);
    }

    private string? Print(string format, string value) =>
        store.Settings.PrintFormats.TryGetValue(format, out var text)
            ? text.Replace("{0}", value, StringComparison.Ordinal)
            : null;

    private static string? Strip(string? text)
    {
        if (text is null) return null;

        while (text.IndexOf('<') is var open and >= 0 && text.IndexOf('>', open) is var close and > 0)
        {
            text = text.Remove(open, close - open + 1);
        }

        return text;
    }

    private static string Percent(double value) =>
        ((decimal)value * 100m).Normalize().ToString(CultureInfo.InvariantCulture);

    private static string Magnitude(decimal value) =>
        Math.Truncate(value).ToString("N0", CultureInfo.InvariantCulture);

    private static string Round(decimal value, int places = 1) =>
        Math.Round(value, places).Normalize().ToString(CultureInfo.InvariantCulture);
}
