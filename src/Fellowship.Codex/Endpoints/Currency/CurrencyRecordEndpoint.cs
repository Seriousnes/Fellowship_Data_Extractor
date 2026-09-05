using System.ComponentModel;

using Fellowship.SDK.Documents;
using Fellowship.SDK.Records;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

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
            .Produces<CurrencyRecord>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] CurrencyRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One<CurrencyRecord>(request.Id, null, http, store, view);
}
