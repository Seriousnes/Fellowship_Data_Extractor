namespace FellowshipExtractor.Tests;

public sealed class HasteScalingTests
{
    private const int Incinerate = 1270;
    private const int FireBall = 1260;
    private const string ChannelInterval = "IncinerateChannelInterval";

    [Fact]
    public void IncinerateDeclaresHasteScaling() =>
        Assert.True(Export.Loaded.Entity("GameplayAbility", Incinerate).ScalesWithHaste);

    [Fact]
    public void IncinerateTicksEveryHalfSecond()
    {
        var entity = Export.Loaded.Entity("GameplayAbility", Incinerate);

        Assert.Equal(0.5, entity.ChannelTick);
        Assert.Equal(0.5, entity.Period);
        Assert.Equal(2.5, entity.ChannelTime);
        Assert.Equal(1.5, entity.CastTime);
    }

    [Fact]
    public void ChannelIntervalTokenResolves()
    {
        var placeholders = Export.Loaded.Entity("GameplayAbility", Incinerate).Placeholders;

        Assert.NotNull(placeholders);
        Assert.True(placeholders!.ContainsKey(ChannelInterval), $"{ChannelInterval} is no longer a token.");

        var token = placeholders[ChannelInterval];

        Assert.Equal(0.5, token.Value);
        Assert.True(token.Tickrate, "The token no longer reports itself as a tickrate, so haste will not reach it.");
        Assert.NotEqual(true, token.IgnoresHaste);
    }

    [Fact]
    public void EveryTokenWrittenInADescriptionHasAPlaceholder()
    {
        var orphans = new List<string>();

        foreach (var entity in Export.Loaded.All())
        {
            if (entity.Description is not { } description) continue;

            foreach (var token in Tooltip.DescriptionTokens.Tokens(description))
            {
                if (entity.Placeholders?.ContainsKey(token) == true) continue;
                orphans.Add($"{entity.Table}:{entity.NetId} {entity.Name} -> {{{token}}}");
            }
        }

        Assert.True(orphans.Count == 0, $"Tokens with no placeholder entry:\n{string.Join("\n", orphans.Take(20))}");
    }

    [Fact]
    public void ResolvedTickratesAreMarkedAsSuch()
    {
        var tickrates = Export.Loaded.All()
            .Where(e => e.Placeholders is not null)
            .SelectMany(e => e.Placeholders!)
            .Where(p => p.Value.Tickrate == true)
            .ToArray();

        Assert.NotEmpty(tickrates);
        Assert.All(tickrates, p => Assert.NotNull(p.Value.Value));
    }

    [Fact]
    public void TheHasteOptOutIsStillShipped()
    {
        var exempt = Export.Loaded.All().Where(e => e.TickrateIgnoresHaste == true).ToArray();

        Assert.True(
            exempt.Length > 0,
            "No effect declares EffectLogic.TickrateNotAffectedByHasteExclusively any more. Either the " +
            "tag is gone from the build or it stopped being read, and the tickrate default rests on it.");

        Assert.Contains(exempt, e => e.Name == "GE_Firemage_PlacedProjectileAoe_DirectDamageBased_Dot");
    }

    [Fact]
    public void FireBallKeepsItsCoefficient()
    {
        var direct = Export.Loaded.Entity("GameplayAbility", FireBall).Placeholders?["FireBallDirect"];

        Assert.NotNull(direct);
        Assert.Equal(5.8482, direct!.Coefficient);
        Assert.Equal("Intellect", direct.Attribute);
    }
}
