using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public static class CompanionPetGroup
{
    public static RouteGroupBuilder MapCompanionPet(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.CompanionPet)}")
            .WithTags(PublicTypes.Label(EntityType.CompanionPet));

        group.MapCompanionPetRecord();
        group.MapCompanionPetTooltip();

        return group;
    }
}
