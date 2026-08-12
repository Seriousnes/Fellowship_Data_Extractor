using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

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
