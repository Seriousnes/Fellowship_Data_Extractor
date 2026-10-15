using Fellowship.Codex.Models;

namespace Fellowship.Codex.Endpoints;

public static class GemTraitGroup
{
    public static RouteGroupBuilder MapGemTrait(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.GemTrait)}")
            .WithTags(PublicTypes.Label(EntityType.GemTrait));

        group.MapGemTraitRecord();
        group.MapGemTraitTooltip();

        return group;
    }
}
