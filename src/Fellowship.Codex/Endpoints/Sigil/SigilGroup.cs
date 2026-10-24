using Fellowship.Codex.Models;

namespace Fellowship.Codex.Endpoints;

public static class SigilGroup
{
    public static RouteGroupBuilder MapSigil(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.Sigil)}")
            .WithTags(PublicTypes.Label(EntityType.Sigil));

        group.MapRecordList<SigilRecord, AugmentationListRequest>();
        group.MapSigilRecord();
        group.MapSigilTooltip();

        return group;
    }
}
