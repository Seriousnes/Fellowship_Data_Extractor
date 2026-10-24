using Fellowship.Codex.Models;

namespace Fellowship.Codex.Endpoints;

public static class EnchantGroup
{
    public static RouteGroupBuilder MapEnchant(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.Enchant)}")
            .WithTags(PublicTypes.Label(EntityType.Enchant));

        group.MapRecordList<EnchantRecord, EnchantListRequest>();
        group.MapEnchantRecord();
        group.MapEnchantTooltip();

        return group;
    }
}
