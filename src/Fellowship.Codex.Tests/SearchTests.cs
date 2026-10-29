using Microsoft.Extensions.Logging.Abstractions;

namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public sealed class SearchTests(Export export)
{
    private const int OmniboxRows = 12;

    private readonly SearchIndexService index = new(export.Store, NullLogger<SearchIndexService>.Instance);

    private SuggestionService Suggestions => new(index, export.View);

    private IReadOnlyList<HeroDocument> Heroes =>
    [
        .. export.Store.Entities.OfType<HeroDocument>().Where(hero => hero.Name is { Length: >= 4 }),
    ];

    private static string Slug(EntityType type) => EntityTypes.Slug(type);

    private static int Rank(string slug) =>
        EntityTypes.TryParse(slug, out var type) ? PublicTypes.Rank(type) : int.MaxValue;

    [Fact]
    public void The_export_has_heroes() => Assert.NotEmpty(Heroes);

    [Fact]
    public void Each_hero_name_lists_the_hero_within_the_omnibox_rows()
    {
        foreach (var hero in Heroes)
        {
            var found = Suggestions.For(hero.Name, null, OmniboxRows).Results;

            Assert.Contains(found, hit => hit.Type == Slug(EntityType.Hero) && hit.Id == hero.Id);
        }
    }

    [Fact]
    public void A_hero_hit_links_to_the_hero_page_with_its_portrait_and_no_tooltip()
    {
        var hero = Heroes[0];
        var hit = Suggestions.For(hero.Name, Slug(EntityType.Hero), OmniboxRows).Results
            .Single(hit => hit.Id == hero.Id);

        Assert.Equal(export.View.Href(hero), hit.Href);
        Assert.StartsWith("/hero/", hit.Href);
        Assert.NotNull(hit.IconUrl);
        Assert.Null(hit.Tooltip);
    }

    [Fact]
    public void A_name_with_two_letters_transposed_still_lists_the_hero()
    {
        foreach (var hero in Heroes)
        {
            var name = hero.Name!;
            var typo = string.Concat(name[..1], name[2..3], name[1..2], name[3..]);

            var found = Suggestions.For(typo, null, OmniboxRows).Results;

            Assert.Contains(found, hit => hit.Type == Slug(EntityType.Hero) && hit.Id == hero.Id);
        }
    }

    [Fact]
    public void A_name_with_one_letter_wrong_still_lists_the_hero()
    {
        foreach (var hero in Heroes)
        {
            var name = hero.Name!;
            var typo = name[..^1] + (name[^1] == 'q' ? 'x' : 'q');

            var found = Suggestions.For(typo, null, OmniboxRows).Results;

            Assert.Contains(found, hit => hit.Type == Slug(EntityType.Hero) && hit.Id == hero.Id);
        }
    }

    [Fact]
    public void A_correctly_spelled_word_reaches_no_misspelled_neighbour()
    {
        var hero = Heroes[0];
        var name = hero.Name!.ToLowerInvariant();

        foreach (var hit in index.Search(hero.Name!, SuggestionService.Ceiling))
        {
            Assert.Contains(
                SearchIndexService.Words(PublicTypes.NameOf(hit.Document)),
                word => word.StartsWith(name, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void An_exact_hero_name_lists_the_hero_first()
    {
        foreach (var hero in Heroes)
        {
            var first = Suggestions.For(hero.Name, null, OmniboxRows).Results[0];

            Assert.Equal((Slug(EntityType.Hero), hero.Id), (first.Type, first.Id));
        }
    }

    [Theory]
    [InlineData("Rime")]
    [InlineData("Frost")]
    public void Omnibox_groups_run_hero_ability_talent_effect_item_then_the_rest(string text)
    {
        var groups = Suggestions.For(text, null, OmniboxRows).Groups;

        Assert.Equal([.. groups.Select(group => Rank(group.Type)).Order()], groups.Select(group => Rank(group.Type)));
    }

    private static bool Furnished(EntityDocument document) => document switch
    {
        AbilityDocument ability => ability is { Icon: not null, Description: not null },
        EffectDocument effect => effect is { Icon: not null, Description: not null, NameBorrowedFrom: null },
        _ => false,
    };

    [Fact]
    public void Of_two_equal_matches_the_higher_type_ranks_first()
    {
        var shared = export.Store.Entities
            .Where(PublicTypes.IsSearched)
            .Where(Furnished)
            .Where(document => PublicTypes.DisplayNameOf(document) is not null)
            .GroupBy(document => PublicTypes.DisplayNameOf(document)!)
            .First(group => group.Any(document => document is AbilityDocument) && group.Any(document => document is EffectDocument));

        var hits = index.Search(shared.Key, SuggestionService.Ceiling)
            .Where(hit => PublicTypes.DisplayNameOf(hit.Document) == shared.Key && Furnished(hit.Document))
            .ToArray();

        var ability = Array.FindIndex(hits, hit => hit.Document is AbilityDocument);
        var effect = Array.FindIndex(hits, hit => hit.Document is EffectDocument);

        Assert.InRange(ability, 0, effect - 1);
        Assert.Equal(
            PublicTypes.Weight(EntityType.Ability) - PublicTypes.Weight(EntityType.Effect),
            hits[ability].Score - hits[effect].Score);
    }

    [Fact]
    public void Type_weights_run_hero_ability_talent_effect_item_then_nothing()
    {
        EntityType[] leading = [EntityType.Hero, EntityType.Ability, EntityType.Talent, EntityType.Effect, EntityType.Item];

        Assert.Equal([50, 40, 30, 20, 10], leading.Select(PublicTypes.Weight));
        Assert.All(Enum.GetValues<EntityType>().Except(leading), type => Assert.Equal(0, PublicTypes.Weight(type)));
    }

    [Fact]
    public void No_type_weight_lifts_a_name_starting_with_the_query_over_an_exact_name() =>
        Assert.True(PublicTypes.Weight(EntityType.Hero) + 900 < 2000);

    [Fact]
    public void The_search_page_lists_the_exact_hero_first()
    {
        var hero = Heroes[0];
        var results = new CodexSearchService(export.Store, export.View.Codex, index).Run(new CodexQuery
        {
            Text = hero.Name,
            Sort = SortingType.Relevance,
            PageSize = int.MaxValue,
        });

        Assert.True(results.Rows[0] is HeroDocument found && found.Id == hero.Id);
    }

    [Fact]
    public void Sorting_search_results_by_name_sorts_every_row_by_name()
    {
        var hero = Heroes[0];
        var rows = new CodexSearchService(export.Store, export.View.Codex, index).Run(new CodexQuery
        {
            Text = hero.Name,
            Sort = SortingType.Name,
            PageSize = int.MaxValue,
        }).Rows;

        var names = rows.Select(PublicTypes.NameOf).ToArray();
        Assert.Equal([.. names.Order(StringComparer.OrdinalIgnoreCase)], names);
    }

    [Fact]
    public void A_search_defaults_to_relevance_and_a_list_to_name()
    {
        Assert.Equal(SortingType.Relevance, CodexQuery.DefaultSort("rime").Sorting);
        Assert.Equal(SortingType.Name, CodexQuery.DefaultSort(null).Sorting);
    }

    [Theory]
    [InlineData("rime", "rime", 1, 0)]
    [InlineData("rimw", "rime", 1, 1)]
    [InlineData("rmie", "rime", 1, 1)]
    [InlineData("frostbolt", "frostbolts", 2, 0)]
    [InlineData("forstbolt", "frostbolt", 2, 1)]
    [InlineData("frsotbolx", "frostbolt", 2, 2)]
    [InlineData("abcd", "wxyz", 2, 3)]
    [InlineData("rimew", "rime", 1, 1)]
    public void Prefix_edit_distance_counts_edits_up_to_one_past_the_bound(
        string typed, string term, int bound, int edits) =>
        Assert.Equal(edits, SearchIndexService.Distance(typed, term, bound));
}
