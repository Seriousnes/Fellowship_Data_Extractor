using System.ComponentModel;

using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public sealed record CurrencyRecordRequest
{
    [Description("The id to answer.")]
    public int Id { get; init; }
}

public static class CurrencyRecordEndpoint
{
    public static RouteHandlerBuilder MapCurrencyRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetCurrency")
            .WithSummary($"One {PublicTypes.Noun(EntityType.Currency)}'s record.")
            .Produces<PublicEntity>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] CurrencyRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One(EntityType.Currency, request.Id, null, http, store, view);
}
