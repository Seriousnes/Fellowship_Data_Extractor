using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Reflection;

using Fellowship.SDK.Client;
using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public static class RequestProperties
{
    private static readonly ConcurrentDictionary<Type, FrozenSet<string>> Properties = new();

    public static IReadOnlySet<string> Of(Type request) => Properties.GetOrAdd(request, Read);

    public static IReadOnlySet<string> OfEveryTooltip { get; } = Enum.GetValues<EntityType>()
        .Select(TooltipRequestTypes.Find)
        .OfType<Type>()
        .SelectMany(request => Of(request))
        .ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static FrozenSet<string> Read(Type request) =>
        request.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToFrozenSet(StringComparer.OrdinalIgnoreCase);
}
