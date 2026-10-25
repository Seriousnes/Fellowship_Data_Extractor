using Fellowship.SDK.Documents;

namespace Fellowship.Codex.Models;

public sealed record Flag(FlagType Type, Func<EntityDocument, bool> Predicate)
{
    public string Key => QueryKeys.Key(Type);

    public string Label => CodexKeys.Label(Type);

    public bool Hides { get; init; }

    public bool Admits(EntityDocument document) => !Hides || !Predicate(document);
}
