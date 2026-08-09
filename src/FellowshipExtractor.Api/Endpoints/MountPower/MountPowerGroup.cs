using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public static class MountPowerGroup
{
    public static RouteGroupBuilder MapMountPower(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.MountPower)}")
            .WithTags(PublicTypes.Label(EntityType.MountPower));

        group.MapMountPowerRecord();
        group.MapMountPowerTooltip();

        return group;
    }
}
