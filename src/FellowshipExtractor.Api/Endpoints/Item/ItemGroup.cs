using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

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
