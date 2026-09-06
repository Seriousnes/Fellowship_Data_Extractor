using System.Reflection;
using System.Text.Json;

using Fellowship.SDK.Client;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;

namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class ParameterTests(Export export)
{
    private static IQueryCollection Query(params (string Key, string[] Values)[] pairs) =>
        new QueryCollection(pairs.ToDictionary(pair => pair.Key, pair => new StringValues(pair.Values)));

    private sealed record Probe
    {
        public int Id { get; init; }

        public string? Hero { get; init; }
    }

    private static IReadOnlySet<string> Declared => RequestProperties.Of(typeof(Probe));

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
        StatQuery.FromQuery(Query(("nonesuch", ["1"])), Declared, export.Store.Stats, out var error);

        Assert.Equal("Unknown parameter 'nonesuch'.", error);
    }

    [Fact]
    public void A_stat_the_build_names_is_read_off_the_raw_query()
    {
        var stats = StatQuery.FromQuery(Query(("Stamina", ["40"])), Declared, export.Store.Stats, out var error);

        Assert.Null(error);

        var query = StatQuery.Read(stats, export.Store.Stats, out error);

        Assert.Null(error);
        Assert.Equal(40m, query.Total("Stamina"));
    }

    [Fact]
    public void A_property_of_the_model_is_left_for_the_binder()
    {
        StatQuery.FromQuery(Query(("hero", ["Barbarian"])), Declared, export.Store.Stats, out var error);

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
    private static readonly string[] Methods = ["get", "query"];

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

    private JsonElement Operation(string path, string method) =>
        document.RootElement.GetProperty("paths").GetProperty(path).GetProperty(method);

    private static IEnumerable<JsonElement> Operations(JsonElement path) =>
        Methods.Where(method => path.TryGetProperty(method, out _)).Select(method => path.GetProperty(method));

    private IEnumerable<(string Path, JsonElement Operation)> Tooltips() =>
        document.RootElement.GetProperty("paths").EnumerateObject()
            .Where(entry => entry.Name.EndsWith("/tooltip", StringComparison.Ordinal))
            .Select(entry => (entry.Name, entry.Value.GetProperty("query")));

    private JsonElement Component(string name) =>
        document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty(name);

    private static IReadOnlySet<string> Properties(JsonElement schema) =>
        schema.GetProperty("properties").EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static IReadOnlySet<string> Kinds(JsonElement schema)
    {
        if (!schema.TryGetProperty("type", out var type)) return new HashSet<string>(StringComparer.Ordinal);

        return type.ValueKind == JsonValueKind.Array
            ? type.EnumerateArray().Select(entry => entry.GetString()!).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal) { type.GetString()! };
    }

    private static IReadOnlyList<(string Name, bool Required, string? In)> Parameters(JsonElement operation) =>
        operation.TryGetProperty("parameters", out var declared)
            ? [.. declared.EnumerateArray().Select(parameter => (
                parameter.GetProperty("name").GetString()!,
                parameter.TryGetProperty("required", out var required) && required.GetBoolean(),
                parameter.TryGetProperty("in", out var where) ? where.GetString() : null))]
            : [];

    [Fact]
    public void A_tooltip_takes_the_id_on_the_route_and_everything_else_off_the_shared_request()
    {
        var operation = Operation("/api/ability/{id}/tooltip", "query");

        Assert.Equal(["id"], Parameters(operation).Select(parameter => parameter.Name));

        Assert.Equal(
            $"#/components/schemas/{nameof(TooltipRequest)}",
            operation.GetProperty("requestBody")
                .GetProperty("content")
                .GetProperty("application/json")
                .GetProperty("schema")
                .GetProperty("$ref")
                .GetString());

        Assert.Equal(
            RequestProperties.Of(typeof(TooltipRequest)).Order(StringComparer.OrdinalIgnoreCase),
            Properties(Component(nameof(TooltipRequest))).Order(StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_shared_request_alone_declares_the_level_the_tempering_and_the_potential()
    {
        string[] owned = ["ItemLevel", "Upgrades", "MaxUpgrades", "PotentialPower"];

        var properties = Properties(Component(nameof(TooltipRequest)));

        foreach (var name in owned)
        {
            Assert.True(properties.Contains(name), $"The shared request declares no '{name}' property.");
        }

        foreach (var (path, operation) in Tooltips())
        {
            var names = Parameters(operation)
                .Select(parameter => parameter.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var name in owned)
            {
                Assert.False(
                    names.Contains(name),
                    $"{path} declares '{name}' as a parameter of its own rather than reading it off the body.");
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

            foreach (var operation in Operations(path))
            {
                foreach (var (name, _, _) in Parameters(operation))
                {
                    Assert.True(
                        declared.Contains(name) || filters.Contains(name),
                        $"{published} declares '{name}' and its model has no such property, " +
                        "and the codex names no such facet or flag.");
                }
            }
        }
    }

    [Fact]
    public void A_list_property_publishes_the_entries_it_carries_rather_than_the_string_they_were_written_as()
    {
        var properties = Component(nameof(TooltipRequest)).GetProperty("properties");

        foreach (var (name, entry, fields) in new[]
        {
            ("modifiers", nameof(ItemModifier), new[] { "kind", "id", "amount" }),
            ("imbuedTraits", nameof(ImbuedTrait), new[] { "id", "rank" }),
        })
        {
            Assert.True(
                properties.TryGetProperty(name, out var schema), $"The request declares no '{name}' property.");

            Assert.True(Kinds(schema).Contains("array"), $"'{name}' publishes {schema}");
            Assert.True(schema.TryGetProperty("items", out _), $"'{name}' publishes no shape for its entries.");

            var declared = Properties(Component(entry));

            foreach (var field in fields)
            {
                Assert.True(declared.Contains(field), $"{entry} declares no '{field}' field.");
            }
        }
    }

    [Fact]
    public void The_stats_property_publishes_one_list_of_rolls_per_stat()
    {
        var stats = Component(nameof(TooltipRequest)).GetProperty("properties").GetProperty("stats");

        Assert.True(Kinds(stats).Contains("object"), $"The stats publish {stats}");
        Assert.True(
            stats.TryGetProperty("additionalProperties", out _), "The stats publish no shape per stat name.");

        var rolls = Component(nameof(StatRolls)).GetProperty("properties");

        foreach (var name in new[] { "rating", "percentage" })
        {
            var schema = rolls.GetProperty(name);

            Assert.True(Kinds(schema).Contains("array"), $"'{name}' publishes {schema}");
        }
    }

    [Fact]
    public void The_id_is_the_only_required_parameter_on_any_route()
    {
        foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
        {
            foreach (var operation in Operations(path.Value))
            {
                foreach (var (name, required, where) in Parameters(operation))
                {
                    if (!required) continue;

                    Assert.Equal("id", name);
                    Assert.Equal("path", where);
                }
            }
        }
    }
}
