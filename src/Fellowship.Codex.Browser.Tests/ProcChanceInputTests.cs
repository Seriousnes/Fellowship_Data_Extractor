using Fellowship.Codex.Browser.Tests.Pages;

namespace Fellowship.Codex.Browser.Tests;

[Collection(Browsing.Name)]
public sealed class ProcChanceInputTests(Site site)
{
    /// <summary>A proc rolling at a known rate with every attribute at 0, so the chance moves with the seconds typed.</summary>
    private ProcRollSource Steady =>
        ProcChance.Sources(site.Store).First(source =>
            ProcChance.Rate(source.Rolls[0], _ => 0) is > 0 && source.Rolls[0] is { ScalesOff.Count: 0, InternalCooldown: null });

    private static async Task<string> ChanceAtAsync(ProcChancePage tool, string seconds)
    {
        var before = await tool.Chance.TextContentAsync();

        await tool.SinceLastRoll.ReplaceAsync(seconds);
        await Assertions.Expect(tool.Chance).Not.ToHaveTextAsync(before ?? string.Empty);

        return (await tool.Chance.TextContentAsync())!;
    }

    [Fact]
    public async Task A_roll_with_an_internal_cooldown_opens_with_both_clocks_at_it()
    {
        await using var context = await site.NewContextAsync();
        var tool = new ProcChancePage(await context.NewPageAsync());
        await tool.OpenAsync(ProcChance.Sources(site.Store).First(source => source.Rolls[0].InternalCooldown == 15));

        Assert.Equal("15", await tool.SinceLastRoll.ValueAsync());
        Assert.Equal("15", await tool.SinceLastProc.ValueAsync());
    }

    [Fact]
    public async Task Backspacing_the_seconds_since_the_last_roll_leaves_the_box_empty_and_the_chance_at_zero_seconds()
    {
        await using var context = await site.NewContextAsync();
        var tool = new ProcChancePage(await context.NewPageAsync());
        await tool.OpenAsync(Steady);

        var atOne = (await tool.Chance.TextContentAsync())!;
        var atZero = await ChanceAtAsync(tool, "0");
        var atFour = await ChanceAtAsync(tool, "4");
        Assert.NotEqual(atOne, atFour);

        await tool.SinceLastRoll.ReplaceAsync("1");
        await tool.RenderedAsync(atOne);

        await tool.SinceLastRoll.PressAsync("Backspace");
        await tool.RenderedAsync(atZero);
        Assert.Equal(string.Empty, await tool.SinceLastRoll.ValueAsync());

        await tool.SinceLastRoll.TypeAsync("4");
        await tool.RenderedAsync(atFour);
        Assert.Equal("4", await tool.SinceLastRoll.ValueAsync());
    }

    [Fact]
    public async Task A_leading_zero_typed_into_the_seconds_since_the_last_roll_stays_after_the_render()
    {
        await using var context = await site.NewContextAsync();
        var tool = new ProcChancePage(await context.NewPageAsync());
        await tool.OpenAsync(Steady);

        var atFour = await ChanceAtAsync(tool, "4");

        await tool.SinceLastRoll.ReplaceAsync("1");
        await Assertions.Expect(tool.Chance).Not.ToHaveTextAsync(atFour);

        await tool.SinceLastRoll.ReplaceAsync("04");
        await tool.RenderedAsync(atFour);
        Assert.Equal("04", await tool.SinceLastRoll.ValueAsync());
    }
}
