namespace Fellowship.Codex.Models;

public sealed record ChangeGate(IReadOnlyList<int> Effects, bool OnTarget);
