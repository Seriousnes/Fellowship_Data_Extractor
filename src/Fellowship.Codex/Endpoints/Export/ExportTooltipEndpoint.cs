using System.ComponentModel;

using Fellowship.SDK.Client;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record ExportTooltipRequest
{
    [Description(
        "True when the entity's texture is to be drawn beside the card. A card that draws its texture inside ignores it.")]
    public bool? ShowIcon { get; init; }

    [Description(
        "True when the card is to come wrapped in the chrome a phone draws it in: a scrim, a close button " +
        "and a link to its page.")]
    public bool? Mobile { get; init; }

    [Description("The hero whose version of the entity is drawn.")]
    public string? Hero { get; init; }
}

public static class ExportTooltipEndpoint
{
    public static string Address(string build, EntityType type, int id) =>
        $"/{CodexAddresses.Export}/{build}/{EntityTypes.Slug(type)}/{id}/tooltip";

    public static RouteHandlerBuilder MapExportTooltip(this IEndpointRouteBuilder group) =>
        group.MapMethods("/{build}/{slug}/{id:int}/tooltip", [HttpMethods.Query], (
                [Description("The Steam build id.")] string build,
                [Description("The entity type.")] string slug,
                [Description("The id.")] int id,
                ExportTooltipRequest? request,
                HttpContext http,
                ITooltipService tooltips) =>
            Handle(build, slug, id, request, http, tooltips))
            .WithName("QueryExportTooltip")
            .WithSummary("Self-contained HTML tooltip fragment for one entity as an earlier build has it.")
            .Accepts<ExportTooltipRequest>("application/json")
            .Produces<string>(StatusCodes.Status200OK, "text/html")
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static Task<IResult> Handle(
        string build, string slug, int id, ExportTooltipRequest? request, HttpContext http, ITooltipService tooltips)
    {
        if (!EntityTypes.TryParse(slug, out var type) || !PublicTypes.IsListed(type))
        {
            return Task.FromResult(Results.Problem(
                title: "No such type.",
                detail: $"'{slug}' is not an entity type.",
                statusCode: StatusCodes.Status404NotFound));
        }

        var query = new TooltipQuery
        {
            ShowIcon = request?.ShowIcon == true,
            Mobile = request?.Mobile == true,
            Hero = StatQuery.NullIfEmpty(request?.Hero),
        };

        return tooltips.Get(type, id, build, query, Origins.Of(http));
    }
}
