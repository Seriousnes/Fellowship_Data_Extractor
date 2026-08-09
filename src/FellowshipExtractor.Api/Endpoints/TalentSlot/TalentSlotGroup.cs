using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public static class TalentSlotGroup
{
    public static RouteGroupBuilder MapTalentSlot(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.TalentSlot)}")
            .WithTags(PublicTypes.Label(EntityType.TalentSlot));

        group.MapTalentSlotRecord();
        group.MapTalentSlotTooltip();

        return group;
    }
}
