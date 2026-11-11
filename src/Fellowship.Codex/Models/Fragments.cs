using Microsoft.AspNetCore.Components;

namespace Fellowship.Codex.Models;

public static class Fragments
{
    /// <summary>
    /// Link to <paramref name="anchor"/> on the current page. The base href is the site root, so a bare fragment
    /// resolves against the home page.
    /// </summary>
    public static string Fragment(this NavigationManager nav, string anchor) =>
        $"{nav.ToAbsoluteUri(nav.Uri).PathAndQuery}#{anchor}";
}
