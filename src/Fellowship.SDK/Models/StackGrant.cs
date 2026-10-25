namespace Fellowship.SDK.Models;

public sealed record StackGrant(string Effect, int Stacks, IReadOnlyList<string> Gates);
