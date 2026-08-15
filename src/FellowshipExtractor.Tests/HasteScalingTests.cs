using System.Text.Json.Nodes;

namespace FellowshipExtractor.Tests;

public sealed class HasteScalingTests
{
    private const int Incinerate = 1270;
    private const int FireBall = 1260;
    private const int Chronoshift = 355;
    private const int StoneShield = 1122;
    private const int HeavyStrike = 1929;
    private const int SweepingStrike = 981;
    private const int TemporalBarrage = 1926;
    private const string ChannelInterval = "IncinerateChannelInterval";
    private const string PulseInterval = "AshaTickPeriod";
    private const string ShieldStrength = "StoneShieldHealth";
    private const string BarrageDamage = "TemporBarrDmg";

    private static JsonObject Document(EntityType type, int id) =>
        Export.Loaded.Documents
            .Where(document => document.Type == type && document.Id == id)
            .Select(document => document.Body)
            .FirstOrDefault()
        ?? throw new InvalidOperationException(
            $"{type} {id} is not in the export. Either the id moved or the entity is gone.");

    private static Entity Resolved(string table, int netId)
    {
        var tag = Export.Resolver.FindTag(table, netId.ToString())
            ?? throw new InvalidOperationException($"{table} {netId} is no longer a tag in the build.");

        return Export.Resolver.Resolve(tag, icons: null)[0];
    }

    [Fact]
    public void IncinerateDeclaresHasteScaling() =>
        Assert.True(Document(EntityType.Ability, Incinerate)["scalesWithHaste"]?.GetValue<bool>());

    [Fact]
    public void IncinerateTicksEveryHalfSecond()
    {
        var ability = Document(EntityType.Ability, Incinerate);

        Assert.Equal(0.5, ability["channelTick"]?.GetValue<double>());
        Assert.Equal(0.5, ability["period"]?.GetValue<double>());
        Assert.Equal(2.5, ability["channelTime"]?.GetValue<double>());
        Assert.Equal(1.5, ability["castTime"]?.GetValue<double>());
    }

    [Fact]
    public void ChannelIntervalTokenResolves()
    {
        var placeholders = Resolved("GameplayAbility", Incinerate).Placeholders;

        Assert.NotNull(placeholders);
        Assert.True(placeholders!.ContainsKey(ChannelInterval), $"{ChannelInterval} is no longer a token.");

        var token = placeholders[ChannelInterval];

        Assert.Equal(0.5, token.Value);
        Assert.True(token.Tickrate, "The token no longer reports itself as a tickrate, so haste will not reach it.");
        Assert.NotEqual(true, token.IgnoresHaste);
    }

    [Fact]
    public void ChronoshiftPulsesEveryChannelTick()
    {
        var power = Document(EntityType.WeaponPower, Chronoshift);

        Assert.Equal(1.5, power["channelTick"]?.GetValue<double>());
    }

    [Fact]
    public void ChronoshiftPulseIntervalTokenResolves()
    {
        var placeholders = Resolved("CRCharacterAugmentationAsset", Chronoshift).Placeholders;

        Assert.NotNull(placeholders);
        Assert.True(placeholders!.ContainsKey(PulseInterval), $"{PulseInterval} is no longer a token.");

        var token = placeholders[PulseInterval];

        Assert.Equal(1.5, token.Value);
        Assert.True(token.Tickrate, "The token no longer reports itself as a tickrate, so haste will not reach it.");
        Assert.NotEqual(true, token.IgnoresHaste);
    }

    [Fact]
    public void SweepingStrikeTellsItsTwoTokensApartByTheIdentifierTheyCarry()
    {
        var placeholders = Resolved("GameplayAbility", SweepingStrike).Placeholders;

        Assert.NotNull(placeholders);

        Assert.Equal(6, placeholders!["BloodRushDuration"].Value);
        Assert.Equal(0.05, placeholders["BloodRushHeal"].Value);
    }

    [Fact]
    public void HeavyStrikeSeparatesItsThreeDamageTokens()
    {
        var placeholders = Resolved("GameplayAbility", HeavyStrike).Placeholders;

        Assert.NotNull(placeholders);

        Assert.Equal(5.66, placeholders!["HeavyStrikeDmg"].Coefficient);
        Assert.Equal(1.698, placeholders["HeavyStrikeClvDmg"].Coefficient);
        Assert.Equal(1.98, placeholders["HeavyStrikeLightnDmg"].Coefficient);

        Assert.All(
            new[] { "HeavyStrikeDmg", "HeavyStrikeClvDmg", "HeavyStrikeLightnDmg" },
            key => Assert.Equal("Strength", placeholders[key].Attribute));
    }

    [Fact]
    public void TheRangeFunctionAgreesWithTheEffectRouteOnFireBall()
    {
        var direct = Resolved("GameplayAbility", FireBall).Placeholders?["FireBallDirect"];

        Assert.NotNull(direct);
        Assert.Equal(5.8482, direct!.Coefficient);
        Assert.Equal("Intellect", direct.Attribute);
        Assert.Equal(0.2, direct.Spread);
        Assert.True(direct.ScalesWithExpertise);
    }

    [Fact]
    public void SelfNamingTokensReadTheArithmeticTheirBodyWrapsTheReadIn()
    {
        var strength = Resolved("GameplayAbility", StoneShield).Placeholders?[ShieldStrength];
        var damage = Resolved("GameplayAbility", TemporalBarrage).Placeholders?[BarrageDamage];

        Assert.NotNull(strength);
        Assert.NotNull(damage);

        Assert.Null(strength!.Value);
        Assert.Equal(8.624, strength.Coefficient);
        Assert.Equal("Agility", strength.Attribute);
        Assert.Equal(-1, strength.Addend);
        Assert.Equal(
            new[] { "CritChance", "Expertise", "Haste", "Spirit" },
            strength.Terms?.Select(term => term.Attribute).ToArray());

        Assert.Null(damage!.Value);
        Assert.Equal(1.91, damage.Coefficient);
        Assert.Equal("Intellect", damage.Attribute);
    }

    [Fact]
    public void EveryTokenWrittenInADescriptionHasAPlaceholder()
    {
        var orphans = new List<string>();

        foreach (var entity in Corpus())
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
        var tickrates = Tokens()
            .Where(token => token["tickrate"]?.GetValue<bool>() == true)
            .ToArray();

        Assert.NotEmpty(tickrates);
        Assert.All(tickrates, token => Assert.NotNull(token["value"]));
    }

    [Fact]
    public void TheHasteOptOutIsStillShipped()
    {
        var exempt = Export.Loaded.Documents
            .Where(document => document.Body["tickrateIgnoresHaste"]?.GetValue<bool>() == true)
            .ToArray();

        Assert.True(
            exempt.Length > 0,
            "No effect declares EffectLogic.TickrateNotAffectedByHasteExclusively any more. Either the " +
            "tag is gone from the build or it stopped being read, and the tickrate default rests on it.");

        var dot = Export.Resolver
            .FindTag("GameplayEffect", "GE_Firemage_PlacedProjectileAoe_DirectDamageBased_Dot")?.NetId;

        Assert.Contains(exempt, e => e.Type == EntityType.Effect && e.Id == dot);
    }

    [Fact]
    public void FireBallKeepsItsCoefficient()
    {
        var direct = Resolved("GameplayAbility", FireBall).Placeholders?["FireBallDirect"];

        Assert.NotNull(direct);
        Assert.Equal(5.8482, direct!.Coefficient);
        Assert.Equal("Intellect", direct.Attribute);
    }

    private static IEnumerable<JsonObject> Tokens() =>
        Export.Loaded.Documents
            .Select(document => document.Body["placeholders"])
            .OfType<JsonArray>()
            .SelectMany(tokens => tokens)
            .OfType<JsonObject>();

    private static IEnumerable<Entity> Corpus()
    {
        foreach (var tag in Export.Resolver.Tags)
        {
            foreach (var row in Export.Resolver.Resolve(tag, icons: null))
            {
                foreach (var reached in Reachable(row)) yield return reached;
            }
        }
    }

    private static IEnumerable<Entity> Reachable(Entity entity)
    {
        yield return entity;

        foreach (var nested in Nested(entity))
        {
            foreach (var reached in Reachable(nested)) yield return reached;
        }
    }

    private static IEnumerable<Entity> Nested(Entity entity)
    {
        if (entity.Grants is { } granted) yield return granted;

        foreach (var stat in entity.LegendaryStats ?? []) yield return stat;

        foreach (var variant in entity.Variants ?? [])
        {
            if (variant.Grants is { } grantedToVariant) yield return grantedToVariant;
        }
    }
}
