using Fellowship.Codex.Endpoints;

namespace Fellowship.Codex.Tests;

public sealed class ParameterKeyTests
{
    private static IReadOnlySet<string> Properties(params Type[] requests) =>
        requests.SelectMany(request => request.GetProperties()).Select(property => property.Name).ToHashSet();

    [Fact]
    public void Every_reading_parameter_is_a_tooltip_request_property()
    {
        var properties = Properties(
            [.. typeof(ItemTooltipRequest).Assembly.GetTypes().Where(type => type.Name.EndsWith("TooltipRequest"))]);

        Assert.All(Enum.GetNames<ReadingParameter>(), name => Assert.Contains(name, properties));
    }

    [Fact]
    public void Every_browse_parameter_is_an_entities_or_search_request_property() =>
        Assert.All(
            Enum.GetNames<BrowseParameter>(),
            name => Assert.Contains(name, Properties(typeof(EntitiesRequest), typeof(SearchRequest))));

    [Theory]
    [InlineData(FalloffParameter.Ability, EntityType.Ability)]
    [InlineData(FalloffParameter.Talent, EntityType.Talent)]
    public void A_falloff_source_key_is_its_type_slug(FalloffParameter parameter, EntityType type) =>
        Assert.Equal(EntityTypes.Slug(type), QueryKeys.Key(parameter));

    [Fact]
    public void A_parameter_key_is_the_camel_cased_member_name()
    {
        Assert.Equal("itemLevel", QueryKeys.Key(ReadingParameter.ItemLevel));
        Assert.Equal("rankSources", QueryKeys.Key(ReadingParameter.RankSources));
        Assert.Equal("q", QueryKeys.Key(BrowseParameter.Q));
    }
}
