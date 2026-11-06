using Fellowship.Codex.Models;

namespace Fellowship.Codex.Endpoints;

public static class ItemTraitGroup
{
    public static RouteGroupBuilder MapItemTrait(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.ItemTrait)}")
            .WithTags(PublicTypes.Label(EntityType.ItemTrait));

        group.MapRecordList<ItemTraitRecord, AugmentationListRequest>();
        group.MapItemTraitRecord();
        group.MapItemTraitTooltip();

        return group;
    }
}
