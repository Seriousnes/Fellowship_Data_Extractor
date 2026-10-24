using Fellowship.Codex.Models;

namespace Fellowship.Codex.Endpoints;

public static class DungeonGroup
{
    public static RouteGroupBuilder MapDungeon(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.Dungeon)}")
            .WithTags(PublicTypes.Label(EntityType.Dungeon));

        group.MapRecordList<DungeonRecord, RecordListRequest>();
        group.MapDungeonRecord();
        group.MapDungeonTooltip();

        return group;
    }
}
