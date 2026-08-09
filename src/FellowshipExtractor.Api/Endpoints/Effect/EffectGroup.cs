using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public static class EffectGroup
{
    public static RouteGroupBuilder MapEffect(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.Effect)}")
            .WithTags(PublicTypes.Label(EntityType.Effect));

        group.MapEffectRecord();
        group.MapEffectTooltip();

        return group;
    }
}
