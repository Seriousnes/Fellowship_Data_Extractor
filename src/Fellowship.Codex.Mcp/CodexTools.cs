using System.ComponentModel;
using System.Text.Json;

using Fellowship.SDK.Documents;

using Fellowship.Codex.Endpoints;
using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Fellowship.Codex.Mcp;

[McpServerToolType]
public sealed class CodexTools(
    IEntityStoreService store,
    ICodexService codex,
    ISuggestionService suggestions,
    ICodexViewService view)
{
    [McpServerTool(Name = "list_types", ReadOnly = true), Description(
        "Lists every entity type the build carries, with the hero and rarity names other tools accept.")]
    public TypesResult ListTypes() => new(
        codex.Count,
        [.. codex.Types.Select(type => new TypeSummary(type.Slug, type.Label, type.Group, type.Count))],
        [.. store.Settings.Heroes.Select(hero => new HeroSummary(hero.Name, hero.ArmorType, hero.PrimaryStat))],
        [.. store.Settings.Rarities.Select(rung =>
            new RaritySummary(rung.Tier, rung.DisplayName, rung.GemDisplayName))]);

    [McpServerTool(Name = "find_entity", ReadOnly = true), Description(
        "Finds entities by name and returns the type and id that get_entity takes. Matches name " +
        "prefixes only, so \"ire\" does not reach \"Fire\"; camel-case subwords are indexed, so " +
        "\"Bolt\" reaches \"FireBolt\". Entities the game leaves unnamed are not reachable by text. " +
        "A number matches ids across every type.")]
    public FoundResult FindEntity(
        [Description("The name prefix to match, or an id.")] string q,
        [Description("Narrow to one type slug from list_types.")] string? type = null,
        [Description("How many results to return, 1 to 100.")] int limit = 10)
    {
        var found = suggestions.For(q, type, Math.Clamp(limit, 1, SuggestionService.MaximumLimit));

        return new FoundResult(
            found.Total,
            [.. found.Groups.Select(group => new GroupSummary(group.Type, group.Label, group.Count))],
            [.. found.Results.Select(hit => new FoundEntity(
                hit.Type, hit.Id, hit.Name, hit.IconUrl, hit.Rarity, hit.Slot, hit.ArmorType, hit.Heroes))]);
    }

    [McpServerTool(Name = "get_entity", ReadOnly = true), Description(
        "Returns one entity's full record. Ids are namespaced per type, so a slug is always required.")]
    public CallToolResult GetEntity(
        [Description("An entity type slug from list_types, for example ability or item.")] string slug,
        [Description("The entity id, as returned by find_entity.")] int id,
        [Description("The hero to read the entity as, when one id reads differently per hero.")]
        string? hero = null)
    {
        if (!EntityTypes.TryParse(slug, out var type) || !PublicTypes.Listed(type))
        {
            return Trouble(PublicTypes.NoSuchType(slug));
        }

        var asked = StatQuery.Given(hero);

        if (asked is not null && store.Hero(asked) is null)
        {
            return Trouble(Lookups.NoHero(asked, store.Settings.Heroes).Detail);
        }

        var outcome = store.Resolve(type, id, asked, out var document, out var wearer);

        return outcome == Lookup.Found
            ? Read(PublicEntity.From(document!, view, string.Empty))
            : Trouble(Lookups.Trouble(outcome, type, id, wearer, store.All(type, id)).Detail);
    }

    private static CallToolResult Read(PublicEntity record) => new()
    {
        Content = [new TextContentBlock { Text = JsonSerializer.Serialize(record, CodexMcp.Json) }],
    };

    private static CallToolResult Trouble(string detail) => new()
    {
        IsError = true,
        Content = [new TextContentBlock { Text = detail }],
    };
}

public sealed record TypesResult(
    int Entities,
    IReadOnlyList<TypeSummary> Types,
    IReadOnlyList<HeroSummary> Heroes,
    IReadOnlyList<RaritySummary> Rarities);

public sealed record TypeSummary(string Slug, string Label, string Group, int Count);

public sealed record HeroSummary(string Name, string? ArmorType, string PrimaryStat);

public sealed record RaritySummary(int Tier, string DisplayName, string GemDisplayName);

public sealed record FoundResult(
    int Total, IReadOnlyList<GroupSummary> Groups, IReadOnlyList<FoundEntity> Results);

public sealed record GroupSummary(string Type, string Label, int Count);

public sealed record FoundEntity(
    string Type,
    int Id,
    string Name,
    string? Icon,
    string? Rarity,
    string? Slot,
    string? ArmorType,
    IReadOnlyList<string>? Heroes);
