using Fellowship.SDK.Models;

namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public sealed class HeroBadgeTests(Export export)
{
    private const double Floor = 4.5;

    [Fact]
    public void Every_hero_names_a_hero_document()
    {
        foreach (var hero in Enum.GetValues<Hero>())
        {
            Assert.NotNull(export.View.HeroNamed(hero.ToString()));
        }
    }

    [Fact]
    public void Every_hero_badge_text_meets_the_contrast_floor_on_its_fill()
    {
        foreach (var hero in export.Store.Entities.OfType<HeroDocument>().Where(hero => hero.Name is not null))
        {
            var fill = export.View.HeroFill(hero.Name!);
            var ink = export.View.HeroInk(hero.Name!);

            Assert.NotNull(fill);
            Assert.NotNull(ink);

            var (one, two) = (CodexView.Luminance(fill!), CodexView.Luminance(ink!));

            Assert.True(
                CodexView.Contrast(Math.Max(one, two), Math.Min(one, two)) >= Floor,
                $"{hero.Name}: {ink} on {fill}");
        }
    }

    [Fact]
    public void A_hero_tooltip_link_has_no_address()
    {
        var hero = export.Store.Entities.OfType<HeroDocument>().First();

        Assert.Null(export.View.Tip(hero).Address);
    }
}
