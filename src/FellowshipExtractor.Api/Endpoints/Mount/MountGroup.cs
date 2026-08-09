using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public static class MountGroup
{
    public static RouteGroupBuilder MapMount(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.Mount)}")
            .WithTags(PublicTypes.Label(EntityType.Mount));

        group.MapMountRecord();
        group.MapMountTooltip();

        return group;
    }
}
