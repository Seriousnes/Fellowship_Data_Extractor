using System.ComponentModel;

using Fellowship.SDK.Client;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record ItemRecordRequest
{
    [Description("The Item id.")]
    public int Id { get; init; }

    [Description("Hero variant of the Item.")]
    public string? Hero { get; init; }

    [Description(ItemReading.ItemLevel)]
    public int? ItemLevel { get; init; }

    [Description(ItemReading.Upgrades)]
    public int? Upgrades { get; init; }

    [Description(ItemReading.Rarity)]
    public string? Rarity { get; init; }

    public RecordReading ToReading() =>
        new() { Hero = Hero, ItemLevel = ItemLevel, Upgrades = Upgrades, Rarity = StatQuery.NullIfEmpty(Rarity) };
}

public sealed record ItemListRequest : RecordListRequest
{
    [Description("The hero to list the items of. Each row is that hero's reading.")]
    public string? Hero { get; init; }

    [Description(ItemReading.ItemLevel)]
    public int? ItemLevel { get; init; }

    [Description(ItemReading.Upgrades)]
    public int? Upgrades { get; init; }

    [Description(ItemReading.Rarity + " The list then holds the items rolling that rarity.")]
    public string? Rarity { get; init; }

    public override RecordReading ToReading() =>
        new()
        {
            Hero = StatQuery.NullIfEmpty(Hero),
            ItemLevel = ItemLevel,
            Upgrades = Upgrades,
            Rarity = StatQuery.NullIfEmpty(Rarity),
        };
}

public static class ItemReading
{
    public const string ItemLevel =
        "The item level the stats are computed at. Defaults to the drop level. Any of itemLevel, upgrades " +
        "and rarity adds magnitudes to the record.";

    public const string Upgrades = "How many times the item is tempered. Defaults to 0.";

    public const string Rarity =
        "The rarity the stats are computed at, by the name the game prints or by tier. Defaults to the " +
        "item's highest rarity.";
}

public static class ItemRecordEndpoint
{
    public static RouteHandlerBuilder MapItemRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetItem")
            .WithSummary($"Get {PublicTypes.Noun(EntityType.Item)} by ID.")
            .Produces<ItemRecord>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] ItemRecordRequest request,
        HttpContext http,
        IEntityStoreService store,
        ICodexViewService view,
        ITooltipViewService tooltips) =>
        Records.One<ItemRecord>(request.Id, request.ToReading(), http, store, view, tooltips);
}
