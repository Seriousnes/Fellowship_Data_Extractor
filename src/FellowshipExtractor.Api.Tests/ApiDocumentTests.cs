using Microsoft.AspNetCore.Http;

namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class ApiDocumentTests(Export export)
{
    private static readonly IReadOnlyList<ApiParameter> Declared =
        [.. TooltipQuery.Parameters, .. CodexQuery.Parameters, .. SuggestionService.Parameters];

    [Fact]
    public void Every_parameter_the_document_declares_is_one_a_reader_can_name()
    {
        foreach (var parameter in Declared)
        {
            Assert.False(
                string.IsNullOrWhiteSpace(parameter.Name),
                "A parameter with no name would land in the document as an unusable field.");

            Assert.EndsWith(".", parameter.Description, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void No_parameter_is_declared_twice_on_one_endpoint()
    {
        foreach (var set in new[] { TooltipQuery.Parameters, CodexQuery.Parameters, SuggestionService.Parameters })
        {
            var names = set.Select(parameter => parameter.Name).ToArray();

            Assert.Equal(names.Length, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
    }

    [Fact]
    public void Every_tooltip_parameter_the_document_declares_changes_the_reading()
    {
        var bare = Signature(TooltipQuery.Parse(Query([]), export.Store.Stats, out _));

        foreach (var parameter in TooltipQuery.Parameters)
        {
            var query = Query(new Dictionary<string, string> { [parameter.Name] = Sample(parameter) });

            var parsed = TooltipQuery.Parse(query, export.Store.Stats, out var error);

            Assert.True(
                error is null,
                $"The document offers '{parameter.Name}' and the parser answers: {error}");

            Assert.True(
                Signature(parsed) != bare,
                $"'{parameter.Name}' is offered by the document and reserved by the parser, and giving it " +
                "changes nothing about the reading, so a caller would set it and see no difference.");
        }
    }

    private static string Signature(TooltipQuery query) => string.Join(
        '|',
        query.Hero, query.Rarity, query.State, query.EquippedTier,
        query.ItemLevel, query.Upgrades, query.MaxUpgrades, query.NoSocket,
        query.PotentialPower, query.Armor, query.AllocatedTraits, query.MaxTraits,
        query.Traits.Count, query.GemPowers.Count, query.Stats.Count, query.Attributes.Count);

    [Fact]
    public void A_parameter_the_document_never_declares_is_refused()
    {
        TooltipQuery.Parse(
            Query(new Dictionary<string, string> { ["nonesuch"] = "1" }), export.Store.Stats, out var error);

        Assert.NotNull(error);
    }

    [Fact]
    public void Every_filter_parameter_the_document_declares_is_read_rather_than_refused()
    {
        var codex = new CodexService(export.Store, new DesignTokenService(export.Store.Vocabulary));

        IEnumerable<ApiParameter> offered =
        [
            .. CodexQuery.Parameters,
            .. codex.Facets.Select(facet => new ApiParameter(facet.Key, ApiValue.Text, facet.Label)),
            .. codex.Flags.Select(flag => new ApiParameter(flag.Key, ApiValue.Flag, flag.Label)),
        ];

        foreach (var parameter in offered)
        {
            var value = codex.Facet(parameter.Name) is { } facet
                ? codex.Offered(facet).FirstOrDefault() ?? string.Empty
                : Sample(parameter);

            CodexQuery.Parse(
                Query(new Dictionary<string, string> { [parameter.Name] = value }), codex, out var error);

            Assert.True(
                error is null,
                $"The document offers '{parameter.Name}' and the filter answers: {error}");
        }

        foreach (var measure in codex.Measures)
        {
            foreach (var bound in new[] { measure.Key + "Min", measure.Key + "Max" })
            {
                CodexQuery.Parse(
                    Query(new Dictionary<string, string> { [bound] = "1" }), codex, out var error);

                Assert.True(error is null, $"The document names '{bound}' and the filter answers: {error}");
            }
        }
    }

    [Fact]
    public void The_search_limit_holds_between_the_bounds_the_document_states()
    {
        Assert.Equal(SuggestionService.DefaultLimit, SuggestionService.Limit(null));
        Assert.Equal(SuggestionService.DefaultLimit, SuggestionService.Limit("not a number"));
        Assert.Equal(1, SuggestionService.Limit("0"));
        Assert.Equal(1, SuggestionService.Limit("-5"));
        Assert.Equal(SuggestionService.MaximumLimit, SuggestionService.Limit((SuggestionService.MaximumLimit + 1).ToString()));
        Assert.Equal(25, SuggestionService.Limit("25"));
    }

    private static string Sample(ApiParameter parameter) => parameter.Value switch
    {
        ApiValue.Whole => "1",
        ApiValue.Number => "1",
        ApiValue.Flag => "true",
        _ when parameter.Name.Equals("gemPowers", StringComparison.Ordinal) => "1:2",
        _ when parameter.Name.Equals("sort", StringComparison.Ordinal) => "name",
        _ when parameter.Name.Equals("type", StringComparison.Ordinal) => PublicTypes.All[0].Slug,
        _ when parameter.Name.Equals("group", StringComparison.Ordinal) => "slot",
        _ => "1",
    };

    private static IQueryCollection Query(Dictionary<string, string> pairs) =>
        new QueryCollection(pairs.ToDictionary(
            pair => pair.Key, pair => new Microsoft.Extensions.Primitives.StringValues(pair.Value)));
}
