using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public static class GemGroup
{
    public static RouteGroupBuilder MapGem(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.Gem)}")
            .WithTags(PublicTypes.Label(EntityType.Gem));

        group.MapGemRecord();
        group.MapGemTooltip();

        return group;
    }
}
