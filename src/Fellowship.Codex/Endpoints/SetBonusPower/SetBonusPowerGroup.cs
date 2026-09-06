using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public static class SetBonusPowerGroup
{
    public static RouteGroupBuilder MapSetBonusPower(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.SetBonusPower)}")
            .WithTags(PublicTypes.Label(EntityType.SetBonusPower));

        group.MapSetBonusPowerRecord();
        group.MapTooltip(EntityType.SetBonusPower);

        return group;
    }
}
