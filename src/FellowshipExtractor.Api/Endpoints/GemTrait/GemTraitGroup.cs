using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

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
