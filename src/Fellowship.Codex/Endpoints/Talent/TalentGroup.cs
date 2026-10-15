using Fellowship.Codex.Models;

namespace Fellowship.Codex.Endpoints;

public static class TalentGroup
{
    public static RouteGroupBuilder MapTalent(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.Talent)}")
            .WithTags(PublicTypes.Label(EntityType.Talent));

        group.MapTalentRecord();
        group.MapTalentTooltip();

        return group;
    }
}
