using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports;

namespace FellowshipExtractor.Tooltip;

public sealed record ChargeCounts(double? Charges, IReadOnlyDictionary<string, double>? ByLevelType)
{
    public static readonly ChargeCounts None = new(null, null);
}

public static class MaximumCharges
{
    private const string Function = "GetMaximumCharges";

    public static ChargeCounts Of(UObject cdo, ConstantsTable constants, TypeMappings mappings)
    {
        if (BareStateWalk.Compiled(cdo, Function) is not { ScriptBytecode.Length: > 0 } body)
        {
            return ChargeCounts.None;
        }

        var walk = new BareStateWalk(body, cdo, constants, mappings) { Unreadable = Unreadable(cdo) };

        return new ChargeCounts(walk.Answer(), walk.ByLevelType);
    }

    private static Func<string, InvalidOperationException> Unreadable(UObject cdo) =>
        what => new InvalidOperationException(
            $"{cdo.Owner?.Name ?? cdo.Name}.{Function} {what}. The charge count a tooltip prints is what that " +
            "function returns for a character holding no talents, and it cannot be guessed from a row name.");
}
