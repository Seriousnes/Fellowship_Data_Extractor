using Fellowship.Codex.Models;

namespace Fellowship.Codex.Endpoints;

public static class CurrencyGroup
{
    public static RouteGroupBuilder MapCurrency(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{EntityTypes.Slug(EntityType.Currency)}")
            .WithTags(PublicTypes.Label(EntityType.Currency));

        group.MapRecordList<CurrencyRecord, RecordListRequest>();
        group.MapCurrencyRecord();
        group.MapCurrencyTooltip();

        return group;
    }
}
