using System.Text.Json.Nodes;

namespace FellowshipExtractor.Tests;

/// <summary>
/// What the build says about haste, and the numbers Incinerate is the worked example for.
///
/// Two kinds of assertion live here and they fail for different reasons. Anchored ones name one
/// entity and one number, so they go red when that ability is retuned. Structural ones say
/// something that has to hold whatever the designers do, so they go red when the extractor stops
/// reading something rather than when the game changes.
///
/// A document names its tokens nowhere: the placeholder list carries the tokens' values without the
/// keys the sentences spend them under, and a document carries the name a reader is shown rather
/// than the name the build files the row under. The tests that turn on either of those read the
/// resolver, which is the same resolution the export is projected from.
/// </summary>
public sealed class HasteScalingTests
{
    private const int Incinerate = 1270;
    private const int FireBall = 1260;
    private const string ChannelInterval = "IncinerateChannelInterval";

    /// <summary>One document by the type and the id the export files it under.</summary>
    private static JsonObject Document(EntityType type, int id) =>
        Export.Loaded.Documents
            .Where(document => document.Type == type && document.Id == id)
            .Select(document => document.Body)
            .FirstOrDefault()
        ?? throw new InvalidOperationException(
            $"{type} {id} is not in the export. Either the id moved or the entity is gone.");

    /// <summary>One row resolved off the cooked build, for the facts a document does not carry.</summary>
    private static Entity Resolved(string table, int netId)
    {
        var tag = Export.Resolver.FindTag(table, netId.ToString())
            ?? throw new InvalidOperationException($"{table} {netId} is no longer a tag in the build.");

        return Export.Resolver.Resolve(tag, icons: null)[0];
    }

    /// <summary>
    /// The ability declares its times run with haste. Read from CRGameplayAbility's own
    /// ScaleTimeWithHaste, which is what separates the abilities a wearer's haste reaches.
    /// </summary>
    [Fact]
    public void IncinerateDeclaresHasteScaling() =>
        Assert.True(Document(EntityType.Ability, Incinerate)["scalesWithHaste"]?.GetValue<bool>());

    /// <summary>
    /// Its channel interval, which the tooltip prints twice: as the tick beside the cast time and
    /// again in the sentence. Both come from Firemage.ChanneledForwardAoeDamage.TickInterval.
    /// </summary>
    [Fact]
    public void IncinerateTicksEveryHalfSecond()
    {
        var ability = Document(EntityType.Ability, Incinerate);

        Assert.Equal(0.5, ability["channelTick"]?.GetValue<double>());
        Assert.Equal(0.5, ability["period"]?.GetValue<double>());
        Assert.Equal(2.5, ability["channelTime"]?.GetValue<double>());
        Assert.Equal(1.5, ability["castTime"]?.GetValue<double>());
    }

    /// <summary>
    /// The token that started all this. It names its own ability rather than an effect, so the only
    /// route to its number is the ability's own rows - and a row promoted into a named field has to
    /// stay reachable for it to be found at all.
    ///
    /// Asked of the resolver because a document's placeholder list holds the token's number with no
    /// key beside it, so nothing in the export says which entry this token is.
    /// </summary>
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

    /// <summary>
    /// Every token a description writes has somewhere to get its number from. This is the check
    /// that would have caught the original bug: the token existed, carried nothing, and the
    /// sentence printed its own placeholder at the reader.
    ///
    /// Asked of the resolver over every tag in the build, because pairing a written token with the
    /// entry answering it needs the key the export drops. The walk still descends into the granted
    /// ability, the legendary stat lines and each hero variant's own grant, which the export now
    /// writes as documents of their own.
    /// </summary>
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

    /// <summary>
    /// A tickrate that resolved to a number is one haste can act on. Structural rather than
    /// anchored: it does not care how many there are, only that the ones carrying a value still say
    /// what they are, because that flag is the only thing the API reads to decide.
    /// </summary>
    [Fact]
    public void ResolvedTickratesAreMarkedAsSuch()
    {
        var tickrates = Tokens()
            .Where(token => token["tickrate"]?.GetValue<bool>() == true)
            .ToArray();

        Assert.NotEmpty(tickrates);
        Assert.All(tickrates, token => Assert.NotNull(token["value"]));
    }

    /// <summary>
    /// Fire Ball's damage-over-time takes itself out of haste with
    /// EffectLogic.TickrateNotAffectedByHasteExclusively. That tag is an opt-out, and its existence
    /// is the whole argument that tickrates scale by default - if the build stopped shipping any,
    /// the reasoning behind the default would be gone and worth revisiting.
    ///
    /// The exempt set is read off the export. Which of its rows is the Fire Ball dot is asked of the
    /// build, because a document is named for the reader and the row is filed under an asset name no
    /// reader sees.
    /// </summary>
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

    /// <summary>
    /// Fire Ball still carries the coefficient its own tooltip is the worked example for. It shares
    /// an ability with the exempt effect above, so a change here says the pair moved together.
    ///
    /// Asked of the resolver for the same reason as the channel interval: the coefficient is in the
    /// export, and which token it belongs to is not.
    /// </summary>
    [Fact]
    public void FireBallKeepsItsCoefficient()
    {
        var direct = Resolved("GameplayAbility", FireBall).Placeholders?["FireBallDirect"];

        Assert.NotNull(direct);
        Assert.Equal(5.8482, direct!.Coefficient);
        Assert.Equal("Intellect", direct.Attribute);
    }

    /// <summary>Every token every document carries, which is where the tokens' numbers are.</summary>
    private static IEnumerable<JsonObject> Tokens() =>
        Export.Loaded.Documents
            .Select(document => document.Body["placeholders"])
            .OfType<JsonArray>()
            .SelectMany(tokens => tokens)
            .OfType<JsonObject>();

    /// <summary>
    /// Every row the build resolves to and everything nested inside one. The export writes the
    /// nested rows as documents of their own, so this is the same set of rows reached by the route
    /// that still carries their tokens' keys.
    /// </summary>
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

        // A variant is one hero's reading of an item rather than an entity of its own, and the
        // ability it grants is where the tokens live.
        foreach (var variant in entity.Variants ?? [])
        {
            if (variant.Grants is { } grantedToVariant) yield return grantedToVariant;
        }
    }
}
