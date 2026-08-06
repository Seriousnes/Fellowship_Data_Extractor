namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class TuningTests : BunitContext
{
    public TuningTests(Export export) => Services.AddSingleton(export.View);

    [Fact]
    public void Tuning_renders_nothing_without_rows()
    {
        var cut = Render<Tuning>();

        Assert.Empty(cut.Markup);
    }

    [Fact]
    public void Tuning_renders_a_row_per_number_and_marks_an_absent_one()
    {
        IReadOnlyList<TuningRow> rows =
        [
            new TuningRow("Coefficient", "0.85", false),
            new TuningRow("Radius", "not set", true),
        ];

        var cut = Render<Tuning>(p => p.Add(x => x.Rows, rows));

        Assert.Equal("Tuning", cut.Find("div.cx-panel-head").FirstChild!.TextContent.Trim());
        Assert.Equal("2", cut.Find("div.cx-panel-head span.cx-num").TextContent);

        var values = cut.FindAll("ul.cx-kv span.cx-v");

        Assert.Equal(2, values.Count);
        Assert.DoesNotContain("cx-miss", values[0].ClassList);
        Assert.Contains("cx-miss", values[1].ClassList);
    }

    [Fact]
    public void Talent_tuning_renders_nothing_without_talents()
    {
        var cut = Render<TalentTuning>(p => p.AddCascadingValue(new Entity()));

        Assert.Empty(cut.Markup);
    }

    [Fact]
    public void Talent_tuning_names_each_group_and_sorts_them_ignoring_case()
    {
        var entity = new Entity
        {
            Talents = new Dictionary<string, IReadOnlyDictionary<string, object?>>
            {
                ["ZealousStrike"] = new Dictionary<string, object?> { ["Coefficient"] = 0.85 },
                ["AegisOfDawn"] = new Dictionary<string, object?> { ["Radius"] = 500.0 },
            },
        };

        var cut = Render<TalentTuning>(p => p.AddCascadingValue(entity));

        var heads = cut.FindAll("p.cx-sub-head");

        Assert.Equal(2, heads.Count);
        Assert.Equal("Aegis Of Dawn", heads[0].TextContent);
        Assert.Equal("Zealous Strike", heads[1].TextContent);
        Assert.Equal(2, cut.FindAll("ul.cx-kv").Count);
    }

    [Fact]
    public void Talent_tuning_marks_a_gated_group_as_conditional()
    {
        var entity = new Entity
        {
            Talents = new Dictionary<string, IReadOnlyDictionary<string, object?>>
            {
                ["AegisOfDawn"] = new Dictionary<string, object?> { ["Radius"] = 500.0 },
            },
            TalentGates = new Dictionary<string, string> { ["AegisOfDawn"] = "while shielded" },
        };

        var cut = Render<TalentTuning>(p => p.AddCascadingValue(entity));

        Assert.Equal("conditional", cut.Find("p.cx-sub-head span.cx-num").TextContent);
    }

    [Fact]
    public void Substitutions_renders_nothing_without_tokens()
    {
        var cut = Render<Substitutions>();

        Assert.Empty(cut.Markup);
    }

    [Fact]
    public void Substitutions_numbers_each_token_and_marks_an_unresolved_reading()
    {
        IReadOnlyList<TokenReading> tokens =
        [
            new TokenReading(1, "85% of Strength", true, "0.85 x Strength", []),
            new TokenReading(2, "nothing in the build settles this", false, "", []),
        ];

        var cut = Render<Substitutions>(p => p.Add(x => x.Tokens, tokens));

        var cards = cut.FindAll("div.cx-token");

        Assert.Equal(2, cards.Count);
        Assert.Equal("#1", cards[0].QuerySelector("span.cx-token-name")!.TextContent);
        Assert.Equal("0.85 x Strength", cards[0].QuerySelector("div.cx-token-work")!.TextContent);

        Assert.Null(cards[0].QuerySelector("span.cx-miss"));
        Assert.Equal(
            "nothing in the build settles this",
            cards[1].QuerySelector("div.cx-token-read span.cx-miss")!.TextContent);

        Assert.Null(cards[1].QuerySelector("div.cx-token-work"));
    }

    [Fact]
    public void Substitutions_lists_a_tokens_own_tuning_rows_under_it()
    {
        IReadOnlyList<TokenReading> tokens =
        [
            new TokenReading(1, "85% of Strength", true, "", [new TuningRow("Coefficient", "0.85", false)]),
        ];

        var cut = Render<Substitutions>(p => p.Add(x => x.Tokens, tokens));

        Assert.Equal("Coefficient", cut.Find("div.cx-token ul.cx-kv span.cx-k").TextContent);
    }
}
