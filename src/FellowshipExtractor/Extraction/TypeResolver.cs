namespace FellowshipExtractor.Extraction;

public sealed class TypeResolver(
    IReadOnlyList<TagRecord> tags,
    TalentTrees talents,
    GemDefinitions gems,
    ItemTraits traits,
    ItemSetBonuses setBonuses,
    GrantedAbilityMappings mappings,
    DungeonNames dungeons,
    ProgressionItems progression,
    CustomizationItems customizations,
    HeroNames heroes)
{
    public const string LevelTable = "Level.ID";

    private readonly HashSet<string> _augmentations = new(
        tags.Where(tag => tag.Table.Equals(TalentTrees.AugmentationTable, StringComparison.OrdinalIgnoreCase))
            .Select(tag => tag.Name),
        StringComparer.OrdinalIgnoreCase);

    private static readonly (string Folder, EntityType Type)[] Folders =
    [
        ("/Game/Items/MountAugmentations/", EntityType.MountPower),
        ("/Game/Items/RelicAugmentations/", EntityType.RelicPower),
        ("/Game/Abilities/RelicPowers/", EntityType.RelicPower),
        ("/Game/Abilities/Weapons/", EntityType.WeaponPower),
        ("/Game/Abilities/Finesses/", EntityType.Blessing),
        ("/Game/Abilities/LegendaryStats/", EntityType.LegendaryStat),
    ];

    private static readonly Dictionary<string, EntityType> ItemSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        ["HeroItem"] = EntityType.Item,
        ["Mount"] = EntityType.Mount,
        ["Currency"] = EntityType.Currency,
        ["SetBonus"] = EntityType.SetBonus,
        ["CompanionPet"] = EntityType.CompanionPet,
    };

    private static readonly Dictionary<string, EntityType> Tables = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GameplayAbility"] = EntityType.Ability,
        ["GameplayEffect"] = EntityType.Effect,
        ["CRAICharacter"] = EntityType.Creature,
        [ItemTraits.Table] = EntityType.Trait,
        [ModifierInfo.Table] = EntityType.Modifier,
    };

    public EntityType? Of(TagRecord tag, Entity entity, string? packageName)
    {
        if (tag.Table.Equals(TalentTrees.AugmentationTable, StringComparison.OrdinalIgnoreCase))
        {
            return Augmentation(tag, entity, packageName);
        }

        if (tag.Table.Equals("ItemID", StringComparison.OrdinalIgnoreCase)) return Item(tag, entity);

        if (tag.Table.Equals(TalentTrees.SlotTable, StringComparison.OrdinalIgnoreCase))
        {
            return talents.InSlot(tag.NetId).Count > 0 ? EntityType.TalentSlot : null;
        }

        if (tag.Table.Equals(LevelTable, StringComparison.OrdinalIgnoreCase))
        {
            return dungeons.Find(tag.Name) is not null ? EntityType.Dungeon : null;
        }

        if (tag.Table.Equals(HeroNames.Table, StringComparison.OrdinalIgnoreCase))
        {
            return heroes.Rostered(tag.Name) is not null ? EntityType.Hero : null;
        }

        return Tables.TryGetValue(tag.Table, out var table) ? table : null;
    }

    private EntityType? Augmentation(TagRecord tag, Entity entity, string? packageName)
    {
        if (talents.ForAugmentation(tag.Name) is not null) return EntityType.Talent;
        if (entity.GemNode is not null || gems.ForAugmentation(tag.Name) is not null) return EntityType.GemTrait;
        if (traits.PointsAt(tag.NetId)) return EntityType.TraitPower;
        if (setBonuses.PointsAt(tag.NetId)) return EntityType.SetBonusPower;

        foreach (var (folder, type) in Folders)
        {
            if (packageName?.Contains(folder, StringComparison.OrdinalIgnoreCase) is true) return type;
        }

        return mappings.PointsAt(tag.Name) ? EntityType.LegendaryPower : null;
    }

    private EntityType? Item(TagRecord tag, Entity entity)
    {
        var segments = tag.Name.Split('.');
        if (segments.Length < 2) return null;

        if (segments[1].Equals("GemType", StringComparison.OrdinalIgnoreCase))
        {
            return entity.GemTier is not null ? EntityType.Gem : EntityType.GemType;
        }

        if (segments[1].Equals(ProgressionItems.Segment, StringComparison.OrdinalIgnoreCase))
        {
            return progression.Find(tag.Name) is not null ? EntityType.Item : null;
        }

        if (segments[1].Equals(CustomizationItems.Segment, StringComparison.OrdinalIgnoreCase))
        {
            return customizations.Find(tag.Name) is not null ? EntityType.Item : null;
        }

        if (!ItemSegments.TryGetValue(segments[1], out var type)) return null;

        return type is EntityType.Mount && !PairsWithAugmentation(segments) ? null : type;
    }

    private bool PairsWithAugmentation(string[] segments) =>
        segments.Length > 2 && _augmentations.Contains($"CA_Mount_{string.Join('_', segments[2..])}");
}
