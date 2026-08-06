namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class CorpusTests : BunitContext
{
    private readonly EntityStore Store;

    public CorpusTests(Export export)
    {
        Store = export.Store;
        Services.AddSingleton(export.Tooltip);
    }

    [Fact]
    public void Every_id_in_the_corpus_resolves_through_all_and_through_its_wearers_name()
    {
        var ids = Store.Entities
            .Select(document => (Type: EntityTypes.TypeOf(document), document.Id))
            .Distinct()
            .ToArray();

        Assert.NotEmpty(ids);

        foreach (var (type, id) in ids)
        {
            var readings = Store.All(type, id);
            Assert.NotEmpty(readings);

            var heroName = Store.Wearer(readings[0])?.Name;

            var outcome = Store.Resolve(type, id, heroName, out var document, out _);

            Assert.Equal(Lookup.Found, outcome);
            Assert.NotNull(document);
        }
    }

    [Fact]
    public void Every_wholly_resolvable_row_renders_no_unresolved_token()
    {
        var withPlaceholders = Store.Entities
            .Where(document => Tokens(document).Count > 0)
            .ToArray();

        Assert.NotEmpty(withPlaceholders);

        var blank = withPlaceholders.Where(document => Tokens(document).Any(IsBlank)).ToArray();
        var resolvable = withPlaceholders.Except(blank).ToArray();

        Assert.NotEmpty(resolvable);
        Assert.NotEmpty(blank);

        foreach (var document in resolvable)
        {
            var cut = Render<Components.Tooltip.Tooltip>(p => p.Add(x => x.Entity, document));

            Assert.DoesNotContain("class=\"tok\"", cut.Markup, StringComparison.Ordinal);
        }
    }

    private static bool IsBlank(Placeholder token) => token is { Text: null, Coefficient: null, Value: null };

    private static IReadOnlyList<Placeholder> Tokens(EntityDocument document) =>
        document is GemTypeDocument { OverflowPlaceholders: { } overflow } ? overflow : CodexView.PlaceholdersOf(document);

    [Fact]
    public void Ability_1855_spends_BrillFlashHeal_twice_carrying_the_same_number()
    {
        var ability = Assert.Single(Store.All(EntityType.Ability, 1855));

        var cut = Render<Components.Tooltip.Tooltip>(p => p.Add(x => x.Entity, ability));

        var numbers = cut.FindAll("p.desc span.sub").Select(e => e.TextContent).ToArray();

        Assert.Equal(3, numbers.Length);
        Assert.Equal(numbers[0], numbers[2]);
    }
}
