using Fellowship.Codex.Models;

namespace Fellowship.Codex.Endpoints;

public static class MountGroup
{
    public static RouteGroupBuilder MapMount(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.Mount)}")
            .WithTags(PublicTypes.Label(EntityType.Mount));

        group.MapRecordList<MountRecord, RecordListRequest>();
        group.MapMountRecord();
        group.MapMountTooltip();

        return group;
    }
}
