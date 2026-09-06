using Fellowship.SDK.Models;

using Microsoft.AspNetCore.Components.Web;

namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class DifficultyKnobTests : BunitContext
{
    private readonly Export export;

    public DifficultyKnobTests(Export export)
    {
        this.export = export;
        Services.AddSingleton(export.Store);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private SettingsFile Settings => export.Store.Settings;

    private LeagueEntry Eternal => Settings.Leagues.Single(league => league.Kind == LeagueKind.Eternal);

    private int Deep => Eternal.MinimumDifficulty + 44;

    private IRenderedComponent<DifficultyKnob> Picker(int difficulty) =>
        Render<DifficultyKnob>(p => p.Add(knob => knob.Difficulty, difficulty));

    private static IElement Face(IRenderedComponent<DifficultyKnob> picker) =>
        picker.Find("button[data-difficulty]");

    private IRenderedComponent<DifficultyKnob> Opened(int difficulty)
    {
        var picker = Picker(difficulty);
        Face(picker).Click();

        return picker;
    }

    [Fact]
    public void Reads_the_step_it_stands_at_on_a_shut_face()
    {
        var picker = Picker(Deep);

        var face = Face(picker);

        Assert.Equal("combobox", face.GetAttribute("role"));
        Assert.Equal("false", face.GetAttribute("aria-expanded"));
        Assert.Contains($"{Eternal.Name} +44", face.TextContent, StringComparison.Ordinal);
        Assert.Contains(Deep.ToString(), face.TextContent, StringComparison.Ordinal);
        Assert.Empty(picker.FindAll("[role=listbox]"));
    }

    [Fact]
    public void Opens_on_the_league_that_covers_the_difficulty()
    {
        var picker = Opened(Deep);

        Assert.Equal("true", Face(picker).GetAttribute("aria-expanded"));

        var pressed = picker.FindAll(".cx-picker-chip[aria-pressed=true]");

        Assert.Single(pressed);
        Assert.StartsWith(Eternal.Name, pressed[0].TextContent.Trim(), StringComparison.Ordinal);

        var offered = picker.FindAll("[role=option]");

        Assert.Equal(
            Difficulties.Ladder(Settings).Count(step => step.League == Eternal.Name),
            offered.Count);
    }

    [Fact]
    public void Names_every_league_the_ladder_is_cut_into()
    {
        var picker = Opened(Deep);

        var chips = picker.FindAll(".cx-picker-chip").Select(chip => chip.TextContent.Trim()).ToArray();

        var named = Difficulties.Ladder(Settings)
            .Where(step => step.League.Length > 0)
            .GroupBy(step => step.League, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(named.Length + 1, chips.Length);
        Assert.Equal($"All ({Difficulties.Ladder(Settings).Count})", chips[0]);
        Assert.All(named, league => Assert.Contains($"{league.Key} ({league.Count()})", chips));
    }

    [Fact]
    public void Offers_every_difficulty_the_ladder_carries_once_the_scope_is_all()
    {
        var picker = Opened(Deep);

        picker.FindAll(".cx-picker-chip")[0].Click();

        var offered = picker.FindAll("[role=option]");

        Assert.Equal(Settings.DifficultyScaling.Health.Count, offered.Count);

        var deep = offered.Single(step => step.GetAttribute("aria-selected") == "true");

        Assert.Contains($"{Eternal.Name} +44", deep.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Carries_every_step_to_the_address_that_reads_at_it()
    {
        var picker = Opened(Deep);

        var offered = picker.FindAll("[role=option]");

        Assert.All(
            offered.Zip(Difficulties.Ladder(Settings).Where(step => step.League == Eternal.Name)),
            pair => Assert.EndsWith(
                $"{DifficultyKnob.Key}={pair.Second.Difficulty}",
                pair.First.GetAttribute("href"),
                StringComparison.Ordinal));
    }

    [Fact]
    public void Marks_the_step_the_page_stands_at_and_no_other()
    {
        var picker = Opened(Deep);

        var marked = picker.FindAll("[role=option][aria-selected=true]");

        Assert.Single(marked);
        Assert.EndsWith($"{DifficultyKnob.Key}={Deep}", marked[0].GetAttribute("href"), StringComparison.Ordinal);
    }

    [Fact]
    public void Opens_on_an_arrow_pressed_against_a_shut_face()
    {
        var picker = Picker(Deep);

        Face(picker).KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        Assert.Equal("true", Face(picker).GetAttribute("aria-expanded"));
        Assert.NotEmpty(picker.FindAll("[role=option]"));
    }

    [Fact]
    public void Walks_the_cursor_down_the_ladder_on_an_arrow()
    {
        var picker = Opened(Deep);

        picker.Find("[role=listbox]").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        var walked = picker.Find("[role=option][data-active]");

        Assert.Contains($"{Eternal.Name} +45", walked.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Shuts_on_escape()
    {
        var picker = Opened(Deep);

        picker.Find("[role=listbox]").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Equal("false", Face(picker).GetAttribute("aria-expanded"));
        Assert.Empty(picker.FindAll("[role=listbox]"));
    }

    [Fact]
    public void Shuts_again_when_the_face_is_pressed_twice()
    {
        var picker = Opened(Deep);

        Face(picker).Click();

        Assert.Equal("false", Face(picker).GetAttribute("aria-expanded"));
        Assert.Empty(picker.FindAll("[role=listbox]"));
    }
}
