using Tool = Fellowship.Codex.Components.Pages.ProcChancePage;

namespace Fellowship.Codex.Browser.Tests.Pages;

/// <summary>The proc chance tool at /tools/proc-chance.</summary>
public sealed class ProcChancePage(IPage page) : CodexPage(page)
{
    public NumberBox SinceLastRoll => new(Live.GetByLabel("Seconds since the last roll"));

    public NumberBox SinceLastProc => new(Live.GetByLabel("Seconds since the last proc"));

    public ILocator Chance => Fact("Chance");

    public Task OpenAsync(ProcRollSource source) => OpenAsync(Tool.Address(source, 0));

    /// <summary>Waits for the server's render of the latest input, which shows <paramref name="chance"/>.</summary>
    public Task RenderedAsync(string chance) => Assertions.Expect(Chance).ToHaveTextAsync(chance);
}
