using Fellowship.SDK.Models;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Extraction;

public sealed record PhaseTiming
{
    public bool? EnabledByDefault { get; init; }

    public PhaseCooldown? InitialCooldown { get; init; }

    public PhaseCooldown? Cooldown { get; init; }

    public PhaseCooldown? InterruptCooldown { get; init; }

    public PhaseCooldown? CCCooldown { get; init; }

    public PhaseCooldown? PostAbilityPadding { get; init; }
}

public static class PhaseCooldowns
{
    private const string ValueTypeEnum = "ECRComplexValueType";

    private const string Disabled = "Disabled";
    private const string FixedValue = "FixedValue";
    private const string RandomRange = "RandomRange";

    private const string EnabledField = "EnabledByDefault";
    private const string TypeField = "Type";
    private const string ValueField = "Value";
    private const string MinimumField = "ValueMin";
    private const string MaximumField = "ValueMax";

    public static PhaseTiming Read(IPropertyHolder slot, TypeMappings mappings, ConstantsTable constants) => new()
    {
        EnabledByDefault = PropertyWalk.Get(slot, EnabledField)?.GenericValue is false ? false : null,
        InitialCooldown = Cooldown(slot, "InitialCooldown", mappings, constants),
        Cooldown = Cooldown(slot, "Cooldown", mappings, constants),
        InterruptCooldown = Cooldown(slot, "InterruptCooldown", mappings, constants),
        CCCooldown = Cooldown(slot, "CCCooldown", mappings, constants),
        PostAbilityPadding = Cooldown(slot, "PostAbilityPadding", mappings, constants),
    };

    private static PhaseCooldown? Cooldown(
        IPropertyHolder slot, string field, TypeMappings mappings, ConstantsTable constants)
    {
        if (PropertyWalk.GetStruct(slot, field) is not { } value) return null;

        var members = EnumOrdinals.Members(mappings, ValueTypeEnum);
        var type = EnumOrdinals.Member(mappings, ValueTypeEnum, PropertyWalk.GetString(value, TypeField))
            ?? members[members.Keys.Min()];

        return type switch
        {
            Disabled => null,
            FixedValue => new PhaseCooldown(type) { Seconds = Seconds(value, ValueField, constants) },
            RandomRange => new PhaseCooldown(type)
            {
                MinSeconds = Seconds(value, MinimumField, constants),
                MaxSeconds = Seconds(value, MaximumField, constants),
            },
            _ => new PhaseCooldown(type),
        };
    }

    private static double? Seconds(IPropertyHolder value, string field, ConstantsTable constants) =>
        PropertyWalk.GetStruct(value, field) is { } scalable &&
        MagnitudeCalculations.Scalable(scalable, constants) is { } seconds
            ? Math.Round(seconds, 10)
            : null;
}
