using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public static class GemTypeGroup
{
    public static RouteGroupBuilder MapGemType(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.GemType)}")
            .WithTags(PublicTypes.Label(EntityType.GemType));

        group.MapGemTypeRecord();
        group.MapGemTypeTooltip();

        return group;
    }
}
