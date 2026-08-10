using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Components.Browse;

public static class Grouped
{
    public static Func<T, string?>? By<T>(BrowseView view, ICodexService codex) where T : EntityDocument =>
        view.Query.Group is { } key && codex.Facet(key) is { } facet
            ? row => facet.Values(row).FirstOrDefault() is { } value ? codex.Printed(facet, value) : null
            : null;
}
