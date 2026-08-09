using System.Reflection;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

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

        var declared = metadata.OfType<MethodInfo>()
            .SelectMany(method => method.GetParameters())
            .Where(parameter => parameter.GetCustomAttribute<AsParametersAttribute>() is not null)
            .SelectMany(parameter => RequestProperties.Of(parameter.ParameterType))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var accepted = store.Stats.Names.Where(name => !declared.Contains(name.Replace(
            " ", string.Empty, StringComparison.Ordinal))).ToArray();

        var note =
            "Any stat the build names may also be given, as `?Stamina=40`, to state a magnitude the tooltip " +
            $"prints rather than computes, and a rating may be given as a percentage with the " +
            $"`{StatConversion.PercentageSuffix}` suffix. The stats are " + string.Join(", ", accepted) + ".";

        operation.Description = operation.Description is { Length: > 0 } already
            ? already + "\n\n" + note
            : note;

        return Task.CompletedTask;
    }
}
