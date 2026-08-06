using FellowshipExtractor.Tooltip;
using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Tests;

public sealed class DocumentTests
{
    /// <summary>
    /// The class is the type. A property naming it would be a second answer that can disagree, and
    /// camel-cased it collides with the discriminator the line already leads with, which
    /// <c>JsonObject.Insert</c> throws on part way through a write.
    ///
    /// Every subclass, found by reflection rather than listed, because the base declares only Id and
    /// Name and the collision is always a subclass's own property.
    /// </summary>
    [Fact]
    public void ADocumentDoesNotRestateItsType()
    {
        var documents = typeof(EntityDocument).Assembly
            .GetTypes()
            .Where(type => type.IsSubclassOf(typeof(EntityDocument)))
            .ToArray();

        // One document class per type, so a test finding none would pass by finding nothing.
        Assert.Equal(Enum.GetValues<EntityType>().Length, documents.Length);

        Assert.All(documents, document => Assert.DoesNotContain(
            document.GetProperties(),
            property => property.Name is "Type" or "Kind"));
    }

    /// <summary>
    /// A field every row of a type states is required. All 24 gem tiers state a type, a tier, a
    /// power and a rarity, and all 216 talent slots state a hero, a position and a cost.
    /// </summary>
    [Fact]
    public void AFieldEveryRowStatesIsRequired()
    {
        Assert.All(
            new[] { "Gem", "Tier", "Power", "Rarity" },
            name => Assert.True(IsRequired(typeof(GemTier).GetProperty(name)!)));

        Assert.All(
            new[] { "Hero", "Row", "Column", "Cost", "TalentId" },
            name => Assert.True(IsRequired(typeof(TalentSlot).GetProperty(name)!)));
    }

    [Fact]
    public void SchoolsAreTheDamageTagsSpacedOut()
    {
        Assert.Equal("Magic / Fire", DocumentProjection.Spaced("Magic.Fire"));
        Assert.Equal("Physical / Bleed", DocumentProjection.Spaced("Physical.Bleed"));
        Assert.Equal("Damage Scale", DocumentProjection.Spaced("DamageScale"));
    }

    /// <summary>
    /// A slug addresses one type. Slug is derived from the member name, so two members differing
    /// only in where their capitals fall would collide and one of them would be unreachable.
    /// </summary>
    [Fact]
    public void EveryTypeHasASlugOfItsOwn()
    {
        var slugs = Enum.GetValues<EntityType>().Select(EntityTypes.Slug).ToArray();
        Assert.Equal(slugs.Length, slugs.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void ADocumentsRuntimeClassIsItsType()
    {
        Assert.Equal(EntityType.Ability, DocumentProjection.TypeOf(new Ability { Id = 1 }));
        Assert.Equal(EntityType.GemType, DocumentProjection.TypeOf(
            new GemType { Id = 1, Gem = GemTypeName.Ruby, Attribute = "RubyPower" }));
        Assert.Equal(EntityType.TalentSlot, DocumentProjection.TypeOf(
            new TalentSlot { Id = 8, Hero = Hero.Mara, Row = 4, Column = 1, Cost = 1, TalentId = 8 }));
    }

    /// <summary>
    /// The dictionary files Alpha before Bravo, but the description spends Bravo first. The projected
    /// list follows the text rather than the dictionary, and each entry carries the key the text
    /// spelled it under.
    /// </summary>
    [Fact]
    public void PlaceholdersAreOrderedByWhereTheTextSpendsThemAndCarryTheirKey()
    {
        var entity = new Entity
        {
            Type = EntityType.Ability,
            NetId = 1,
            Description = "Deals {Bravo} damage, then {Alpha} healing.",
            Placeholders = new Dictionary<string, Placeholder>
            {
                ["Alpha"] = new Placeholder { Value = 1 },
                ["Bravo"] = new Placeholder { Value = 2 },
            },
        };

        var ability = (Ability)DocumentProjection.From(entity, (_, _) => null);

        Assert.Equal(["Bravo", "Alpha"], ability.Placeholders?.Select(token => token.Key));
    }

    /// <summary>
    /// A shipped name that is a build label projects no <see cref="EntityDocument.Name"/>. The
    /// eighteen spellings here are exactly the build labels the current export carries: three
    /// characters and one join, each failing <c>DocumentProjection</c>'s words test a different way.
    /// </summary>
    [Theory]
    [InlineData("UB_CadavrianBannerman.WarHorn")]
    [InlineData("AutoAttack")]
    [InlineData("CastProjectile")]
    [InlineData("CastAndImpact")]
    [InlineData("AimedShot")]
    [InlineData("EarthStomp")]
    [InlineData("EarthBreaker")]
    [InlineData("KillShot")]
    [InlineData("MeleeAutoAttack")]
    [InlineData("ChannelProjectile")]
    [InlineData("AoE Heal")]
    [InlineData("AOE Pools (RuinsLasher)")]
    [InlineData("ArcaneBlast (Elf Warden)")]
    [InlineData("EarthStomp (EarthElemental)")]
    [InlineData("[PH - StarMap Token]")]
    [InlineData("Playtest GearToken")]
    [InlineData("Playtest GemToken")]
    [InlineData("Playtest ResourceToken")]
    public void ABuildLabelProjectsNoName(string label)
    {
        var entity = new Entity { Type = EntityType.Ability, NetId = 1, DisplayName = label };

        var ability = (Ability)DocumentProjection.From(entity, (_, _) => null);

        Assert.Null(ability.Name);
    }

    /// <summary>A name written as words is kept, so the test above is not simply dropping every name.</summary>
    [Fact]
    public void ANameWrittenAsWordsIsKept()
    {
        var entity = new Entity { Type = EntityType.Ability, NetId = 1, DisplayName = "Searing Blaze" };

        var ability = (Ability)DocumentProjection.From(entity, (_, _) => null);

        Assert.Equal("Searing Blaze", ability.Name);
    }

    /// <summary>
    /// The talent a slot-borne row implies goes through the same test, because it is a full document
    /// in its own right and not a field nested inside another: <c>ExportAllCommand</c> writes it as
    /// its own line, addressed by the slot's own id.
    /// </summary>
    [Fact]
    public void ASlotBorneTalentsNameGoesThroughTheSameTest()
    {
        var entity = new Entity
        {
            Type = EntityType.TalentSlot,
            NetId = 8,
            SlotBorneTalent = true,
            DisplayName = "AutoAttack",
        };

        var talent = (Fellowship.SDK.Documents.Talent)DocumentProjection.Implied(entity)!;

        Assert.Null(talent.Name);
    }

    private static bool IsRequired(System.Reflection.PropertyInfo property) =>
        property.GetCustomAttributes(typeof(System.Runtime.CompilerServices.RequiredMemberAttribute), false).Length > 0;
}
