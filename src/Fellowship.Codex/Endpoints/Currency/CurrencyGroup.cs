using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public static class CurrencyGroup
{
    public static RouteGroupBuilder MapCurrency(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.Currency)}")
            .WithTags(PublicTypes.Label(EntityType.Currency));

        group.MapCurrencyRecord();
        group.MapTooltip(EntityType.Currency);

        return group;
    }
}
