using Fellowship.Codex.Models;

namespace Fellowship.Codex.Endpoints;

public static class RelicPassiveGroup
{
    public static RouteGroupBuilder MapRelicPassive(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.RelicPassive)}")
            .WithTags(PublicTypes.Label(EntityType.RelicPassive));

        group.MapRecordList<RelicPassiveRecord, RecordListRequest>();
        group.MapRelicPassiveRecord();
        group.MapRelicPassiveTooltip();

        return group;
    }
}
