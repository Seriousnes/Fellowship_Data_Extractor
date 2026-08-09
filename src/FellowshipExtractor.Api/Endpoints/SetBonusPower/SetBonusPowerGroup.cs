using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public static class SetBonusPowerGroup
{
    public static RouteGroupBuilder MapSetBonusPower(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.SetBonusPower)}")
            .WithTags(PublicTypes.Label(EntityType.SetBonusPower));

        group.MapSetBonusPowerRecord();
        group.MapSetBonusPowerTooltip();

        return group;
    }
}
