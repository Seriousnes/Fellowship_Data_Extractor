using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public static class RelicPowerGroup
{
    public static RouteGroupBuilder MapRelicPower(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.RelicPower)}")
            .WithTags(PublicTypes.Label(EntityType.RelicPower));

        group.MapRelicPowerRecord();
        group.MapRelicPowerTooltip();

        return group;
    }
}
