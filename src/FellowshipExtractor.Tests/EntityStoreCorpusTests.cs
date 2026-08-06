using System.Reflection;

using FellowshipExtractor.Api;
using FellowshipExtractor.Api.Models;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace FellowshipExtractor.Tests;

public sealed class EntityStoreCorpusTests
{
    private static readonly EntityStore Store = Build();

    private static EntityStore Build()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["FELLOWSHIP_EXPORT"] = Export.Loaded.Folder })
            .Build();

        return new EntityStore(new ExportPaths(configuration), NullLogger<EntityStore>.Instance);
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
    public void TheStoreHolds12437Entities() => Assert.Equal(12437, Store.Count);
}
