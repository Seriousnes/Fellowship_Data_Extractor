using Fellowship.Codex.Models;

namespace Fellowship.Codex.Endpoints;

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
