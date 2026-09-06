using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public static class ItemGroup
{
    public static RouteGroupBuilder MapItem(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.Item)}")
            .WithTags(PublicTypes.Label(EntityType.Item));

        group.MapItemRecord();
        group.MapItemTooltip();

        return group;
    }
}
