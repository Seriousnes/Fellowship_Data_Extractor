using FellowshipExtractor.Tooltip;
using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Tests;

public sealed class DocumentTests
{
    [Fact]
    public void ADocumentDoesNotRestateItsType()
    {
        var documents = typeof(EntityDocument).Assembly
            .GetTypes()
            .Where(type => type.IsSubclassOf(typeof(EntityDocument)))
            .ToArray();

        Assert.Equal(Enum.GetValues<EntityType>().Length, documents.Length);

        Assert.All(documents, document => Assert.True(
            document.Name.EndsWith("Document", StringComparison.Ordinal) &&
            Enum.TryParse<EntityType>(document.Name[..^"Document".Length], out _)));

        Assert.All(documents, document => Assert.DoesNotContain(
            document.GetProperties(),
            property => property.Name is "Type" or "Kind"));
    }

    [Fact]
    public void AFieldEveryRowStatesIsRequired()
    {
        Assert.All(
            new[] { "Gem", "Tier", "Power", "Rarity" },
            name => Assert.True(IsRequired(typeof(GemDocument).GetProperty(name)!)));

        Assert.All(
            new[] { "Hero", "Row", "Column", "Cost", "TalentId" },
            name => Assert.True(IsRequired(typeof(TalentSlotDocument).GetProperty(name)!)));
    }

    [Fact]
    public void SchoolsAreTheDamageTagsSpacedOut()
    {
        Assert.Equal("Magic / Fire", DocumentProjection.Spaced("Magic.Fire"));
        Assert.Equal("Physical / Bleed", DocumentProjection.Spaced("Physical.Bleed"));
        Assert.Equal("Damage Scale", DocumentProjection.Spaced("DamageScale"));
    }

    [Fact]
    public void EveryTypeHasASlugOfItsOwn()
    {
        var slugs = Enum.GetValues<EntityType>().Select(EntityTypes.Slug).ToArray();
        Assert.Equal(slugs.Length, slugs.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void ASlugParsesRegardlessOfCase()
    {
        Assert.True(EntityTypes.TryParse("Talent", out var single));
        Assert.Equal(EntityType.Talent, single);

        Assert.True(EntityTypes.TryParse("SET-BONUS-POWER", out var shouted));
        Assert.Equal(EntityType.SetBonusPower, shouted);

        Assert.True(EntityTypes.TryParse("set-bonus-power", out var exact));
        Assert.Equal(EntityType.SetBonusPower, exact);

        Assert.False(EntityTypes.TryParse("SetBonusPower", out _));
    }

    [Fact]
    public void ADocumentsRuntimeClassIsItsType()
    {
        Assert.Equal(EntityType.Ability, EntityTypes.TypeOf(new AbilityDocument { Id = 1 }));
        Assert.Equal(EntityType.GemType, EntityTypes.TypeOf(
            new GemTypeDocument { Id = 1, Type = GemType.Ruby, Attribute = "RubyPower" }));
        Assert.Equal(EntityType.TalentSlot, EntityTypes.TypeOf(
            new TalentSlotDocument { Id = 8, Hero = Hero.Mara, Row = 4, Column = 1, Cost = 1, TalentId = 8 }));
    }

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

        var ability = (AbilityDocument)DocumentProjection.From(entity, (_, _) => null);

        Assert.Equal(["Bravo", "Alpha"], ability.Placeholders?.Select(token => token.Key));
    }

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

        var ability = (AbilityDocument)DocumentProjection.From(entity, (_, _) => null);

        Assert.Null(ability.Name);
    }

    [Fact]
    public void ANameWrittenAsWordsIsKept()
    {
        var entity = new Entity { Type = EntityType.Ability, NetId = 1, DisplayName = "Searing Blaze" };

        var ability = (AbilityDocument)DocumentProjection.From(entity, (_, _) => null);

        Assert.Equal("Searing Blaze", ability.Name);
    }

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

        var talent = (TalentDocument)DocumentProjection.Implied(entity)!;

        Assert.Null(talent.Name);
    }

    private static bool IsRequired(System.Reflection.PropertyInfo property) =>
        property.GetCustomAttributes(typeof(System.Runtime.CompilerServices.RequiredMemberAttribute), false).Length > 0;
}
