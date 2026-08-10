using System.Reflection;

using Fellowship.SDK.Documents;

using FellowshipExtractor.Api;
using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

using Microsoft.Extensions.Logging.Abstractions;

namespace FellowshipExtractor.Tests;

public sealed class EntityStoreCorpusTests
{
    private static readonly EntityStoreService Store = Build();

    private static EntityStoreService Build()
    {
        var snapshot = new FolderExportSource(Export.Loaded.Folder)
            .LoadAsync(CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        return new EntityStoreService(snapshot, NullLogger<EntityStoreService>.Instance);
    }

    public static IEnumerable<object[]> Counts()
    {
        var method = typeof(TypeResolverTests).GetMethod(nameof(TypeResolverTests.TheCorpusHoldsTheseCounts))!;

        return method.GetCustomAttributes<InlineDataAttribute>().Select(attribute => attribute.GetData(method).Single());
    }

    [Theory]
    [MemberData(nameof(Counts))]
    public void TheStoreHoldsTheseCounts(EntityType type, int expected) =>
        Assert.Equal(expected, Store.Entities.Count(document => EntityTypes.TypeOf(document) == type));

    [Fact]
    public void TheStoreHolds12473Entities() => Assert.Equal(12473, Store.Count);

    private static readonly IReadOnlySet<int> Granted = Store.Entities
        .OfType<ItemDocument>()
        .Select(item => item.GrantsId)
        .OfType<int>()
        .ToHashSet();

    private static EntityDocument[] Ungranted(EntityType type) =>
    [
        .. Store.Entities
            .Where(document => EntityTypes.TypeOf(document) == type)
            .Where(document => !Granted.Contains(document.Id)),
    ];

    [Theory]
    [InlineData(EntityType.WeaponPower)]
    [InlineData(EntityType.RelicPower)]
    public void EveryPowerAnItemCarriesIsReachedByAnItemsGrantsId(EntityType type) =>
        Assert.Empty(Ungranted(type).Select(document => $"{document.Id} {document.Name}"));

    [Fact]
    public void TheOneLegendaryPowerNoItemGrantsIsUnimplemented()
    {
        var placeholder = Assert.Single(Ungranted(EntityType.LegendaryPower));

        Assert.Equal(713, placeholder.Id);
        Assert.Equal(
            "Placeholder - Not Implemented",
            Assert.IsType<LegendaryPowerDocument>(placeholder).Description);
    }

    [Fact]
    public void EveryGrantsIdNamesAPowerTheStoreHolds()
    {
        var unreachable = Store.Entities
            .OfType<ItemDocument>()
            .Where(item => item.GrantsId is { } id && Store.GrantedPower(id) is null)
            .Select(item => $"{item.Id} {item.Name} grants {item.GrantsId}")
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(unreachable);
    }
}
