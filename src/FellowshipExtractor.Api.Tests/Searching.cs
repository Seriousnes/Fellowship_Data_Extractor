using System.Runtime.CompilerServices;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;

namespace FellowshipExtractor.Api.Tests;

public static class Searching
{
    private static readonly ConditionalWeakTable<Export, ISearchIndexService> Indexes = [];

    public static ISearchIndexService Index(Export export) =>
        Indexes.GetValue(export, built => new SearchIndexService(built.Store, NullLogger<SearchIndexService>.Instance));

    public static CodexQuery Ask(Export export, out string? error, params (string Key, string Value)[] asked) =>
        CodexQuery.Parse(
            new QueryCollection(asked.ToDictionary(pair => pair.Key, pair => new StringValues(pair.Value))),
            export.View.Codex,
            RequestProperties.Of(typeof(EntitiesRequest)),
            out error);

    public static CodexResults Run(Export export, params (string Key, string Value)[] asked)
    {
        var query = Ask(export, out var error, asked);

        Assert.Null(error);

        return Run(export, query);
    }

    public static CodexResults Run(Export export, CodexQuery query) =>
        new CodexSearchService(export.Store, export.View.Codex, Index(export)).Run(query with { PageSize = 200 });
}
