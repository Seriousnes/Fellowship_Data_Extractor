using System.Globalization;

namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class TooltipDumpTests : BunitContext
{
    private const string Variable = "FELLOWSHIP_TIP_DUMP";

    private readonly Export export;

    public TooltipDumpTests(Export export)
    {
        this.export = export;
        Services.AddSingleton(export.Tooltip);
        Services.AddSingleton(export.View);
    }

    [Fact]
    public void Draws_every_case_into_the_folder_the_variable_names()
    {
        var folder = Environment.GetEnvironmentVariable(Variable);

        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(folder),
            $"{Variable} names no folder, so there is nothing to write.");

        Directory.CreateDirectory(folder!);

        var written = 0;

        foreach (var type in Enum.GetValues<EntityType>().OrderBy(EntityTypes.Slug, StringComparer.Ordinal))
        {
            var slug = EntityTypes.Slug(type);

            var ids = export.Store.Entities
                .Where(row => EntityTypes.TypeOf(row) == type)
                .Select(row => row.Id)
                .Distinct()
                .Order()
                .Take(3);

            foreach (var id in ids)
            {
                var document = export.Store.All(type, id)[0];

                foreach (var (name, query) in Cases(document))
                {
                    var hero = query.Hero is null ? null : export.Store.Hero(query.Hero);

                    var cut = Render<Components.Tooltip.Tooltip>(p => p
                        .Add(x => x.Entity, document)
                        .Add(x => x.Query, query)
                        .Add(x => x.Hero, hero));

                    File.WriteAllText(
                        Path.Combine(folder!, $"{slug}-{id.ToString(CultureInfo.InvariantCulture)}-{name}.html"),
                        cut.Markup);

                    written++;
                }
            }
        }

        Assert.True(written > 0, "The store answered no rows, so the dump measures nothing.");
    }

    private IEnumerable<(string Name, TooltipQuery Query)> Cases(EntityDocument document)
    {
        yield return ("bare", new TooltipQuery());

        var rung = export.Store.Vocabulary.Rarities[^1];
        yield return ("rarity", new TooltipQuery { Rarity = rung.Tier.ToString(CultureInfo.InvariantCulture) });

        yield return ("level", new TooltipQuery
        {
            ItemLevel = export.Store.DropItemLevel(document),
            Upgrades = 2,
        });

        if (FirstTrait() is { } trait)
        {
            yield return ("traits", new TooltipQuery
            {
                Traits = [new ImbuedTrait(trait, 2)],
                MaxTraits = 3,
            });
        }

        yield return ("potential", new TooltipQuery { PotentialPower = 42m });

        if (export.Store.Wearer(document) is { } wearer)
        {
            yield return ("hero", new TooltipQuery { Hero = wearer.Name });
        }
    }

    private int? FirstTrait() => export.Store.Entities
        .Where(row => EntityTypes.TypeOf(row) == TooltipView.TraitType)
        .Select(row => (int?)row.Id)
        .Order()
        .FirstOrDefault();
}
