using Fellowship.Codex.Models;

namespace Fellowship.Codex.Endpoints;

public static class WeaponPowerGroup
{
    public static RouteGroupBuilder MapWeaponPower(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.WeaponPower)}")
            .WithTags(PublicTypes.Label(EntityType.WeaponPower));

        group.MapRecordList<WeaponPowerRecord, RecordListRequest>();
        group.MapWeaponPowerRecord();
        group.MapWeaponPowerTooltip();

        return group;
    }
}
