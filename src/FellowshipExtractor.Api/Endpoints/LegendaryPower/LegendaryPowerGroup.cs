using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public static class LegendaryPowerGroup
{
    public static RouteGroupBuilder MapLegendaryPower(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.LegendaryPower)}")
            .WithTags(PublicTypes.Label(EntityType.LegendaryPower));

        group.MapLegendaryPowerRecord();
        group.MapLegendaryPowerTooltip();

        return group;
    }
}
