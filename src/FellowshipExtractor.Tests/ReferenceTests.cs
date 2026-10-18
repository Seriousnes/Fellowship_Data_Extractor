using System.Reflection;

using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;

using Fellowship.Codex.Services;

namespace FellowshipExtractor.Tests;

public sealed class ReferenceTests
{
    private static readonly IEntityStoreService Store = EntityStoreCorpusTests.Store;

    private static IEnumerable<(EntityDocument Holder, string Field, EntityReference Reference)> All()
    {
        foreach (var document in Store.Entities)
        {
            foreach (var property in Carriers(document.GetType()))
            {
                switch (property.GetValue(document))
                {
                    case EntityReference one:
                        yield return (document, property.Name, one);
                        break;

                    case IReadOnlyList<EntityReference> many:
                        foreach (var reference in many) yield return (document, property.Name, reference);
                        break;

                    case IReadOnlyList<CreatureAbility> wielded:
                        foreach (var slot in wielded) yield return (document, property.Name, slot.Ability);
                        break;

                    case IReadOnlyList<ItemSource> sources:
                        foreach (var source in sources)
                        {
                            if (source.Dungeon is { } dungeon) yield return (document, property.Name, dungeon);
                        }

                        break;

                    case IReadOnlyList<TargetCountFalloff> falloffs:
                        foreach (var falloff in falloffs)
                        {
                            if (falloff.Effect is { } effect) yield return (document, property.Name, effect);
                        }

                        break;
                }
            }
        }
    }

    private static PropertyInfo[] Carriers(Type document) =>
    [
        .. document.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property =>
                property.PropertyType == typeof(EntityReference) ||
                property.PropertyType == typeof(IReadOnlyList<EntityReference>) ||
                property.PropertyType == typeof(IReadOnlyList<CreatureAbility>) ||
                property.PropertyType == typeof(IReadOnlyList<ItemSource>) ||
                property.PropertyType == typeof(IReadOnlyList<TargetCountFalloff>)),
    ];

    [Fact]
    public void EveryReferenceNamesADocumentTheExportHolds()
    {
        var dangling = All()
            .Where(carried => Store.All(carried.Reference.Type, carried.Reference.Id).Count == 0)
            .Select(carried =>
                $"{EntityTypes.TypeOf(carried.Holder)} {carried.Holder.Id}.{carried.Field} names " +
                $"{carried.Reference.Type} {carried.Reference.Id}")
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(dangling);
    }

    [Fact]
    public void EveryReferenceCarriesTheNameTheDocumentItNamesCarries()
    {
        var disagreeing = All()
            .Where(carried => carried.Reference.Name is not null)
            .Where(carried => Store.All(carried.Reference.Type, carried.Reference.Id) is [var named, ..] &&
                !string.Equals(named.Name, carried.Reference.Name, StringComparison.Ordinal))
            .Select(carried =>
                $"{EntityTypes.TypeOf(carried.Holder)} {carried.Holder.Id}.{carried.Field} calls " +
                $"{carried.Reference.Type} {carried.Reference.Id} '{carried.Reference.Name}'")
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(disagreeing);
    }

    [Fact]
    public void NoDocumentPointsAtAnotherByABareId()
    {
        var scalars = typeof(EntityDocument).Assembly
            .GetTypes()
            .Where(type => type.IsSubclassOf(typeof(EntityDocument)))
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.Name is not nameof(EntityDocument.Id))
                .Where(property => property.Name.EndsWith("Id", StringComparison.Ordinal)
                    || property.Name.EndsWith("Ids", StringComparison.Ordinal))
                .Select(property => $"{type.Name}.{property.Name}"))
            .ToArray();

        Assert.Empty(scalars);
    }

    [Fact]
    public void ACreatureIsPlacedInDungeonsTheExportHoldsDocumentsFor()
    {
        var dungeons = Store.Entities.OfType<DungeonDocument>().Select(dungeon => dungeon.Id).ToHashSet();

        Assert.NotEmpty(dungeons);

        var placed = Store.Entities
            .OfType<CreatureDocument>()
            .SelectMany(creature => creature.FoundIn ?? [])
            .Select(level => level.Id)
            .Distinct()
            .ToArray();

        Assert.NotEmpty(placed);
        Assert.All(placed, id => Assert.Contains(id, dungeons));
    }

    [Fact]
    public void EveryDungeonAnItemDropsInIsTheDungeonDocumentOfThatName()
    {
        var sources = Store.Entities
            .OfType<ItemDocument>()
            .SelectMany(item => item.Sources ?? [])
            .Where(source => source.Kind == SourceKind.Dungeon)
            .Distinct()
            .ToArray();

        Assert.NotEmpty(sources);

        var unnamed = sources
            .Where(source => source.Dungeon is null)
            .Select(source => source.Name)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["Any dungeon"], unnamed);

        Assert.All(
            sources.Where(source => source.Dungeon is { } dungeon &&
                Store.All(dungeon.Type, dungeon.Id) is [DungeonDocument named, ..] &&
                !string.Equals(named.Name, source.Name, StringComparison.Ordinal)),
            source => Assert.Fail($"{source.Name} names {source.Dungeon!.Id}, which reads differently."));
    }
}
