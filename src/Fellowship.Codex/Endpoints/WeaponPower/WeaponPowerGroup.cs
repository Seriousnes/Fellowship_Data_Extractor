using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public static class WeaponPowerGroup
{
    public static RouteGroupBuilder MapWeaponPower(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.WeaponPower)}")
            .WithTags(PublicTypes.Label(EntityType.WeaponPower));

        group.MapWeaponPowerRecord();
        group.MapTooltip(EntityType.WeaponPower);

        return group;
    }
}
