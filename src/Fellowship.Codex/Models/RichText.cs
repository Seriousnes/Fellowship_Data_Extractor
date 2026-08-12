namespace Fellowship.Codex.Models;

public readonly record struct Substitution(
    string Text, string? Title = null, string? Html = null, bool Resolved = true)
{
    public static Substitution Missing { get; } = new(
        "[not in the build]", "nothing in the build resolves this substitution", Resolved: false);
}
