namespace Fellowship.Codex.Browser.Tests.Pages;

/// <summary>
/// A Codex page. Its knobs and sliders carry <c>data-live="on"</c> once they render interactively, and every
/// control locates inside one, so input never reaches a prerendered element the interactive render replaces.
/// </summary>
public abstract class CodexPage(IPage page)
{
    protected IPage Page { get; } = page;

    public string Url => Page.Url;

    protected ILocator Live => Page.Locator(".cx-knob[data-live=on], .cx-slider[data-live=on]");

    protected async Task OpenAsync(string address)
    {
        await Page.GotoAsync(address);
        await Live.First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached });
    }

    /// <summary>The value cell of the fact row headed <paramref name="label"/>.</summary>
    protected ILocator Fact(string label) =>
        Page.Locator("table.cx-facts tr").Filter(new LocatorFilterOptions
        {
            Has = Page.Locator("th").GetByText(label, new LocatorGetByTextOptions { Exact = true }),
        }).Locator("td");
}
