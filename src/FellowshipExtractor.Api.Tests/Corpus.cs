namespace FellowshipExtractor.Api.Tests;

/// <summary>
/// The collection that shares one <see cref="Export"/> across every class needing a real CodexView,
/// so the entity stream is read once per run rather than once per class. A class fixture would be
/// scoped to a single class, and ten test classes need this one instance.
/// </summary>
[CollectionDefinition(Name)]
public sealed class Corpus : ICollectionFixture<Export>
{
    public const string Name = "corpus";
}
