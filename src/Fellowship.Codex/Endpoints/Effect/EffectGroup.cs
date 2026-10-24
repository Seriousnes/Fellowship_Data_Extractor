using Fellowship.Codex.Models;

namespace Fellowship.Codex.Endpoints;

public static class EffectGroup
{
    public static RouteGroupBuilder MapEffect(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.Effect)}")
            .WithTags(PublicTypes.Label(EntityType.Effect));

        group.MapRecordList<EffectRecord, RecordListRequest>();
        group.MapEffectRecord();
        group.MapEffectTooltip();

        return group;
    }
}
