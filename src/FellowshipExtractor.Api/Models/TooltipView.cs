using System.Globalization;

using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Api.Models;

public static class TooltipView
{
    public const string Root = "fx-tip";

    public const EntityType TraitType = EntityType.Trait;

    public const int SlotWidth = 48;

    public static string Magnitude(StatLine line) =>
        (line.Value < 0 ? string.Empty : "+") +
        line.Value.Normalize().ToString(CultureInfo.InvariantCulture);

    public sealed record Fact(string Label, string Value, string? Title);

    public sealed record TraitLine(string Name, int Rank);
}
