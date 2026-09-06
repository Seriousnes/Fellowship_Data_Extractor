using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

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
