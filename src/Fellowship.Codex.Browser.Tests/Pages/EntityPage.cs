namespace Fellowship.Codex.Browser.Tests.Pages;

/// <summary>An entity's own page, with the tooltip and the knobs reading it.</summary>
public sealed class EntityPage(IPage page) : CodexPage(page)
{
    private static readonly LocatorGetByRoleOptions ItemLevelName = new() { Name = "Item level", Exact = true };

    /// <summary>The item level number box, on an item's knobs or beside an enchant's slider.</summary>
    public NumberBox ItemLevel => new(Live.GetByRole(AriaRole.Spinbutton, ItemLevelName));

    /// <summary>The range half of an enchant's item level slider.</summary>
    public ILocator ItemLevelBar => Live.GetByRole(AriaRole.Slider, ItemLevelName);

    /// <summary>The item power the tooltip shows.</summary>
    public ILocator ItemPower => Page.Locator(".cx-craft-tip .power");

    public Task OpenAsync(EntityDocument entity, ICodexViewService ui) => OpenAsync(ui.Href(entity));
}
