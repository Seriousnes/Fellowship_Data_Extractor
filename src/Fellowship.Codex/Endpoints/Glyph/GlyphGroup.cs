using Fellowship.Codex.Models;

namespace Fellowship.Codex.Endpoints;

public static class GlyphGroup
{
    public static RouteGroupBuilder MapGlyph(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.Glyph)}")
            .WithTags(PublicTypes.Label(EntityType.Glyph));

        group.MapRecordList<GlyphRecord, RecordListRequest>();
        group.MapGlyphRecord();
        group.MapGlyphTooltip();

        return group;
    }
}
