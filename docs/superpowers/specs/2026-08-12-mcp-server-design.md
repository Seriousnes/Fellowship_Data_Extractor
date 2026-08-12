# An MCP server on the codex API

**Goal:** an AI agent asks the running codex for game data over MCP with three tools. `list_types`
says what the build carries, `find_entity` turns a name into a type and an id, and `get_entity`
returns that entity's record. The tools are a second front door onto the services `Program.cs`
already registers, sharing the one loaded `ExportSnapshot` with the HTTP API and the Blazor site,
reading the same documents through the same services and computing nothing of their own.

## What the runtime states

Measured on 2026-08-12 against `ModelContextProtocol.AspNetCore` 2.1.0 on .NET SDK 10.0.400, and
against this checkout's export running under the AppHost.

### The package answers on net10.0 and survives the pipeline

`ModelContextProtocol.AspNetCore` 2.1.0 restores against net10.0, pulling `ModelContextProtocol`,
`ModelContextProtocol.Core` and `Microsoft.Extensions.AI.Abstractions` 10.8.3. A probe host
registering `AddMcpServer(...).WithHttpTransport().WithTools<T>()` and mapping `MapMcp("/mcp")`
answered `initialize`, `tools/list` and `tools/call` through a pipeline carrying
`AddRazorComponents().AddInteractiveServerComponents()`, `UseResponseCompression()` and
`UseAntiforgery()`. Antiforgery does not reject the JSON-RPC POST and compression does not break the
`text/event-stream` response.

The transport is stateless: the server issues no `mcp-session-id`, and a `tools/call` sent with no
prior `initialize` is answered. Every call therefore runs on its own HTTP request scope, and
`IHttpContextAccessor.HttpContext` read inside a tool returns that request. A probe tool returning
`$"{http.Request.Scheme}://{http.Request.Host}{http.Request.PathBase}"` answered
`http://127.0.0.1:5322` both with and without a preceding handshake, so `Origins.Of(http)` works
unchanged inside a tool.

Input schemas are generated from the method signature: a `[Description]` on the method becomes the
tool description, a `[Description]` on a parameter becomes that property's description, a parameter
with a default becomes optional and carries its `default`, and a parameter without one lands in
`required`.

### The catalog is too large to be a tool result

| Response | Bytes |
| --- | --- |
| `GET /api/catalog` | 46,602 |
| `GET /api/search?q=fire&limit=10` | 2,488 |
| `GET /api/ability/277` | 358 |
| `GET /api/creature/{first}` | 1,015 |
| `GET /api/item/5150?hero=Helena` | 1,156 |
| largest of 20 sampled item records | 1,341 |
| `GET /api/dungeon/{first}` | 2,375 |

The catalog's weight is `facets`, which enumerate every value of all 13 facets with counts. Its
`types`, `heroes` and `rarities` are together about 2 KB. Every record is small enough to return
whole.

### The existing failure messages are already agent-shaped

`GET /api/item/5150` with no hero answers 400 with the detail:

> The build has 12 readings of item 5150 and no hero was given. Add hero=&lt;name&gt; to pick one.
> The readings are Frostruned Robe [Cloth/Intellect]; ... Brittleforged Chestpiece [Mail/Agility]; ...

`Lookups.NoSuchHero` lists the roster and `Lookups.Problem` maps `Lookup.NoSuchId` to 404 and
`Lookup.NotForThatHero` to 404. Each states what to send next, which is what a tool result needs.

### Only one of the Api's addresses belongs to the Api

Running `dotnet run --project src/FellowshipExtractor.AppHost` from this checkout, three ports
answer `/api/catalog` with 200, held by two processes:

| Port | Process | Comes from |
| --- | --- | --- |
| 5320 | `FellowshipExtractor.Api` | `Port(builder, "Ports:Codex", stack.Offset, 5280)`, `isProxied: false`, offset 40 for this checkout |
| 5488 | `dcp` | the `applicationUrl` of the `http` profile in `Properties/launchSettings.json` |
| 7346 | `dcp` | the `applicationUrl` of the `https` profile in the same file |

Only 5320 is bound by the Api. 5488 and 7346 are Aspire's orchestrator proxying the launch profile,
so they exist only while `dcp` runs and they are identical in every checkout, which means two
checkouts running at once contend for them. The address a client should be given is the one the
AppHost declares, and `FELLOWSHIP_PORT_OFFSET=0` pins it to 5280 by making `stack.Offset` zero.

## The tools

`src/FellowshipExtractor.Api/Mcp/CodexTools.cs`:

```csharp
[McpServerToolType]
public sealed class CodexTools(
    IEntityStoreService store,
    ICodexService codex,
    ISuggestionService suggestions,
    ICodexViewService view,
    IHttpContextAccessor context)
```

### `list_types`

```csharp
[McpServerTool(Name = "list_types", ReadOnly = true), Description(
    "Lists every entity type the build carries, with the hero and rarity names other tools accept.")]
public TypesResult ListTypes()
```

Returns `entities` (the `ICodexService.Count` of listed documents), `types` as
`{ slug, label, group, count }` from `ICodexService.Types`, `heroes` as
`{ name, armorType, primaryStat }` and `rarities` as `{ tier, displayName, gemDisplayName }` from
`IEntityStoreService.Settings`. `ICodexService.Types` is built from `PublicTypes.All` filtered by
`PublicTypes.Listed`, so the 21 routed slugs are what it publishes and `TalentSlot` and `MountPower`
cannot appear.

### `find_entity`

```csharp
[McpServerTool(Name = "find_entity", ReadOnly = true), Description(
    "Finds entities by name and returns the type and id that get_entity takes. Matches name " +
    "prefixes only, so \"ire\" does not reach \"Fire\"; camel-case subwords are indexed, so " +
    "\"Bolt\" reaches \"FireBolt\". Entities the game leaves unnamed are not reachable by text. " +
    "A number matches ids across every type.")]
public FoundResult FindEntity(
    [Description("The name prefix to match, or an id.")] string q,
    [Description("Narrow to one type slug from list_types.")] string? type = null,
    [Description("How many results to return, 1 to 100.")] int limit = 10)
```

Calls `ISuggestionService.For(q, type, Math.Clamp(limit, 1, SuggestionService.MaximumLimit))` and
projects each `SearchResult` to `{ type, id, name, rarity, slot, armorType, heroes }`, alongside the
`SearchResponse.Total` and its `Groups` as `{ type, label, count }`.

`IconUrl`, `Href` and `Tooltip` are the browser's fields and are not carried: `IconUrl` is an
absolute blob URL naming the storage emulator, and the other two are site and API paths the tools
do not take. `type` and `id` are what feed `get_entity`.

### `get_entity`

```csharp
[McpServerTool(Name = "get_entity", ReadOnly = true), Description(
    "Returns one entity's full record. Ids are namespaced per type, so a slug is always required.")]
public CallToolResult GetEntity(
    [Description("An entity type slug from list_types, for example ability or item.")] string slug,
    [Description("The entity id, as returned by find_entity.")] int id,
    [Description("The hero to read the entity as, when one id reads differently per hero.")]
    string? hero = null)
```

Makes the two calls `Records.One` makes: `IEntityStoreService.Resolve(type, id, StatQuery.Given(hero),
out var document, out var wearer)`, then on `Lookup.Found` returns
`PublicEntity.From(document, view, Origins.Of(context.HttpContext))`. The record is returned whole,
so no field of a document is projected twice.

A list tool returns what identifies a row plus what feeds the next call; a get tool returns the
document as the API serves it. That is why `find_entity` trims and `get_entity` does not.

## Errors are results, not protocol faults

Every tool returns `CallToolResult { IsError = true }` carrying the same sentence the HTTP route
puts in its problem detail, so a model reads what to send next and recovers on its next turn.

The sentences live once. `Models/Lookup.cs` gains
`public sealed record Fault(string Title, string Detail, int Status)`, and the text that
`Lookups.Problem` and `Lookups.NoSuchHero` build today moves into
`Lookups.Trouble(outcome, type, id, hero, readings)` and `Lookups.NoHero(named, roster)`, each
returning a `Fault`. `Problem` and `NoSuchHero` become one call to `Results.Problem` over that
`Fault`, and the tools read its `Detail`.

| Cause | Content |
| --- | --- |
| `slug` parses to no `EntityType`, or to an unlisted one | the valid slugs, read from `ICodexService.Types` |
| `hero` names no hero | `Lookups.NoHero(named, roster).Detail` |
| `Lookup.NoSuchId` | `Lookups.Trouble(...).Detail` |
| `Lookup.NeedsHero` | `Lookups.Trouble(...).Detail`, naming `hero` as the parameter to add |
| `Lookup.NotForThatHero` | `Lookups.Trouble(...).Detail` |

`slug` is a `string`, not an `EntityType`. The enum carries 23 members and 21 of them route, so an
enum-typed parameter would publish `talent-slot` and `mount-power` in the input schema as values
that always fail. `list_types` publishes the real set instead, and a wrong slug costs one
recoverable turn.

## Registration

`src/FellowshipExtractor.Api/Mcp/McpServer.cs` holds the server's identity and its registration
pair, in the shape `ApiDocument.cs` and `ApiEndpoints.cs` already use:

```csharp
public static class McpServer
{
    public const string Name = "fellowship-codex";

    public const string Route = "/mcp";

    public const string Instructions =
        "This server reads a datamined export of Fellowship. Call list_types for the entity types " +
        "the build carries, find_entity to turn a name into a type and an id, then get_entity for " +
        "that entity's record. Ids are namespaced per type, so the same number names a different " +
        "entity under each slug. Names match by prefix, not by substring. A record's damage and " +
        "healing coefficients read as text such as '180% of Intellect' rather than a number, " +
        "because a record is not parameterised by hero, item level or rarity.";

    public static IHostApplicationBuilder AddMcpTools(this IHostApplicationBuilder builder);

    public static IEndpointRouteBuilder MapMcpTools(this IEndpointRouteBuilder app);
}
```

`AddMcpTools` calls `AddHttpContextAccessor()` then `AddMcpServer`, setting
`ServerInfo = new() { Name = Name, Version = typeof(McpServer).Assembly.GetName().Version?.ToString() ?? "1.0.0" }`
and `ServerInstructions = Instructions`, and chaining `.WithHttpTransport().WithTools<CodexTools>()`.
`MapMcpTools` calls `MapMcp(Route)`. `Program.cs` calls the first alongside the other service
registrations and the second after `MapApiEndpoints()`.

The Api csproj gains `<PackageReference Include="ModelContextProtocol.AspNetCore" Version="2.1.0" />`.

## Reaching it from Claude Code

`.mcp.json` at the repository root:

```json
{
  "mcpServers": {
    "fellowship-codex": {
      "type": "http",
      "url": "${FELLOWSHIP_CODEX_URL}/mcp"
    }
  }
}
```

Claude Code expands `${VAR}` in an HTTP server's `url`. The variable carries no default: a checkout
states its own address, and an unset variable is reported as a missing-variable warning by
`claude mcp list`, which is a better failure than a default that silently connects to whichever
other checkout is running. `FELLOWSHIP_CODEX_URL` is also where a fixed hostname goes once one
exists.

`README.md` gains a section stating the tool names, the `.mcp.json` entry, that
`FELLOWSHIP_PORT_OFFSET=0` pins the Api to `http://localhost:5280` so `FELLOWSHIP_CODEX_URL` can be
set to a constant, and that the AppHost must be running for an agent to reach the tools. The server
being down is a reported connection failure and an error from any tool call, which is the behaviour
a permanently registered server should have.

## Verification

`src/FellowshipExtractor.Api.Tests/McpTests.cs` takes `RouteTests.Factory` through
`IClassFixture<RouteTests.Factory>` and drives a real client over the in-memory handler:

```csharp
var transport = new HttpClientTransport(
    new HttpClientTransportOptions { Endpoint = new Uri("http://localhost/mcp") },
    factory.CreateClient());

await using var client = await McpClient.CreateAsync(transport, cancellationToken: token);
```

This shape was run against `xunit.v3` 3.2.2, `xunit.runner.visualstudio` 3.1.5,
`Microsoft.NET.Test.Sdk` 18.8.1 and `Microsoft.AspNetCore.Mvc.Testing` 10.0.10, the versions the
test project pins, and it lists and calls a tool successfully. The test csproj gains
`ModelContextProtocol.Core` 2.1.0.

The tests confirm:

- `ListToolsAsync` returns exactly `list_types`, `find_entity` and `get_entity`.
- Every tool and every parameter in the published schemas carries a description ending in a full
  stop, the rule `ApiDocumentTests` already holds the HTTP parameters to.
- `list_types` returns slugs equal to
  `Enum.GetValues<EntityType>().Where(PublicTypes.Listed).Select(EntityTypes.Slug)`, its hero names
  equal to `store.Settings.Heroes`, and its rarity tiers equal to `store.Settings.Rarities`.
- `get_entity` answers for one id of every listed type, taken from `RouteTests.Sample(type)`, and
  the record it returns matches the body `GET /api/{slug}/{id}` returns for the same id.
- `get_entity` returns `IsError` naming the valid slugs for an unknown slug, and `IsError` for an id
  the export does not carry.
- `get_entity` on item 5150 with no hero returns `IsError` whose text equals the `detail` that
  `GET /api/item/5150` puts in its problem response, and the same call with `hero` supplied
  succeeds. That equality is what holds the split `Fault` to one wording.
- `find_entity` finds a document by its exact name, returns nothing for a substring that is not a
  prefix, honours `type`, and clamps `limit` above 100 and below 1.
- `find_entity` results carry no icon, href or tooltip field.

Run with `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`, which
needs `output/export` seeded and runs in Debug.

Manual confirmation, with the AppHost running:

```powershell
curl -s -H 'Content-Type: application/json' -H 'Accept: application/json, text/event-stream' `
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}' http://localhost:5280/mcp
```

and `claude mcp list` reporting `fellowship-codex` connected.
