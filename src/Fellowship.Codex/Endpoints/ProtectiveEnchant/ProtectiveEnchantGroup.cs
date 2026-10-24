using Fellowship.Codex.Models;

namespace Fellowship.Codex.Endpoints;

public static class ProtectiveEnchantGroup
{
    public static RouteGroupBuilder MapProtectiveEnchant(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.ProtectiveEnchant)}")
            .WithTags(PublicTypes.Label(EntityType.ProtectiveEnchant));

        group.MapRecordList<ProtectiveEnchantRecord, AugmentationListRequest>();
        group.MapProtectiveEnchantRecord();
        group.MapProtectiveEnchantTooltip();

        return group;
    }
}
