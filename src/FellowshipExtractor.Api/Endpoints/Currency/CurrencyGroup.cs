using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public static class CurrencyGroup
{
    public static RouteGroupBuilder MapCurrency(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.Currency)}")
            .WithTags(PublicTypes.Label(EntityType.Currency));

        group.MapCurrencyRecord();
        group.MapCurrencyTooltip();

        return group;
    }
}
