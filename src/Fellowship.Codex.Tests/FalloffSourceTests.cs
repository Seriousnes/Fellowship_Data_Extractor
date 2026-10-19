namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class FalloffSourceTests(Export export)
{
    private FalloffSource Offered(EntityType type, string name) =>
        Assert.Single(DamageFalloff.Sources(export.Store),
            source => source.Type == type && PublicTypes.NameOf(source.Document) == name);

    [Fact]
    public void Coalescing_Frost_falls_off_past_five_targets()
    {
        var talent = Offered(EntityType.Talent, "Coalescing Frost");

        Assert.Equal(5, Assert.Single(talent.Entries).Threshold);
        Assert.Equal("talent", talent.Key);
    }

    [Fact]
    public void Every_offered_source_resolves_by_its_type_and_id()
    {
        foreach (var source in DamageFalloff.Sources(export.Store))
        {
            var found = DamageFalloff.Source(export.Store, source.Type, source.Document.Id);

            Assert.NotNull(found);
            Assert.Equal(source.Entries.Count, found.Entries.Count);
        }
    }
}
