using Fellowship.Codex.Models;

namespace Fellowship.Codex.Endpoints;

public static class BlessingGroup
{
    public static RouteGroupBuilder MapBlessing(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.Blessing)}")
            .WithTags(PublicTypes.Label(EntityType.Blessing));

        group.MapBlessingRecord();
        group.MapBlessingTooltip();

        return group;
    }
}
