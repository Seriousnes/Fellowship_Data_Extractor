namespace FellowshipExtractor.Tooltip;

/// <summary>
/// What type a row is, asked of the build rather than of the row's own spelling.
///
/// A reference is asked before a folder because a folder holds more than the assets of one type:
/// /Game/Abilities/Talents/Mosse ships 33 assets and 18 of them are the talents Mosse's slots point
/// at. Reading the folder alone counts 467 talents where the build has 216.
/// </summary>
public sealed class TypeResolver(
    IReadOnlyList<TagRecord> tags,
    TalentTrees talents,
    GemDefinitions gems,
    ItemTraits traits,
    ItemSetBonuses setBonuses)
{
    /// <summary>
    /// Every augmentation the build ships, by name, so a mount can be asked whether one pairs with
    /// it. Read off the tag table rather than listed, so a mount the build adds pairs on its own.
    /// </summary>
    private readonly HashSet<string> _augmentations = new(
        tags.Where(tag => tag.Table.Equals(TalentTrees.AugmentationTable, StringComparison.OrdinalIgnoreCase))
            .Select(tag => tag.Name),
        StringComparer.OrdinalIgnoreCase);

    /// <summary>The folders that name a type, for the types no reference points at.</summary>
    private static readonly (string Folder, EntityType Type)[] Folders =
    [
        ("/Game/Items/MountAugmentations/", EntityType.MountPower),
        ("/Game/Items/RelicAugmentations/", EntityType.RelicPower),
        ("/Game/Abilities/RelicPowers/", EntityType.RelicPower),
        ("/Game/Abilities/Weapons/", EntityType.WeaponPower),
        ("/Game/Abilities/Finesses/", EntityType.Finesse),
        ("/Game/Abilities/LegendaryStats/", EntityType.LegendaryStat),
    ];

    /// <summary>What an ItemID tag's second segment names.</summary>
    private static readonly Dictionary<string, EntityType> ItemSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        ["HeroItem"] = EntityType.Item,
        ["Mount"] = EntityType.Mount,
        ["Currency"] = EntityType.Currency,
        ["SetBonus"] = EntityType.SetBonus,
        ["CompanionPet"] = EntityType.CompanionPet,
    };

    /// <summary>The tables holding exactly one type.</summary>
    private static readonly Dictionary<string, EntityType> Tables = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GameplayAbility"] = EntityType.Ability,
        ["GameplayEffect"] = EntityType.Effect,
        ["CRAICharacter"] = EntityType.Creature,
        [ItemTraits.Table] = EntityType.Trait,
    };

    /// <summary>The type a row is, or null where no rule reaches it and the row is not written.</summary>
    public EntityType? Of(TagRecord tag, Entity entity, string? packageName)
    {
        if (tag.Table.Equals(TalentTrees.AugmentationTable, StringComparison.OrdinalIgnoreCase))
        {
            return Augmentation(tag, entity, packageName);
        }

        if (tag.Table.Equals("ItemID", StringComparison.OrdinalIgnoreCase)) return Item(tag, entity);

        // A slot no hero's tree ever filled is an id the build places no talent at. 21 of the 40
        // Talent.ID tags the build ships are in that state and the other 19 expand to the 216 slot
        // rows the trees fill. Occupancy is asked the same way Resolve asks it for the expansion
        // that follows: talents.InSlot, not entity.TalentRow, which this table never sets.
        if (tag.Table.Equals(TalentTrees.SlotTable, StringComparison.OrdinalIgnoreCase))
        {
            return talents.InSlot(tag.NetId).Count > 0 ? EntityType.TalentSlot : null;
        }

        return Tables.TryGetValue(tag.Table, out var table) ? table : null;
    }

    private EntityType? Augmentation(TagRecord tag, Entity entity, string? packageName)
    {
        if (talents.ForAugmentation(tag.Name) is not null) return EntityType.Talent;
        if (entity.GemNode is not null || gems.ForAugmentation(tag.Name) is not null) return EntityType.GemTrait;
        if (traits.PointsAt(tag.NetId)) return EntityType.TraitPower;
        if (setBonuses.PointsAt(tag.NetId)) return EntityType.SetBonusPower;

        if (packageName is null) return null;

        foreach (var (folder, type) in Folders)
        {
            if (packageName.Contains(folder, StringComparison.OrdinalIgnoreCase)) return type;
        }

        return null;
    }

    private EntityType? Item(TagRecord tag, Entity entity)
    {
        var segments = tag.Name.Split('.');
        if (segments.Length < 2) return null;

        // The gem segment holds both the six types and their four tiers each, and a tier is the
        // thing that carries one. The segment's null member is dropped by the null-member rule the
        // export applies to every tag, so it is not asked about here.
        if (segments[1].Equals("GemType", StringComparison.OrdinalIgnoreCase))
        {
            return entity.GemTier is not null ? EntityType.GemTier : EntityType.GemType;
        }

        if (!ItemSegments.TryGetValue(segments[1], out var type)) return null;

        // Spec 3.3: a mount whose name pairs with no CA_Mount augmentation is a type decided
        // against, and the augmentation is where a mount's mechanics live.
        return type is EntityType.Mount && !PairsWithAugmentation(segments) ? null : type;
    }

    /// <summary>
    /// Whether a mount tag names a CA_Mount augmentation: ItemID.Mount.Bear.UnarmoredBlack pairs
    /// with CA_Mount_Bear_UnarmoredBlack, the tag tree's separator read as the asset name's.
    /// </summary>
    private bool PairsWithAugmentation(string[] segments) =>
        segments.Length > 2 && _augmentations.Contains($"CA_Mount_{string.Join('_', segments[2..])}");
}
