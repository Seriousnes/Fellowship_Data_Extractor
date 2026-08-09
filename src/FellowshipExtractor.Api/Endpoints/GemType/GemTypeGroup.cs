using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

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
