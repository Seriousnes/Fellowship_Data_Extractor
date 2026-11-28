using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Extraction;

/// <summary>
/// The <c>RespawnCooldownEffect</c> of the <c>CRGameModeDungeon</c> Blueprint, with its duration and the tag a respawn
/// request is refused under. The rule is the shipping binary's (build 26320967: <c>ServerRequestRespawn</c>
/// 0x14532E770, respawn 0x145347EA0, wipe check 0x145349640).
/// </summary>
public static class GameModeRespawn
{
    public const string GameModeClass = "CRGameModeDungeon";

    // Binary 26320967: the tag global 0x14A3CA740 the request tests on the character.
    public const string CooldownTag = "Cooldown.Respawn";

    private const string NativeParentTag = "NativeParentClass";
    private const string EffectField = "RespawnCooldownEffect";
    private const string DurationField = "DurationMagnitude";
    private const string ScalableField = "ScalableFloatMagnitude";
    private const string GrantedContainerField = "InheritableGrantedTagsContainer";
    private const string CombinedField = "CombinedTags";
    private const string ClassSuffix = "_C";

    private const string Rule =
        "A dead player revives on that player's own respawn request. The server accepts the request while the " +
        "character is dead, no boss fight is in progress and the character has no cooldown tag. A respawn before the " +
        "match's end applies the cooldown effect to the character. A team wipe is every player character " +
        "dead: the game mode counts it, the dungeon reset follows, and each player then revives on a request of their own.";

    public static RespawnSettings Read(
        AbstractFileProvider provider, IReadOnlyList<FAssetData> assets, ConstantsTable constants)
    {
        var effects = assets
            .Where(asset => NativeParent(asset)?.EndsWith($".{GameModeClass}", StringComparison.Ordinal) is true)
            .Select(asset => ClassDefaults.Load(asset.ObjectPath, provider))
            .OfType<UObject>()
            .Select(cdo => PropertyWalk.AsString(ClassDefaults.Inherited(cdo, EffectField, provider)?.GenericValue))
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (effects is not [var effectPath])
        {
            throw new InvalidOperationException(
                $"The {GameModeClass} Blueprints name {effects.Length} {EffectField} classes, not one.");
        }

        var effect = ClassDefaults.Load(effectPath, provider)
            ?? throw new InvalidOperationException($"No container has {effectPath}.");

        var scalable = PropertyWalk.GetStruct(effect, DurationField) is { } magnitude
            ? PropertyWalk.GetStruct(magnitude, ScalableField)
            : null;

        var (duration, row) = scalable is null ? (null, null) : MagnitudeCalculations.Read(scalable, constants);

        if (duration is null || row is null)
        {
            throw new InvalidOperationException($"{effectPath} reads no constants row for its duration.");
        }

        var granted = provider.TryLoadPackage(AbilityPresentationIndex.PackagePath(effectPath), out var package)
            ? package.GetExports()
                .Select(export => PropertyWalk.GetStruct(export, GrantedContainerField))
                .OfType<IPropertyHolder>()
                .SelectMany(container => PropertyWalk.GetTags(container, CombinedField) ?? [])
                .ToArray()
            : [];

        if (!granted.Contains(CooldownTag, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"{effectPath} grants [{string.Join(", ", granted)}], not {CooldownTag}, the tag a respawn request tests.");
        }

        return new RespawnSettings
        {
            CooldownEffect = Leaf(effectPath),
            CooldownDuration = duration.Value,
            CooldownDurationRow = row,
            CooldownTag = CooldownTag,
            DisplayText = Rule,
        };
    }

    private static string? NativeParent(FAssetData asset)
    {
        if (AssetRegistry.TagValue(asset, NativeParentTag) is not { } reference) return null;

        var open = reference.IndexOf('\'');
        var close = reference.LastIndexOf('\'');

        return open >= 0 && close > open ? reference[(open + 1)..close] : reference;
    }

    private static string Leaf(string classPath)
    {
        var leaf = classPath[(classPath.LastIndexOf('.') + 1)..];

        return leaf.EndsWith(ClassSuffix, StringComparison.Ordinal) ? leaf[..^ClassSuffix.Length] : leaf;
    }
}
