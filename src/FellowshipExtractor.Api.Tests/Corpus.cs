namespace FellowshipExtractor.Api.Tests;

[CollectionDefinition(Name)]
public sealed class Corpus : ICollectionFixture<Export>
{
    public const string Name = "corpus";
}
