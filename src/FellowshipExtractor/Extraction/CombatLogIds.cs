using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.UObject;

namespace FellowshipExtractor.Extraction;

/// <summary>
/// The members the combat log reads for the id of a hit's line. <c>CRCombatLogAbilityEffectData</c> has the
/// <c>AbilityClass</c> and the <c>EffectClass</c> of the hit, and its resolver (shipping exe 26320967, 0x145285FC0)
/// writes the id of one of the two. <c>docs/combat-log-ids.md</c> has the rule.
/// </summary>
public static class CombatLogIds
{
    /// <summary>Member of <c>CRCombatLogAbilityEffectData</c> with the hit's effect.</summary>
    public const string EffectClass = "EffectClass";

    /// <summary>Member of <c>CRCombatLogAbilityEffectData</c> with the hit's instigating ability.</summary>
    public const string AbilityClass = "AbilityClass";

    /// <summary>
    /// The name <c>CRAbilityDataSubsystem</c> returns for an ability class with no entry in its <c>AbilityDataMap</c>
    /// (literal 0x148D2A698). The resolver takes an ability with this name, or with an empty one, as unnamed.
    /// </summary>
    public const string InvalidAbilityData = "Invalid Ability Data";

    /// <summary>
    /// The ability tags of an auto attack: <c>AbilityType.AutoAttack</c> (tag global 0x14A3C9FA8) and
    /// <c>Ability.AutoAttack</c> (0x14A3CAA10), read at 0x1452AE694 and 0x1452AE6F0.
    /// </summary>
    public static readonly IReadOnlySet<string> AutoAttackTags =
        new HashSet<string>(StringComparer.Ordinal) { "AbilityType.AutoAttack", "Ability.AutoAttack" };

    /// <summary>
    /// The asset tags of an effect with combat log lines of its application: the two tags the logger asks for by
    /// name (literals 0x148D34608, 0x148D34620) and the tag globals 0x14A3CA2B8 and 0x14A3CA2C0, in the container
    /// 0x145292650 returns.
    /// </summary>
    public static readonly IReadOnlySet<string> EffectEventTags = new HashSet<string>(StringComparer.Ordinal)
    {
        "Effect.Buff",
        "Effect.Debuff",
        "EffectVisualization.Icon",
        "EffectVisualization.IconOnPartyUnitFrame",
    };

    private const string BuffDebuffClass = "GameplayEffectUIData_BuffDebuff";
    private const string UiDataPrefix = "GameplayEffectUIData";
    private const string ComponentsField = "GEComponents";
    private const string DamageEffectClass = "CRGameplayEffectDamage";
    private const string AlwaysField = "AlwaysUseVisualDisplayName";
    private const string NameField = "VisualDisplayName";
    private const string LogAsField = "LogAsADifferentAbility";
    private const string InstantPolicy = "Instant";
    private const string ClassSuffix = "_C";
    private const string NoClass = "None";
    private const int MaxDepth = 16;

    /// <summary>
    /// The effect's members the resolver reads. <paramref name="Damage"/> is true for a class deriving from
    /// <c>CRGameplayEffectDamage</c>, the class with the two name members. Its constructor 0x1450E84D0 stores false at
    /// <c>AlwaysUseVisualDisplayName</c> (+0xAC0, 0x1450E8528) and an empty text at <c>VisualDisplayName</c> (+0xAB0).
    /// </summary>
    public sealed record EffectMembers(bool Damage, bool AlwaysUseVisualDisplayName, string? VisualDisplayName)
    {
        /// <summary>
        /// True where the line has the effect's id whatever ability the hit has: an effect with a duration
        /// (0x1452860FA), and an instant effect with <c>AlwaysUseVisualDisplayName</c> and a
        /// <c>VisualDisplayName</c> on a hit of no auto attack ability (0x1452863D3 to 0x1452863F6).
        /// </summary>
        public bool OwnId(string durationPolicy, bool autoAttack) =>
            Damage && (durationPolicy != InstantPolicy || (!autoAttack && NamedAlways));

        /// <summary>
        /// <see cref="EffectClass"/> for an effect whose members give the line the effect's id, and
        /// <see cref="AbilityClass"/> for an effect whose line has the id of the hit's ability.
        /// </summary>
        public string IdClass(string durationPolicy) =>
            Damage && (durationPolicy != InstantPolicy || NamedAlways) ? EffectClass : AbilityClass;

        /// <summary>True for <c>AlwaysUseVisualDisplayName</c> with a <c>VisualDisplayName</c>.</summary>
        public bool NamedAlways => AlwaysUseVisualDisplayName && VisualDisplayName is not null;
    }

    public static EffectMembers Of(UObject effect, AbstractFileProvider provider, TypeMappings mappings)
    {
        if (!DerivesFromDamage(effect, provider, mappings)) return new EffectMembers(false, false, null);

        var always = ClassDefaults.Inherited(effect, AlwaysField, provider)?.GenericValue is true;
        var name = PropertyWalk.AsString(ClassDefaults.Inherited(effect, NameField, provider)?.GenericValue);

        return new EffectMembers(true, always, string.IsNullOrEmpty(name) ? null : name);
    }

    /// <summary>
    /// Asset name of the ability's <c>LogAsADifferentAbility</c> class (<c>CRGameplayAbility</c> +0x610). The log has
    /// the ability under that class in its place (0x145299170, 0x145299390). The constructor 0x1450D7D60 stores null
    /// (0x1450D7FCA).
    /// </summary>
    public static string? LogAsADifferentAbility(UObject ability, AbstractFileProvider provider)
    {
        var path = PropertyWalk.AsString(ClassDefaults.Inherited(ability, LogAsField, provider)?.GenericValue);

        if (string.IsNullOrEmpty(path) || path == NoClass) return null;

        var name = AbilityPresentationIndex.AssetName(path);

        return name.EndsWith(ClassSuffix, StringComparison.Ordinal) ? name[..^ClassSuffix.Length] : name;
    }

    /// <summary>True for the <c>AbilityDataMap</c> name of an ability the resolver takes as named (0x145286324 to 0x14528635F).</summary>
    public static bool Names(string? dataName) => !string.IsNullOrEmpty(dataName) && dataName != InvalidAbilityData;

    /// <summary>
    /// True for an effect whose application, refresh, stack change and removal each write a combat log line. The
    /// logger's handler of an applied effect 0x145295100 takes the active effect of the handle, so an
    /// <c>Instant</c> effect has none (0x145295147). It goes on for an effect whose cached asset tags (+0xA28) have
    /// one of <see cref="EffectEventTags"/> (0x145295162 to 0x1452951C7), and binds the handlers of the other lines
    /// there. The line's <c>CRCombatLogEffectData</c> resolver 0x145286AB0 then needs a
    /// <c>GameplayEffectUIData_BuffDebuff</c> as the effect's UI data, or <c>AlwaysUseVisualDisplayName</c> with a
    /// <c>VisualDisplayName</c>, and a line with neither is left out (0x1452AE1CE).
    /// </summary>
    public static bool LogsEffectEvents(
        UObject effect, EffectMembers members, string durationPolicy, AbstractFileProvider provider) =>
        durationPolicy != InstantPolicy &&
        EffectAssetTags.Of(effect, provider).Any(EffectEventTags.Contains) &&
        (UiData(effect, provider) == BuffDebuffClass || members.NamedAlways);

    /// <summary>
    /// Class of the effect's UI data: the first <c>GameplayEffectUIData</c> component of the nearest class of its
    /// chain with a <c>GEComponents</c> list (0x1449EECC0).
    /// </summary>
    private static string? UiData(UObject effect, AbstractFileProvider provider)
    {
        foreach (var holder in ClassDefaults.Chain(effect, provider))
        {
            if (PropertyWalk.Get(holder, ComponentsField)?.GenericValue is not UScriptArray components) continue;

            var listed = components.Properties
                .Select(element => (element?.GenericValue as FPackageIndex)?.ResolvedObject?.Name.Text)
                .OfType<string>()
                .ToArray();

            var exports = (holder.Owner?.GetExports() ?? []).ToArray();

            return listed
                .Select(name => exports.FirstOrDefault(export => export.Name == name)?.ExportType)
                .FirstOrDefault(type => type is not null && type.StartsWith(UiDataPrefix, StringComparison.Ordinal));
        }

        return null;
    }

    private static bool DerivesFromDamage(UObject effect, AbstractFileProvider provider, TypeMappings mappings)
    {
        var native = ClassDefaults.NativeParent(effect, provider);
        var name = native?[(native.LastIndexOf('.') + 1)..];

        for (var depth = 0; name is not null && depth < MaxDepth; depth++)
        {
            if (name == DamageEffectClass) return true;

            name = mappings.Types.TryGetValue(name, out var type) ? type.SuperType : null;
        }

        return false;
    }
}
