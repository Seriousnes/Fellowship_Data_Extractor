using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public static class EffectGroup
{
    public static RouteGroupBuilder MapEffect(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.Effect)}")
            .WithTags(PublicTypes.Label(EntityType.Effect));

        group.MapEffectRecord();
        group.MapTooltip(EntityType.Effect);

        return group;
    }
}
