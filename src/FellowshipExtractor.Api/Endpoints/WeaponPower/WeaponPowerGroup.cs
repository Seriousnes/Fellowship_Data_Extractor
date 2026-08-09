using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public static class WeaponPowerGroup
{
    public static RouteGroupBuilder MapWeaponPower(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.WeaponPower)}")
            .WithTags(PublicTypes.Label(EntityType.WeaponPower));

        group.MapWeaponPowerRecord();
        group.MapWeaponPowerTooltip();

        return group;
    }
}
