using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

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
