using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public static class CreatureGroup
{
    public static RouteGroupBuilder MapCreature(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.Creature)}")
            .WithTags(PublicTypes.Label(EntityType.Creature));

        group.MapCreatureRecord();
        group.MapCreatureTooltip();

        return group;
    }
}
