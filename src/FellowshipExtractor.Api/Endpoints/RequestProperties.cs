using System.Collections.Concurrent;
using System.Collections.Frozen;

using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public static class RequestProperties
{
    private static readonly ConcurrentDictionary<Type, FrozenSet<string>> Declared = new();

    public static IReadOnlySet<string> Of(Type request) => Declared.GetOrAdd(request, Read);

    private static FrozenSet<string> Read(Type request) =>
        request.GetProperties()
            .Select(property => property.Name)
            .ToFrozenSet(StringComparer.OrdinalIgnoreCase);
}
