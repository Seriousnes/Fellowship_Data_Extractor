namespace Fellowship.Codex.Embeds;

public sealed record CardModel
{
    public const int Rows = 2;

    public const string Separator = " · ";

    public const string LeadMark = ": ";

    private const int SummaryLength = 200;

    public required string Name { get; init; }

    public required string Colour { get; init; }

    public required IReadOnlyList<string> Kind { get; init; }

    public IReadOnlyList<CardLine> Lines { get; init; } = [];

    public IReadOnlyList<CardLayer> Icon { get; init; } = [];

    public string KindLine => string.Join(Separator, Kind);

    public string? Summary()
    {
        if (Lines.Count == 0) return null;

        var texts = Lines.Select(line => line.Lead is null ? line.Text : line.Lead + LeadMark + line.Text).ToList();

        var text = string.Concat(texts.Select((line, index) =>
            index == 0 ? line : (char.IsPunctuation(texts[index - 1][^1]) ? " " : ". ") + line));

        text = text.Replace(CardText.NoBreak, ' ');

        return text.Length <= SummaryLength ? text : text[..SummaryLength].TrimEnd() + CardText.Ellipsis;
    }
}
