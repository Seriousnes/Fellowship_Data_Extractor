using FellowshipExtractor.Tooltip;

namespace FellowshipExtractor.Tests;

public sealed class TypeResolverTests
{
    private static readonly TooltipResolver Resolver = Export.Resolver;

    private static readonly IReadOnlySet<string> Branches = ExportAllCommand.Branches(Resolver.Tags);

    /// <summary>The tag the build ships under this name, so a rule is asked about a real row.</summary>
    private static TagRecord Tag(string table, string name) =>
        Resolver.FindTag(table, name)
        ?? throw new InvalidOperationException($"{name} is no longer a tag in {table}.");

    /// <summary>Why the export leaves this tag's row out, or null where it writes it.</summary>
    private static string? NotWritten(TagRecord tag) =>
        ExportAllCommand.NotWritten(Resolver.Resolve(tag, icons: null).Single(), Branches);

    [Fact]
    public void TheTypeSetIsTheOneTheSpecNames() => Assert.Equal(21, Enum.GetValues<EntityType>().Length);

    [Theory]
    [InlineData(EntityType.Item, "item")]
    [InlineData(EntityType.GemTier, "gem-tier")]
    [InlineData(EntityType.SetBonusPower, "set-bonus-power")]
    [InlineData(EntityType.TalentSlot, "talent-slot")]
    public void ASlugIsTheTypeNameHyphenated(EntityType type, string slug) =>
        Assert.Equal(slug, EntityTypes.Slug(type));

    [Theory]
    [InlineData("/Game/Items/MountAugmentations/CA_Mount_Bear", EntityType.MountPower)]
    [InlineData("/Game/Items/RelicAugmentations/CA_RelicPower_Sanctuary", EntityType.RelicPower)]
    [InlineData("/Game/Abilities/RelicPowers/Portal/CA_RelicPower_Portal", EntityType.RelicPower)]
    [InlineData("/Game/Abilities/Weapons/ChainHealDamage/CA_Weapon_ChainHealDamage", EntityType.WeaponPower)]
    [InlineData("/Game/Abilities/Finesses/BasicToAoe/CAA_Finesse_BasicToAoe", EntityType.Finesse)]
    [InlineData("/Game/Abilities/LegendaryStats/X/CAA_LegendaryStat_X", EntityType.LegendaryStat)]
    [InlineData("/Game/Abilities/Talents/Mosse/CAA_Talent_Mosse_Talent1", null)]
    public void AFolderNamesTheTypeNothingPointsAt(string package, EntityType? expected)
    {
        var tag = new TagRecord("CRCharacterAugmentationAsset", "CA_Unclaimed", 999_999, 0);
        Assert.Equal(expected, Resolver.Types.Of(tag, new Entity(), package));
    }

    [Theory]
    [InlineData("ItemID.HeroItem.Dynamic.Chest.A_Crit", EntityType.Item)]
    [InlineData("ItemID.Mount.CatPanther", EntityType.Mount)]
    [InlineData("ItemID.Mount.DefaultHorse", null)]
    [InlineData("ItemID.Currency.Gold", EntityType.Currency)]
    [InlineData("ItemID.SetBonus.SetA_Percentage_HDT", EntityType.SetBonus)]
    [InlineData("ItemID.CompanionPet.Cat.Base", EntityType.CompanionPet)]
    [InlineData("ItemID.VisualCollection.Naked", null)]
    [InlineData("ItemID.PlayerCustomization.Avatar.ChiefRebel", null)]
    [InlineData("ItemID.HeroSkin.Warmaster_Default_Black_Back", null)]
    public void AnItemTagsSecondSegmentNamesItsType(string name, EntityType? expected)
    {
        var tag = new TagRecord("ItemID", name, 999_999, 0);
        Assert.Equal(expected, Resolver.Types.Of(tag, new Entity(), null));
    }

    [Fact]
    public void AGemTypeTagIsATierWhereItCarriesOne()
    {
        var tag = new TagRecord("ItemID", "ItemID.GemType.Ruby.Small", 999_999, 0);

        Assert.Equal(EntityType.GemTier, Resolver.Types.Of(tag, new Entity { GemTier = 1 }, null));
        Assert.Equal(EntityType.GemType, Resolver.Types.Of(tag, new Entity(), null));
    }

    [Theory]
    [InlineData("GameplayAbility", EntityType.Ability)]
    [InlineData("GameplayEffect", EntityType.Effect)]
    [InlineData("CRAICharacter", EntityType.Creature)]
    [InlineData("ItemTrait.ID", EntityType.Trait)]
    [InlineData("UIMaps", null)]
    [InlineData("Encounters", null)]
    public void AOneTypeTableIsItsOwnType(string table, EntityType? expected)
    {
        var tag = new TagRecord(table, "Whatever", 999_999, 0);
        Assert.Equal(expected, Resolver.Types.Of(tag, new Entity(), null));
    }

    /// <summary>
    /// A Talent.ID id is a talent slot where a hero's tree filled it. The row's own coordinates
    /// cannot answer this: TalentRow is null on the entity the resolver is handed, so occupancy is
    /// read from the trees, which is the question Resolve asks for the expansion that follows. 19
    /// of the 40 shipped ids are filled and expand to one slot row per tree that filled them, 216
    /// in all; the 21 the build places no talent at are not slots.
    /// </summary>
    [Fact]
    public void ATalentSlotIsARowCarryingCoordinates()
    {
        var ids = Resolver.Tags
            .Where(t => t.Table.Equals(TalentTrees.SlotTable, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var filled = ids.Where(t => Resolver.Talents.InSlot(t.NetId).Count > 0).ToArray();
        var unfilled = ids.Where(t => Resolver.Talents.InSlot(t.NetId).Count == 0).ToArray();

        Assert.Equal(19, filled.Length);
        Assert.Equal(21, unfilled.Length);
        Assert.Equal(216, filled.Sum(t => Resolver.Talents.InSlot(t.NetId).Count));

        Assert.All(filled, tag => Assert.Equal(EntityType.TalentSlot, Resolver.Types.Of(tag, new Entity(), null)));
        Assert.All(unfilled, tag => Assert.Null(Resolver.Types.Of(tag, new Entity(), null)));
    }

    /// <summary>
    /// A branch of the tag tree is only a non-entity where it also says nothing. ItemID.GemType.Ruby
    /// is a branch, its four tiers hanging off it, and a gem type in its own right, so it is written;
    /// ItemID.HeroItem is a branch that carries nothing, so it is not.
    /// </summary>
    [Fact]
    public void AnEmptyBranchIsDroppedAndAFullOneIsNot()
    {
        var ruby = Tag("ItemID", "ItemID.GemType.Ruby");
        var heroItem = Tag("ItemID", "ItemID.HeroItem");

        Assert.Contains(heroItem.Name, Branches);
        Assert.Contains(ruby.Name, Branches);

        Assert.Null(NotWritten(ruby));
        Assert.Equal("empty branch", NotWritten(heroItem));

        Assert.Single(Export.Loaded.Documents, d => d.Type == EntityType.GemType && d.Id == ruby.NetId);
        Assert.DoesNotContain(Export.Loaded.Documents, d => d.Type == EntityType.Item && d.Id == heroItem.NetId);
    }

    /// <summary>
    /// A creature carrying nothing beyond the tag naming it is not written. The table is the game's
    /// whole cast and most of it is never shown to a reader: 106 of the 338 CRAICharacter tags
    /// resolve to a row stating nothing at all, and the rest are the creatures the store holds.
    /// </summary>
    [Fact]
    public void ACreatureCarryingNothingIsNotWritten()
    {
        var creatures = Resolver.Tags
            .Where(tag => tag.Table.Equals("CRAICharacter", StringComparison.OrdinalIgnoreCase))
            .SelectMany(tag => Resolver.Resolve(tag, icons: null))
            .ToArray();

        var empty = creatures
            .Where(row => ExportAllCommand.NotWritten(row, Branches) == "a creature carrying nothing")
            .ToArray();

        Assert.Equal(338, creatures.Length);
        Assert.Equal(106, empty.Length);
        Assert.All(empty, row => Assert.Equal(EntityType.Creature, row.Type));

        Assert.Equal(
            Export.Loaded.Documents.Count(d => d.Type == EntityType.Creature),
            creatures.Count(row => ExportAllCommand.NotWritten(row, Branches) is null));
    }

    /// <summary>
    /// A mount tag is a mount where the build ships the CA_Mount augmentation its name pairs with,
    /// which is where a mount's mechanics live. ItemID.Mount.CatPanther pairs with
    /// CA_Mount_CatPanther and is written; ItemID.Mount.DefaultHorse pairs with no augmentation the
    /// build ships, and it is one of the 43 mount tags that name no mount for that reason.
    /// </summary>
    [Fact]
    public void AMountPairingWithNoAugmentationIsNotWritten()
    {
        var mounts = Resolver.Tags
            .Where(tag => tag.Name.StartsWith("ItemID.Mount.", StringComparison.OrdinalIgnoreCase))
            .SelectMany(tag => Resolver.Resolve(tag, icons: null))
            .ToArray();

        var unpaired = mounts.Where(row => row.Type is null && !Branches.Contains(row.Name!)).ToArray();
        var written = mounts.Where(row => ExportAllCommand.NotWritten(row, Branches) is null).ToArray();

        Assert.Equal(43, unpaired.Length);
        Assert.Contains(unpaired, row => row.Name == "ItemID.Mount.DefaultHorse");
        Assert.Contains(written, row => row.Name == "ItemID.Mount.CatPanther");
        Assert.Equal(Export.Loaded.Documents.Count(d => d.Type == EntityType.Mount), written.Length);
    }

    /// <summary>
    /// A tag whose leaf segment is None or Invalid names a shipped enumeration's null member rather
    /// than an entity, and five tags in the build end that way. ItemID.CompanionPet.None is the one
    /// this rule alone keeps out: its second segment types it a companion pet, so every other rule
    /// writes it.
    /// </summary>
    [Fact]
    public void ATagNamingAnEnumerationsNullMemberIsNotWritten()
    {
        string[] shipped =
        [
            "ItemID.CompanionPet.None",
            "ItemID.GemType.Invalid",
            "ItemID.PlayerCustomization.Avatar.None",
            "ItemID.PlayerCustomization.Banner.None",
            "ItemID.PlayerCustomization.Title.None",
        ];

        var nullMembers = Resolver.Tags
            .Where(tag => tag.Name.EndsWith(".None", StringComparison.OrdinalIgnoreCase)
                || tag.Name.EndsWith(".Invalid", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Equal(shipped, nullMembers.Select(tag => tag.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.All(nullMembers, tag => Assert.Equal("a null enum member", NotWritten(tag)));

        var pet = Tag("ItemID", "ItemID.CompanionPet.None");

        Assert.Equal(EntityType.CompanionPet, Resolver.Resolve(pet, icons: null).Single().Type);
        Assert.DoesNotContain(
            Export.Loaded.Documents, d => d.Type == EntityType.CompanionPet && d.Id == pet.NetId);
    }

    /// <summary>
    /// The count for every type, against the spec's section 3.4. These move when the game moves,
    /// which is the point: a number changing here is the build changing rather than this code.
    /// </summary>
    [Theory]
    [InlineData(EntityType.Item, 5744)]
    [InlineData(EntityType.Effect, 3261)]
    [InlineData(EntityType.Ability, 2307)]
    [InlineData(EntityType.Creature, 232)]
    [InlineData(EntityType.Talent, 216)]
    [InlineData(EntityType.TalentSlot, 216)]
    [InlineData(EntityType.Mount, 96)]
    [InlineData(EntityType.Trait, 62)]
    [InlineData(EntityType.GemTrait, 60)]
    [InlineData(EntityType.MountPower, 54)]
    [InlineData(EntityType.TraitPower, 40)]
    [InlineData(EntityType.Currency, 32)]
    [InlineData(EntityType.GemTier, 24)]
    [InlineData(EntityType.SetBonus, 23)]
    [InlineData(EntityType.SetBonusPower, 15)]
    [InlineData(EntityType.Finesse, 14)]
    [InlineData(EntityType.WeaponPower, 12)]
    [InlineData(EntityType.RelicPower, 11)]
    [InlineData(EntityType.CompanionPet, 9)]
    [InlineData(EntityType.GemType, 6)]
    [InlineData(EntityType.LegendaryStat, 3)]
    public void TheCorpusHoldsTheseCounts(EntityType type, int expected) =>
        Assert.Equal(expected, Export.Loaded.Documents.Count(d => d.Type == type));

    [Fact]
    public void EveryHeroHasEighteenSlots()
    {
        var perHero = Export.Loaded.Documents
            .Where(d => d.Type == EntityType.TalentSlot)
            .GroupBy(d => d.Body["hero"]!.GetValue<string>())
            .ToArray();

        Assert.Equal(12, perHero.Length);
        Assert.All(perHero, hero => Assert.Equal(18, hero.Count()));
    }

    [Fact]
    public void SixtyGemTraitsTenPerType()
    {
        var traits = Export.Loaded.Documents.Where(d => d.Type == EntityType.GemTrait).ToArray();

        Assert.Equal(60, traits.Length);
        Assert.All(traits.GroupBy(t => t.Body["gem"]!.GetValue<string>()), type => Assert.Equal(10, type.Count()));
    }

    /// <summary>
    /// Every slot names a talent. 215 point at an augmentation; Talent8 carries Macabre Stratagem
    /// itself, so the talent for that slot is the slot row read as one.
    /// </summary>
    [Fact]
    public void TheSlotBorneTalentIsTheTwoHundredAndSixteenth()
    {
        var talent = Export.Loaded.Documents.Single(
            d => d.Type == EntityType.Talent && d.Name == "Macabre Stratagem");

        Assert.Equal(8, talent.Id);
    }

    /// <summary>
    /// A grant and a legendary stat line are augmentation rows already in the corpus, so an item
    /// points at them by their own id and nothing is minted.
    /// </summary>
    [Fact]
    public void AnItemPointsAtAugmentationsThatExist()
    {
        var ids = Export.Loaded.Documents.Select(d => d.Id).ToHashSet();

        var legendary = Export.Loaded.Documents
            .Where(d => d.Type == EntityType.Item && d.Body["legendaryStatIds"] is not null)
            .ToArray();

        Assert.Equal(37, legendary.Length);
        Assert.All(legendary, item => Assert.All(
            item.Body["legendaryStatIds"]!.AsArray(),
            stat => Assert.Contains(stat!.GetValue<int>(), ids)));

        // legendaryStatIds is built by mapping tag names through a lookup that returns null where
        // no tag names the asset, then dropping the nulls, so a silently dropped id would still
        // pass the assertions above while coming out shorter than the three the item actually has.
        Assert.All(legendary, item => Assert.Equal(3, item.Body["legendaryStatIds"]!.AsArray().Count));
    }

    [Fact]
    public void AnItemStatesARarityOnlyWhereTheBuildFixesIt()
    {
        var items = Export.Loaded.Documents.Where(d => d.Type == EntityType.Item).ToArray();
        var stated = items.Where(d => d.Body["rarity"] is not null).ToArray();

        Assert.Equal(37, stated.Length);
        Assert.All(stated, item => Assert.Equal("Legendary", item.Body["rarity"]!.GetValue<string>()));
        Assert.All(stated, item => Assert.NotNull(item.Body["legendaryStatIds"]));
    }
}
