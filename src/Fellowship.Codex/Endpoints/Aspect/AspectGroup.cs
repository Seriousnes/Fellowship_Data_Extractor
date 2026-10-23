using Fellowship.Codex.Models;

namespace Fellowship.Codex.Endpoints;

public static class AspectGroup
{
    public static RouteGroupBuilder MapAspect(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.Aspect)}")
            .WithTags(PublicTypes.Label(EntityType.Aspect));

        group.MapAspectRecord();
        group.MapAspectTooltip();

        return group;
    }
}
