using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record ItemTooltipRequest
{
    [Description("The id to draw.")]
    public int Id { get; init; }

    [Description("The hero whose reading of the id to draw, where one id reads differently per hero.")]
    public string? Hero { get; init; }

    [Description("The rung to read at, by the name the game prints or by tier.")]
    public string? Rarity { get; init; }

    [Range(0, 10_000)]
    [Description("The item level to compute stat magnitudes at, 0 to 10,000.")]
    public int? ItemLevel { get; init; }

    [Description("How many times the item has been tempered.")]
    public int? Upgrades { get; init; }

    [Description("The tempering ceiling to print in place of the one the shipped ladder states for this rung and level.")]
    public int? MaxUpgrades { get; init; }

    [Description("Given as true when the item's gem socket is to read as empty.")]
    public bool? NoSocket { get; init; }

    [Description("The gem id filling the item's socket.")]
    public int? Gem { get; init; }

    [Description("The power potential to print.")]
    public decimal? PotentialPower { get; init; }

    [Description("The armour to print in place of the one the tooltip computes from the item level.")]
    public decimal? Armor { get; init; }

    [Description(
        "The stats a random slot rolled, comma separated, named once per roll and without a magnitude, " +
        "which the tooltip computes. Rolling a stat the item already declares aggregates into that line.")]
    public RolledStats? RandomStats { get; init; }

    [Description(
        "A modifier the item carries as a kind token, an id and an amount separated by colons, " +
        "comma separated for several. The kinds are trait, blessing and essence.")]
    public ItemModifiers? Modifiers { get; init; }

    [Description("An item trait id, optionally a colon and the rank it sits at, comma separated for several.")]
    public ImbuedTraits? ImbuedTraits { get; init; }

    [Description("How many imbued trait slots are filled.")]
    public int? AllocatedTraits { get; init; }

    [Description("Given as true when the imbued traits line is to be drawn whatever slot the item sits in.")]
    public bool? HasImbuedTraits { get; init; }

    public TooltipQuery ToQuery(
        IQueryCollection raw, StatParameters stats, IReadOnlyList<string> rollable, out string? error)
    {
        var read = StatQuery.Read(this, raw, stats, out error);

        var rolled = error is null ? StatQuery.Rolled(RandomStats, stats, rollable, out error) : [];

        return read with
        {
            RandomStats = rolled,
            Hero = StatQuery.Given(Hero),
            Rarity = StatQuery.Given(Rarity),
            ItemLevel = ItemLevel,
            Upgrades = Upgrades,
            MaxUpgrades = MaxUpgrades,
            NoSocket = NoSocket == true,
            Gem = Gem,
            PotentialPower = PotentialPower,
            Armor = Armor,
            Modifiers = Modifiers?.Values ?? [],
            ImbuedTraits = ImbuedTraits?.Values ?? [],
            AllocatedTraits = AllocatedTraits,
            HasImbuedTraits = HasImbuedTraits,
        };
    }
}

public static class ItemTooltipEndpoint
{
    public static RouteHandlerBuilder MapItemTooltip(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}/tooltip", Handle)
            .WithName("GetItemTooltip")
            .WithSummary($"One {PublicTypes.Noun(EntityType.Item)}'s tooltip as a self-contained HTML fragment.")
            .WithStats()
            .Produces<string>(StatusCodes.Status200OK, "text/html")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static Task<IResult> Handle(
        [AsParameters] ItemTooltipRequest request,
        HttpContext http,
        ITooltipService tooltips,
        IEntityStoreService store)
    {
        var query = request.ToQuery(
            http.Request.Query, store.Stats, store.Settings.StatCalculation.RandomStatPool, out var error);

        return error is null
            ? tooltips.Get(EntityType.Item, request.Id, query)
            : Task.FromResult(StatQuery.Unreadable(error, store.Stats));
    }
}
