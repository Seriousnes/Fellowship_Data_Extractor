using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public static class MountGroup
{
    public static RouteGroupBuilder MapMount(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.Mount)}")
            .WithTags(PublicTypes.Label(EntityType.Mount));

        group.MapMountRecord();
        group.MapTooltip(EntityType.Mount);

        return group;
    }
}
