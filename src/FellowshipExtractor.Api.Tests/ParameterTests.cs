using System.Reflection;
using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;

namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class ParameterTests(Export export)
{
    private static IQueryCollection Query(params (string Key, string[] Values)[] asked) =>
        new QueryCollection(asked.ToDictionary(a => a.Key, a => new StringValues(a.Values)));

    private sealed record Probe
    {
        public int Id { get; init; }

        public string? Hero { get; init; }
    }

    [Fact]
    public void A_request_model_declares_its_own_property_names()
    {
        var declared = RequestProperties.Of(typeof(Probe));

        Assert.Equal(["Hero", "Id"], declared.Order(StringComparer.Ordinal));
        Assert.Contains("hero", declared);
    }

    [Fact]
    public void A_key_that_is_neither_a_property_nor_a_stat_is_named_back()
    {
        StatQuery.Read(new Probe(), Query(("nonesuch", ["1"])), export.Store.Stats, out var error);

        Assert.Equal("Unknown parameter 'nonesuch'.", error);
    }

    [Fact]
    public void A_stat_the_build_names_is_read_off_the_raw_query()
    {
        var query = StatQuery.Read(new Probe(), Query(("Stamina", ["40"])), export.Store.Stats, out var error);

        Assert.Null(error);
        Assert.Equal(40m, query.Total("Stamina"));
    }

    [Fact]
    public void A_property_of_the_model_is_left_for_the_binder()
    {
        StatQuery.Read(new Probe(), Query(("hero", ["Barbarian"])), export.Store.Stats, out var error);

        Assert.Null(error);
    }

    [Fact]
    public void A_comma_and_a_repetition_read_the_same_traits()
    {
        Assert.True(ImbuedTraits.TryParse("1:2,3:4", out var joined));
        Assert.True(ImbuedTraits.TryParse(string.Join(',', "1:2", "3:4"), out var repeated));

        Assert.Equal([new ImbuedTrait(1, 2), new ImbuedTrait(3, 4)], joined.Values);
        Assert.Equal(joined.Values, repeated.Values);

        Assert.True(ImbuedTraits.TryParse("7", out var bare));
        Assert.Equal([new ImbuedTrait(7, 1)], bare.Values);

        Assert.False(ImbuedTraits.TryParse("hunters-focus", out _));
    }

    [Fact]
    public void One_modifier_and_several_read_back_as_the_entries_they_name()
    {
        Assert.True(ItemModifiers.TryParse("trait:412:1", out var alone));

        Assert.Equal([new ItemModifier(ModifierKind.Trait, 412, 1)], alone.Values);

        Assert.True(ItemModifiers.TryParse("trait:412:1,blessing:7:1,essence:3:100", out var several));

        Assert.Equal(
            [
                new ItemModifier(ModifierKind.Trait, 412, 1),
                new ItemModifier(ModifierKind.Blessing, 7, 1),
                new ItemModifier(ModifierKind.Essence, 3, 100),
            ],
            several.Values);
    }

    [Fact]
    public void Every_kind_the_enum_names_is_read_from_a_token_of_its_own()
    {
        foreach (var kind in Enum.GetValues<ModifierKind>())
        {
            Assert.True(ItemModifiers.TryParse($"{TooltipView.Token(kind)}:1:2", out var read));
            Assert.Equal([new ItemModifier(kind, 1, 2)], read.Values);

            Assert.True(ItemModifiers.TryParse($"{TooltipView.Token(kind).ToUpperInvariant()}:1:2", out var loud));
            Assert.Equal(read.Values, loud.Values);
        }
    }

    [Fact]
    public void An_entry_the_reader_cannot_make_out_is_refused()
    {
        Assert.False(ItemModifiers.TryParse("nonesuch:1:2", out _));
        Assert.False(ItemModifiers.TryParse("trait:1", out _));
        Assert.False(ItemModifiers.TryParse("trait", out _));
        Assert.False(ItemModifiers.TryParse("trait:one:2", out _));
        Assert.False(ItemModifiers.TryParse("trait:1:two", out _));
        Assert.False(ItemModifiers.TryParse("trait:1:2:3", out _));
        Assert.False(ItemModifiers.TryParse("trait:412:1,nonesuch:7:1", out _));

        Assert.True(ItemModifiers.TryParse(null, out var none));
        Assert.Empty(none.Values);
    }
}

public sealed class PublishedParameterTests : IClassFixture<RouteTests.Factory>
{
    private readonly JsonDocument document;

    private readonly IReadOnlyList<RouteEndpoint> endpoints;

    private readonly IReadOnlySet<string> filters;

    public PublishedParameterTests(RouteTests.Factory factory)
    {
        var client = factory.CreateClient();

        document = JsonDocument.Parse(
            client.GetStringAsync(ApiDocument.Route).GetAwaiter().GetResult());

        endpoints =
            [.. factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()];

        var codex = factory.Services.GetRequiredService<ICodexService>();

        filters = codex.Facets.Select(facet => facet.Key)
            .Concat(codex.Flags.Select(flag => flag.Key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private JsonElement Operation(string path) =>
        document.RootElement.GetProperty("paths").GetProperty(path).GetProperty("get");

    private static IReadOnlyList<(string Name, bool Required, string? In)> Parameters(JsonElement operation) =>
        operation.TryGetProperty("parameters", out var declared)
            ? [.. declared.EnumerateArray().Select(parameter => (
                parameter.GetProperty("name").GetString()!,
                parameter.TryGetProperty("required", out var required) && required.GetBoolean(),
                parameter.TryGetProperty("in", out var where) ? where.GetString() : null))]
            : [];

    [Fact]
    public void An_ability_tooltip_declares_the_id_and_the_hero_and_nothing_else()
    {
        var names = Parameters(Operation("/api/ability/{id}/tooltip"))
            .Select(parameter => parameter.Name)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(["Hero", "id"], names);
    }

    [Fact]
    public void The_item_alone_declares_the_level_the_tempering_and_the_potential()
    {
        string[] owned = ["ItemLevel", "Upgrades", "MaxUpgrades", "PotentialPower"];

        foreach (var (path, operation) in document.RootElement.GetProperty("paths").EnumerateObject()
            .Where(entry => entry.Name.EndsWith("/tooltip", StringComparison.Ordinal))
            .Select(entry => (entry.Name, entry.Value.GetProperty("get"))))
        {
            var names = Parameters(operation)
                .Select(parameter => parameter.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var name in owned)
            {
                if (!names.Contains(name)) continue;

                Assert.Equal("/api/item/{id}/tooltip", path);
            }
        }
    }

    [Fact]
    public void Every_declared_parameter_binds_a_property_of_that_endpoint_s_request_model()
    {
        foreach (var endpoint in endpoints)
        {
            var pattern = endpoint.RoutePattern.RawText;
            if (pattern is null || !pattern.StartsWith("/api/", StringComparison.Ordinal)) continue;

            var published = pattern.Replace(":int", string.Empty, StringComparison.Ordinal);
            if (!document.RootElement.GetProperty("paths").TryGetProperty(published, out var path)) continue;

            var model = endpoint.Metadata.OfType<MethodInfo>()
                .SelectMany(method => method.GetParameters())
                .FirstOrDefault(parameter => parameter.GetCustomAttribute<AsParametersAttribute>() is not null);

            if (model is null) continue;

            var declared = RequestProperties.Of(model.ParameterType);

            foreach (var (name, _, _) in Parameters(path.GetProperty("get")))
            {
                Assert.True(
                    declared.Contains(name) || filters.Contains(name),
                    $"{published} declares '{name}' and its model has no such property, " +
                    "and the codex names no such facet or flag.");
            }
        }
    }

    [Fact]
    public void The_id_is_the_only_required_parameter_on_any_route()
    {
        foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
        {
            foreach (var (name, required, where) in Parameters(path.Value.GetProperty("get")))
            {
                if (!required) continue;

                Assert.Equal("id", name);
                Assert.Equal("path", where);
            }
        }
    }
}
