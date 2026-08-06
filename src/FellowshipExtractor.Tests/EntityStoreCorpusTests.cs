using System.Reflection;
using FellowshipExtractor.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Sdk;

namespace FellowshipExtractor.Tests;

/// <summary>
/// The corpus the API's EntityStore reads back is not empty: it holds every row the resolver
/// produced, indexed rather than merely deserialised. This is the assertion that fails when the
/// store deserialises every line and indexes none of them, the failure mode the whole move to typed
/// documents was guarding against.
///
/// The per-type counts are read off <see cref="TypeResolverTests.TheCorpusHoldsTheseCounts"/>'s own
/// InlineData by reflection rather than restated here, so the 21 numbers live in exactly one place
/// and the two suites cannot silently drift onto different figures for the same population.
/// </summary>
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

    /// <summary>12,437 rows over 21 types, spec section 3.4's own total.</summary>
    [Fact]
    public void TheStoreHolds12437Entities() => Assert.Equal(12437, Store.Count);
}
