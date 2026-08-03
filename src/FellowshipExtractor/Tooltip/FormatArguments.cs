using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Tooltip;

/// <summary>
/// The substitutions an FText carries beside its format string.
///
/// This is a second, unrelated mechanism to the one <see cref="DescriptionTokens"/> reads. An
/// ability's text is a DynamicDescription holding Text and Tokens, where a token names a gameplay
/// effect and a calculation. A gem or set bonus's text is an FText holding FormatString and
/// Arguments, where an argument is a scalable float and a numeric format and nothing else. Reading
/// one shape with the other's reader returns nothing at all, which is why gem descriptions export
/// today with their braces still in them.
/// </summary>
public static class FormatArguments
{
    private const string ArgumentsField = "Arguments";
    private const string ValueField = "Value";
    private const string NumericTypeField = "NumericType";
    private const string CurveField = "Curve";
    private const string RowNameField = "RowName";

    /// <summary>
    /// The arguments an FText struct declares, each resolved to the number it prints. Null when the
    /// text carries none, which is every one of the 21 set bonus rows.
    /// </summary>
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

    /// <summary>
    /// The same arguments, plus the name the struct holding the text carries, given to whatever
    /// token the arguments do not cover.
    ///
    /// An argument map only ever supplies numbers, so a token naming a thing is left out of it and
    /// the substitution ships beside the text instead. The gem type rows are where the build does
    /// that: MaxRankOverflowData pairs its Description with an OverflowName reading "Ruby" or
    /// "Amethyst", equal on all six rows to the row key and to the type's own DisplayName, and the
    /// sentence's one uncovered token is {typename}. The token's spelling is not tested, because
    /// the data does not spell the join - what the data says is that one token is unanswered and
    /// one name sits beside it.
    /// </summary>
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

    /// <summary>
    /// The number an argument prints. A scalable float carries both a literal and a curve row and
    /// the row wins where it resolves: Emerald node 3 stores 1 beside
    /// GemBonus.ApplyPeriodicAbsorb.AbsorbMaxHealthFraction.Small, and the shield it grants is 5% of
    /// maximum health rather than 100%.
    /// </summary>
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

    /// <summary>
    /// What an argument's NumericType ordinal asks the renderer for. The usmap carries no enum for
    /// it, so the decode is established by reproducing text the shipped tables and the locres agree
    /// on, in both directions:
    ///   0 prints the number as it stands. Emerald node 3's Cooldown 60 reads "Every 60 seconds".
    ///   1 prints a fraction as an unsigned percent. Ruby node 1's HealthThreshold 0.8 reads
    ///     "above 80% health"; Emerald node 5's CooldownReduction 0.04 reads "4% Ability Cooldown
    ///     reduction".
    ///   2 prints a multiplier as a signed delta. Ruby node 1's PrimaryStatIncrease 1.02 reads
    ///     "+2%"; Emerald node 10's CooldownReduction 1.12 reads "+12%".
    /// The pair at Emerald 5 and 10 is what separates 1 from 2: the same argument name, the same
    /// sentence and the same percent sign, from 0.04 and from 1.12.
    /// </summary>
    private static string? Format(int? numericType) => numericType switch
    {
        0 => "number",
        1 => "percent",
        2 => "delta",
        _ => null,
    };
}
