using Fellowship.Codex.Services;
using Fellowship.SDK.Client;

namespace Fellowship.Codex.Endpoints;

public static class ExportManifestEndpoint
{
    public static RouteHandlerBuilder MapExportManifest(this IEndpointRouteBuilder group) =>
        group.MapGet(string.Empty, Handle)
            .WithName("GetExport")
            .WithSummary("The build the codex reads, and the routes answering its entities.jsonl and settings.json.")
            .Produces<ExportResponse>();

    private static IResult Handle(ExportHistoryHolder holder) =>
        Results.Ok(new ExportResponse(
            holder.History.Latest,
            $"/{CodexAddresses.ExportEntities}",
            $"/{CodexAddresses.ExportSettings}"));
}
