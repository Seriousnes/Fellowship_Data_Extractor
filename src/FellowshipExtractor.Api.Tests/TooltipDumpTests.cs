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

            var ids = Ids(type);

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

        var rung = export.Store.Settings.Rarities[^1];
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
                ImbuedTraits = [new ImbuedTrait(trait, 2)],
                HasImbuedTraits = true,
            });
        }

        if (Modifiers() is { Count: > 0 } modifiers)
        {
            yield return ("modifiers", new TooltipQuery { Modifiers = modifiers });
        }

        yield return ("potential", new TooltipQuery { PotentialPower = 42m });

        if (export.Store.Wearer(document) is { } wearer)
        {
            yield return ("hero", new TooltipQuery { Hero = wearer.Name });
        }
    }

    private IReadOnlyList<int> Ids(EntityType type)
    {
        var rows = export.Store.Entities.Where(row => EntityTypes.TypeOf(row) == type).ToArray();

        var ids = rows.Select(row => row.Id).Distinct().Order().Take(3).ToList();

        foreach (var id in Shapes(rows))
        {
            if (!ids.Contains(id)) ids.Add(id);
        }

        return [.. ids.Order()];
    }

    private static IEnumerable<int> Shapes(IReadOnlyList<EntityDocument> rows)
    {
        int? First(Func<EntityDocument, bool> carries) =>
            rows.Where(carries).Select(row => (int?)row.Id).Order().FirstOrDefault();

        int?[] picks =
        [
            First(row => row is ItemDocument { GrantsId: not null }),
            First(row => row is ItemDocument { LegendaryStatIds.Count: > 0 }),
            First(row => row is ItemDocument { SetBonus.Count: > 0 }),
            First(row => row is ItemDocument { GemSlot: true }),
            First(row => row is ItemDocument { UniqueEquipped: true }),
            First(row => row is ItemDocument { ArmorType: not null }),
            First(row => row is ItemDocument { Slot: not null }),
            First(row => row is ItemDocument { Stats.Count: > 0 }),
            First(row => row is ItemDocument { Description: not null }),
            First(row => row is AbilityDocument { Detail.Count: > 0 }),
            First(row => row is GemTypeDocument { OverflowDescription: not null }),
            .. TooltipView.ImbuedTraitCapacities.Keys.Select(slot => First(row =>
                row is ItemDocument worn &&
                string.Equals(worn.Slot, slot, StringComparison.OrdinalIgnoreCase))),
        ];

        return picks.Where(id => id is not null).Select(id => id!.Value);
    }

    private int? FirstTrait() => Lowest(TooltipView.TraitType);

    private IReadOnlyList<ItemModifier> Modifiers() =>
    [
        .. Enum.GetValues<ModifierKind>()
            .Select(kind => (Kind: kind, Id: Lowest(TooltipView.TypeOf(kind))))
            .Where(carried => carried.Id is not null)
            .Select(carried => new ItemModifier(carried.Kind, carried.Id!.Value, 1)),
    ];

    private int? Lowest(EntityType type) => export.Store.Entities
        .Where(row => EntityTypes.TypeOf(row) == type)
        .Select(row => (int?)row.Id)
        .Order()
        .FirstOrDefault();
}
