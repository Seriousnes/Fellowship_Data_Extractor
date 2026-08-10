using System.Text.Json.Nodes;

using FellowshipExtractor.Tooltip;

namespace FellowshipExtractor.Tests;

public sealed class AbilityOwnerTests
{
    private const string SharedSpirit = "GA_Shared_Passive_SpiritMechanic";
    private const string Interact = "GA_Interact";
    private const string Interrupt = "GA_Warmaster_MeleeInterrupt";
    private const string LockoutEffect = "GE_Warmaster_MeleeInterrupt_Lockout";
    private const string InterruptEffect = "GE_Warmaster_MeleeInterrupt_Interrupt";
    private const string BossChilgar = "BP_NPC_VV_BossChilgar";

    [Fact]
    public void TwelveHeroesOwnAnAbilityAndGundeIsOneOfThem()
    {
        var owners = Export.Resolver.Owners;

        var claimed = Export.Resolver.Tags
            .Where(tag => tag.Table.Equals("GameplayAbility", StringComparison.OrdinalIgnoreCase))
            .SelectMany(tag => owners.HeroesOf(tag.Name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(12, claimed.Length);
        Assert.Contains("Gunde", claimed, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnAbilityTheUniversalSetGrantsCarriesNoHeroOfItsOwn()
    {
        Assert.Equal(6, Export.Resolver.Owners.SharedAbilities);
        Assert.Empty(Export.Resolver.Owners.HeroesOf(Interact));
    }

    [Fact]
    public void TheSpiritMechanicIsOwnedByEveryHeroThatSlotsIt()
    {
        Assert.Equal(12, Export.Resolver.Owners.HeroesOf(SharedSpirit).Count);
    }

    [Fact]
    public void ABossOwnsTheAbilitiesItsBehaviourComponentNames()
    {
        var owned = Export.Resolver.Tags
            .Where(tag => tag.Table.Equals("GameplayAbility", StringComparison.OrdinalIgnoreCase))
            .Where(tag => BossChilgar.Equals(Export.Resolver.Owners.CreatureOf(tag.Name), StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(owned);
    }

    [Fact]
    public void AnAbilityReachesTheEffectsOnlyItsBytecodeNames()
    {
        var walked = Export.Resolver.Owners;

        foreach (var effect in (string[])[LockoutEffect, InterruptEffect])
        {
            Assert.Contains("Warmaster", walked.HeroesOf(effect), StringComparer.OrdinalIgnoreCase);
        }

        Assert.Contains("Warmaster", walked.HeroesOf(Interrupt), StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnEffectManyOwnersReachCarriesNoneOfThem()
    {
        var owners = Export.Resolver.Owners;

        var contested = Export.Resolver.Tags
            .Where(tag => tag.Table.Equals("GameplayEffect", StringComparison.OrdinalIgnoreCase))
            .Count(tag => owners.CreatureOf(tag.Name) == AbilityOwners.Contested);

        Assert.True(contested > 0, "No effect is reached by two creatures, so nothing exercises the contest rule.");
    }

    [Fact]
    public void TheApplierGraphReachesMoreEffectsThanTheAbilitiesThemselves()
    {
        var owners = Export.Resolver.Owners;

        var effects = Export.Resolver.Tags
            .Where(tag => tag.Table.Equals("GameplayEffect", StringComparison.OrdinalIgnoreCase))
            .Count(tag => owners.HeroesOf(tag.Name).Count > 0 || owners.CreatureOf(tag.Name) is { Length: > 0 });

        Assert.True(effects > 1500, $"Only {effects} of 3261 effects reach an owner.");
    }

    [Fact]
    public void AnAbilityStatesTheHeroesThatOwnItInTheExport()
    {
        var owned = Export.Loaded.Documents
            .Where(document => document.Type == EntityType.Ability && document.Body["heroes"] is JsonArray)
            .ToArray();

        Assert.NotEmpty(owned);
    }

    [Fact]
    public void AnEffectStatesTheCreatureThatOwnsItInTheExport()
    {
        var owned = Export.Loaded.Documents
            .Where(document => document.Type == EntityType.Effect && document.Body["owner"] is JsonObject)
            .ToArray();

        Assert.NotEmpty(owned);
    }

    [Fact]
    public void AnEffectStatesTheAbilityThatAppliesIt()
    {
        Assert.Equal(Interrupt, Export.Resolver.Owners.ApplierOf(LockoutEffect));
        Assert.Equal(Interrupt, Export.Resolver.Owners.ApplierOf(InterruptEffect));
    }

    [Fact]
    public void AnEffectSeveralAbilitiesApplyStatesNoApplierAtAll()
    {
        var owners = Export.Resolver.Owners;

        var contested = Export.Resolver.Tags
            .Where(tag => tag.Table.Equals(EffectTable, StringComparison.OrdinalIgnoreCase))
            .Count(tag => owners.ApplierOf(tag.Name) is { Length: 0 });

        Assert.Equal(203, contested);
        Assert.Equal(1884, owners.AppliedEffectCount);
    }

    [Fact]
    public void AnEffectBorrowsTheSchoolsOfTheAbilityThatAppliesIt()
    {
        var owners = Export.Resolver.Owners;

        Assert.Contains("Physical", owners.AbilityTagsOf(Interrupt), StringComparer.Ordinal);

        var schooled = Export.Resolver.Tags
            .Where(tag => tag.Table.Equals(EffectTable, StringComparison.OrdinalIgnoreCase))
            .Select(tag => owners.ApplierOf(tag.Name))
            .Where(applier => applier is { Length: > 0 })
            .Count(applier => owners.AbilityTagsOf(applier!).Any(Schooled));

        Assert.Equal(1001, schooled);
    }

    private const string EffectTable = "GameplayEffect";

    private static bool Schooled(string tag) =>
        tag.StartsWith("Magic", StringComparison.Ordinal) ||
        tag.StartsWith("Physical", StringComparison.Ordinal);
}
