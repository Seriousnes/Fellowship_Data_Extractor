using Fellowship.Codex.Models;

namespace Fellowship.Codex.Endpoints;

public static class GemAbilityGroup
{
    public static RouteGroupBuilder MapGemAbility(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.GemAbility)}")
            .WithTags(PublicTypes.Label(EntityType.GemAbility));

        group.MapRecordList<GemAbilityRecord, RecordListRequest>();
        group.MapGemAbilityRecord();
        group.MapGemAbilityTooltip();

        return group;
    }
}
