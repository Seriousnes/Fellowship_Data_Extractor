using Fellowship.SDK.Client;

namespace Fellowship.Codex.Endpoints;

public static class ExportGroup
{
    public const string Tag = "Export";

    public static RouteGroupBuilder MapExport(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{CodexAddresses.Export}").WithTags(Tag);

        group.MapExportManifest();
        group.MapExportFiles();

        return group;
    }
}
