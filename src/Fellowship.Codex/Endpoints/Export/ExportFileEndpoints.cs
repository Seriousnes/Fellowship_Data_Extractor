using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public static class ExportFileEndpoints
{
    private const string EntitiesType = "application/x-ndjson";

    private const string SettingsType = "application/json";

    public static IEndpointRouteBuilder MapExportFiles(this IEndpointRouteBuilder group)
    {
        group.MapGet($"/{ExportReader.EntitiesName}", (ExportHistoryHolder holder) =>
                Results.Bytes(holder.History.Files.Entities, EntitiesType, ExportReader.EntitiesName))
            .WithName("GetExportEntities")
            .WithSummary("The entities.jsonl of the build the codex reads, one document per line.")
            .Produces(StatusCodes.Status200OK, contentType: EntitiesType);

        group.MapGet($"/{ExportReader.SettingsName}", (ExportHistoryHolder holder) =>
                Results.Bytes(holder.History.Files.Settings, SettingsType, ExportReader.SettingsName))
            .WithName("GetExportSettings")
            .WithSummary("The settings.json of the build the codex reads, the input of the SDK enums.")
            .Produces(StatusCodes.Status200OK, contentType: SettingsType);

        return group;
    }
}
