using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public static class AbilityGroup
{
    public static RouteGroupBuilder MapAbility(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.Ability)}")
            .WithTags(PublicTypes.Label(EntityType.Ability));

        group.MapAbilityRecord();
        group.MapAbilityTooltip();

        return group;
    }
}
