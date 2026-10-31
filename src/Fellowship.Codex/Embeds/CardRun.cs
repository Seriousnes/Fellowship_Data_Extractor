namespace Fellowship.Codex.Embeds;

public sealed record CardRun(string Text, string? Colour = null, bool Bold = false, bool Italic = false)
{
    public bool Styled(CardRun other) => Colour == other.Colour && Bold == other.Bold && Italic == other.Italic;
}
