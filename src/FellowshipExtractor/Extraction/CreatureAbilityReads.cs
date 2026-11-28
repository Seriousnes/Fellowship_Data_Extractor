using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;

using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

public sealed record CreatureAbilityFacts
{
    public string? TargetAcquisition { get; init; }

    public TargetSelection? TargetSelection { get; init; }

    public bool? CastInterruptible { get; init; }

    public bool? ChannelInterruptible { get; init; }

    public bool? ChangeTargetOnTaunt { get; init; }

    public bool? AutoAttack { get; init; }

    public bool? Area { get; init; }

    public bool? Projectile { get; init; }

    public IReadOnlyList<string>? AbilityTags { get; init; }

    public IReadOnlyList<string>? BlockAbilitiesWithTag { get; init; }

    public bool? ScaleTimeWithAttackSpeed { get; init; }

    public double? InterruptCooldownDuration { get; init; }

    public FieldSource? InterruptCooldownSource { get; init; }

    public IReadOnlyList<EndAbilityPath>? EndAbilityPaths { get; init; }
}

/// <summary>
/// The targeting, interrupt, kind, tags and time scaling of a creature ability, each read from the nearest class of its
/// Blueprint chain that declares it, and the routes of its graph to <c>K2_EndAbility</c>. A subobject a policy property
/// names is read from the package that holds it.
/// </summary>
public sealed class CreatureAbilityReads(AbstractFileProvider provider, TypeMappings mappings, ConstantsTable constants)
{
    private const string AcquisitionField = "TargetAcquisitionPolicy";
    private const string SelectionField = "TargetSelectionPolicy";
    private const string AcquisitionPrefix = "CRAbilityTargetingAcquisitionPolicy_";
    private const string SelectionPrefix = "CRAbilityTargetSelectionPolicy_";

    private const string ThreatSelection = "CRAbilityTargetSelectionPolicy_Threat";
    private const string ActorSetSelection = "CRAbilityTargetSelectionPolicy_ActorSet";
    private const string HealthSelection = "CRAbilityTargetSelectionPolicy_RemainingHealthActorSet";
    private const string ThreatModeEnum = "ECRAbilityTargetSelectionPolicy_ThreatSelectionType";
    private const string HealthOrderEnum = "ECRAbilityTargetSelectionPolicy_RemainingHealth";
    private const string RoleEnum = "EPlayerRole";
    private const string SelectionTypeField = "SelectionType";
    private const string PreferredRoleField = "PreferredRole";
    private const string ShouldHaveField = "ShouldHaveTags";
    private const string ShouldNotHaveField = "ShouldNotHaveTags";
    private const string MinimumField = "MinNumberOfTargets";
    private const string MaximumField = "MaxNumberOfTargets";
    private const string BlueprintField = "Blueprint";

    /// <summary>
    /// <c>CRAbilityTargetSelectionPolicy_Threat</c>'s constructor default for <c>SelectionType</c> (+0x50): 0, the
    /// object's zero fill. Shipping exe 26320967, constructor 0x14506F350 stores nothing past +0x48.
    /// </summary>
    public const string DefaultThreatMode = "ThreatSorted";

    /// <summary>
    /// The constructor default of <c>MinNumberOfTargets</c> (+0x28) and <c>MaxNumberOfTargets</c> (+0x50) on every
    /// <c>CRAbilityTargetSelectionPolicy_ActorSet</c> class: <c>Value</c> 1.0 with no curve row. Shipping exe 26320967,
    /// ActorSet constructor 0x14506EE80, stores 0x14506EEA9 and 0x14506EEC7; RandomActorSet constructor 0x14506F0F0,
    /// stores 0x14506F10B and 0x14506F12D.
    /// </summary>
    public const double DefaultTargetCount = 1;

    /// <summary>
    /// <c>CRAbilityTargetSelectionPolicy_RemainingHealthActorSet</c>'s constructor default for <c>SelectionType</c>
    /// (+0x78): 0. Shipping exe 26320967, constructor 0x14506F2E0, store 0x14506F341.
    /// </summary>
    public const string DefaultHealthOrder = "LeastHealthLeft";

    /// <summary>
    /// <c>CRAbilityTargetSelectionPolicy_RandomPreferRoleActorSet</c>'s constructor default for <c>PreferredRole</c>
    /// (+0x78): 0. Shipping exe 26320967, constructor 0x14506F160, store 0x14506F1C1.
    /// </summary>
    public const string DefaultPreferredRole = "ROLE_TANK";

    private const string RoleSelection = "CRAbilityTargetSelectionPolicy_RandomPreferRoleActorSet";

    private const string CastingField = "CastingData";
    private const string ChannelingField = "ChannelingData";
    private const string TargetedCastingField = "TargetedCastingData";
    private const string InterruptibleField = "Interruptible";
    private const string TauntField = "ChangeTargetOnTaunt";

    private const string AbilityTagsField = "AbilityTags";
    private const string BlockedTagsField = "BlockAbilitiesWithTag";
    private const string AttackSpeedField = "ScaleTimeWithAttackSpeed";
    private const string InterruptCooldownField = "InterruptCooldownDuration";
    private const string AutoAttackTag = "AbilityType.AutoAttack";
    private const string AreaTag = "AbilityType.Aoe";

    private const string ProjectileMovement = "ProjectileMovementComponent";
    private const string ScriptRoot = "/Script/";
    private const string ClassSuffix = "_C";
    private const int MaxDepth = 16;

    private readonly Dictionary<string, bool> _projectiles = new(StringComparer.OrdinalIgnoreCase);

    public EndAbilityPaths EndPaths { get; } = new(provider, constants);

    public CreatureAbilityFacts Of(UObject cdo)
    {
        var chain = ClassDefaults.Chain(cdo, provider).ToArray();
        var tags = PropertyWalk.AsTags(ClassDefaults.Inherited(cdo, AbilityTagsField, provider)?.GenericValue) ?? [];

        var blocked = PropertyWalk.AsTags(ClassDefaults.Inherited(cdo, BlockedTagsField, provider)?.GenericValue) ?? [];
        var interrupt = ClassDefaults.Inherited(cdo, InterruptCooldownField, provider)?.GenericValue is FScriptStruct { StructType: IPropertyHolder scalable }
            ? scalable
            : null;
        var paths = EndPaths.Of(cdo);

        var (acquisition, selection) = Targeting(chain);

        return new CreatureAbilityFacts
        {
            TargetAcquisition = acquisition,
            TargetSelection = selection,
            CastInterruptible = Flag(chain, CastingField, InterruptibleField),
            ChannelInterruptible = Flag(chain, ChannelingField, InterruptibleField),
            ChangeTargetOnTaunt = Flag(chain, TargetedCastingField, TauntField),
            AutoAttack = tags.Contains(AutoAttackTag, StringComparer.Ordinal) ? true : null,
            Area = tags.Contains(AreaTag, StringComparer.Ordinal) ? true : null,
            Projectile = chain.Any(SpawnsProjectile) ? true : null,
            AbilityTags = tags.Length > 0 ? tags : null,
            BlockAbilitiesWithTag = blocked.Length > 0 ? blocked : null,
            ScaleTimeWithAttackSpeed = ClassDefaults.Inherited(cdo, AttackSpeedField, provider)?.GenericValue is true ? true : null,
            InterruptCooldownDuration = interrupt is null ? null : MagnitudeCalculations.Scalable(interrupt, constants),
            InterruptCooldownSource = interrupt is null ? null : MagnitudeCalculations.Source(interrupt, constants),
            EndAbilityPaths = paths.Count > 0 ? paths : null,
        };
    }

    private (string? Acquisition, TargetSelection? Selection) Targeting(IReadOnlyList<UObject> chain)
    {
        foreach (var holder in chain)
        {
            if (PropertyWalk.GetString(holder, AcquisitionField) is not { } path) continue;
            if (Subobject(path) is not { } acquisition) return (null, null);

            var acquired = Stripped(acquisition, AcquisitionPrefix);
            var selection = PropertyWalk.GetString(acquisition, SelectionField) is { } selected &&
                            Subobject(selected) is { } policy
                ? Selection(policy)
                : null;

            return (acquired, selection);
        }

        return (null, null);
    }

    /// <summary>
    /// The policy fields a parent Blueprint's subobject of the same name sets while the ability's own policy leaves
    /// them unset, and whether the ability's class inherits its whole acquisition policy from a parent class.
    /// </summary>
    public (bool Inherited, IReadOnlyList<string> Fields) ArchetypeHeld(UObject cdo)
    {
        var chain = ClassDefaults.Chain(cdo, provider).ToArray();
        var at = Array.FindIndex(chain, holder => PropertyWalk.GetString(holder, AcquisitionField) is not null);

        if (at < 0) return (false, []);

        var path = PropertyWalk.GetString(chain[at], AcquisitionField)!;
        var held = new List<string>();

        if (Subobject(path) is not { } acquisition) return (at > 0, held);

        var policy = PropertyWalk.GetString(acquisition, SelectionField) is { } selected ? Subobject(selected) : null;

        foreach (var parent in chain.Skip(at + 1))
        {
            if (parent.Owner is not { } package) continue;

            var exports = package.GetExports().ToArray();
            var archetype = exports.FirstOrDefault(export => export.Name == acquisition.Name);
            var selection = policy is null ? null : exports.FirstOrDefault(export => export.Name == policy.Name);

            if (archetype is not null && policy is null && PropertyWalk.GetString(archetype, SelectionField) is not null)
            {
                held.Add($"{parent.Name}.{SelectionField}");
            }

            if (policy is null || selection is null) continue;

            foreach (var field in (string[])[MinimumField, MaximumField, SelectionTypeField, PreferredRoleField])
            {
                if (PropertyWalk.Get(policy, field) is null && PropertyWalk.Get(selection, field) is not null)
                {
                    held.Add($"{parent.Name}.{field}");
                }
            }
        }

        return (at > 0, held);
    }

    private TargetSelection Selection(UObject policy)
    {
        var type = policy.Class?.Name.Text ?? policy.ExportType;
        var lineage = Lineage(type);

        var selection = new TargetSelection(Stripped(policy, SelectionPrefix))
        {
            ThreatMode = lineage.Contains(ThreatSelection)
                ? Member(policy, SelectionTypeField, ThreatModeEnum) ?? DefaultThreatMode
                : null,
            HealthOrder = lineage.Contains(HealthSelection)
                ? Member(policy, SelectionTypeField, HealthOrderEnum) ?? DefaultHealthOrder
                : null,
            PreferredRole = lineage.Contains(RoleSelection)
                ? Member(policy, PreferredRoleField, RoleEnum) ?? DefaultPreferredRole
                : null,
            PreferredTags = PropertyWalk.GetTags(policy, ShouldHaveField) is { Length: > 0 } wanted ? wanted : null,
            AvoidedTags = PropertyWalk.GetTags(policy, ShouldNotHaveField) is { Length: > 0 } avoided ? avoided : null,
            MinNumberOfTargets = lineage.Contains(ActorSetSelection) ? Count(policy, MinimumField) : null,
            MaxNumberOfTargets = lineage.Contains(ActorSetSelection) ? Count(policy, MaximumField) : null,
            Blueprint = PropertyWalk.GetString(policy, BlueprintField) is { } blueprint ? Leaf(blueprint) : null,
        };

        return selection;
    }

    private string? Member(UObject policy, string field, string enumeration) =>
        PropertyWalk.GetString(policy, field) is { } text ? EnumOrdinals.Member(mappings, enumeration, text) : null;

    private double Count(UObject policy, string field)
    {
        if (PropertyWalk.GetStruct(policy, field) is not { } scalable) return DefaultTargetCount;

        return MagnitudeCalculations.Scalable(scalable, constants)
            ?? throw new InvalidOperationException(
                $"{policy.Owner?.Name}'s {policy.Name} reads {field} from a row no constants table has.");
    }

    /// <summary>The native class and its native ancestors.</summary>
    private HashSet<string> Lineage(string type)
    {
        var lineage = new HashSet<string>(StringComparer.Ordinal);

        for (var name = type; name is { Length: > 0 } && lineage.Add(name);)
        {
            name = mappings.Types.TryGetValue(name, out var structure) ? structure.SuperType : null;
        }

        return lineage;
    }

    private UObject? Subobject(string path)
    {
        var colon = path.IndexOf(':');
        var package = colon < 0 ? path : path[..colon];
        var dot = package.LastIndexOf('.');
        if (dot > 0) package = package[..dot];

        var name = path[(path.LastIndexOfAny(['.', ':']) + 1)..];

        if (!provider.TryLoadPackage(package, out var loaded)) return null;

        return loaded.GetExports().FirstOrDefault(export => export.Name.Equals(name, StringComparison.Ordinal));
    }

    private static string Stripped(UObject policy, string prefix)
    {
        var type = policy.Class?.Name.Text ?? policy.ExportType;

        return type.StartsWith(prefix, StringComparison.Ordinal) ? type[prefix.Length..] : type;
    }

    /// <summary>
    /// A struct field's bool, from the nearest class with that member in a property of that name. A class deriving from
    /// the property's first class has the members it changes alone.
    /// </summary>
    private static bool? Flag(IReadOnlyList<UObject> chain, string field, string member) =>
        Held(chain, field)
            .Select(data => PropertyWalk.Get(data, member)?.GenericValue as bool?)
            .FirstOrDefault(value => value is not null);

    /// <summary>
    /// The structs of the chain's property of that name, spaces aside, nearest class first. Where the chain holds two
    /// such properties, the one the ability's graph reads is the one read.
    /// </summary>
    public static IReadOnlyList<IPropertyHolder> Held(IReadOnlyList<UObject> chain, string field)
    {
        var held = new List<(string Name, IPropertyHolder Data)>();

        foreach (var holder in chain)
        {
            foreach (var property in holder.Properties)
            {
                if (!property.Name.Text.Replace(" ", string.Empty).Equals(field, StringComparison.OrdinalIgnoreCase)) continue;
                if (property.Tag?.GenericValue is not FScriptStruct { StructType: IPropertyHolder data }) continue;

                held.Add((property.Name.Text, data));
            }
        }

        var names = held.Select(entry => entry.Name).Distinct(StringComparer.Ordinal).ToArray();

        if (names.Length == 0) return [];

        var read = names.Length == 1 ? names[0] : names.FirstOrDefault(name => Reads(chain, name)) ?? names[0];

        return [.. held.Where(entry => entry.Name == read).Select(entry => entry.Data)];
    }

    /// <summary>Whether a graph of the chain's packages reads the instance variable.</summary>
    private static bool Reads(IReadOnlyList<UObject> chain, string variable) =>
        chain.Any(holder => holder.Owner?.GetExports()
            .OfType<UStruct>()
            .Where(body => body.ScriptBytecode is { Length: > 0 })
            .Any(body => body.ScriptBytecode!.Any(statement => Names(statement, 0).Contains(variable))) is true);

    private static IEnumerable<string> Names(KismetExpression? expression, int depth)
    {
        if (expression is null || depth > MaxDepth) yield break;

        if (expression is EX_InstanceVariable && DynamicTextFloats.Name(expression) is { } name) yield return name;

        foreach (var child in ResourceGains.Children(expression))
        {
            foreach (var found in Names(child, depth + 1)) yield return found;
        }
    }

    private bool SpawnsProjectile(UObject holder) =>
        RowSinks.SpawnedPaths(holder).Any(IsProjectile);

    /// <summary>An actor class with a component template, in its own package or a parent's, of a projectile movement class.</summary>
    private bool IsProjectile(string classPath)
    {
        if (_projectiles.TryGetValue(classPath, out var known)) return known;

        _projectiles[classPath] = false;

        var found = ClassDefaults.Load(classPath, provider) is { } actor &&
                    ClassDefaults.Chain(actor, provider).Any(holder =>
                        holder.Owner?.GetExports().Any(export => Moves(export.Class)) is true);

        return _projectiles[classPath] = found;
    }

    private bool Moves(ResolvedObject? type)
    {
        for (var depth = 0; type is not null && depth < MaxDepth; depth++)
        {
            var name = type.Name.Text;

            if (type.GetPathName().StartsWith(ScriptRoot, StringComparison.Ordinal) || type.Load() is not UStruct loaded)
            {
                return Lineage(name).Contains(ProjectileMovement);
            }

            type = loaded.SuperStruct?.ResolvedObject;
        }

        return false;
    }

    private static string Leaf(string path)
    {
        var name = path[(path.LastIndexOfAny(['.', '/']) + 1)..];

        return name.EndsWith(ClassSuffix, StringComparison.Ordinal) ? name[..^ClassSuffix.Length] : name;
    }
}
