using System.Reflection;
using System.Text.Json;

using Fellowship.SDK.Client;
using Fellowship.SDK.Records;

using Microsoft.AspNetCore.Http;

namespace Fellowship.Codex.Tests;

public sealed class ApiDocumentTests : IClassFixture<RouteTests.Factory>
{
    private static readonly string[] Methods = ["get", "query"];

    private readonly JsonDocument document;

    private readonly ICodexService codex;

    private readonly IEntityStoreService store;

    public ApiDocumentTests(RouteTests.Factory factory)
    {
        var client = factory.CreateClient();

        document = JsonDocument.Parse(client.GetStringAsync(ApiDocument.Route).GetAwaiter().GetResult());
        codex = factory.Services.GetRequiredService<ICodexService>();
        store = factory.Services.GetRequiredService<IEntityStoreService>();
    }

    private IEnumerable<(string Path, JsonElement Operation)> Operations() =>
        document.RootElement.GetProperty("paths").EnumerateObject()
            .SelectMany(entry => Methods
                .Where(method => entry.Value.TryGetProperty(method, out _))
                .Select(method => (entry.Name, entry.Value.GetProperty(method))));

    private static IEnumerable<JsonElement> Parameters(JsonElement operation) =>
        operation.TryGetProperty("parameters", out var declared) ? declared.EnumerateArray() : [];

    private JsonElement PathItem(string path) =>
        document.RootElement.GetProperty("paths").GetProperty(path);

    private JsonElement Operation(string path, string method) => PathItem(path).GetProperty(method);

    private string Description(string path, string parameter) =>
        Parameters(Operation(path, "get"))
            .First(declared => string.Equals(
                declared.GetProperty("name").GetString(), parameter, StringComparison.OrdinalIgnoreCase))
            .GetProperty("description")
            .GetString()!;

    [Fact]
    public void Every_parameter_the_document_declares_is_one_a_reader_can_name()
    {
        foreach (var (path, operation) in Operations())
        {
            foreach (var parameter in Parameters(operation))
            {
                var name = parameter.GetProperty("name").GetString();

                Assert.False(
                    string.IsNullOrWhiteSpace(name),
                    $"{path} declares a parameter with no name, which would land in the document unusable.");

                Assert.True(
                    parameter.TryGetProperty("description", out var described),
                    $"{path} declares '{name}' and states nothing about it.");

                Assert.EndsWith(".", described.GetString()!, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void No_parameter_is_declared_twice_on_one_endpoint()
    {
        foreach (var (path, operation) in Operations())
        {
            var names = Parameters(operation).Select(parameter => parameter.GetProperty("name").GetString()!).ToArray();

            Assert.True(
                names.Length == names.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                $"{path} declares a parameter twice.");
        }
    }

    [Fact]
    public void Every_type_the_codex_lists_publishes_its_tooltip_as_a_query_naming_the_stats_it_reads()
    {
        var published = document.RootElement.GetProperty("paths").EnumerateObject()
            .Where(entry => entry.Name.EndsWith("/tooltip", StringComparison.Ordinal))
            .Select(entry => entry.Name)
            .Order(StringComparer.Ordinal);

        var listed = Enum.GetValues<EntityType>().Where(PublicTypes.IsListed).ToArray();

        Assert.Equal(
            listed.Select(type => $"/api/{EntityTypes.Slug(type)}/{{id}}/tooltip").Order(StringComparer.Ordinal),
            published);

        foreach (var type in listed)
        {
            var path = PathItem($"/api/{EntityTypes.Slug(type)}/{{id}}/tooltip");

            Assert.False(
                path.TryGetProperty("get", out _),
                $"{type} still publishes its tooltip as a GET rather than a QUERY.");

            var operation = path.GetProperty("query");

            Assert.Equal($"Query{type}Tooltip", operation.GetProperty("operationId").GetString());

            var described = operation.GetProperty("description").GetString()!;

            Assert.Contains("stats", described, StringComparison.Ordinal);

            foreach (var stat in store.Stats.Names)
            {
                Assert.Contains(stat, described, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void Each_type_takes_its_own_tooltip_request_carrying_only_what_that_card_reads()
    {
        foreach (var type in Enum.GetValues<EntityType>().Where(PublicTypes.IsListed))
        {
            var operation = Operation($"/api/{EntityTypes.Slug(type)}/{{id}}/tooltip", "query");

            var reference = operation
                .GetProperty("requestBody")
                .GetProperty("content")
                .GetProperty("application/json")
                .GetProperty("schema")
                .GetProperty("$ref")
                .GetString()!;

            var name = reference[(reference.LastIndexOf('/') + 1)..];

            Assert.Equal($"{type}TooltipRequest", name);

            var record = TooltipRequestTypes.Find(type);

            Assert.NotNull(record);

            Assert.Equal(
                record.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Select(property => JsonNamingPolicy.CamelCase.ConvertName(property.Name))
                    .Order(StringComparer.Ordinal),
                SchemaProperties(name));
        }
    }

    [Fact]
    public void Only_an_item_can_be_asked_for_a_power_potential()
    {
        Assert.Contains("potentialPower", SchemaProperties($"{EntityType.Item}TooltipRequest"));

        foreach (var type in Enum.GetValues<EntityType>().Where(PublicTypes.IsListed))
        {
            if (type is EntityType.Item) continue;

            Assert.DoesNotContain(
                "potentialPower",
                SchemaProperties($"{type}TooltipRequest"));
        }
    }

    private IReadOnlyList<string> SchemaProperties(string name) =>
    [
        .. document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty(name)
            .GetProperty("properties")
            .EnumerateObject()
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal),
    ];

    [Fact]
    public void The_search_limit_description_names_the_bounds_the_service_holds_to()
    {
        var described = Description("/api/search", "Limit");

        Assert.Contains(
            SuggestionService.MaximumLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),
            described,
            StringComparison.Ordinal);

        Assert.Contains(
            SuggestionService.DefaultLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),
            described,
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_page_size_description_names_the_bounds_the_filter_holds_to()
    {
        var described = Description("/api/entities", "Size");

        Assert.Contains(
            CodexQuery.MaximumPageSize.ToString(System.Globalization.CultureInfo.InvariantCulture),
            described,
            StringComparison.Ordinal);

        Assert.Contains(
            CodexQuery.DefaultPageSize.ToString(System.Globalization.CultureInfo.InvariantCulture),
            described,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Every_record_route_answers_the_record_class_named_for_the_type_it_addresses()
    {
        foreach (var type in Enum.GetValues<EntityType>().Where(PublicTypes.IsListed))
        {
            var answered = Operation($"/api/{EntityTypes.Slug(type)}/{{id}}", "get")
                .GetProperty("responses")
                .GetProperty("200")
                .GetProperty("content")
                .GetProperty("application/json")
                .GetProperty("schema")
                .GetProperty("$ref")
                .GetString();

            Assert.Equal($"#/components/schemas/{EntityRecords.Record(type).Name}", answered);
        }
    }

    [Fact]
    public void The_type_a_record_carries_is_documented_as_the_slug_it_is_written_as()
    {
        var documented = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty(nameof(EntityType))
            .GetProperty("enum")
            .EnumerateArray()
            .Select(entry => entry.GetString())
            .ToArray();

        Assert.Equal(Enum.GetValues<EntityType>().Select(EntityTypes.Slug), documented);
    }

    [Fact]
    public void Every_tag_is_a_type_label_or_the_browsing_tag()
    {
        var offered = PublicTypes.All
            .Select(type => type.Label)
            .Append(BrowsingGroup.Tag)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var (_, operation) in Operations())
        {
            if (!operation.TryGetProperty("tags", out var tags)) continue;

            foreach (var tag in tags.EnumerateArray())
            {
                Assert.Contains(tag.GetString()!, offered, StringComparer.Ordinal);
            }
        }
    }

    [Fact]
    public void The_search_limit_holds_between_the_bounds_the_document_states()
    {
        Assert.Equal(SuggestionService.DefaultLimit, SuggestionService.Limit(null));
        Assert.Equal(SuggestionService.DefaultLimit, SuggestionService.Limit("not a number"));
        Assert.Equal(1, SuggestionService.Limit("0"));
        Assert.Equal(1, SuggestionService.Limit("-5"));
        Assert.Equal(
            SuggestionService.MaximumLimit,
            SuggestionService.Limit((SuggestionService.MaximumLimit + 1).ToString()));
        Assert.Equal(25, SuggestionService.Limit("25"));
    }

    [Fact]
    public void Every_filter_parameter_the_document_declares_is_read_rather_than_refused()
    {
        var declared = RequestProperties.Of(typeof(EntitiesRequest));

        foreach (var parameter in Parameters(Operation("/api/entities", "get")))
        {
            var name = parameter.GetProperty("name").GetString()!;

            var value = codex.Facet(name) is { } facet
                ? codex.Offered(facet).FirstOrDefault() ?? string.Empty
                : Sample(name);

            CodexQuery.Parse(
                Query(new Dictionary<string, string> { [name] = value }), codex, declared, null, out var error);

            Assert.True(error is null, $"The document offers '{name}' and the filter answers: {error}");
        }

        foreach (var measure in codex.Measures)
        {
            foreach (var bound in new[] { measure.Key + "Min", measure.Key + "Max" })
            {
                CodexQuery.Parse(
                    Query(new Dictionary<string, string> { [bound] = "1" }), codex, declared, null, out var error);

                Assert.True(error is null, $"The document names '{bound}' and the filter answers: {error}");
            }
        }
    }

    private static string Sample(string name) => name switch
    {
        _ when name.Equals("sort", StringComparison.OrdinalIgnoreCase) => "name",
        _ when name.Equals("type", StringComparison.OrdinalIgnoreCase) => PublicTypes.All[0].Slug,
        _ when name.Equals("group", StringComparison.OrdinalIgnoreCase) => "slot",
        _ => "1",
    };

    private static IQueryCollection Query(Dictionary<string, string> pairs) =>
        new QueryCollection(pairs.ToDictionary(
            pair => pair.Key, pair => new Microsoft.Extensions.Primitives.StringValues(pair.Value)));
}
