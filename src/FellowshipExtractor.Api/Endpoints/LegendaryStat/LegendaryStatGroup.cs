using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public static class LegendaryStatGroup
{
    public static RouteGroupBuilder MapLegendaryStat(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.LegendaryStat)}")
            .WithTags(PublicTypes.Label(EntityType.LegendaryStat));

        group.MapLegendaryStatRecord();
        group.MapLegendaryStatTooltip();

        return group;
    }
}
