using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public static class BlessingGroup
{
    public static RouteGroupBuilder MapBlessing(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.Blessing)}")
            .WithTags(PublicTypes.Label(EntityType.Blessing));

        group.MapBlessingRecord();
        group.MapTooltip(EntityType.Blessing);

        return group;
    }
}
