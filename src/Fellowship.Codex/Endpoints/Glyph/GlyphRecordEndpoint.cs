using System.ComponentModel;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record GlyphRecordRequest
{
    [Description("The id.")]
    public int Id { get; init; }
}

public static class GlyphRecordEndpoint
{
    public static RouteHandlerBuilder MapGlyphRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetGlyph")
            .WithSummary($"Get {PublicTypes.Noun(EntityType.Glyph)} by ID.")
            .Produces<GlyphRecord>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] GlyphRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One<GlyphRecord>(request.Id, null, http, store, view);
}
