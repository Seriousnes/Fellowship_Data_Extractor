using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

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
