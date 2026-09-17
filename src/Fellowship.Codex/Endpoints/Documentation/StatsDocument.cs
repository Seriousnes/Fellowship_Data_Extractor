using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record StatsMarker;

public static class StatsDocument
{
    public static RouteHandlerBuilder WithStats(this RouteHandlerBuilder route) =>
        route.WithMetadata(new StatsMarker());
}

public sealed class StatsNote(IEntityStoreService store) : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellation)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;

        if (!metadata.OfType<StatsMarker>().Any()) return Task.CompletedTask;

        var note =
            "A magnitude the tooltip prints rather than computes is stated in the request body's `stats` " +
            "object, keyed by the name the build gives the stat and holding `rating`, `percentage` or both, " +
            "one number per roll, as `\"stats\": { \"Haste\": { \"rating\": [1000] } }`. A rating is the " +
            "number the item has and a percentage is what that rating converts to, read only for a stat " +
            "that converts, so a percentage stated for a stat that does not is refused. The stats are " +
            string.Join(", ", store.Stats.Names) + ".";

        operation.Description = operation.Description is { Length: > 0 } existing
            ? existing + "\n\n" + note
            : note;

        return Task.CompletedTask;
    }
}
