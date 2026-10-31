using Fellowship.Codex.Models;

namespace Fellowship.Codex.Endpoints;

public static class DungeonAffixGroup
{
    public static RouteGroupBuilder MapDungeonAffix(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.DungeonAffix)}")
            .WithTags(PublicTypes.Label(EntityType.DungeonAffix));

        group.MapRecordList<DungeonAffixRecord, RecordListRequest>();
        group.MapDungeonAffixRecord();
        group.MapDungeonAffixTooltip();

        return group;
    }
}
