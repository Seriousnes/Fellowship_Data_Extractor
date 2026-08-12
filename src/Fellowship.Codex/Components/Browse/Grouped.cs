using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Components.Browse;

public static class Grouped
{
    public static Func<T, string?>? By<T>(BrowseView view, ICodexService codex) where T : EntityDocument =>
        view.Query.Group is { } key && codex.Facet(key) is { } facet
            ? row => facet.Values(row).FirstOrDefault() is { } value ? codex.Printed(facet, value) : null
            : null;
}
