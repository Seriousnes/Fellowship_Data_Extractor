using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public static class TalentGroup
{
    public static RouteGroupBuilder MapTalent(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.Talent)}")
            .WithTags(PublicTypes.Label(EntityType.Talent));

        group.MapTalentRecord();
        group.MapTalentTooltip();

        return group;
    }
}
