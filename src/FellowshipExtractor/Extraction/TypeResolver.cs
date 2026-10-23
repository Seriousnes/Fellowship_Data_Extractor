namespace FellowshipExtractor.Extraction;

public sealed class TypeResolver(
    IReadOnlyList<TagRecord> tags,
    TalentTrees talents,
    GemDefinitions gems,
    ItemTraits traits,
    GrantedAbilityMappings mappings,
    DungeonNames dungeons,
    ProgressionItems progression,
    CustomizationItems customizations,
    ItemTableNames currencies,
    HeroNames heroes,
    IEnumerable<string> mounts)
{
    public const string LevelTable = "Level.ID";

    private readonly HashSet<string> _mounts = new(mounts, StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _augmentations = new(
        tags.Where(tag => tag.Table.Equals(TalentTrees.AugmentationTable, StringComparison.OrdinalIgnoreCase))
            .Select(tag => tag.Name),
        StringComparer.OrdinalIgnoreCase);

    private static readonly (string Folder, EntityType Type)[] Folders =
    [
        ("/Game/Items/MountAugmentations/", EntityType.MountPower),
        ("/Game/Items/RelicAugmentations/", EntityType.RelicPower),
        ("/Game/Abilities/RelicPowers/", EntityType.RelicPower),
        ("/Game/Items/RelicPassives/", EntityType.RelicPassive),
        ("/Game/Abilities/Weapons/", EntityType.WeaponPower),
        ("/Game/Abilities/Finesses/", EntityType.Glyph),
        ("/Game/Abilities/LegendaryStats/", EntityType.LegendaryStat),
    ];

    private static readonly Dictionary<string, EntityType> ItemSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        ["HeroItem"] = EntityType.Item,
        ["Mount"] = EntityType.Mount,
        ["Currency"] = EntityType.Currency,
        ["CompanionPet"] = EntityType.CompanionPet,
    };

    private static readonly Dictionary<string, EntityType> Tables = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GameplayAbility"] = EntityType.Ability,
        ["GameplayEffect"] = EntityType.Effect,
        ["CRAICharacter"] = EntityType.Creature,
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
            return heroes.ForTag(tag.Name) is not null ? EntityType.Hero : null;
        }

        if (tag.Table.Equals(ItemTraits.Table, StringComparison.OrdinalIgnoreCase))
        {
            return entity.TraitCategoryTag is { } category && ItemTraitTypes.ByCategory.TryGetValue(category, out var type)
                ? type
                : null;
        }

        return Tables.TryGetValue(tag.Table, out var table) ? table : null;
    }

    private EntityType? Augmentation(TagRecord tag, Entity entity, string? packageName)
    {
        if (talents.ForAugmentation(tag.Name) is not null) return EntityType.Talent;
        if (entity.GemNode is not null || gems.ForAugmentation(tag.Name) is not null) return EntityType.GemTrait;
        if (traits.ForAugmentation(tag.NetId) is { CategoryTag: { } category })
        {
            return ItemTraitTypes.ByCategory.ContainsKey(category) ? EntityType.TraitPower : null;
        }

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
            return entity.GemTier is not null ? EntityType.Gem : EntityType.Radiance;
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

        if (type is EntityType.Currency) return currencies.Find(tag.Name) is not null ? type : null;

        return type is EntityType.Mount && !_mounts.Contains(tag.Name) && !PairsWithAugmentation(segments)
            ? null
            : type;
    }

    private bool PairsWithAugmentation(string[] segments) =>
        segments.Length > 2 && _augmentations.Contains($"CA_Mount_{string.Join('_', segments[2..])}");
}
