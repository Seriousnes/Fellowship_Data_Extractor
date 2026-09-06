using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

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
