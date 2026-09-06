using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public static class SetBonusGroup
{
    public static RouteGroupBuilder MapSetBonus(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.SetBonus)}")
            .WithTags(PublicTypes.Label(EntityType.SetBonus));

        group.MapSetBonusRecord();
        group.MapSetBonusTooltip();

        return group;
    }
}
