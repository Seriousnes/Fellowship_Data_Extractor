using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;

using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

public sealed record StatEnchant(
    string Name,
    IReadOnlyDictionary<string, string> NameByPrimaryStat,
    IReadOnlyList<TraitStat> Stats,
    string? Description,
    IReadOnlyDictionary<string, Placeholder> Placeholders);

public sealed class StatEnchants
{
    public const string SettingsSection = "/Script/fellowship.CRItemTraitSettings";
    public const string PowerKey = "StatEnchantItemLevelToTraitPower";

    private const string NameField = "DynamicName";
    private const string DescriptionField = "DynamicDescription";
    private const string TextField = "Text";
    private const string TokensField = "Tokens";
    private const string TokenNameField = "TokenName";
    private const string TokenTypeField = "Type";
    private const string AttributeCalculationType = "AttributeCalculation";
    private const string ObjectField = "Object";
    private const string FunctionField = "CustomSourceFunction";
    private const string CustomFunctionType = "CustomSourceFunction";
    private const string EffectsField = "GrantedGameplayEffects";
    private const string ModifiersField = "Modifiers";
    private const string AttributeField = "Attribute";
    private const string AttributeNameField = "AttributeName";
    private const string MagnitudeField = "ModifierMagnitude";
    private const string CustomMagnitudeField = "CustomMagnitude";
    private const string CoefficientField = "Coefficient";
    private const string ValueField = "Value";
    private const string CurveField = "Curve";
    private const string RowNameField = "RowName";
    private const string CurveTableField = "CurveTable";
    private const string LookupField = "FinalLookupCurve";
    private const string SwitchedField = "PrimaryAttribute";
    private const string CurveRowName = "Curve";
    private const string NoName = "None";

    private readonly Dictionary<int, StatEnchant> _byAugmentation = [];

    public Curve Power { get; }

    public int Count => _byAugmentation.Count;

    public StatEnchants(AbstractFileProvider provider, ItemTraits traits, ConstantsTable constants, HeroNames heroes)
    {
        var powerTable = ConfigIni.Read(provider, ItemSettings.ConfigPath, SettingsSection).Text(PowerKey).Trim('"');
        Power = CurveReader.Read(provider, powerTable, CurveRowName);

        foreach (var trait in traits.All)
        {
            if (!trait.NameFromAugmentation) continue;

            if (trait is not { AugmentationPath: { } path, Augmentation: { } netId } ||
                !provider.TryLoadPackageObject<UObject>(AbilityPresentationIndex.PackagePath(path), out var augmentation))
            {
                throw new InvalidOperationException(
                    $"Trait {trait.DisplayName} takes its name from its augmentation, and names none that loads.");
            }

            var stats = new List<TraitStat>();

            foreach (var effect in Strings(augmentation, EffectsField))
            {
                var cdo = ClassDefaults.Load(effect, provider)
                    ?? throw new InvalidOperationException($"{path} grants {effect}, and no default object loads.");

                foreach (var modifier in Structs(cdo, ModifiersField))
                {
                    stats.Add(Stat(provider, modifier, constants, effect));
                }
            }

            if (stats.Count == 0) throw new InvalidOperationException($"{path} grants no modifier an enchant reads.");

            var (name, names) = Names(provider, augmentation, heroes, path);
            var description = PropertyWalk.GetStruct(augmentation, DescriptionField) is { } dynamic
                ? PropertyWalk.GetText(dynamic, TextField)
                : null;

            _byAugmentation[netId] = new StatEnchant(
                name, names, stats, description, PerRank(augmentation, stats, path));
        }
    }

    private static Dictionary<string, Placeholder> PerRank(
        IPropertyHolder augmentation, IReadOnlyList<TraitStat> stats, string path)
    {
        var placeholders = new Dictionary<string, Placeholder>(StringComparer.Ordinal);

        foreach (var token in Structs(PropertyWalk.GetStruct(augmentation, DescriptionField), TokensField))
        {
            if (PropertyWalk.GetString(token, TokenTypeField)?
                    .EndsWith(AttributeCalculationType, StringComparison.Ordinal) is not true) continue;

            var key = PropertyWalk.GetString(token, TokenNameField);
            var attribute = PropertyWalk.GetStruct(token, AttributeField) is { } held
                ? PropertyWalk.GetString(held, AttributeNameField)
                : null;

            if (key is null || stats.SingleOrDefault(stat => stat.Attribute == attribute) is not { } stat)
            {
                throw new InvalidOperationException(
                    $"{path}'s description token {key} reads {attribute}, and the enchant's effect modifies no " +
                    "such attribute.");
            }

            placeholders[key] = new Placeholder
            {
                Key = key,
                Attribute = attribute,
                Value = stat.Coefficient,
                PerRank = true,
                ScalesOff = [attribute!],
            };
        }

        return placeholders;
    }

    public StatEnchant? ForAugmentation(int netId) => _byAugmentation.GetValueOrDefault(netId);

    private static TraitStat Stat(
        AbstractFileProvider provider, IPropertyHolder modifier, ConstantsTable constants, string effect)
    {
        var attribute = PropertyWalk.GetStruct(modifier, AttributeField) is { } held
            ? PropertyWalk.GetString(held, AttributeNameField)
            : null;

        var custom = PropertyWalk.GetStruct(modifier, MagnitudeField) is { } magnitude
            ? PropertyWalk.GetStruct(magnitude, CustomMagnitudeField)
            : null;

        var coefficient = custom is null ? null : PropertyWalk.GetStruct(custom, CoefficientField);

        if (attribute is null || coefficient is null)
        {
            throw new InvalidOperationException(
                $"A modifier of {effect} states no attribute or custom coefficient, so the enchant's stat is unread.");
        }

        var value = PropertyWalk.AsNumber(PropertyWalk.Get(coefficient, ValueField)?.GenericValue) ?? 1;
        var scaled = PropertyWalk.GetStruct(coefficient, CurveField) is { } handle &&
                     Named(PropertyWalk.GetString(handle, RowNameField)) is { } rowName &&
                     constants.Lookup(rowName).Record is { Value: { } rowValue }
            ? value * rowValue
            : value;

        var lookup = PropertyWalk.GetStruct(custom!, LookupField);
        var table = lookup is null ? null : Named(PropertyWalk.GetString(lookup, CurveTableField));
        var row = lookup is null ? null : Named(PropertyWalk.GetString(lookup, RowNameField));

        return new TraitStat(attribute, scaled)
        {
            Lookup = table is not null && row is not null ? CurveReader.Read(provider, table, row) : null,
        };
    }

    private static string? Named(string? value) => value is null or NoName ? null : value;

    private static (string Name, IReadOnlyDictionary<string, string> NameByPrimaryStat) Names(
        AbstractFileProvider provider, UObject augmentation, HeroNames heroes, string path)
    {
        var token = Structs(PropertyWalk.GetStruct(augmentation, NameField), TokensField)
            .SingleOrDefault(candidate => PropertyWalk.GetString(candidate, TokenTypeField)?
                .EndsWith(CustomFunctionType, StringComparison.Ordinal) is true)
            ?? throw new InvalidOperationException($"{path}'s {NameField} has no one {CustomFunctionType} token.");

        var owner = PropertyWalk.GetString(token, ObjectField);
        var function = PropertyWalk.GetString(token, FunctionField);

        if (owner is null || function is null ||
            !provider.TryLoadPackage(AbilityPresentationIndex.PackagePath(owner), out var package) ||
            package.GetExports().OfType<UStruct>().FirstOrDefault(export => export.Name == function)
                is not { ScriptBytecode: { Length: > 0 } bytecode })
        {
            throw new InvalidOperationException($"{path} names its name function {owner}.{function}, and it compiles no body.");
        }

        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        string? switched = null;
        EX_SwitchValue? choice = null;

        foreach (var statement in bytecode)
        {
            if (statement is not EX_Let let || DynamicTextFloats.Name(let.Variable) is not { } local) continue;

            switch (let.Assignment)
            {
                case EX_TextConst { Value: { } text } when Text(text) is { } printed:
                    texts[local] = printed;
                    break;
                case EX_Context { ContextExpression: { } read } when DynamicTextFloats.Name(read) == SwitchedField:
                    switched = local;
                    break;
                case EX_SwitchValue chosen when DynamicTextFloats.Name(chosen.IndexTerm) == switched:
                    choice = chosen;
                    break;
            }
        }

        if (choice is null)
        {
            throw new InvalidOperationException($"{owner}.{function} chooses its name on no hero {SwitchedField}.");
        }

        var byAttribute = new Dictionary<long, string>();

        foreach (var branch in choice.Cases)
        {
            if (branch.CaseIndexValueTerm is not EX_ByteConst { Value: var ordinal } ||
                DynamicTextFloats.Name(branch.CaseTerm) is not { } local ||
                !texts.TryGetValue(local, out var printed))
            {
                throw new InvalidOperationException($"{owner}.{function} has a branch naming no string table text.");
            }

            byAttribute[ordinal] = printed;
        }

        var byStat = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var hero in heroes.Profiles.OrderBy(hero => hero.PrimaryAttribute))
        {
            if (!byAttribute.TryGetValue(hero.PrimaryAttribute, out var printed))
            {
                throw new InvalidOperationException(
                    $"{owner}.{function} names nothing for {hero.ShippedName}'s {SwitchedField} {hero.PrimaryAttribute}.");
            }

            if (byStat.TryGetValue(hero.PrimaryStat, out var named) && named != printed)
            {
                throw new InvalidOperationException(
                    $"{owner}.{function} names two enchants for the primary stat {hero.PrimaryStat}: {named} and {printed}.");
            }

            byStat[hero.PrimaryStat] = printed;
        }

        return (string.Join(" / ", byStat.Values), byStat);
    }

    private static string? Text(FScriptText text) =>
        text.TableIdString is EX_StringConst { Value: { } table } && text.KeyString is EX_StringConst { Value: { } key }
            ? StringTables.Find(table, key)
            : null;

    private static IEnumerable<string> Strings(IPropertyHolder holder, string field)
    {
        if (PropertyWalk.Get(holder, field)?.GenericValue is not UScriptArray array) yield break;

        foreach (var element in array.Properties)
        {
            if (PropertyWalk.AsString(element?.GenericValue) is { } text) yield return text;
        }
    }

    private static IEnumerable<IPropertyHolder> Structs(IPropertyHolder? holder, string field)
    {
        if (holder is null || PropertyWalk.Get(holder, field)?.GenericValue is not UScriptArray array) yield break;

        foreach (var element in array.Properties)
        {
            if (element?.GenericValue is FScriptStruct { StructType: IPropertyHolder held }) yield return held;
        }
    }
}
