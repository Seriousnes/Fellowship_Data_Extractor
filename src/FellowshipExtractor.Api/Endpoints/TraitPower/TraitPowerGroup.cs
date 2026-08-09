using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public static class TraitPowerGroup
{
    public static RouteGroupBuilder MapTraitPower(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.TraitPower)}")
            .WithTags(PublicTypes.Label(EntityType.TraitPower));

        group.MapTraitPowerRecord();
        group.MapTraitPowerTooltip();

        return group;
    }
}
