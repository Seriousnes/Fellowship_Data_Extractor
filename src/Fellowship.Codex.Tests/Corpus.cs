namespace Fellowship.Codex.Tests;

[CollectionDefinition(Name)]
public sealed class Corpus : ICollectionFixture<Export>
{
    public const string Name = "corpus";
}
