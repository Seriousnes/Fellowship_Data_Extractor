using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Api.Tests;

/// <summary>
/// The two remaining spec section 9 claims that need the real corpus rather than a hand-built row:
/// every id the export carries resolves through the store the way a route asks for it, and every
/// row with a number to fill in fills it, ability 1855's twice-spent token included.
/// </summary>
[Collection(Corpus.Name)]
public class CorpusTests : BunitContext
{
    private readonly EntityStore Store;

    public CorpusTests(Export export)
    {
        Store = export.Store;
        Services.AddSingleton(export.Tooltip);
    }

    /// <summary>
    /// For every (type, id) pair the corpus carries, <see cref="EntityStore.All"/> answers at least
    /// one document, and <see cref="EntityStore.Resolve"/> answers <see cref="Lookup.Found"/> once
    /// given the name of a hero the row's own <see cref="EntityStore.Wearer"/> already answers for
    /// it - the same pairing a tooltip route resolves an ambiguous id with a hero query.
    /// </summary>
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

    /// <summary>
    /// Every row whose placeholders the build gives a formula for renders every number it needs: no
    /// unresolved-token marker (<c>class="tok"</c>, <see cref="Substitution.Missing"/>'s own span)
    /// survives a real render. Covers both populations Task 1 measured: the rows read through
    /// <see cref="CodexView.PlaceholdersOf"/> and the gem types with an overflow sentence of their
    /// own.
    ///
    /// A token carrying none of <see cref="Placeholder.Text"/>, <see cref="Placeholder.Coefficient"/>
    /// or <see cref="Placeholder.Value"/> is not this assertion's to police: the build itself states
    /// no formula for it, and <see cref="Placeholders.Unresolved"/> prints the honest "[worked out in
    /// game]" or "[not in the build]" reading for it by design, in the same <c>class="tok"</c> span a
    /// wiring bug would produce - and 8 of these are never spent by any text at all, so their row
    /// renders no marker for them either way. Rows carrying at least one are excluded here rather
    /// than silently folded into the population that must render clean.
    /// </summary>
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
        document is GemType { OverflowPlaceholders: { } overflow } ? overflow : CodexView.PlaceholdersOf(document);

    /// <summary>
    /// Ability 1855's description spends <c>{BrillFlashHeal}</c> twice, ahead of the one place it
    /// spends <c>{BrillFlashDmg}</c>, and both BrillFlashHeal readings carry the same number: the
    /// substitution map is built once per key, so every occurrence of a key reads the one entry.
    /// </summary>
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
