namespace Fellowship.Codex.Browser.Tests;

/// <summary>The details panel prints what the export states about durations, applications and rolls.</summary>
[Collection(Browsing.Name)]
public sealed class EffectDetailsTests(Site site)
{
    [Theory]
    [InlineData("/effect/106", "10% / 12% / 14% / 16% of WeaponAbilityCooldown")]
    [InlineData("/effect/2719", "0.33334 × (Haste - 1) / Stacks")]
    [InlineData("/effect/2719", "Applied through")]
    [InlineData("/effect/2719", "Invictus")]
    [InlineData("/effect/3266", "Applied when you use")]
    [InlineData("/effect/3266", "Lonesome Song")]
    [InlineData("/effect/106", "Weapon abilities")]
    [InlineData("/ability/171", "Internal cooldown")]
    [InlineData("/ability/171", "Excludes")]
    public async Task The_details_panel_prints(string address, string text)
    {
        await using var context = await site.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync(address);

        await page.Locator(".cx-entity-details").GetByText(text).First.WaitForAsync();
    }
}
