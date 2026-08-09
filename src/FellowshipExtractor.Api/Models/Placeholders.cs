namespace FellowshipExtractor.Api.Models;

internal static class DecimalExtensions
{
    public static decimal Normalize(this decimal value) => value == 0m ? 0m : value / 1.000000000000000000000000000m;
}
