namespace FellowshipExtractor.Tests;

/// <summary>
/// What the build says about haste, and the numbers Incinerate is the worked example for.
///
/// Two kinds of assertion live here and they fail for different reasons. Anchored ones name one
/// entity and one number, so they go red when that ability is retuned. Structural ones say
/// something that has to hold whatever the designers do, so they go red when the extractor stops
/// reading something rather than when the game changes.
/// </summary>
public sealed class HasteScalingTests
{
    private const int Incinerate = 1270;
    private const int FireBall = 1260;
    private const string ChannelInterval = "IncinerateChannelInterval";

    /// <summary>
    /// The ability declares its times run with haste. Read from CRGameplayAbility's own
    /// ScaleTimeWithHaste, which is what separates the abilities a wearer's haste reaches.
    /// </summary>
    [Fact]
    public void IncinerateDeclaresHasteScaling() =>
        Assert.True(Export.Loaded.Entity("GameplayAbility", Incinerate).ScalesWithHaste);

    /// <summary>
    /// Its channel interval, which the tooltip prints twice: as the tick beside the cast time and
    /// again in the sentence. Both come from Firemage.ChanneledForwardAoeDamage.TickInterval.
    /// </summary>
    [Fact]
    public void IncinerateTicksEveryHalfSecond()
    {
        var entity = Export.Loaded.Entity("GameplayAbility", Incinerate);

        Assert.Equal(0.5, entity.ChannelTick);
        Assert.Equal(0.5, entity.Period);
        Assert.Equal(2.5, entity.ChannelTime);
        Assert.Equal(1.5, entity.CastTime);
    }

    /// <summary>
    /// The token that started all this. It names its own ability rather than an effect, so the only
    /// route to its number is the ability's own rows - and a row promoted into a named field has to
    /// stay reachable for it to be found at all.
    /// </summary>
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

    /// <summary>
    /// Every token a description writes has somewhere to get its number from. This is the check
    /// that would have caught the original bug: the token existed, carried nothing, and the
    /// sentence printed its own placeholder at the reader.
    /// </summary>
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

    /// <summary>
    /// A tickrate that resolved to a number is one haste can act on. Structural rather than
    /// anchored: it does not care how many there are, only that the ones carrying a value still say
    /// what they are, because that flag is the only thing the API reads to decide.
    /// </summary>
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

    /// <summary>
    /// Fire Ball's damage-over-time takes itself out of haste with
    /// EffectLogic.TickrateNotAffectedByHasteExclusively. That tag is an opt-out, and its existence
    /// is the whole argument that tickrates scale by default - if the build stopped shipping any,
    /// the reasoning behind the default would be gone and worth revisiting.
    /// </summary>
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

    /// <summary>
    /// Fire Ball still carries the coefficient its own tooltip is the worked example for. It shares
    /// an ability with the exempt effect above, so a change here says the pair moved together.
    /// </summary>
    [Fact]
    public void FireBallKeepsItsCoefficient()
    {
        var direct = Export.Loaded.Entity("GameplayAbility", FireBall).Placeholders?["FireBallDirect"];

        Assert.NotNull(direct);
        Assert.Equal(5.8482, direct!.Coefficient);
        Assert.Equal("Intellect", direct.Attribute);
    }
}
