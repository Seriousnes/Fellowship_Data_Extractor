using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;

using SkiaSharp;

using Fellowship.Codex.Endpoints;

namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class CardTests(Export export)
{
    private readonly CardService cards = new(
        export.View,
        export.Tooltip,
        new FragmentService(Services(export), NullLoggerFactory.Instance),
        new FolderTextureReader(Path.Combine(Project.Repository, "output")));

    private static IServiceScopeFactory Services(Export export) =>
        new ServiceCollection()
            .AddSingleton(export.View)
            .AddSingleton(export.Tooltip)
            .AddSingleton(export.Store)
            .AddSingleton(export.Chrome)
            .BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();

    private TooltipQuery Query(EntityDocument document, params (string Key, string Value)[] pairs) =>
        PageQuery.Read(
            pairs.ToDictionary(pair => pair.Key, pair => new StringValues(pair.Value), StringComparer.OrdinalIgnoreCase),
            export.Store,
            null,
            document);

    private ItemDocument Rolled => export.Store.Entities.OfType<ItemDocument>()
        .First(item => item.Icon is not null && item.Rarity is not null &&
            export.Tooltip.Rolls(item, new TooltipQuery(), export.Tooltip.Rarity(item, new TooltipQuery())).Count > 0);

    [Fact]
    public async Task A_card_is_a_png_at_the_card_size()
    {
        var card = await cards.Card(Rolled, Query(Rolled));

        using var bitmap = SKBitmap.Decode(await cards.Render(card, TestContext.Current.CancellationToken));

        Assert.Equal(cards.Width, bitmap.Width);
        Assert.Equal(cards.Height, bitmap.Height);
    }

    [Fact]
    public async Task A_card_draws_the_entity_icon()
    {
        var card = new EntityCard("Card", "#FFFFFF", "Item", [],
            [new CardLayer("/" + TextureSizes.Key(Rolled.Icon!, TextureSize.Full), 1, false)]);

        using var drawn = SKBitmap.Decode(await cards.Render(card, TestContext.Current.CancellationToken));
        using var bare = SKBitmap.Decode(await cards.Render(card with { Icon = [] }, TestContext.Current.CancellationToken));

        var centre = cards.Height / 2;

        Assert.NotEqual(bare.GetPixel(centre, centre), drawn.GetPixel(centre, centre));
    }

    [Fact]
    public async Task An_item_card_lists_the_stats_its_tooltip_rolls()
    {
        var query = Query(Rolled);
        var card = await cards.Card(Rolled, query);

        foreach (var line in export.Tooltip.Rolls(Rolled, query, export.Tooltip.Rarity(Rolled, query)))
        {
            Assert.Contains(StatNames.Short(line.Stat, line.DisplayName).Replace(' ', ' '), card.Lines[0].Text);
        }
    }

    [Fact]
    public void Every_short_stat_name_names_a_stat_the_settings_read()
    {
        Assert.All(StatNames.Shortened.Keys, key => Assert.Contains(key, export.Store.Settings.StatCalculation.Attributes.Keys));
    }

    [Fact]
    public async Task An_item_card_names_the_ability_the_item_grants()
    {
        var granting = export.Store.Entities.OfType<ItemDocument>()
            .First(item => export.Tooltip.Granted(item) is { Name: not null });

        var card = await cards.Card(granting, Query(granting));

        Assert.Contains(card.Lines, line => line.Lead == export.Tooltip.Granted(granting)!.Name);
    }

    [Fact]
    public async Task A_creature_card_reads_its_kill_score_and_dungeons()
    {
        var creature = export.Store.Entities.OfType<CreatureDocument>()
            .First(entry => entry.KillScore is not null &&
                entry.FoundIn?.Count(found => found.Type == EntityType.Dungeon) > 1);

        var card = await cards.Card(creature, Query(creature));

        Assert.Contains(creature.KillScore!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), card.Kind);
        var shown = card.Lines[0].Text.Split(", ");

        foreach (var found in creature.FoundIn!.Where(found => found.Type == EntityType.Dungeon))
        {
            var words = export.View.Name(EntityType.Dungeon, found.Id).Split(' ', StringSplitOptions.RemoveEmptyEntries);

            Assert.Contains(words.Length == 1 ? words[0] : string.Concat(words.Select(word => word[0])), shown);
        }
    }

    [Fact]
    public async Task A_card_names_the_rarity_its_address_requests()
    {
        var rarity = export.Store.Settings.Rarities.First(entry => entry.Tier != (int)Rolled.Rarity!);

        var card = await cards.Card(Rolled, Query(Rolled, ("rarity", rarity.Name)));

        Assert.Equal(CodexView.Readable(export.View.Tokens.Rarity(rarity.Name)), card.Colour);
        Assert.StartsWith(rarity.DisplayName, card.Kind);
    }

    [Fact]
    public async Task An_enchant_card_reads_the_rank_its_sources_give()
    {
        var enchant = export.Store.Entities.OfType<EnchantDocument>()
            .First(entry => AugmentationRanks.Carriers(export.Store, entry).Count > 1);
        var sources = RankSources.Write(AugmentationRanks.Carriers(export.Store, enchant).Select(carrier => carrier.Type))!;

        var bare = await cards.Card(enchant, Query(enchant));
        var ranked = await cards.Card(enchant, Query(enchant, ("rankSources", sources)));

        Assert.NotEqual(bare.Lines, ranked.Lines);
    }

    [Fact]
    public async Task A_glyph_card_draws_the_allocated_crest()
    {
        var glyph = export.Store.Entities.OfType<GlyphDocument>().First();

        var card = await cards.Card(glyph, Query(glyph));

        Assert.Equal(export.Chrome.GlyphPlate(ChromeAssetService.GlyphState(1)), card.Icon[0].Address);
        Assert.Equal(export.Chrome.GlyphRune(glyph, ChromeAssetService.GlyphState(1)), card.Icon[1].Address);
    }
}
