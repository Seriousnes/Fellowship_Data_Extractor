using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public static class BrowsingGroup
{
    public const string Tag = "Browsing";

    public static RouteGroupBuilder MapBrowsing(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").WithTags(Tag);

        group.MapSearch();
        group.MapEntities();
        group.MapCatalog();

        return group;
    }
}
