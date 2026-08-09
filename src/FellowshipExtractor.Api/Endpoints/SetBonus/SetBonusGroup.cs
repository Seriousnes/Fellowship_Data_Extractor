using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

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
