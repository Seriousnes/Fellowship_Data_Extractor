using System.ComponentModel;

using Fellowship.SDK.Client;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record GlyphRecordRequest
{
    [Description("The id.")]
    public int Id { get; init; }

    [Description(GlyphReading.EquippedTier)]
    public int? EquippedTier { get; init; }

    public RecordReading ToReading() => new() { EquippedTier = EquippedTier };
}

public sealed record GlyphListRequest : RecordListRequest
{
    [Description(GlyphReading.EquippedTier)]
    public int? EquippedTier { get; init; }

    public override RecordReading ToReading() => new() { EquippedTier = EquippedTier };
}

public static class GlyphReading
{
    public const string EquippedTier =
        "Which of the glyph's tiers is equipped, from 0. Stating it adds equipped to the record, with the " +
        "description at that tier.";
}

public static class GlyphRecordEndpoint
{
    public static RouteHandlerBuilder MapGlyphRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetGlyph")
            .WithSummary($"Get {PublicTypes.Noun(EntityType.Glyph)} by ID.")
            .Produces<GlyphRecord>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] GlyphRecordRequest request,
        HttpContext http,
        IEntityStoreService store,
        ICodexViewService view,
        ITooltipViewService tooltips) =>
        Records.One<GlyphRecord>(request.Id, request.ToReading(), http, store, view, tooltips);
}
