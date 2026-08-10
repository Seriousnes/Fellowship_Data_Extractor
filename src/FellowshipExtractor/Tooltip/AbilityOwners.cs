using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Tooltip;

public sealed class AbilityOwners
{
    private const string AbilitySetClass = "CRGameplayAbilitySet";
    private const string AugmentationClass = "CRCharacterAugmentationAsset";
    private const string MountClass = "CRMountData";
    private const string CreatureTable = "CRAICharacter";
    private const string AbilityTable = "GameplayAbility";
    private const string EffectTable = "GameplayEffect";

    private const string UniversalSet = "DS_UniversalHeroAbilitySet";

    private const string HeroIdField = "HeroID";
    private const string AbilitySetField = "AbilitySet";
    private const string TalentDataField = "TalentData";
    private const string IsLiveField = "IsLive";
    private const string InfoField = "Info";
    private const string HeroInfoClass = "CRHeroInfo";

    private const string AbilitiesField = "Abilities";
    private const string AbilityClassField = "GameplayAbilityClass";
    private const string TalentsField = "Talents";
    private const string AugmentationField = "CharacterAugmentationAsset";
    private const string GrantedAbilitiesField = "GrantedAbilities";
    private const string GrantedEffectsField = "GrantedGameplayEffects";
    private const string GrantedAssetField = "GrantedAbilityAsset";
    private const string MountIdField = "MountID";
    private const string MountAssetField = "MountDataAsset";

    private const string BehaviourField = "BehaviorComponentClass";
    private const string PhasesField = "Phases";
    private const string PhaseAbilityField = "AbilityClass";
    private const string PhaseEffectsField = "Effects";
    private const string PhaseEffectField = "Effect";
    private const string LifetimeAbilitiesField = "LifetimePassiveAbilities";
    private const string LifetimeEffectsField = "LifetimePassiveEffects";
    private const string PassiveEffectsField = "PassiveGameplayEffects";
    private const string AbilityTagsField = "AbilityTags";
    private const string AbilityTypePrefix = "AbilityType.";

    private readonly AbstractFileProvider _provider;
    private readonly IReadOnlyList<FAssetData> _assets;

    private readonly Dictionary<string, FAssetData> _byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _granted = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, SortedSet<string>> _heroes =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, string> _creatures = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _outsiders = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _appliers = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, IReadOnlyList<string>> _applierTags =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _shared = new(StringComparer.OrdinalIgnoreCase);

    public int Heroes => _heroes.Count;

    public int Creatures => _creatures.Count;

    public int Outsiders => _outsiders.Count;

    public int SharedAbilities => _shared.Count;

    public int AppliedEffectCount => _appliers.Count(pair => pair.Value.Length > 0);

    public AbilityOwners(
        AbstractFileProvider provider, IReadOnlyList<FAssetData> assets, IReadOnlyList<TagRecord> tags)
    {
        _provider = provider;
        _assets = assets;

        foreach (var asset in assets) _byName.TryAdd(asset.AssetName.Text, asset);

        var wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tag in tags)
        {
            if (tag.Table.Equals(AbilityTable, StringComparison.OrdinalIgnoreCase) ||
                tag.Table.Equals(EffectTable, StringComparison.OrdinalIgnoreCase) ||
                tag.Table.Equals(TalentTrees.AugmentationTable, StringComparison.OrdinalIgnoreCase))
            {
                wanted.Add(tag.Name);
            }
        }

        HoldOutShared();
        SeedHeroes(wanted);
        SeedCreatures(tags, wanted);
        SeedMounts(wanted);
        Propagate(wanted);
        SeedAppliers(tags);
    }

    public IReadOnlyList<string> HeroesOf(string name) =>
        _heroes.TryGetValue(name, out var owners) ? [.. owners] : [];

    public string? CreatureOf(string name) => _creatures.GetValueOrDefault(name);

    public string? ApplierOf(string name) => _appliers.GetValueOrDefault(name);

    public IReadOnlyList<string> AbilityTagsOf(string ability) => _applierTags.GetValueOrDefault(ability, []);

    public string? OutsiderOf(string name) => _outsiders.GetValueOrDefault(name);

    private void HoldOutShared()
    {
        if (!_byName.TryGetValue(UniversalSet, out var universal)) return;

        foreach (var name in SetMembers(universal.ObjectPath)) _shared.Add(name);
    }

    private IReadOnlyList<string> SetMembers(string objectPath)
    {
        if (DungeonNames.Load(_provider, objectPath) is not { } set) return [];
        if (PropertyWalk.Get(set, AbilitiesField)?.GenericValue is not UScriptArray listed) return [];

        var members = new List<string>(listed.Properties.Count);

        foreach (var entry in listed.Properties)
        {
            if (entry?.GenericValue is not FScriptStruct { StructType: IPropertyHolder slot }) continue;
            if (PropertyWalk.GetString(slot, AbilityClassField) is not { } path) continue;

            members.Add(Leaf(path));
        }

        return members;
    }

    private void SeedHeroes(IReadOnlySet<string> wanted)
    {
        foreach (var (codename, ability, talents) in Rosters())
        {
            foreach (var member in ability is null ? [] : SetMembers(ability))
            {
                if (_shared.Contains(member)) continue;

                Claim(member, codename, wanted);
            }

            if (talents is null) continue;

            foreach (var augmentation in TalentAugmentations(talents))
            {
                Claim(augmentation, codename, wanted);

                foreach (var granted in Grants(augmentation)) Claim(granted, codename, wanted);
            }
        }

        foreach (var (asset, codename) in MappedAugmentations())
        {
            Claim(asset, codename, wanted);

            foreach (var granted in Grants(asset)) Claim(granted, codename, wanted);
        }
    }

    private IEnumerable<(string Codename, string? AbilitySet, string? TalentData)> Rosters()
    {
        foreach (var objectPath in TableFinder.ByRowStructure(_assets, HeroNames.CharacterInfoRowStructure))
        {
            if (!_provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            foreach (var (_, row) in table.RowMap)
            {
                if (PropertyWalk.Get(row, IsLiveField)?.GenericValue is not true) continue;
                if (PropertyWalk.GetString(row, HeroIdField) is not { } heroId) continue;

                yield return (
                    HeroNames.LastSegment(heroId),
                    PropertyWalk.GetString(row, AbilitySetField),
                    PropertyWalk.GetString(row, TalentDataField));
            }
        }

        foreach (var asset in _assets)
        {
            if (!asset.AssetClass.Text.Equals(HeroInfoClass, StringComparison.OrdinalIgnoreCase)) continue;
            if (DungeonNames.Load(_provider, asset.ObjectPath) is not { } hero) continue;
            if (PropertyWalk.GetStruct(hero, InfoField) is not { } info) continue;
            if (PropertyWalk.GetString(info, HeroIdField) is not { } heroId) continue;

            yield return (
                HeroNames.LastSegment(heroId),
                PropertyWalk.GetString(info, AbilitySetField),
                PropertyWalk.GetString(info, TalentDataField));
        }
    }

    private IEnumerable<string> TalentAugmentations(string talentData)
    {
        if (DungeonNames.Load(_provider, talentData) is not { } talents) yield break;
        if (PropertyWalk.Get(talents, TalentsField)?.GenericValue is not UScriptArray listed) yield break;

        foreach (var entry in listed.Properties)
        {
            if (entry?.GenericValue is not FScriptStruct { StructType: IPropertyHolder talent }) continue;
            if (PropertyWalk.GetString(talent, AugmentationField) is not { } path) continue;

            yield return Leaf(path);
        }
    }

    private IEnumerable<(string Asset, string Codename)> MappedAugmentations()
    {
        foreach (var objectPath in TableFinder.ByRowStructure(_assets, GrantedAbilityMappings.RowStructure))
        {
            if (!_provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            foreach (var (_, row) in table.RowMap)
            {
                if (PropertyWalk.GetString(row, HeroIdField) is not { } heroId) continue;
                if (PropertyWalk.GetString(row, GrantedAssetField) is not { } asset) continue;

                yield return (Leaf(asset), HeroNames.LastSegment(heroId));
            }
        }
    }

    private IEnumerable<string> Grants(string augmentation)
    {
        if (!_byName.TryGetValue(augmentation, out var asset)) yield break;
        if (DungeonNames.Load(_provider, asset.ObjectPath) is not { } loaded) yield break;

        foreach (var field in (string[])[GrantedAbilitiesField, GrantedEffectsField])
        {
            if (PropertyWalk.Get(loaded, field)?.GenericValue is not UScriptArray listed) continue;

            foreach (var entry in listed.Properties)
            {
                if (PropertyWalk.AsString(entry?.GenericValue) is not { } path) continue;
                if (!path.Contains('/')) continue;

                yield return Leaf(path);
            }
        }
    }

    private void SeedCreatures(IReadOnlyList<TagRecord> tags, IReadOnlySet<string> wanted)
    {
        foreach (var tag in tags)
        {
            if (!tag.Table.Equals(CreatureTable, StringComparison.OrdinalIgnoreCase)) continue;
            if (!_byName.TryGetValue(tag.Name, out var asset)) continue;
            if (DungeonNames.Load(_provider, asset.ObjectPath) is not { } creature) continue;

            foreach (var named in Owned(creature)) Owns(_creatures, named, tag.Name, wanted);
        }
    }

    private IEnumerable<string> Owned(UObject creature)
    {
        foreach (var field in (string[])[AbilitiesField, PassiveEffectsField])
        {
            foreach (var named in Paths(creature, field)) yield return named;
        }

        if (PropertyWalk.GetString(creature, BehaviourField) is not { } behaviour) yield break;
        if (!_byName.TryGetValue(Leaf(behaviour), out var asset)) yield break;
        if (DungeonNames.Load(_provider, asset.ObjectPath) is not { } component) yield break;

        foreach (var field in (string[])[LifetimeAbilitiesField, LifetimeEffectsField])
        {
            foreach (var named in Paths(component, field)) yield return named;
        }

        if (PropertyWalk.Get(component, PhasesField)?.GenericValue is not UScriptMap phases) yield break;

        foreach (var (_, value) in phases.Properties)
        {
            if (value?.GenericValue is not FScriptStruct { StructType: IPropertyHolder phase }) continue;

            foreach (var named in Slotted(phase, AbilitiesField, PhaseAbilityField)) yield return named;
            foreach (var named in Slotted(phase, PhaseEffectsField, PhaseEffectField)) yield return named;
        }
    }

    private static IEnumerable<string> Slotted(IPropertyHolder holder, string field, string member)
    {
        if (PropertyWalk.Get(holder, field)?.GenericValue is not UScriptArray listed) yield break;

        foreach (var entry in listed.Properties)
        {
            if (entry?.GenericValue is not FScriptStruct { StructType: IPropertyHolder slot }) continue;
            if (PropertyWalk.GetString(slot, member) is not { } path) continue;
            if (!path.Contains('/')) continue;

            yield return Leaf(path);
        }
    }

    private static IEnumerable<string> Paths(IPropertyHolder holder, string field)
    {
        if (PropertyWalk.Get(holder, field)?.GenericValue is not UScriptArray listed) yield break;

        foreach (var entry in listed.Properties)
        {
            if (PropertyWalk.AsString(entry?.GenericValue) is not { } path) continue;
            if (!path.Contains('/')) continue;

            yield return Leaf(path);
        }
    }

    private void SeedMounts(IReadOnlySet<string> wanted)
    {
        foreach (var asset in _assets)
        {
            if (!asset.AssetClass.Text.Equals(MountClass, StringComparison.OrdinalIgnoreCase)) continue;
            if (DungeonNames.Load(_provider, asset.ObjectPath) is not { } mount) continue;
            if (PropertyWalk.GetString(mount, MountAssetField) is not { } augmentation) continue;

            if (PropertyWalk.GetString(mount, MountIdField) is not { } mountId) continue;

            Owns(_outsiders, Leaf(augmentation), mountId, wanted);

            foreach (var granted in Grants(Leaf(augmentation))) Owns(_outsiders, granted, mountId, wanted);
        }
    }

    private void SeedAppliers(IReadOnlyList<TagRecord> tags)
    {
        var effects = tags
            .Where(tag => tag.Table.Equals(EffectTable, StringComparison.OrdinalIgnoreCase))
            .Select(tag => tag.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var tag in tags)
        {
            if (!tag.Table.Equals(AbilityTable, StringComparison.OrdinalIgnoreCase)) continue;
            if (!_byName.TryGetValue(tag.Name, out var asset)) continue;

            var applied = AppliedEffects.Named(_provider, asset.ObjectPath, effects);
            if (applied.Count == 0) continue;

            _applierTags[tag.Name] = AbilityTypes(asset.ObjectPath);

            foreach (var name in applied) Owns(_appliers, name, tag.Name, effects);
        }
    }

    private IReadOnlyList<string> AbilityTypes(string objectPath)
    {
        if (!_provider.TryLoadPackage(AbilityPresentationIndex.PackagePath(objectPath), out var package))
        {
            return [];
        }

        foreach (var export in package.GetExports())
        {
            if (PropertyWalk.GetTags(export, AbilityTagsField) is not { Length: > 0 } listed) continue;

            var named = listed
                .Where(tag => tag.StartsWith(AbilityTypePrefix, StringComparison.Ordinal))
                .Select(tag => tag[AbilityTypePrefix.Length..])
                .Order(StringComparer.Ordinal)
                .ToArray();

            if (named.Length > 0) return named;
        }

        return [];
    }

    private void Propagate(IReadOnlySet<string> wanted)
    {
        var frontier = new Queue<string>(_heroes.Keys.Concat(_creatures.Keys).Concat(_outsiders.Keys));
        var walked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (frontier.Count > 0)
        {
            var applier = frontier.Dequeue();
            if (!walked.Add(applier)) continue;
            if (!_byName.TryGetValue(applier, out var asset)) continue;

            var reached = AppliedEffects.Named(_provider, asset.ObjectPath, wanted);
            if (reached.Count == 0) continue;

            var owningHeroes = HeroesOf(applier);
            var owningCreature = _creatures.GetValueOrDefault(applier);
            var owningOutsider = _outsiders.GetValueOrDefault(applier);

            foreach (var applied in reached)
            {
                if (applied.Equals(applier, StringComparison.OrdinalIgnoreCase)) continue;

                foreach (var codename in owningHeroes) Claim(applied, codename, wanted);
                if (owningCreature is not null) Owns(_creatures, applied, owningCreature, wanted);
                if (owningOutsider is not null) Owns(_outsiders, applied, owningOutsider, wanted);

                if (!walked.Contains(applied)) frontier.Enqueue(applied);
            }
        }
    }

    private void Claim(string name, string codename, IReadOnlySet<string> wanted)
    {
        if (!wanted.Contains(name)) return;
        if (_shared.Contains(name)) return;

        if (!_heroes.TryGetValue(name, out var owners))
        {
            _heroes[name] = owners = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        owners.Add(codename);
    }

    private static void Owns(
        Dictionary<string, string> owners, string name, string owner, IReadOnlySet<string> wanted)
    {
        if (!wanted.Contains(name)) return;

        if (owners.TryGetValue(name, out var existing))
        {
            if (!existing.Equals(owner, StringComparison.OrdinalIgnoreCase)) owners[name] = Contested;
            return;
        }

        owners[name] = owner;
    }

    public const string Contested = "";

    private static string Leaf(string path)
    {
        var name = AbilityPresentationIndex.AssetName(path);

        return name.EndsWith("_C", StringComparison.Ordinal) ? name[..^2] : name;
    }
}
