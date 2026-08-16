using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Extraction;

public static class FormatArguments
{
    private const string ArgumentsField = "Arguments";
    private const string ValueField = "Value";
    private const string NumericTypeField = "NumericType";
    private const string CurveField = "Curve";
    private const string RowNameField = "RowName";

    public static IReadOnlyDictionary<string, Placeholder>? Read(IPropertyHolder text, ConstantsTable constants)
    {
        if (PropertyWalk.Get(text, ArgumentsField)?.GenericValue is not UScriptMap arguments) return null;

        var found = new Dictionary<string, Placeholder>(StringComparer.Ordinal);

        foreach (var pair in arguments.Properties)
        {
            if (PropertyWalk.AsString(pair.Key?.GenericValue) is not { } key) continue;
            if (pair.Value?.GenericValue is not FScriptStruct { StructType: IPropertyHolder argument }) continue;
            if (PropertyWalk.GetStruct(argument, ValueField) is not { } scalable) continue;

            found[key] = new Placeholder
            {
                Value = Number(scalable, constants),
                Format = Format(PropertyWalk.AsOrdinal(PropertyWalk.Get(argument, NumericTypeField)?.GenericValue)),
            };
        }

        return found.Count == 0 ? null : found;
    }

    public static IReadOnlyDictionary<string, Placeholder>? Read(
        IPropertyHolder owner, string textField, string nameField, ConstantsTable constants)
    {
        if (PropertyWalk.GetStruct(owner, textField) is not { } text) return null;

        var arguments = Read(text, constants);

        if (PropertyWalk.GetString(owner, nameField) is not { } name) return arguments;
        if (PropertyWalk.GetText(owner, textField) is not { } format) return arguments;

        var named = arguments is null
            ? new Dictionary<string, Placeholder>(StringComparer.Ordinal)
            : new Dictionary<string, Placeholder>(arguments, StringComparer.Ordinal);

        foreach (var token in DescriptionTokens.Tokens(format))
        {
            if (!named.ContainsKey(token)) named[token] = new Placeholder { Text = name };
        }

        return named.Count == 0 ? null : named;
    }

    private static double? Number(IPropertyHolder scalable, ConstantsTable constants)
    {
        if (PropertyWalk.GetStruct(scalable, CurveField) is { } curve &&
            PropertyWalk.GetString(curve, RowNameField) is { } rowName &&
            constants.Lookup(rowName).Record is { } record)
        {
            return record.Value;
        }

        return PropertyWalk.AsNumber(PropertyWalk.Get(scalable, ValueField)?.GenericValue);
    }

    private static string? Format(int? numericType) => numericType switch
    {
        0 => "number",
        1 => "percent",
        2 => "delta",
        _ => null,
    };
}
