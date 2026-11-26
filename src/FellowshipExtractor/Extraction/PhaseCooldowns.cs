using Fellowship.SDK.Models;
using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Extraction;

public sealed record PhaseTiming
{
    public int? Priority { get; init; }

    public string? Activation { get; init; }

    public double? DifficultyUnlocked { get; init; }

    public bool? Interruptible { get; init; }

    public string? ActivationTag { get; init; }

    public bool? EnabledByDefault { get; init; }

    public bool? PreserveCooldownBetweenPhases { get; init; }

    public PhaseCooldown? InitialCooldown { get; init; }

    public PhaseCooldown? Cooldown { get; init; }

    public PhaseCooldown? InterruptCooldown { get; init; }

    public PhaseCooldown? CCCooldown { get; init; }

    public PhaseCooldown? PostAbilityPadding { get; init; }
}

public static class PhaseCooldowns
{
    private const string ValueTypeEnum = "ECRComplexValueType";
    private const string ActivationEnum = "ECRPhaseAbilityActivationType";

    private const string Disabled = "Disabled";
    private const string FixedValue = "FixedValue";
    private const string RandomRange = "RandomRange";
    private const string CustomCalculation = "CustomCalculation";

    private const string PriorityField = "Priority";
    private const string ActivationField = "ActivationType";
    private const string DifficultyUnlockedField = "DifficultyUnlocked";
    private const string InterruptibleField = "IsAbilityInterruptable";
    private const string ActivationTagField = "ActivationTag";
    private const string EnabledField = "EnabledByDefault";
    private const string PreserveField = "PreserveCooldownBetweenPhases";
    private const string TypeField = "Type";
    private const string ValueField = "Value";
    private const string MinimumField = "ValueMin";
    private const string MaximumField = "ValueMax";
    private const string CalculationClassField = "CooldownCalculationClass";

    public static PhaseTiming Read(
        IPropertyHolder slot, TypeMappings mappings, ConstantsTable constants, AbstractFileProvider provider) => new()
    {
        Priority = PropertyWalk.AsNumber(PropertyWalk.Get(slot, PriorityField)?.GenericValue) is { } priority
            ? (int)priority
            : null,
        Activation = EnumOrdinals.Member(mappings, ActivationEnum, PropertyWalk.GetString(slot, ActivationField)),
        DifficultyUnlocked = PropertyWalk.GetStruct(slot, DifficultyUnlockedField) is { } unlocked &&
                             Scalable(unlocked, constants, provider) is { } difficulty and > 0
            ? difficulty
            : null,
        Interruptible = PropertyWalk.Get(slot, InterruptibleField)?.GenericValue as bool?,
        ActivationTag = PropertyWalk.GetString(slot, ActivationTagField),
        EnabledByDefault = PropertyWalk.Get(slot, EnabledField)?.GenericValue is false ? false : null,
        PreserveCooldownBetweenPhases = PropertyWalk.Get(slot, PreserveField)?.GenericValue as bool?,
        InitialCooldown = Cooldown(slot, "InitialCooldown", mappings, constants, provider),
        Cooldown = Cooldown(slot, "Cooldown", mappings, constants, provider),
        InterruptCooldown = Cooldown(slot, "InterruptCooldown", mappings, constants, provider),
        CCCooldown = Cooldown(slot, "CCCooldown", mappings, constants, provider),
        PostAbilityPadding = Cooldown(slot, "PostAbilityPadding", mappings, constants, provider),
    };

    private static PhaseCooldown? Cooldown(
        IPropertyHolder slot, string field, TypeMappings mappings, ConstantsTable constants, AbstractFileProvider provider)
    {
        if (PropertyWalk.GetStruct(slot, field) is not { } value) return null;

        var members = EnumOrdinals.Members(mappings, ValueTypeEnum);
        var type = EnumOrdinals.Member(mappings, ValueTypeEnum, PropertyWalk.GetString(value, TypeField))
            ?? members[members.Keys.Min()];

        return type switch
        {
            Disabled => null,
            FixedValue => new PhaseCooldown(type) { Seconds = Seconds(value, ValueField, constants, provider) },
            RandomRange => new PhaseCooldown(type)
            {
                MinSeconds = Seconds(value, MinimumField, constants, provider),
                MaxSeconds = Seconds(value, MaximumField, constants, provider),
            },
            CustomCalculation => new PhaseCooldown(type) { Calculation = Leaf(PropertyWalk.GetString(value, CalculationClassField)) },
            _ => new PhaseCooldown(type),
        };
    }

    private static double? Seconds(IPropertyHolder value, string field, ConstantsTable constants, AbstractFileProvider provider) =>
        PropertyWalk.GetStruct(value, field) is { } scalable &&
        Scalable(scalable, constants, provider) is { } seconds
            ? Math.Round(seconds, 10)
            : null;

    /// <summary>
    /// A scalable float's value: its scale times its row, read from the curve table it names or, with no table, from
    /// the constants tables. A row no table has reads as null.
    /// </summary>
    private static double? Scalable(IPropertyHolder scalable, ConstantsTable constants, AbstractFileProvider provider)
    {
        if (PropertyWalk.GetStruct(scalable, "Curve") is { } handle &&
            PropertyWalk.GetString(handle, "CurveTable") is { } table &&
            PropertyWalk.GetString(handle, "RowName") is { } row)
        {
            var scale = PropertyWalk.AsNumber(PropertyWalk.Get(scalable, ValueField)?.GenericValue) ?? 1;
            var points = CurveReader.Read(provider, table, row).Points.Select(point => point.Value).Distinct().ToArray();

            return points is [var only] ? scale * only : null;
        }

        return MagnitudeCalculations.Scalable(scalable, constants);
    }

    private static string? Leaf(string? path)
    {
        if (path is null) return null;

        var name = path[(path.LastIndexOfAny(['.', '/']) + 1)..];

        return name.EndsWith("_C", StringComparison.Ordinal) ? name[..^2] : name;
    }
}
