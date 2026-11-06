using System.Text.Json.Nodes;

namespace FellowshipExtractor.Tests;

/// <summary>An effect applied through a <c>CRLinkedEffectComponent</c> names its parent effect and gating talent.</summary>
public sealed class LinkedEffectTests
{
    private const int SolarShield = 2551;
    private const int Invictus = 604;
    private const int CasterBuff = 2719;
    private const int TargetBuff = 2720;

    private static JsonObject Effect(int id) =>
        Export.Loaded.Documents.Single(document => document.Type == EntityType.Effect && document.Id == id).Body;

    [Theory]
    [InlineData(CasterBuff, "Source")]
    [InlineData(TargetBuff, "Target")]
    public void InvictusBuffsNameSolarShieldTheReceiverAndTheTalent(int child, string receiver)
    {
        var effect = Effect(child);
        var link = Assert.Single(effect["linkedBy"]!.AsArray())!;

        Assert.Equal("effect", link["parent"]!["type"]!.GetValue<string>());
        Assert.Equal(SolarShield, link["parent"]!["id"]!.GetValue<int>());
        Assert.Equal(receiver, link["applyTo"]!.GetValue<string>());
        Assert.Equal(Invictus, link["talent"]!["id"]!.GetValue<int>());
        Assert.Equal(SolarShield, effect["appliedBy"]!["id"]!.GetValue<int>());
        Assert.Equal("effect", effect["appliedBy"]!["type"]!.GetValue<string>());
    }

    [Fact]
    public void TheInvictusCasterBuffKeepsItsHeroAndIsInfinite()
    {
        var effect = Effect(CasterBuff);

        Assert.Equal(["Xavian"], effect["heroes"]!.AsArray().Select(hero => hero!.GetValue<string>()));
        Assert.True(effect["infinite"]!.GetValue<bool>());
    }

    [Fact]
    public void AChildAnAbilityAlsoAppliesKeepsThatAbilityAsItsApplier()
    {
        var appliers = Export.Loaded.Documents
            .Where(document => document.Type == EntityType.Effect && document.Body["linkedBy"] is not null)
            .Select(document => document.Body["appliedBy"]?["type"]?.GetValue<string>())
            .ToHashSet();

        Assert.Equal(new HashSet<string?> { "ability", "effect", null }, appliers);
    }

    [Fact]
    public void ALinkBehindATagNoTalentHoldsKeepsTheTag()
    {
        var links = Export.Loaded.Documents
            .Where(document => document.Type == EntityType.Effect)
            .SelectMany(document => document.Body["linkedBy"]?.AsArray() ?? [])
            .Where(link => link!["requiredSourceTags"] is not null)
            .ToArray();

        Assert.NotEmpty(links);
        Assert.All(links, link => Assert.Null(link!["talent"]));
    }
}
