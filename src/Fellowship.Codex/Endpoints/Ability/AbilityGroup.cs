using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public static class AbilityGroup
{
    public static RouteGroupBuilder MapAbility(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.Ability)}")
            .WithTags(PublicTypes.Label(EntityType.Ability));

        group.MapAbilityRecord();
        group.MapTooltip(EntityType.Ability);

        return group;
    }
}
