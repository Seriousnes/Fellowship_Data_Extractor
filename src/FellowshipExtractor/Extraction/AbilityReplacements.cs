using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;

namespace FellowshipExtractor.Extraction;

public static class AbilityReplacements
{
    private const string Replace = "ReplaceAbility";
    private const string ClassSuffix = "_C";
    public const char Separator = '|';

    public static IReadOnlyList<string> Of(UObject? cdo)
    {
        if (cdo?.Owner is null) return [];

        var pairs = new List<(string Original, string Replacement)>();

        foreach (var export in cdo.Owner.GetExports())
        {
            if (export is not UStruct { ScriptBytecode: { Length: > 0 } bytecode }) continue;

            foreach (var statement in bytecode)
            {
                if (Call(statement) is not { } parameters || parameters.Length < 2) continue;
                if (Asset(cdo, parameters[0]) is not { } original || Asset(cdo, parameters[1]) is not { } replacement) continue;
                if (pairs.Contains((original, replacement)) || pairs.Contains((replacement, original))) continue;

                pairs.Add((original, replacement));
            }
        }

        return [.. pairs.Select(pair => $"{pair.Original}{Separator}{pair.Replacement}")];
    }

    private static KismetExpression[]? Call(KismetExpression statement) => statement switch
    {
        EX_Context { ContextExpression: { } inner } => Call(inner),
        EX_FinalFunction call when call.StackNode.Name.Equals(Replace, StringComparison.Ordinal) => call.Parameters,
        EX_VirtualFunction call when call.VirtualFunctionName.Text.Equals(Replace, StringComparison.Ordinal) => call.Parameters,
        _ => null,
    };

    private static string? Asset(UObject cdo, KismetExpression parameter)
    {
        if (DynamicTextFloats.Name(parameter) is not { } property) return null;
        if (PropertyWalk.GetString(cdo, property) is not { Length: > 0 } path) return null;

        var name = path[(path.LastIndexOf('.') + 1)..];

        return name.EndsWith(ClassSuffix, StringComparison.Ordinal) ? name[..^ClassSuffix.Length] : name;
    }
}
