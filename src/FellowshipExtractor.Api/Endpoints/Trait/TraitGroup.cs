using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public static class TraitGroup
{
    public static RouteGroupBuilder MapTrait(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.Trait)}")
            .WithTags(PublicTypes.Label(EntityType.Trait));

        group.MapTraitRecord();
        group.MapTraitTooltip();

        return group;
    }
}
