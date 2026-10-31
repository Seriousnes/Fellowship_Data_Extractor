namespace Fellowship.Codex.Embeds;

// Continues is a line after a single line break in the text, drawn with no paragraph gap above it.
public sealed record CardLine(string? Lead, IReadOnlyList<CardRun> Runs, bool Continues = false)
{
    public CardLine(string? lead, string text) : this(lead, [new CardRun(text)])
    {
    }

    public string Text => string.Concat(Runs.Select(run => run.Text));

    public bool Equals(CardLine? other) =>
        other is not null && Lead == other.Lead && Continues == other.Continues && Runs.SequenceEqual(other.Runs);

    public override int GetHashCode() => HashCode.Combine(Lead, Text, Continues);
}
