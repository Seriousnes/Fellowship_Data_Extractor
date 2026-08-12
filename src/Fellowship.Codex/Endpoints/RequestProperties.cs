using System.Collections.Concurrent;
using System.Collections.Frozen;

using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public static class RequestProperties
{
    private static readonly ConcurrentDictionary<Type, FrozenSet<string>> Declared = new();

    public static IReadOnlySet<string> Of(Type request) => Declared.GetOrAdd(request, Read);

    private static FrozenSet<string> Read(Type request) =>
        request.GetProperties()
            .Select(property => property.Name)
            .ToFrozenSet(StringComparer.OrdinalIgnoreCase);
}
