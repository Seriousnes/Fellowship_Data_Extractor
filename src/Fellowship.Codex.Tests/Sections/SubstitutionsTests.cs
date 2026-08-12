namespace Fellowship.Codex.Tests;

public class SubstitutionsTests : BunitContext
{
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
