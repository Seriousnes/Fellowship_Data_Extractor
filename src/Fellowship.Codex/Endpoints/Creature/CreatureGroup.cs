using Fellowship.Codex.Models;

namespace Fellowship.Codex.Endpoints;

public static class CreatureGroup
{
    public static RouteGroupBuilder MapCreature(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.Creature)}")
            .WithTags(PublicTypes.Label(EntityType.Creature));

        group.MapRecordList<CreatureRecord, CreatureListRequest>();
        group.MapCreatureRecord();
        group.MapCreatureTooltip();

        return group;
    }
}
