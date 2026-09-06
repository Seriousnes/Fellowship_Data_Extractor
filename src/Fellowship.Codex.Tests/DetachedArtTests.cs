using Fellowship.Codex.Components.Tooltip;
using Fellowship.SDK.Client;

namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class DetachedArtTests : BunitContext
{
    private readonly Export export;

    public DetachedArtTests(Export export)
    {
        this.export = export;
        Services.AddSingleton(export.Tooltip);
        Services.AddSingleton(export.View);
        Services.AddSingleton(export.Store);
    }

    private IRenderedComponent<Tooltip> Draw(EntityDocument document, bool ShowIcon) =>
        Render<Tooltip>(p => p
            .Add(x => x.Entity, document)
            .Add(x => x.Query, new TooltipQuery { ShowIcon = ShowIcon }));

    private EntityDocument[] Beside =>
    [
        export.Store.Entities.OfType<ItemDocument>().First(row => row.Icon is not null),
        export.Store.Entities.OfType<AbilityDocument>().First(row => row.Icon is not null),
        export.Store.Entities.OfType<EffectDocument>().First(row => row.Icon is not null),
        export.Store.Entities.OfType<TalentDocument>().First(row => row.Icon is not null),
    ];

    [Fact]
    public void A_card_asked_for_nothing_draws_no_art_beside_it()
    {
        Assert.All(Beside, document => Assert.Empty(Draw(document, false).FindAll("figure.fx-slot")));
    }

    [Fact]
    public void The_same_card_draws_its_art_when_the_query_asks_for_it()
    {
        Assert.All(Beside, document => Assert.NotEmpty(Draw(document, true).FindAll("figure.fx-slot")));
    }

    [Fact]
    public void Dropping_the_art_leaves_the_card_itself_whole()
    {
        Assert.All(Beside, document =>
        {
            var bare = Draw(document, false);

            Assert.NotNull(bare.Find($".{TooltipView.Root} > .card > .head > .title"));
            Assert.StartsWith($"<div class=\"{TooltipView.Root}", bare.Markup.Bare(), StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Art_a_card_carries_inside_is_untouched_by_the_flag()
    {
        EntityDocument[] inside =
        [
            export.Store.Entities.OfType<TraitDocument>().First(row => row.Icon is not null),
            export.Store.Entities.OfType<BlessingDocument>().First(row => row.Icon is not null),
        ];

        Assert.All(inside, document =>
        {
            Assert.Equal(
                Draw(document, false).Markup.Bare(), Draw(document, true).Markup.Bare());

            Assert.Empty(Draw(document, false).FindAll("figure.fx-slot"));
        });
    }

    [Fact]
    public void A_tooltip_request_asks_for_no_art_unless_the_caller_says_so()
    {
        var document = Beside[0];
        var view = export.View;

        Assert.DoesNotContain(
            nameof(TooltipRequest.ShowIcon),
            view.Tip(document).Body ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);

        Assert.Contains("\"showIcon\":true", view.Tip(document, true).Body!, StringComparison.Ordinal);
    }

    [Fact]
    public void A_tooltip_request_keeps_the_hero_it_already_carried_beside_the_art_it_asks_for()
    {
        var shared = export.Store.Entities
            .OfType<ItemDocument>()
            .FirstOrDefault(row => export.View.Reading(row) is not null);

        Assert.SkipWhen(shared is null, "No id in this build reads differently per hero.");

        var tip = export.View.Tip(shared!, true);

        Assert.Equal($"/{CodexAddresses.Tooltip(EntityType.Item, shared!.Id)}", tip.Address);
        Assert.Contains("\"hero\":", tip.Body!, StringComparison.Ordinal);
        Assert.Contains("\"showIcon\":true", tip.Body!, StringComparison.Ordinal);
    }
}
