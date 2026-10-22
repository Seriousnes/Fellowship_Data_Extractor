namespace Fellowship.Codex.Tests;

[CollectionDefinition(Name)]
public sealed class Corpus : ICollectionFixture<Export>
{
    public const string Name = "corpus";
}

[CollectionDefinition(Name)]
public sealed class Readings25647984 : ICollectionFixture<Export25647984>
{
    public const string Name = "readings taken in build 25647984";
}
