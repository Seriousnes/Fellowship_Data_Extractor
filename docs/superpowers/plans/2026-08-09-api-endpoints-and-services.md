# API endpoints and services Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Every route the API answers lives in a file of its own under `Endpoints/`, addressed by a
literal path that names one entity type; every type registered in the container lives under
`Services/` behind an interface; and `Program.cs` composes the application and registers nothing
else.

**Architecture:** Each of the 22 `EntityType` members owns a folder holding a group and two endpoint
files, so a route's template is a literal string and an address naming no type is a routing 404
before any handler runs. Each endpoint binds a request model whose properties are exactly the
parameters it accepts, which is what the published document reads. Every service keeps its present
body and gains an interface holding its public instance members; its public statics stay where their
callers already name them.

**Tech Stack:** .NET 10, C# 14, ASP.NET Core minimal APIs with `[AsParameters]` binding,
`Microsoft.AspNetCore.OpenApi` 10.0.10 with `Microsoft.OpenApi` 2.11.0, `Microsoft.Extensions.Validation`
(`AddValidation`), Blazor static server rendering, xunit.v3 3.2.2 with bunit 2.8.6 in
`src/FellowshipExtractor.Api.Tests`.

## Global Constraints

- .NET 10, C# 14. `net10.0` everywhere except `Fellowship.SDK.Generators`, which stays `netstandard2.0`.
- No polyfills for missing runtime functionality. Use the modern API directly.
- No em dashes or en dashes anywhere: code, strings, XML, output, commit messages.
- **Add no comments to any file.** Not explanatory ones, not section markers, not doc comments.
- Never hardcode a list, id or data fact a canonical source already states. The 22 types come from
  `Enum.GetValues<EntityType>()`, a slug from `EntityTypes.Slug`, a tag from `PublicTypes.Label`, a
  noun from `PublicTypes.Noun`, and the stat names from `Vocabulary.StatCalculation.Attributes`.
- Match the surrounding code's idiom: expression-bodied members, collection expressions, pattern
  matching, `StringComparer` named explicitly, file-scoped namespaces.
- Test naming: snake_case sentences in `FellowshipExtractor.Api.Tests`. `[Fact]` only.
- Namespaces: every file under `Services/` is `FellowshipExtractor.Api.Services`. Every file under
  `Endpoints/`, whatever its folder, is `FellowshipExtractor.Api.Endpoints`, so no namespace segment
  spells an entity type name.
- `EntityType` and `EntityTypes` are in `Fellowship.SDK`, which the csproj already includes for every
  file. Every file under `Endpoints/` and `Services/` opens with
  `using Fellowship.SDK.Documents;`, `using FellowshipExtractor.Api.Models;` and
  `using FellowshipExtractor.Api.Services;` (the last only outside `Services/`). The code blocks in
  this plan show only the usings beyond those three.
- Build: `dotnet build src/FellowshipExtractor.slnx`. Test: `dotnet test src/FellowshipExtractor.Api.Tests`.
- This checkout is shared with other sessions. Read a file as it stands before editing it. Never run
  `git stash`.

### Two stated deviations from the spec's sketch

1. **`ToQuery` takes three arguments, not one.** `TooltipQuery.Stats` and `TooltipQuery.Attributes`
   are keyed by the attribute table's internal key (`Placeholders.One` reads `query.Total(attribute)`
   with `attribute` coming from `placeholder.Attribute` or `hero.PrimaryStat`), so turning `?Stamina=40`
   into a reading needs the table. The signature is
   `ToQuery(IQueryCollection raw, StatParameters stats, out string? error)`.
2. **The entity page carries `state` only where a card reads one.** `Query.State` is read by
   `Blessing.razor` and `Trait.razor` and nowhere else, and the spec declares `State` on the blessing
   and trait tooltip endpoints alone. An item address carrying `state=` would answer 400 on the
   interactive refetch, so `EntityPage.Address` asks `ITooltipViewService.DrawsState(Entity)` before
   adding it, and `RouteTests` asserts the item page carries the hero and the trait page carries the
   state.

### Two accepted behaviour changes

- `?itemLevel=` with an empty value binds as a binding failure rather than as absent. Nothing in
  `codex.js` or `Knobs.razor` emits one: `stage()` skips a knob whose value is empty.
- `?noSocket` without a value no longer reads as true. `NoSocket` is `bool?`, so the form is
  `?noSocket=true`. Nothing in the app emits the bare form.

## What the build states

Measured on 2026-08-09 against `output/export/vocabulary.json` and the source.

- **`EntityType` has 22 members**, listed in `src/Fellowship.SDK/Models/EntityType.cs`, and
  `PublicTypes.All` already fails construction if one of them has no row.
- **`Vocabulary.StatCalculation.Attributes` has 24 rows carrying 23 distinct display names.** `Armor`
  and `BonusArmor` both print `Armor`. Removing the name two rows share leaves the 22 the spec names:
  Agility, Attack Speed, Block Chance, Cooldown Reduction, Critical Strike, Dodge, Expertise, Haste,
  Health, Health Regen Rate, Intellect, Life Steal, Mana, Mana Regen Rate, Max Mana, Move Speed,
  Parry, Reflect Heal, Spirit, Stamina, Strength and Tenacity.
- **The census names 8 of those 24.** `EntityStore.Census` collects the stats item rows roll, which
  is why a hero sheet's Parry or Dodge answers 400 today.
- **Repeated query values reach a `TryParse` binder joined by a comma**, because the binder reads
  `Request.Query[name]` as `StringValues` and `StringValues.ToString()` joins with `,`. One
  `TryParse` therefore reads both `?trait=1:2,3:4` and `?trait=1:2&trait=3:4`.
- **`StatConversion.PercentageSuffix` is `"Percentage"`, and the published note says `Pct`.** The note
  is wrong today. The note is rebuilt from the constant rather than from a literal, so it states the
  suffix the parser reads.
- **The export in `output/` predates `ff27f4b`.** `PlaceholderReadingTests.Trait_power_684_reads_the_armor_it_adds_rather_than_the_armor_it_leaves`
  and `PlaceholderReadingTests.The_substitutions_panel_reads_the_same_increase_the_sentence_does` both
  read `135%` where `35%` is expected, because the export carries no `Multiplies` flag. That is the
  baseline: 258 passed, 2 failed, 260 total. Regenerating with
  `dotnet run --project src/FellowshipExtractor -- export-all` takes about seven minutes and turns
  them green. No task in this plan touches them.

## File Structure

### Created

```
src/FellowshipExtractor.Api/Services/
  ExportPathService.cs        IExportPathService, ExportPathService
  IconSeederService.cs        IconSeederService
  EntityStoreService.cs       IEntityStoreService, EntityStoreService
  DesignTokenService.cs       IDesignTokenService, DesignTokenService
  RichTextService.cs          IRichTextService, RichTextService
  ChromeAssetService.cs       IChromeAssetService, ChromeAssetService
  PlaceholderService.cs       IPlaceholderService, PlaceholderService
  TooltipViewService.cs       ITooltipViewService, TooltipViewService
  FragmentService.cs          IFragmentService, FragmentService
  TooltipService.cs           ITooltipService, TooltipService
  CodexService.cs             ICodexService, CodexService
  SearchIndexService.cs       ISearchIndexService, SearchIndexService
  CodexSearchService.cs       ICodexSearchService, CodexSearchService
  CodexViewService.cs         ICodexViewService, CodexViewService
  SuggestionService.cs        ISuggestionService, SuggestionService

src/FellowshipExtractor.Api/Endpoints/
  ApiEndpoints.cs             MapApiEndpoints, the one call Program.cs makes
  RequestProperties.cs        the property names a request model declares, cached per type
  StatQuery.cs                reads the open ended stat parameters and writes the 400
  Records.cs                  the shared body of every /api/{slug}/{id} handler
  Documentation/StatsDocument.cs      WithStats and the operation transformer it marks for
  Documentation/BrowsingDocument.cs   the facets, flags and measures on /api/entities
  Documentation/ListDocument.cs       ImbuedTraits and GemPowers as {"type": "string"}
  Browsing/BrowsingGroup.cs   the /api group the three cross type routes hang from
  Browsing/SearchEndpoint.cs
  Browsing/EntitiesEndpoint.cs
  Browsing/CatalogEndpoint.cs
  <Member>/<Member>Group.cs            one folder for each of the 22 EntityType members
  <Member>/<Member>RecordEndpoint.cs
  <Member>/<Member>TooltipEndpoint.cs

src/FellowshipExtractor.Api/Models/
  Lookup.cs                   enum Lookup, static Lookups writing the shared problem responses
  StatParameters.cs           StatParameters, built from the attribute table

src/FellowshipExtractor.Api.Tests/
  TooltipDumpTests.cs
  EndpointTests.cs
  ParameterTests.cs
  StatParameterTests.cs
  AbilityScalingTests.cs
```

### Deleted

```
src/FellowshipExtractor.Api/Models/DesignTokens.cs   nothing left in it
src/FellowshipExtractor.Api/Models/SearchIndex.cs    nothing left in it
src/FellowshipExtractor.Api/Models/IconSeeder.cs     nothing left in it
src/FellowshipExtractor.Api/Models/EntityStore.cs    Lookup and StatParameters move to their own files
```

### Kept, holding shapes only

`Models/Codex.cs`, `Models/CodexQuery.cs`, `Models/CodexView.cs`, `Models/ChromeAssets.cs`,
`Models/Placeholders.cs`, `Models/RichText.cs`, `Models/Suggestions.cs`, `Models/TooltipView.cs`
each lose their service class and keep the records, enums and static twins named after the same file.

### The static rule

A service's public statics stay on the implementation, so `SuggestionService.Limit`,
`SuggestionService.MaximumLimit`, `SuggestionService.Words`, `SuggestionService.Mark`,
`SearchIndexService.Words`, `ChromeAssetService.Prefix`, `ChromeAssetService.NodeStates`,
`ChromeAssetService.BlessingStates` and `ChromeAssetService.Padlocked` gain the new prefix.

Four groups are the exception, because they are pure functions over documents and numbers that no
service state touches and their call sites live in `Models/` and in components. Each keeps its
present name on a static class in `Models/`, so every call site reads unchanged:

- `Models/Codex.cs`: `public static class Codex` with `Spaced`, `Number`, `Label` and `Schools`.
- `Models/CodexView.cs`: `public static class CodexView` with `Readable`, `PlaceholdersOf`, `Number`
  and `Constant`.
- `Models/EntityStore.cs` is deleted, and `public static class EntityStore` with `Reads` and its two
  private enum parsers moves into `Models/Lookup.cs`.
- `Models/TooltipView.cs`: `public static class TooltipView` with `Root`, `TraitType`, `Magnitude`,
  `Block`, `Fact` and `TraitLine`, which the spec names. `SlotWidth` joins them when the cards work
  adds it.

---

### Task 1: The dump harness that measures the move

The evidence that this move changed no rendering is a diff of the drawn cards before and against
after. The harness does not exist yet. It is also what the item and ability cards plan measures
itself with, so it is built once, here.

**Files:**
- Create: `src/FellowshipExtractor.Api.Tests/TooltipDumpTests.cs`

**Interfaces:**
- Consumes: `Export` (the `[Collection(Corpus.Name)]` fixture), `Components.Tooltip.Tooltip`.
- Produces: one HTML file per case under the folder `FELLOWSHIP_TIP_DUMP` names, called
  `{slug}-{id}-{case}.html`.

- [x] **Step 1: Make the export reachable from this worktree**

`output/` is gitignored and lives in the main checkout. `RouteTests.Factory.ExportFolder` walks up
from the test binary and does not read `FELLOWSHIP_EXPORT`, so a junction is what makes both the
fixture and the factory find it.

```powershell
if (-not (Test-Path "G:\source\Fellowship_Data_Extractor.route-redesign\output")) {
    New-Item -ItemType Junction `
        -Path "G:\source\Fellowship_Data_Extractor.route-redesign\output" `
        -Target "G:\source\Fellowship_Data_Extractor\output"
}
Test-Path "G:\source\Fellowship_Data_Extractor.route-redesign\output\export\entities.jsonl"
```

Expected: `True`.

- [x] **Step 2: Record the baseline**

```powershell
dotnet test src/FellowshipExtractor.Api.Tests --nologo -v q
```

Expected: `Failed: 2, Passed: 258, Total: 260`, the two failures being the `PlaceholderReadingTests`
pair named under "What the build states". Any other failure is a problem to solve before going on.

- [x] **Step 3: Write the harness**

Create `src/FellowshipExtractor.Api.Tests/TooltipDumpTests.cs`:

```csharp
using System.Globalization;

namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class TooltipDumpTests : BunitContext
{
    private const string Variable = "FELLOWSHIP_TIP_DUMP";

    private readonly Export export;

    public TooltipDumpTests(Export export)
    {
        this.export = export;
        Services.AddSingleton(export.Tooltip);
        Services.AddSingleton(export.View);
    }

    [Fact]
    public void Draws_every_case_into_the_folder_the_variable_names()
    {
        var folder = Environment.GetEnvironmentVariable(Variable);

        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(folder),
            $"{Variable} names no folder, so there is nothing to write.");

        Directory.CreateDirectory(folder!);

        var written = 0;

        foreach (var type in Enum.GetValues<EntityType>().OrderBy(EntityTypes.Slug, StringComparer.Ordinal))
        {
            var slug = EntityTypes.Slug(type);

            var ids = export.Store.Entities
                .Where(row => EntityTypes.TypeOf(row) == type)
                .Select(row => row.Id)
                .Distinct()
                .Order()
                .Take(3);

            foreach (var id in ids)
            {
                var document = export.Store.All(type, id)[0];

                foreach (var (name, query) in Cases(document))
                {
                    var hero = query.Hero is null ? null : export.Store.Hero(query.Hero);

                    var cut = Render<Components.Tooltip.Tooltip>(p => p
                        .Add(x => x.Entity, document)
                        .Add(x => x.Query, query)
                        .Add(x => x.Hero, hero));

                    File.WriteAllText(
                        Path.Combine(folder!, $"{slug}-{id.ToString(CultureInfo.InvariantCulture)}-{name}.html"),
                        cut.Markup);

                    written++;
                }
            }
        }

        Assert.True(written > 0, "The store answered no rows, so the dump measures nothing.");
    }

    private IEnumerable<(string Name, TooltipQuery Query)> Cases(EntityDocument document)
    {
        yield return ("bare", new TooltipQuery());

        var rung = export.Store.Vocabulary.Rarities[^1];
        yield return ("rarity", new TooltipQuery { Rarity = rung.Tier.ToString(CultureInfo.InvariantCulture) });

        yield return ("level", new TooltipQuery
        {
            ItemLevel = export.Store.DropItemLevel(document),
            Upgrades = 2,
        });

        if (FirstTrait() is { } trait)
        {
            yield return ("traits", new TooltipQuery
            {
                Traits = [new ImbuedTrait(trait, 2)],
                MaxTraits = 3,
            });
        }

        yield return ("potential", new TooltipQuery { PotentialPower = 42m });

        if (export.Store.Wearer(document) is { } wearer)
        {
            yield return ("hero", new TooltipQuery { Hero = wearer.Name });
        }
    }

    private int? FirstTrait() => export.Store.Entities
        .Where(row => EntityTypes.TypeOf(row) == TooltipView.TraitType)
        .Select(row => (int?)row.Id)
        .Order()
        .FirstOrDefault();
}
```

- [x] **Step 4: Run it skipped, then run it for the baseline**

```powershell
dotnet test src/FellowshipExtractor.Api.Tests --nologo --filter "FullyQualifiedName~TooltipDumpTests"
```

Expected: 1 skipped, 0 failed.

```powershell
$env:FELLOWSHIP_TIP_DUMP = "$env:TEMP\fx-tip-before"
Remove-Item "$env:TEMP\fx-tip-before" -Recurse -Force -ErrorAction SilentlyContinue
dotnet test src/FellowshipExtractor.Api.Tests --nologo --filter "FullyQualifiedName~TooltipDumpTests"
(Get-ChildItem "$env:TEMP\fx-tip-before" -Filter *.html).Count
$env:FELLOWSHIP_TIP_DUMP = $null
```

Expected: 1 passed, and a count in the hundreds. Keep that folder. Task 14 diffs against it.

- [x] **Step 5: Commit**

```bash
git add src/FellowshipExtractor.Api.Tests/TooltipDumpTests.cs
git commit -m "Draw every card to a folder so the move can be measured"
```

---

### Task 2: Stat parameters come from the attribute table

**Files:**
- Create: `src/FellowshipExtractor.Api/Models/StatParameters.cs`
- Modify: `src/FellowshipExtractor.Api/Models/EntityStore.cs` (remove `StatParameters`, change the
  `Stats` construction)
- Test: `src/FellowshipExtractor.Api.Tests/StatParameterTests.cs`

**Interfaces:**
- Produces: `StatParameters(IReadOnlyDictionary<string, AttributeDisplay> attributes)` with
  `IReadOnlyList<string> Names`, `string? Resolve(string parameter)`, `AttributeDisplay Row(string stat)`.
  `EntityStore.Stats` keeps its name and type.

- [x] **Step 1: Write the failing test**

Create `src/FellowshipExtractor.Api.Tests/StatParameterTests.cs`:

```csharp
namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class StatParameterTests(Export export)
{
    [Fact]
    public void Names_every_display_name_the_attribute_table_carries_once()
    {
        var rows = export.Store.Vocabulary.StatCalculation.Attributes;

        var shared = rows.Values
            .Select(row => row.DisplayName)
            .Where(name => name is { Length: > 0 })
            .GroupBy(name => name!, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var expected = rows.Values
            .Select(row => row.DisplayName)
            .Where(name => name is { Length: > 0 } && !shared.Contains(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(expected, export.Store.Stats.Names);
    }

    [Fact]
    public void Carries_the_stats_a_hero_sheet_prints_that_no_item_rolls()
    {
        Assert.Contains("Parry", export.Store.Stats.Names, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Dodge", export.Store.Stats.Names, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Cooldown Reduction", export.Store.Stats.Names, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Leaves_a_name_two_attributes_share_for_the_printed_line()
    {
        Assert.Null(export.Store.Stats.Resolve("armor"));
        Assert.DoesNotContain("Armor", export.Store.Stats.Names, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolves_a_display_name_written_without_its_spaces()
    {
        Assert.Equal("BlockChance", export.Store.Stats.Resolve("blockchance"));
        Assert.Equal("CritChance", export.Store.Stats.Resolve("Critical Strike"));
    }
}
```

- [x] **Step 2: Run it and watch it fail**

```powershell
dotnet test src/FellowshipExtractor.Api.Tests --nologo --filter "FullyQualifiedName~StatParameterTests"
```

Expected: FAIL. `Names` today holds the 8 display names the census reaches, so the first, second and
fourth tests fail on the missing names.

- [x] **Step 3: Move `StatParameters` and build it from the table**

Create `src/FellowshipExtractor.Api/Models/StatParameters.cs`:

```csharp
namespace FellowshipExtractor.Api.Models;

public sealed class StatParameters
{
    private readonly IReadOnlyDictionary<string, string> byName;
    private readonly IReadOnlyDictionary<string, AttributeDisplay> byStat;

    public StatParameters(IReadOnlyDictionary<string, AttributeDisplay> attributes)
    {
        var counted = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in attributes.Values)
        {
            if (row.DisplayName is not { Length: > 0 } printed) continue;

            counted[printed] = counted.GetValueOrDefault(printed) + 1;
        }

        var named = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var rows = new Dictionary<string, AttributeDisplay>(StringComparer.OrdinalIgnoreCase);
        var printedNames = new List<string>();

        foreach (var (key, row) in attributes.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (row.DisplayName is not { Length: > 0 } printed) continue;
            if (counted[printed] > 1) continue;

            named[Compact(printed)] = key;
            rows[key] = row;
            printedNames.Add(printed);
        }

        byName = named;
        byStat = rows;
        Names = [.. printedNames.Order(StringComparer.OrdinalIgnoreCase)];
    }

    public IReadOnlyList<string> Names { get; }

    public string? Resolve(string parameter) =>
        byName.TryGetValue(Compact(parameter), out var stat) ? stat : null;

    public AttributeDisplay Row(string stat) => byStat[stat];

    private static string Compact(string name) => name.Replace(" ", string.Empty, StringComparison.Ordinal);
}
```

In `src/FellowshipExtractor.Api/Models/EntityStore.cs`, delete the `StatParameters` class and change
the one construction line:

```csharp
Stats = new StatParameters(Vocabulary.StatCalculation.Attributes);
```

Leave `Census` and the two `logger.LogInformation` calls as they stand. `Census` still fills the
`stats` and `spelled` sets the log lines count, and `stats` is no longer read by anything else.

- [x] **Step 4: Run it and watch it pass**

```powershell
dotnet test src/FellowshipExtractor.Api.Tests --nologo --filter "FullyQualifiedName~StatParameterTests"
```

Expected: 4 passed.

- [x] **Step 5: Run the whole suite**

```powershell
dotnet test src/FellowshipExtractor.Api.Tests --nologo -v q
```

Expected: `Failed: 2, Passed: 262, Total: 264`. `TooltipQueryTests.Leaves_armor_free_for_the_reserved_parameter_to_claim`
still passes, because `Armor` is the name two rows share.

- [x] **Step 6: Commit**

```bash
git add src/FellowshipExtractor.Api/Models/StatParameters.cs src/FellowshipExtractor.Api/Models/EntityStore.cs src/FellowshipExtractor.Api.Tests/StatParameterTests.cs
git commit -m "Take the stat parameters from the attribute table, not from the rolls"
```

---

### Task 3: The export path, the store and the seeder move to Services

**Files:**
- Create: `src/FellowshipExtractor.Api/Services/ExportPathService.cs`,
  `src/FellowshipExtractor.Api/Services/EntityStoreService.cs`,
  `src/FellowshipExtractor.Api/Services/IconSeederService.cs`,
  `src/FellowshipExtractor.Api/Models/Lookup.cs`
- Delete: `src/FellowshipExtractor.Api/Models/IconSeeder.cs`,
  `src/FellowshipExtractor.Api/Models/EntityStore.cs`
- Modify: `src/FellowshipExtractor.Api/Program.cs`,
  `src/FellowshipExtractor.Api/Components/_Imports.razor`,
  `src/FellowshipExtractor.Api/Components/Pages/EntityPage.razor`,
  `src/FellowshipExtractor.Api/Components/Pages/Home.razor`,
  `src/FellowshipExtractor.Api/Components/Sections/Knobs.razor`,
  `src/FellowshipExtractor.Api/Models/ChromeAssets.cs`,
  `src/FellowshipExtractor.Api/Models/Codex.cs`,
  `src/FellowshipExtractor.Api/Models/CodexQuery.cs`,
  `src/FellowshipExtractor.Api/Models/CodexView.cs`,
  `src/FellowshipExtractor.Api/Models/Placeholders.cs`,
  `src/FellowshipExtractor.Api/Models/SearchIndex.cs`,
  `src/FellowshipExtractor.Api/Models/TooltipView.cs`
- Modify: `src/FellowshipExtractor.Api.Tests/GlobalUsings.cs`,
  `src/FellowshipExtractor.Api.Tests/Export.cs`,
  `src/FellowshipExtractor.Api.Tests/RouteTests.cs`,
  `src/FellowshipExtractor.Tests/EntityStoreCorpusTests.cs`,
  `src/FellowshipExtractor.Tests/StaleExportTests.cs`

**Interfaces:**
- Produces: `IExportPathService` with `Export`, `Entities`, `Vocabulary`, `Ui`.
  `IEntityStoreService` with `Vocabulary`, `Entities`, `Types`, `Stats`, `PrimaryAttributes`,
  `Count`, `Ids`, `StatName`, `StatDisplay`, `StatValue`, `Hero`, `All`, `GrantedPower`, `Resolve`,
  `Wearer`, `DropItemLevel`, `Rarity`. `EntityStoreService.Reads` stays static, and
  `public static class EntityStore` in `Models/Lookup.cs` keeps `Reads` under its present name for
  the call sites that already say `EntityStore.Reads`.

- [x] **Step 1: Write the failing registration test**

Add to `src/FellowshipExtractor.Api.Tests/RouteTests.cs`, inside the class:

```csharp
    [Fact]
    public void Every_registered_service_is_resolved_through_an_interface()
    {
        Assert.NotNull(factory.Services.GetRequiredService<IExportPathService>());
        Assert.NotNull(factory.Services.GetRequiredService<IEntityStoreService>());
    }
```

Add the field the test reads by giving `RouteTests` the factory it already takes, keeping the
lowercase name the class uses for `client` and `chrome`, so that `Factory.Store` still binds to the
nested type rather than to an instance:

```csharp
    private readonly Factory factory;
```

and assign `this.factory = factory;` in the constructor.

- [x] **Step 2: Run it and watch it fail**

```powershell
dotnet test src/FellowshipExtractor.Api.Tests --nologo --filter "FullyQualifiedName~Every_registered_service_is_resolved_through_an_interface"
```

Expected: FAIL to compile, `IExportPathService` not found.

- [x] **Step 3: Write the two services and the seeder**

Create `src/FellowshipExtractor.Api/Services/ExportPathService.cs`:

```csharp
namespace FellowshipExtractor.Api.Services;

public interface IExportPathService
{
    string Export { get; }

    string Entities { get; }

    string Vocabulary { get; }

    string Ui { get; }
}

public sealed class ExportPathService(IConfiguration configuration) : IExportPathService
{
    private const string Variable = "FELLOWSHIP_EXPORT";

    public string Export { get; } = configuration[Variable]
        ?? throw new InvalidOperationException(
            $"{Variable} is not set. It names the folder the extractor's export-all command wrote, " +
            "and the AppHost passes it. Set it to run the API outside the AppHost.");

    public string Entities => Path.Combine(Export, "entities.jsonl");

    public string Vocabulary => Path.Combine(Export, "vocabulary.json");

    public string Ui => Path.Combine(Export, "ui");
}
```

Create `src/FellowshipExtractor.Api/Services/EntityStoreService.cs` holding `IEntityStoreService` and
`EntityStoreService`. The body is `EntityStore`'s, unchanged except that the constructor takes
`IExportPathService paths, ILogger<EntityStoreService> logger`, that `Stats` is built as Task 2 left
it, and that `Reads`, `ParseEnum` and `ParseEnumOrNull` are removed from it and read as
`EntityStore.Reads` from `Models/Lookup.cs`. The interface declares every public instance member:

```csharp
public interface IEntityStoreService
{
    VocabularyFile Vocabulary { get; }

    IReadOnlyList<EntityDocument> Entities { get; }

    IReadOnlyList<EntityType> Types { get; }

    StatParameters Stats { get; }

    IReadOnlySet<string> PrimaryAttributes { get; }

    int Count { get; }

    int Ids { get; }

    string StatName(string key);

    AttributeDisplay? StatDisplay(string? key);

    string StatValue(string? key, double value);

    HeroEntry? Hero(string? name);

    IReadOnlyList<EntityDocument> All(EntityType type, int id);

    (EntityType Type, int Id)? GrantedPower(int id);

    Lookup Resolve(EntityType type, int id, string? heroName, out EntityDocument? document, out HeroEntry? hero);

    HeroEntry? Wearer(EntityDocument document);

    int DropItemLevel(EntityDocument document);

    RarityEntry? Rarity(string? word, bool gem);
}
```

Create `src/FellowshipExtractor.Api/Models/Lookup.cs`:

```csharp
using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Api.Models;

public enum Lookup
{
    Found,
    NoSuchId,

    NeedsHero,

    NotForThatHero,
}

public static class EntityStore
{
    public static bool Reads(EntityDocument document, HeroEntry hero) =>
        document switch
        {
            ItemDocument item => item.Heroes is { } heroes
                ? heroes.Contains(ParseEnum<Hero>(hero.Name))
                : (item.ArmorType is null || item.ArmorType == ParseEnumOrNull<ArmorType>(hero.ArmorType)) &&
                  (item.PrimaryStat is null || item.PrimaryStat == ParseEnum<PrimaryStat>(hero.PrimaryStat)),
            TalentSlotDocument slot => slot.Hero == ParseEnum<Hero>(hero.Name),
            _ => true,
        };

    private static T ParseEnum<T>(string name) where T : struct, Enum =>
        Enum.TryParse<T>(name, out var value) ? value : throw new FormatException($"'{name}' names no {typeof(T).Name}.");

    private static T? ParseEnumOrNull<T>(string? name) where T : struct, Enum =>
        name is null ? null : ParseEnum<T>(name);
}
```

Create `src/FellowshipExtractor.Api/Services/IconSeederService.cs` holding `IconSeederService`, the
body of `IconSeeder`, taking `BlobContainerClient container, IExportPathService paths,
IChromeAssetService chrome, ILogger<IconSeederService> logger`. It implements `IHostedService` and
declares no interface of its own, because `AddHostedService` resolves it as the `IHostedService` the
host runs. Until Task 4 renames the chrome service, take `ChromeAssets chrome` and change it there.

Delete `Models/IconSeeder.cs` and `Models/EntityStore.cs`.

- [x] **Step 4: Move the call sites**

`Program.cs`:

```csharp
builder.Services.AddSingleton<IExportPathService, ExportPathService>();
builder.Services.AddHostedService<IconSeederService>();

builder.Services.AddSingleton<IEntityStoreService, EntityStoreService>();
```

and every `s.GetRequiredService<EntityStore>()` becomes `s.GetRequiredService<IEntityStoreService>()`.

Add `@using FellowshipExtractor.Api.Services` to `Components/_Imports.razor`, and change
`@inject EntityStore Store` to `@inject IEntityStoreService Store` in `EntityPage.razor`,
`Home.razor` and `Knobs.razor`.

Every `Models/` file whose constructor or field takes `EntityStore` takes `IEntityStoreService`:
`ChromeAssets`, `Codex`, `CodexQuery` (`CodexSearch`), `CodexView`, `Placeholders`, `SearchIndex`,
`TooltipView`. Each needs `using FellowshipExtractor.Api.Services;`.

Tests: add `global using FellowshipExtractor.Api.Services;` to `GlobalUsings.cs`. In `Export.cs`,
type `Paths` as `IExportPathService` and `Store` as `IEntityStoreService`, and construct
`new ExportPathService(configuration)` and `new EntityStoreService(paths, NullLogger<EntityStoreService>.Instance)`.
In `RouteTests.Factory`, the same two constructions. Its field must also change type, because
`EntityStore` is now the static class in `Models/Lookup.cs`:

```csharp
        public static readonly IEntityStoreService Store = BuildStore();
```

and the seeder removal reads:

```csharp
var seeder = services.SingleOrDefault(descriptor => descriptor.ImplementationType == typeof(IconSeederService));
```

In `src/FellowshipExtractor.Tests`, `EntityStoreCorpusTests` and `StaleExportTests` construct
`new EntityStoreService(new ExportPathService(configuration), NullLogger<EntityStoreService>.Instance)`
and need `using FellowshipExtractor.Api.Services;`.

- [x] **Step 5: Build and run both suites**

```powershell
dotnet build src/FellowshipExtractor.slnx
dotnet test src/FellowshipExtractor.Api.Tests --nologo -v q
dotnet test src/FellowshipExtractor.Tests --nologo -v q
```

Expected: build clean, API `Failed: 2, Passed: 263, Total: 265`, extractor suite green.

- [x] **Step 6: Commit**

```bash
git add -A
git commit -m "Put the export paths, the store and the seeder behind interfaces"
```

---

### Task 4: The rendering services move to Services

**Files:**
- Create: `Services/DesignTokenService.cs`, `Services/RichTextService.cs`,
  `Services/ChromeAssetService.cs`, `Services/PlaceholderService.cs`,
  `Services/TooltipViewService.cs`, `Services/FragmentService.cs`, `Services/TooltipService.cs`
- Delete: `Models/DesignTokens.cs`
- Modify: `Models/RichText.cs`, `Models/ChromeAssets.cs`, `Models/Placeholders.cs`,
  `Models/TooltipView.cs`, `Program.cs`, the 12 components injecting `TooltipView`,
  `Services/IconSeederService.cs`, `Export.cs`, `TooltipTests.cs:303`, `PlaceholderReadingTests.cs`

**Interfaces:**
- Produces: `IDesignTokenService` (`Colors`, `TextStyles`, `Color`, `Rarity`, `Hero`, `Gem`, `Role`,
  `CssVariables`), `IRichTextService` (`Styles`, `Unknown`, `Render`, `Plain`, `Css`),
  `IChromeAssetService` (every public instance member of `ChromeAssets`),
  `IPlaceholderService` (`Resolve`, `Reading`), `ITooltipViewService` (every public instance member
  of `TooltipView`, plus `bool DrawsState(EntityDocument document)`),
  `IFragmentService` (`Task<string> Render<T>(Dictionary<string, object?> parameters) where T : IComponent`),
  `ITooltipService` (`Task<IResult> Get(EntityType type, int id, IQueryCollection request)` for now).

- [x] **Step 1: Write the failing test**

Add to `RouteTests`:

```csharp
    [Fact]
    public void Every_rendering_service_is_resolved_through_an_interface()
    {
        var provider = factory.Services;

        Assert.NotNull(provider.GetRequiredService<IDesignTokenService>());
        Assert.NotNull(provider.GetRequiredService<IRichTextService>());
        Assert.NotNull(provider.GetRequiredService<IChromeAssetService>());
        Assert.NotNull(provider.GetRequiredService<IPlaceholderService>());
        Assert.NotNull(provider.GetRequiredService<ITooltipViewService>());
        Assert.NotNull(provider.GetRequiredService<IFragmentService>());
        Assert.NotNull(provider.GetRequiredService<ITooltipService>());
    }
```

- [x] **Step 2: Run it and watch it fail**

```powershell
dotnet test src/FellowshipExtractor.Api.Tests --nologo --filter "FullyQualifiedName~Every_rendering_service"
```

Expected: FAIL to compile.

- [x] **Step 3: Move the seven classes**

Each moves body-unchanged into `Services/<Name>Service.cs` under
`namespace FellowshipExtractor.Api.Services`, renamed, with an interface above it declaring its
public instance members, and with every constructor parameter typed as the interface of the service
it takes. `Fragments` and `Tooltips` move out of `Program.cs`. `DesignTokens` takes `VocabularyFile`
and `RichTextRenderer` takes `IReadOnlyDictionary<string, TextStyle>`, so their registrations stay
factories.

What each source file keeps:

- `Models/DesignTokens.cs` is deleted.
- `Models/RichText.cs` keeps `Substitution`.
- `Models/ChromeAssets.cs` keeps `Badge`. `Prefix`, `NodeStates`, `BlessingStates` and `Padlocked`
  are static and move to `ChromeAssetService`.
- `Models/Placeholders.cs` keeps `DecimalExtensions`. `Placeholders.Unresolved` is static and moves
  to `PlaceholderService`.
- `Models/TooltipView.cs` keeps `public static class TooltipView` holding `Root`, `TraitType`,
  `Magnitude`, `Block`, `Fact` and `TraitLine`. Everything else moves to `TooltipViewService`, whose
  own file refers to those as `TooltipView.Root` and so on.

Add to `ITooltipViewService` and `TooltipViewService` the one new member the entity page needs:

```csharp
    public bool DrawsState(EntityDocument document) => document is BlessingDocument or TraitDocument;
```

and in `Components/Tooltip/Tooltip.razor`, change the guard in `OnParametersSet` to read
`if (Ui.DrawsState(Entity)) return;`, leaving the two typed branches in the markup as they stand.

- [x] **Step 4: Move the call sites**

`Program.cs`:

```csharp
builder.Services.AddSingleton<IDesignTokenService>(s =>
    new DesignTokenService(s.GetRequiredService<IEntityStoreService>().Vocabulary));
builder.Services.AddSingleton<IRichTextService>(s =>
    new RichTextService(s.GetRequiredService<IEntityStoreService>().Vocabulary.TextStyles));

builder.Services.AddSingleton<IChromeAssetService, ChromeAssetService>();
builder.Services.AddSingleton<IPlaceholderService, PlaceholderService>();
builder.Services.AddSingleton<ITooltipViewService, TooltipViewService>();
builder.Services.AddSingleton<IFragmentService, FragmentService>();
builder.Services.AddSingleton<ITooltipService, TooltipService>();
```

and the two route handlers take `ITooltipService tooltips`.

`@inject TooltipView Ui` becomes `@inject ITooltipViewService Ui` in `Blessing.razor`,
`TipGranted.razor`, `TipIdentity.razor`, `TipLegendaries.razor`, `TipOverflow.razor`,
`TipProse.razor`, `TipSets.razor`, `TipTraits.razor`, `Tooltip.razor` and `Trait.razor`.
`@inject TooltipView Tips` becomes `@inject ITooltipViewService Tips` in `Grants.razor` and
`Legendaries.razor`.

`Export.cs`: type `Chrome` as `IChromeAssetService` and `Tooltip` as `ITooltipViewService`, and
construct `DesignTokenService`, `RichTextService`, `PlaceholderService`, `ChromeAssetService`,
`TooltipViewService` and `CodexView` with the new names. The 24 `Services.AddSingleton(export.X)`
lines in the bunit tests then register the interfaces with no edit, because the argument's compile
time type is what `AddSingleton` registers.

`TooltipTests.cs:303` reads `new DesignTokenService(Store.Vocabulary).Rarity(...)`.
`PlaceholderReadingTests.cs:7` reads `new PlaceholderService(export.Store, new RichTextService(...))`.
`IconSeederService` takes `IChromeAssetService chrome` and reads `ChromeAssetService.Prefix`.

- [x] **Step 5: Build and run the suite**

```powershell
dotnet build src/FellowshipExtractor.slnx
dotnet test src/FellowshipExtractor.Api.Tests --nologo -v q
```

Expected: build clean, `Failed: 2, Passed: 264, Total: 266`.

- [x] **Step 6: Commit**

```bash
git add -A
git commit -m "Put the rendering services behind interfaces"
```

---

### Task 5: The browsing services move to Services

**Files:**
- Create: `Services/CodexService.cs`, `Services/SearchIndexService.cs`,
  `Services/CodexSearchService.cs`, `Services/CodexViewService.cs`, `Services/SuggestionService.cs`
- Delete: `Models/SearchIndex.cs`
- Modify: `Models/Codex.cs`, `Models/CodexQuery.cs`, `Models/CodexView.cs`, `Models/Suggestions.cs`,
  `Models/PublicEntity.cs`, `ApiDocument.cs`, `Program.cs`, `Omnibox.razor`, `Browse.razor`,
  `NotFound.razor`, `App.razor`, `MainLayout.razor`, `MegaMenu.razor`, `EntityPage.razor`,
  `Home.razor`, the nine `Sections` and `Shared` components injecting `CodexView`,
  `BlessingCrest.razor`, `TraitCrest.razor`, `Export.cs`, `ApiDocumentTests.cs`, `ListingTests.cs`,
  `SuggestionsTests.cs`

**Interfaces:**
- Produces: `ICodexService` (`Facets`, `Flags`, `Measures`, `Types`, `Count`, `Menu`, `Of`, `View`,
  `Printed`, `Offered`, `Facet`, `Measure`, `Flag`, `Scaling`), `ISearchIndexService` (`Search`),
  `ICodexSearchService` (`Run`), `ICodexViewService` (every public instance member of `CodexView`),
  `ISuggestionService` (`For`). `SearchIndexService.Hit` stays nested.
  `SuggestionService.Limit`, `Ceiling`, `DefaultLimit`, `MaximumLimit`, `Words` and `Mark` stay static
  on the implementation.

- [x] **Step 1: Write the failing test**

Add to `RouteTests`:

```csharp
    [Fact]
    public void Every_browsing_service_is_resolved_through_an_interface()
    {
        var provider = factory.Services;

        Assert.NotNull(provider.GetRequiredService<ICodexService>());
        Assert.NotNull(provider.GetRequiredService<ISearchIndexService>());
        Assert.NotNull(provider.GetRequiredService<ICodexSearchService>());
        Assert.NotNull(provider.GetRequiredService<ICodexViewService>());
        Assert.NotNull(provider.GetRequiredService<ISuggestionService>());
    }
```

- [x] **Step 2: Run it and watch it fail**

```powershell
dotnet test src/FellowshipExtractor.Api.Tests --nologo --filter "FullyQualifiedName~Every_browsing_service"
```

Expected: FAIL to compile.

- [x] **Step 3: Move the five classes**

Each moves body-unchanged into `Services/<Name>Service.cs` with an interface above it.
`CodexSearch` moves out of `Models/CodexQuery.cs`, which keeps `CodexQuery`, `Listing` and
`CodexResults`. `Models/SearchIndex.cs` is deleted, `Hit` moving with the class.

`Models/Codex.cs` keeps the records, `DocumentFields`, and gains:

```csharp
public static class Codex
{
    public static IReadOnlyList<string> Schools(EntityDocument document) => ...

    public static string Spaced(string value) => ...

    public static string Label(EntityType type) => PublicTypes.Label(type);

    public static string Number(double value) => ...
}
```

with the four bodies moved across unchanged, so every `Codex.Schools`, `Codex.Spaced`, `Codex.Label`
and `Codex.Number` call site reads as it does today.

`Models/CodexView.cs` keeps `TuningRow`, `TokenReading`, and gains:

```csharp
public static class CodexView
{
    public static string? Readable(string? colour) => ...

    public static IReadOnlyList<Placeholder> PlaceholdersOf(EntityDocument document) => ...

    public static string Number(double value) => Codex.Number(value);

    public static string Constant(object? value) => ...
}
```

with the private `Floor`, `Luminance` and `Channel` helpers moving with `Readable`.

`Models/Suggestions.cs` keeps `SearchResult`, `SearchGroup`, `SearchResponse` and `EntitiesResponse`.
`SearchResult.From` and `PublicEntity.From` take `ICodexViewService view`.

- [x] **Step 4: Move the call sites**

`Program.cs`:

```csharp
builder.Services.AddSingleton<ICodexService, CodexService>();
builder.Services.AddSingleton<ISearchIndexService, SearchIndexService>();
builder.Services.AddSingleton<ICodexSearchService, CodexSearchService>();
builder.Services.AddSingleton<ICodexViewService, CodexViewService>();
builder.Services.AddSingleton<ISuggestionService, SuggestionService>();
```

and the handlers take `ICodexService codex, ICodexSearchService search, ICodexViewService view,
ISuggestionService suggestions, IEntityStoreService store`, with `Suggestions.Limit` reading
`SuggestionService.Limit`.

`ApiDocument.cs`: `ApiInfo(ICodexService codex)`, `ApiParameters(IEntityStoreService store, ICodexService codex)`,
and `ApiQuerySet.Search => SuggestionService.Parameters`.

`@inject CodexView Ui` becomes `@inject ICodexViewService Ui` in the 17 places that say it,
`@inject Codex Catalog` becomes `@inject ICodexService Catalog` in three, `@inject CodexSearch Search`
becomes `@inject ICodexSearchService Search`, and `@inject Suggestions Suggest` becomes
`@inject ISuggestionService Suggest`. `Omnibox.razor` reads `SuggestionService.Mark` and
`SuggestionService.Words`.

`Export.cs`: type `View` as `ICodexViewService` and construct `CodexViewService` and `CodexService`.
`ApiDocumentTests.cs:76` reads `new CodexService(export.Store, new DesignTokenService(export.Store.Vocabulary))`.
`ListingTests.cs` reads `new CodexSearchService(...)` and `new SearchIndexService(...)`.
`SuggestionsTests.cs` reads `new SuggestionService(new SearchIndexService(...), export.View)`.

- [x] **Step 5: Build and run the suite**

```powershell
dotnet build src/FellowshipExtractor.slnx
dotnet test src/FellowshipExtractor.Api.Tests --nologo -v q
```

Expected: build clean, `Failed: 2, Passed: 265, Total: 267`.

- [x] **Step 6: Confirm nothing is registered by its concrete type**

```powershell
Select-String -Path src/FellowshipExtractor.Api/Program.cs -Pattern "AddSingleton" -Context 0,0
```

Expected: every line names an interface first, except `AddHostedService<IconSeederService>()` and the
`AddAzureBlobContainerClient` line.

- [x] **Step 7: Commit**

```bash
git add -A
git commit -m "Put the browsing services behind interfaces"
```

---

### Task 6: The shared endpoint plumbing

Nothing here answers a route yet. It is the machinery every endpoint file leans on, plus the empty
`MapApiEndpoints` that Task 7 onward fills.

**Files:**
- Create: `Endpoints/RequestProperties.cs`, `Endpoints/StatQuery.cs`, `Endpoints/Records.cs`,
  `Endpoints/ApiEndpoints.cs`, `Endpoints/Documentation/StatsDocument.cs`,
  `Endpoints/Documentation/ListDocument.cs`
- Modify: `Models/TooltipQuery.cs` (add `ImbuedTraits` and `GemPowers`), `Models/Lookup.cs` (add
  `Lookups`), `Services/TooltipService.cs` (add the `TooltipQuery` overload), `Program.cs`
  (`AddValidation`, `MapApiEndpoints`), `Api.Tests/GlobalUsings.cs`

**Interfaces:**
- Produces:
  - `RequestProperties.Of(Type request)` returning `IReadOnlySet<string>`, cached.
  - `StatQuery.Read<TRequest>(TRequest request, IQueryCollection raw, StatParameters stats, out string? error)`
    returning `TooltipQuery`.
  - `StatQuery.Unreadable(string error, StatParameters stats)` returning `IResult`.
  - `StatQuery.Given(string? value)` returning `string?`.
  - `Records.One(EntityType type, int id, string? hero, IEntityStoreService store, ICodexViewService view)`
    returning `IResult`.
  - `Lookups.Problem(Lookup outcome, EntityType type, int id, HeroEntry? hero, IReadOnlyList<EntityDocument> readings)`
    returning `IResult`.
  - `RouteHandlerBuilder WithStats(this RouteHandlerBuilder route)`.
  - `ITooltipService.Get(EntityType type, int id, TooltipQuery query)`.
  - `ImbuedTraits` and `GemPowers`, each with `TryParse(string?, out T)` and `Values`.

- [ ] **Step 1: Write the failing test**

Create `src/FellowshipExtractor.Api.Tests/ParameterTests.cs` with its first case only:

```csharp
using Microsoft.AspNetCore.Http;
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
    public void A_comma_and_a_repetition_read_the_same_gem_powers()
    {
        Assert.True(GemPowers.TryParse("1:2,3:4", out var joined));

        Assert.Equal([new GemPower(1, 2), new GemPower(3, 4)], joined.Values);
        Assert.False(GemPowers.TryParse("1", out _));
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

```powershell
dotnet test src/FellowshipExtractor.Api.Tests --nologo --filter "FullyQualifiedName~ParameterTests"
```

Expected: FAIL to compile, `RequestProperties`, `StatQuery`, `ImbuedTraits` and `GemPowers` not found.

- [ ] **Step 3: Write `RequestProperties`**

Create `src/FellowshipExtractor.Api/Endpoints/RequestProperties.cs`:

```csharp
using System.Collections.Concurrent;
using System.Collections.Frozen;

namespace FellowshipExtractor.Api.Endpoints;

public static class RequestProperties
{
    private static readonly ConcurrentDictionary<Type, FrozenSet<string>> Declared = new();

    public static IReadOnlySet<string> Of(Type request) => Declared.GetOrAdd(request, Read);

    private static FrozenSet<string> Read(Type request) =>
        request.GetProperties()
            .Select(property => property.Name)
            .ToFrozenSet(StringComparer.OrdinalIgnoreCase);
}
```

- [ ] **Step 4: Write the two list types**

Append to `src/FellowshipExtractor.Api/Models/TooltipQuery.cs`:

```csharp
public sealed class ImbuedTraits
{
    public required IReadOnlyList<ImbuedTrait> Values { get; init; }

    public static bool TryParse(string? value, out ImbuedTraits result)
    {
        var parsed = new List<ImbuedTrait>();

        foreach (var part in (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!ImbuedTrait.TryParse(part, CultureInfo.InvariantCulture, out var trait))
            {
                result = new ImbuedTraits { Values = [] };
                return false;
            }

            parsed.Add(trait);
        }

        result = new ImbuedTraits { Values = parsed };
        return true;
    }
}

public sealed class GemPowers
{
    public required IReadOnlyList<GemPower> Values { get; init; }

    public static bool TryParse(string? value, out GemPowers result)
    {
        var parsed = new List<GemPower>();

        foreach (var part in (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!GemPower.TryParse(part, CultureInfo.InvariantCulture, out var gem))
            {
                result = new GemPowers { Values = [] };
                return false;
            }

            parsed.Add(gem);
        }

        result = new GemPowers { Values = parsed };
        return true;
    }
}
```

- [ ] **Step 5: Write `StatQuery`**

Create `src/FellowshipExtractor.Api/Endpoints/StatQuery.cs`:

```csharp
using System.Globalization;

namespace FellowshipExtractor.Api.Endpoints;

public static class StatQuery
{
    private const decimal Ceiling = 1_000_000_000_000m;

    public static string? Given(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    public static TooltipQuery Read<TRequest>(
        TRequest request, IQueryCollection raw, StatParameters stats, out string? error)
        where TRequest : notnull
    {
        error = null;

        var declared = RequestProperties.Of(request.GetType());

        var values = new Dictionary<string, IReadOnlyList<decimal>>(StringComparer.OrdinalIgnoreCase);
        var attributes = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, given) in raw)
        {
            if (declared.Contains(key)) continue;

            var stat = stats.Resolve(key);
            var percentage = false;

            if (stat is null && key.EndsWith(StatConversion.PercentageSuffix, StringComparison.OrdinalIgnoreCase))
            {
                stat = stats.Resolve(key[..^StatConversion.PercentageSuffix.Length]);
                percentage = stat is not null;
            }

            if (stat is null)
            {
                error = $"Unknown parameter '{key}'.";
                return new TooltipQuery();
            }

            var attribute = stats.Row(stat);

            if (percentage && !StatConversion.Converts(attribute))
            {
                error =
                    $"'{key[..^StatConversion.PercentageSuffix.Length]}' is not a rating, " +
                    $"so '{key}' has nothing to convert.";
                return new TooltipQuery();
            }

            var numbers = new List<decimal>(given.Count);

            foreach (var value in given)
            {
                if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                {
                    error = $"Stat '{key}' has the non-numeric value '{value}'.";
                    return new TooltipQuery();
                }

                if (Math.Abs(number) > Ceiling)
                {
                    error = $"Stat '{key}' is {value}, past the {Ceiling:N0} a coefficient can be applied to.";
                    return new TooltipQuery();
                }

                numbers.Add(number);
            }

            if (!percentage)
            {
                values[stat] = values.TryGetValue(stat, out var already) ? [.. already, .. numbers] : numbers;
            }

            attributes[stat] = attributes.GetValueOrDefault(stat) + (percentage
                ? StatConversion.FromPercentage(numbers.Sum(), attribute)
                : StatConversion.FromRating(numbers.Sum(), attribute));
        }

        return new TooltipQuery { Stats = values, Attributes = attributes };
    }

    public static IResult Unreadable(string error, StatParameters stats) => Results.Problem(
        title: "The request parameters could not be read.",
        detail: $"{error} Stat parameters are {string.Join(", ", stats.Names)}.",
        statusCode: StatusCodes.Status400BadRequest);
}
```

- [ ] **Step 6: Run the parameter tests and watch them pass**

```powershell
dotnet test src/FellowshipExtractor.Api.Tests --nologo --filter "FullyQualifiedName~ParameterTests"
```

Expected: 6 passed.

- [ ] **Step 7: Write `Lookups` and `Records`**

Append to `src/FellowshipExtractor.Api/Models/Lookup.cs`:

```csharp
public static class Lookups
{
    public static IResult Problem(
        Lookup outcome, EntityType type, int id, HeroEntry? hero, IReadOnlyList<EntityDocument> readings)
    {
        var noun = PublicTypes.Noun(type);

        return outcome switch
        {
            Lookup.NoSuchId => Results.Problem(
                title: "No such id.",
                detail: $"The build has no {noun} {id}.",
                statusCode: StatusCodes.Status404NotFound),

            Lookup.NeedsHero => Results.Problem(
                title: "This id reads differently per hero.",
                detail: $"The build has {readings.Count} readings of {noun} {id} and no hero was given. " +
                    "Add hero=<name> to pick one. The readings are " +
                    string.Join("; ", readings.Select(Describe)) + ".",
                statusCode: StatusCodes.Status400BadRequest),

            Lookup.NotForThatHero => Results.Problem(
                title: "Not for that hero.",
                detail: $"The build has no reading of {noun} {id} for {hero!.Name}.",
                statusCode: StatusCodes.Status404NotFound),

            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    public static IResult NoSuchHero(string named, IReadOnlyList<HeroEntry> roster) => Results.Problem(
        title: "No such hero.",
        detail: $"'{named}' is not a hero. The roster is " +
            string.Join(", ", roster.Select(hero => hero.Name).Order()) + ".",
        statusCode: StatusCodes.Status400BadRequest);

    private static string Describe(EntityDocument document) => document switch
    {
        ItemDocument item => $"{PublicTypes.NameOf(item)} " +
            $"[{item.ArmorType?.ToString() ?? "any"}/{item.PrimaryStat?.ToString() ?? "any"}]",
        TalentSlotDocument slot => $"{PublicTypes.NameOf(slot)} [{slot.Hero}]",
        _ => PublicTypes.NameOf(document),
    };
}
```

and delete the identical `switch`, `Describe` and hero problem from `Services/TooltipService.cs`,
which now calls `Lookups.Problem(outcome, type, id, hero, readings)` and
`Lookups.NoSuchHero(named, store.Vocabulary.Heroes)`.

Create `src/FellowshipExtractor.Api/Endpoints/Records.cs`. The roster check comes before `Resolve`,
because `Resolve` answers `NotForThatHero` with a null `HeroEntry` when the name is not a hero at
all, and `Lookups.Problem` names that hero:

```csharp
namespace FellowshipExtractor.Api.Endpoints;

public static class Records
{
    public static IResult One(
        EntityType type, int id, string? hero, IEntityStoreService store, ICodexViewService view)
    {
        var asked = StatQuery.Given(hero);

        if (asked is not null && store.Hero(asked) is null)
        {
            return Lookups.NoSuchHero(asked, store.Vocabulary.Heroes);
        }

        var outcome = store.Resolve(type, id, asked, out var document, out var wearer);

        return outcome == Lookup.Found
            ? Results.Ok(PublicEntity.From(document!, view))
            : Lookups.Problem(outcome, type, id, wearer, store.All(type, id));
    }
}
```

- [ ] **Step 8: Give `ITooltipService` the query overload**

In `Services/TooltipService.cs`, add to the interface and the class:

```csharp
    Task<IResult> Get(EntityType type, int id, TooltipQuery query);
```

The new body is the existing one from the `store.Hero` check onward. The existing
`Get(EntityType, int, IQueryCollection)` keeps its `TooltipQuery.Parse` call and then calls the new
overload. Task 12 deletes it.

- [ ] **Step 9: Write `WithStats`, the stats note and the list schema**

Create `src/FellowshipExtractor.Api/Endpoints/Documentation/StatsDocument.cs`:

```csharp
using System.Reflection;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FellowshipExtractor.Api.Endpoints;

public sealed record StatsMarker;

public static class StatsDocument
{
    public static RouteHandlerBuilder WithStats(this RouteHandlerBuilder route) =>
        route.WithMetadata(new StatsMarker());
}

public sealed class StatsNote(IEntityStoreService store) : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellation)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;

        if (!metadata.OfType<StatsMarker>().Any()) return Task.CompletedTask;

        var declared = metadata.OfType<MethodInfo>()
            .SelectMany(method => method.GetParameters())
            .Where(parameter => parameter.GetCustomAttribute<AsParametersAttribute>() is not null)
            .SelectMany(parameter => RequestProperties.Of(parameter.ParameterType))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var accepted = store.Stats.Names.Where(name => !declared.Contains(name.Replace(
            " ", string.Empty, StringComparison.Ordinal))).ToArray();

        var note =
            "Any stat the build names may also be given, as `?Stamina=40`, to state a magnitude the tooltip " +
            $"prints rather than computes, and a rating may be given as a percentage with the " +
            $"`{StatConversion.PercentageSuffix}` suffix. The stats are " + string.Join(", ", accepted) + ".";

        operation.Description = operation.Description is { Length: > 0 } already
            ? already + "\n\n" + note
            : note;

        return Task.CompletedTask;
    }
}
```

Create `src/FellowshipExtractor.Api/Endpoints/Documentation/ListDocument.cs`:

```csharp
using System.Text.Json.Nodes;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FellowshipExtractor.Api.Endpoints;

public sealed class ListParameters : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellation)
    {
        if (context.JsonTypeInfo.Type == typeof(ImbuedTraits))
        {
            Text(schema, "7:2,12:1");
        }
        else if (context.JsonTypeInfo.Type == typeof(GemPowers))
        {
            Text(schema, "3:40,5:25");
        }

        return Task.CompletedTask;
    }

    private static void Text(OpenApiSchema schema, string example)
    {
        schema.Type = JsonSchemaType.String;
        schema.Properties?.Clear();
        schema.Example = JsonValue.Create(example);
    }
}
```

- [ ] **Step 10: Write the empty `ApiEndpoints` and wire it in**

Create `src/FellowshipExtractor.Api/Endpoints/ApiEndpoints.cs`:

```csharp
namespace FellowshipExtractor.Api.Endpoints;

public static class ApiEndpoints
{
    public static void MapApiEndpoints(this IEndpointRouteBuilder app)
    {
    }
}
```

In `Program.cs`, add `using FellowshipExtractor.Api.Endpoints;`, register validation and the two new
transformers, and make the one call:

```csharp
builder.Services.AddOpenApi(ApiDocument.Name, options =>
{
    options.AddDocumentTransformer<ApiInfo>();
    options.AddOperationTransformer<ApiParameters>();
    options.AddOperationTransformer<StatsNote>();
    options.AddSchemaTransformer<ListParameters>();
});

builder.Services.AddValidation();
```

and, immediately after `app.MapRazorComponents<App>().AddInteractiveServerRenderMode();`:

```csharp
app.MapApiEndpoints();
```

Add `global using FellowshipExtractor.Api.Endpoints;` to `Api.Tests/GlobalUsings.cs`.

- [ ] **Step 11: Build and run the suite**

```powershell
dotnet build src/FellowshipExtractor.slnx
dotnet test src/FellowshipExtractor.Api.Tests --nologo -v q
```

Expected: build clean, `Failed: 2, Passed: 271, Total: 273`.

- [ ] **Step 12: Commit**

```bash
git add -A
git commit -m "Land the plumbing every endpoint file leans on"
```

---

### Task 7: The item endpoints

The item is the shape carrying everything, so it is where `AddValidation`, `[Description]` and the
`WithStats` reflection are proved.

**Files:**
- Create: `Endpoints/Item/ItemGroup.cs`, `Endpoints/Item/ItemRecordEndpoint.cs`,
  `Endpoints/Item/ItemTooltipEndpoint.cs`
- Modify: `Endpoints/ApiEndpoints.cs`, `Program.cs` (delete the old `/api/item/{itemId:int}` route)
- Test: `src/FellowshipExtractor.Api.Tests/RouteTests.cs`

**Interfaces:**
- Produces: `IEndpointRouteBuilder.MapItem()`, `ItemRecordRequest` (`Id`, `Hero`),
  `ItemTooltipRequest` (`Id` and the twelve).

- [ ] **Step 1: Write the failing test**

Add to `RouteTests`:

```csharp
    [Fact]
    public async Task An_item_answers_its_record_and_its_tooltip_at_its_own_address()
    {
        var (id, hero) = Sample(EntityType.Item);
        var wearer = hero is null ? string.Empty : $"?hero={hero}";

        var record = await client.GetAsync($"/api/item/{id}{wearer}", Xunit.TestContext.Current.CancellationToken);
        var tooltip = await client.GetAsync(
            $"/api/item/{id}/tooltip{wearer}", Xunit.TestContext.Current.CancellationToken);

        record.EnsureSuccessStatusCode();
        tooltip.EnsureSuccessStatusCode();

        var body = await record.Content.ReadAsStringAsync(Xunit.TestContext.Current.CancellationToken);

        Assert.Contains("\"type\":\"item\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_optional_property_the_binder_fills_reaches_the_card()
    {
        var id = Factory.Store.Entities.OfType<ItemDocument>().First(row => row.Stats is { Count: > 0 }).Id;

        var drawn = await client.GetStringAsync(
            $"/api/item/{id}/tooltip?armor=303", Xunit.TestContext.Current.CancellationToken);

        Assert.Contains("303", drawn, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_hero_the_roster_does_not_carry_is_refused_by_the_record()
    {
        var (id, _) = Sample(EntityType.Item);

        var response = await client.GetAsync(
            $"/api/item/{id}?hero=nonsense", Xunit.TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("is not a hero", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_item_level_past_the_ceiling_is_refused_by_the_field_that_states_it()
    {
        var (id, hero) = Sample(EntityType.Item);
        var wearer = hero is null ? "?" : $"?hero={hero}&";

        var response = await client.GetAsync(
            $"/api/item/{id}/tooltip{wearer}itemLevel=10001", Xunit.TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("ItemLevel", body, StringComparison.OrdinalIgnoreCase);
    }
```

- [ ] **Step 2: Run it and watch it fail**

```powershell
dotnet test src/FellowshipExtractor.Api.Tests --nologo --filter "FullyQualifiedName~An_item_answers_its_record"
```

Expected: FAIL. `/api/item/{id}` answers the old tooltip route as HTML, so the JSON assertion fails.

- [ ] **Step 3: Write the group**

Create `src/FellowshipExtractor.Api/Endpoints/Item/ItemGroup.cs`:

```csharp
namespace FellowshipExtractor.Api.Endpoints;

public static class ItemGroup
{
    public static RouteGroupBuilder MapItem(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/item").WithTags(PublicTypes.Label(EntityType.Item));

        group.MapItemRecord();
        group.MapItemTooltip();

        return group;
    }
}
```

- [ ] **Step 4: Write the record endpoint**

Create `src/FellowshipExtractor.Api/Endpoints/Item/ItemRecordEndpoint.cs`:

```csharp
using System.ComponentModel;

namespace FellowshipExtractor.Api.Endpoints;

public sealed record ItemRecordRequest
{
    public int Id { get; init; }

    [Description("The hero whose reading of the id to answer, where one id reads differently per hero.")]
    public string? Hero { get; init; }
}

public static class ItemRecordEndpoint
{
    public static RouteHandlerBuilder MapItemRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetItem")
            .WithSummary($"One {PublicTypes.Noun(EntityType.Item)}'s record.")
            .Produces<PublicEntity>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] ItemRecordRequest request, IEntityStoreService store, ICodexViewService view) =>
        Records.One(EntityType.Item, request.Id, request.Hero, store, view);
}
```

- [ ] **Step 5: Write the tooltip endpoint**

Create `src/FellowshipExtractor.Api/Endpoints/Item/ItemTooltipEndpoint.cs`:

```csharp
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace FellowshipExtractor.Api.Endpoints;

public sealed record ItemTooltipRequest
{
    public int Id { get; init; }

    [Description("The hero whose reading of the id to draw, where one id reads differently per hero.")]
    public string? Hero { get; init; }

    [Description("The rung to read at, by the name the game prints or by tier.")]
    public string? Rarity { get; init; }

    [Range(0, 10_000)]
    [Description("The item level to compute stat magnitudes at, 0 to 10,000.")]
    public int? ItemLevel { get; init; }

    [Description("How many times the item has been tempered.")]
    public int? Upgrades { get; init; }

    [Description("The tempering ceiling to print in place of the one the shipped ladder states for this rung and level.")]
    public int? MaxUpgrades { get; init; }

    [Description("Given as true when the item's gem socket is to read as empty.")]
    public bool? NoSocket { get; init; }

    [Description("The power potential to print.")]
    public decimal? PotentialPower { get; init; }

    [Description("The armour to print above the stats the tooltip computes.")]
    public decimal? Armor { get; init; }

    [Description("A gem type id and its power separated by a colon, comma separated for several.")]
    public GemPowers? GemPowers { get; init; }

    [Description("An item trait id, optionally a colon and the rank it sits at, comma separated for several.")]
    public ImbuedTraits? Trait { get; init; }

    [Description("How many trait slots are filled.")]
    public int? AllocatedTraits { get; init; }

    [Description("How many trait slots the item carries.")]
    public int? MaxTraits { get; init; }

    public TooltipQuery ToQuery(IQueryCollection raw, StatParameters stats, out string? error)
    {
        var read = StatQuery.Read(this, raw, stats, out error);
        var traits = Trait?.Values ?? [];

        return read with
        {
            Hero = StatQuery.Given(Hero),
            Rarity = StatQuery.Given(Rarity),
            ItemLevel = ItemLevel,
            Upgrades = Upgrades,
            MaxUpgrades = MaxUpgrades,
            NoSocket = NoSocket == true,
            PotentialPower = PotentialPower,
            Armor = Armor,
            GemPowers = GemPowers?.Values ?? [],
            Traits = traits,
            AllocatedTraits = AllocatedTraits ?? (traits.Count > 0 ? traits.Count : null),
            MaxTraits = MaxTraits,
        };
    }
}

public static class ItemTooltipEndpoint
{
    public static RouteHandlerBuilder MapItemTooltip(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}/tooltip", Handle)
            .WithName("GetItemTooltip")
            .WithSummary($"One {PublicTypes.Noun(EntityType.Item)}'s tooltip as a self-contained HTML fragment.")
            .WithStats()
            .Produces<string>(StatusCodes.Status200OK, "text/html")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static Task<IResult> Handle(
        [AsParameters] ItemTooltipRequest request,
        HttpContext http,
        ITooltipService tooltips,
        IEntityStoreService store)
    {
        var query = request.ToQuery(http.Request.Query, store.Stats, out var error);

        return error is null
            ? tooltips.Get(EntityType.Item, request.Id, query)
            : Task.FromResult(StatQuery.Unreadable(error, store.Stats));
    }
}
```

- [ ] **Step 6: Map it and retire the colliding route**

In `ApiEndpoints.MapApiEndpoints`, add `app.MapItem();`. In `Program.cs`, delete the whole
`app.MapGet("/api/item/{itemId:int}", ...)` block, which now collides with `/api/item/{id:int}`.

- [ ] **Step 7: Run it and watch it pass**

```powershell
dotnet test src/FellowshipExtractor.Api.Tests --nologo --filter "FullyQualifiedName~An_item_answers_its_record|FullyQualifiedName~An_optional_property_the_binder|FullyQualifiedName~A_hero_the_roster_does_not_carry|FullyQualifiedName~An_item_level_past_the_ceiling"
```

Expected: 4 passed.

`An_optional_property_the_binder_fills_reaches_the_card` is the gate on `[AsParameters]` itself. A
`sealed record` with `{ get; init; }` properties gets a compiler generated parameterless constructor
and the generator treats `init` setters as settable, so binding should work. If that test answers 200
with no `303` in the card, binding produced a default valued model and every one of the 22 tooltip
endpoints is wrong. Fix it here, before the scaffold in Task 10 multiplies the shape: give each
request record positional parameters matching its property names, or add
`public ItemTooltipRequest() { }` explicitly, and rerun.

If the item level test fails with a 200 rather than a 400, the validation generator has not picked up
`[AsParameters]` records. In that case delete the `[Range]` attribute, add the check to `ToQuery`
before the `return`, keeping today's wording:

```csharp
        if (error is null && ItemLevel is < 0 or > 10_000)
        {
            error = $"itemLevel {ItemLevel} is outside 0 to 10,000.";
        }
```

and change the test's assertion to `Assert.Contains("itemLevel 10001 is outside", body, StringComparison.Ordinal)`.

- [ ] **Step 8: Read the published operation**

```powershell
$env:FELLOWSHIP_EXPORT = "G:\source\Fellowship_Data_Extractor.route-redesign\output\export"
$app = Start-Process dotnet -ArgumentList "run --project src/FellowshipExtractor.Api --no-launch-profile" -PassThru
```

Prefer the shell tool's background mode so the process is tracked and terminable. Once it answers,
fetch `/openapi/v1.json` and read the item tooltip operation. Confirm three things:

1. It carries twelve query parameters beside `id`, each with the description written above. If the
   descriptions are absent, `[Description]` is not being read; add an operation transformer that
   fills each parameter's description from the matching property's `DescriptionAttribute`.
2. `ItemLevel` carries `minimum: 0` and `maximum: 10000`.
3. `Trait` and `GemPowers` publish as `"type": "string"` with the examples. If they publish as
   objects, the schema transformer is not reached for parameter schemas; move the rewrite into
   `StatsNote` where it can reach `operation.Parameters` directly.
4. The description ends with the sentence naming the stats, and that sentence does not name `Armor`.

Stop the process.

- [ ] **Step 9: Run the whole suite**

```powershell
dotnet test src/FellowshipExtractor.Api.Tests --nologo -v q
```

Expected: `Failed: 2`, everything else green.

- [ ] **Step 10: Commit**

```bash
git add -A
git commit -m "Give the item a folder, two routes and a request model apiece"
```

---

### Task 8: The ability and weapon power endpoints, and the hero that names a stat

**Files:**
- Create: `Endpoints/Ability/AbilityGroup.cs`, `AbilityRecordEndpoint.cs`, `AbilityTooltipEndpoint.cs`,
  `Endpoints/WeaponPower/WeaponPowerGroup.cs`, `WeaponPowerRecordEndpoint.cs`,
  `WeaponPowerTooltipEndpoint.cs`
- Modify: `Endpoints/ApiEndpoints.cs`, `Program.cs` (delete `/api/ability/{abilityId:int}`)
- Test: `src/FellowshipExtractor.Api.Tests/AbilityScalingTests.cs`

**Interfaces:**
- Produces: `MapAbility()`, `MapWeaponPower()`, `AbilityRecordRequest` (`Id`),
  `AbilityTooltipRequest` (`Id`, `Hero`), and the same pair for weapon power.

- [ ] **Step 1: Write the failing test**

Create `src/FellowshipExtractor.Api.Tests/AbilityScalingTests.cs`:

```csharp
using System.Net;

using Microsoft.AspNetCore.Mvc.Testing;

namespace FellowshipExtractor.Api.Tests;

public sealed class AbilityScalingTests : IClassFixture<RouteTests.Factory>
{
    private readonly HttpClient client;

    public AbilityScalingTests(RouteTests.Factory factory) => client = factory.CreateClient();

    private static (int Id, string Attribute) Scaling()
    {
        foreach (var document in RouteTests.Factory.Store.Entities.OfType<AbilityDocument>())
        {
            foreach (var token in document.Placeholders ?? [])
            {
                if (token.Coefficient is null) continue;
                if (!string.Equals(token.Attribute, "PrimaryAttribute", StringComparison.Ordinal)) continue;

                return (document.Id, token.Attribute!);
            }
        }

        throw new InvalidOperationException(
            "No ability carries a PrimaryAttribute coefficient, so nothing reads the hero's stat.");
    }

    [Fact]
    public async Task An_ability_prints_the_stat_the_hero_names()
    {
        var (id, _) = Scaling();
        var hero = RouteTests.Factory.Store.Vocabulary.Heroes[0];

        var drawn = await client.GetStringAsync(
            $"/api/ability/{id}/tooltip?hero={Uri.EscapeDataString(hero.Name)}",
            Xunit.TestContext.Current.CancellationToken);

        Assert.Contains(hero.PrimaryStat, drawn, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_hero_and_one_primary_read_the_same_magnitude()
    {
        var (id, _) = Scaling();
        var hero = RouteTests.Factory.Store.Vocabulary.Heroes[0];
        var stat = hero.PrimaryStat;

        var named = await client.GetStringAsync(
            $"/api/ability/{id}/tooltip?hero={Uri.EscapeDataString(hero.Name)}&{stat}=1000",
            Xunit.TestContext.Current.CancellationToken);

        var supplied = await client.GetStringAsync(
            $"/api/ability/{id}/tooltip?{stat}=1000", Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(named, supplied);
    }

    [Fact]
    public async Task Several_primaries_at_once_take_the_hero_the_request_names()
    {
        var (id, _) = Scaling();

        var hero = RouteTests.Factory.Store.Vocabulary.Heroes
            .First(candidate => string.Equals(candidate.PrimaryStat, "Strength", StringComparison.Ordinal));

        var both = await client.GetStringAsync(
            $"/api/ability/{id}/tooltip?hero={Uri.EscapeDataString(hero.Name)}&Strength=1000&Intellect=4000",
            Xunit.TestContext.Current.CancellationToken);

        var strengthOnly = await client.GetStringAsync(
            $"/api/ability/{id}/tooltip?hero={Uri.EscapeDataString(hero.Name)}&Strength=1000",
            Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(strengthOnly, both);
    }

    [Fact]
    public async Task An_ability_takes_the_hero_and_nothing_else_the_item_takes()
    {
        var (id, _) = Scaling();

        var response = await client.GetAsync(
            $"/api/ability/{id}/tooltip?itemLevel=200", Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

```powershell
dotnet test src/FellowshipExtractor.Api.Tests --nologo --filter "FullyQualifiedName~AbilityScalingTests"
```

Expected: FAIL, 404 on every address.

- [ ] **Step 3: Write the four files for ability**

`Endpoints/Ability/AbilityGroup.cs` follows `ItemGroup` with `EntityType.Ability`.

`Endpoints/Ability/AbilityRecordEndpoint.cs`:

```csharp
namespace FellowshipExtractor.Api.Endpoints;

public sealed record AbilityRecordRequest
{
    public int Id { get; init; }
}

public static class AbilityRecordEndpoint
{
    public static RouteHandlerBuilder MapAbilityRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetAbility")
            .WithSummary($"One {PublicTypes.Noun(EntityType.Ability)}'s record.")
            .Produces<PublicEntity>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] AbilityRecordRequest request, IEntityStoreService store, ICodexViewService view) =>
        Records.One(EntityType.Ability, request.Id, null, store, view);
}
```

`Endpoints/Ability/AbilityTooltipEndpoint.cs`:

```csharp
using System.ComponentModel;

namespace FellowshipExtractor.Api.Endpoints;

public sealed record AbilityTooltipRequest
{
    public int Id { get; init; }

    [Description("The hero whose stat a PrimaryAttribute coefficient scales off.")]
    public string? Hero { get; init; }

    public TooltipQuery ToQuery(IQueryCollection raw, StatParameters stats, out string? error) =>
        StatQuery.Read(this, raw, stats, out error) with { Hero = StatQuery.Given(Hero) };
}

public static class AbilityTooltipEndpoint
{
    public static RouteHandlerBuilder MapAbilityTooltip(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}/tooltip", Handle)
            .WithName("GetAbilityTooltip")
            .WithSummary($"One {PublicTypes.Noun(EntityType.Ability)}'s tooltip as a self-contained HTML fragment.")
            .WithStats()
            .Produces<string>(StatusCodes.Status200OK, "text/html")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static Task<IResult> Handle(
        [AsParameters] AbilityTooltipRequest request,
        HttpContext http,
        ITooltipService tooltips,
        IEntityStoreService store)
    {
        var query = request.ToQuery(http.Request.Query, store.Stats, out var error);

        return error is null
            ? tooltips.Get(EntityType.Ability, request.Id, query)
            : Task.FromResult(StatQuery.Unreadable(error, store.Stats));
    }
}
```

The two magnitude tests compare whole markups. If they differ, the difference is what to read: the
only thing a hero changes on an ability card is which attribute a `PrimaryAttribute` coefficient
reads, so narrow both assertions to the substituted magnitude by matching
`<span class="sub"` and comparing the captured text, and keep the rest of the test as written.

- [ ] **Step 4: Write the same three for weapon power**

Copy the ability trio into `Endpoints/WeaponPower/`, replacing `Ability` with `WeaponPower` in the
class names, the request record names, the method names, the `WithName` values and the `EntityType`
member. `WeaponPowerRecordRequest` carries `Id` alone, and `WeaponPowerTooltipRequest` carries `Id`
and `Hero` with the same description, because the 10 legendary weapon powers are equippable by the
whole roster and name no hero's stat outright.

- [ ] **Step 5: Map them and retire the colliding route**

Add `app.MapAbility();` and `app.MapWeaponPower();` to `MapApiEndpoints`. Delete the
`app.MapGet("/api/ability/{abilityId:int}", ...)` block from `Program.cs`.

- [ ] **Step 6: Run it and watch it pass**

```powershell
dotnet test src/FellowshipExtractor.Api.Tests --nologo --filter "FullyQualifiedName~AbilityScalingTests"
```

Expected: 4 passed.

- [ ] **Step 7: Run the whole suite and commit**

```powershell
dotnet test src/FellowshipExtractor.Api.Tests --nologo -v q
```

```bash
git add -A
git commit -m "Declare hero on the two kinds of card whose stat it names"
```

---

### Task 9: The talent slot, blessing, trait, gem and currency endpoints

**Files:**
- Create: three files each under `Endpoints/TalentSlot/`, `Endpoints/Blessing/`, `Endpoints/Trait/`,
  `Endpoints/Gem/`, `Endpoints/Currency/`
- Modify: `Endpoints/ApiEndpoints.cs`
- Test: `src/FellowshipExtractor.Api.Tests/RouteTests.cs`

**Interfaces:**
- Produces: `MapTalentSlot()`, `MapBlessing()`, `MapTrait()`, `MapGem()`, `MapCurrency()`.
  `TalentSlotRecordRequest` and `TalentSlotTooltipRequest` carry `Id` and `Hero`.
  `BlessingTooltipRequest` carries `Id`, `State`, `EquippedTier`. `TraitTooltipRequest` carries
  `Id`, `State`. `GemTooltipRequest` and `CurrencyTooltipRequest` carry `Id`, `Rarity`. The record
  request for blessing, trait, gem and currency carries `Id` alone.

- [ ] **Step 1: Write the failing test**

Add to `RouteTests`:

```csharp
    [Fact]
    public async Task A_state_reaches_the_card_that_draws_one()
    {
        var trait = Factory.Store.Entities.OfType<TraitDocument>().First(row => row.Icon is not null);

        var drawn = await client.GetStringAsync(
            $"/api/trait/{trait.Id}/tooltip?state=unavailable", Xunit.TestContext.Current.CancellationToken);

        Assert.Contains(chrome.TraitNode("unavailable"), drawn, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_talent_slot_reads_the_hero_its_id_needs()
    {
        var (id, hero) = Sample(EntityType.TalentSlot);

        var bare = await client.GetAsync($"/api/talent-slot/{id}", Xunit.TestContext.Current.CancellationToken);
        var named = await client.GetAsync(
            $"/api/talent-slot/{id}?hero={hero}", Xunit.TestContext.Current.CancellationToken);

        named.EnsureSuccessStatusCode();

        var expected = Factory.Store.All(EntityType.TalentSlot, id).Count > 1
            ? System.Net.HttpStatusCode.BadRequest
            : System.Net.HttpStatusCode.OK;

        Assert.Equal(expected, bare.StatusCode);
    }

    [Fact]
    public async Task A_rarity_reaches_a_gem_and_nothing_reaches_it_that_belongs_to_an_item()
    {
        var (id, _) = Sample(EntityType.Gem);

        var rung = await client.GetAsync(
            $"/api/gem/{id}/tooltip?rarity=1", Xunit.TestContext.Current.CancellationToken);
        var level = await client.GetAsync(
            $"/api/gem/{id}/tooltip?itemLevel=100", Xunit.TestContext.Current.CancellationToken);

        rung.EnsureSuccessStatusCode();

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, level.StatusCode);
    }
```

- [ ] **Step 2: Run it and watch it fail**

```powershell
dotnet test src/FellowshipExtractor.Api.Tests --nologo --filter "FullyQualifiedName~A_state_reaches_the_card|FullyQualifiedName~A_talent_slot_reads|FullyQualifiedName~A_rarity_reaches_a_gem"
```

Expected: FAIL, 404 on every address.

- [ ] **Step 3: Write the five trios**

Each group file follows `ItemGroup` with its own `EntityType` member. Each record endpoint follows
`AbilityRecordEndpoint`, except talent slot, whose request carries `Hero` and whose handler passes
`request.Hero` to `Records.One`. Each tooltip endpoint follows `AbilityTooltipEndpoint` with its own
request model:

```csharp
public sealed record TalentSlotTooltipRequest
{
    public int Id { get; init; }

    [Description("The hero whose reading of the id to draw, where one id reads differently per hero.")]
    public string? Hero { get; init; }

    public TooltipQuery ToQuery(IQueryCollection raw, StatParameters stats, out string? error) =>
        StatQuery.Read(this, raw, stats, out error) with { Hero = StatQuery.Given(Hero) };
}

public sealed record BlessingTooltipRequest
{
    public int Id { get; init; }

    [Description("The state to draw the row at: allocated or unallocated.")]
    public string? State { get; init; }

    [Description("Which of a blessing's tiers is equipped. None given reads as none equipped.")]
    public int? EquippedTier { get; init; }

    public TooltipQuery ToQuery(IQueryCollection raw, StatParameters stats, out string? error) =>
        StatQuery.Read(this, raw, stats, out error) with
        {
            State = StatQuery.Given(State),
            EquippedTier = EquippedTier,
        };
}

public sealed record TraitTooltipRequest
{
    public int Id { get; init; }

    [Description("The state to draw the node at: selected, available or unavailable.")]
    public string? State { get; init; }

    public TooltipQuery ToQuery(IQueryCollection raw, StatParameters stats, out string? error) =>
        StatQuery.Read(this, raw, stats, out error) with { State = StatQuery.Given(State) };
}

public sealed record GemTooltipRequest
{
    public int Id { get; init; }

    [Description("The rung to read at, by the name the game prints or by tier.")]
    public string? Rarity { get; init; }

    public TooltipQuery ToQuery(IQueryCollection raw, StatParameters stats, out string? error) =>
        StatQuery.Read(this, raw, stats, out error) with { Rarity = StatQuery.Given(Rarity) };
}
```

`CurrencyTooltipRequest` is `GemTooltipRequest` with its own name.

- [ ] **Step 4: Map them**

Add `app.MapTalentSlot();`, `app.MapBlessing();`, `app.MapTrait();`, `app.MapGem();` and
`app.MapCurrency();` to `MapApiEndpoints`.

- [ ] **Step 5: Run it and watch it pass**

```powershell
dotnet test src/FellowshipExtractor.Api.Tests --nologo --filter "FullyQualifiedName~A_state_reaches_the_card|FullyQualifiedName~A_talent_slot_reads|FullyQualifiedName~A_rarity_reaches_a_gem"
```

Expected: 3 passed.

- [ ] **Step 6: Run the whole suite and commit**

```powershell
dotnet test src/FellowshipExtractor.Api.Tests --nologo -v q
```

```bash
git add -A
git commit -m "Give the state and the rung to the cards that read them"
```

---

### Task 10: The remaining fourteen types

Effect, CompanionPet, Creature, GemTrait, GemType, LegendaryPower, LegendaryStat, Mount, MountPower,
RelicPower, SetBonus, SetBonusPower, Talent and TraitPower each take `Id` alone on both routes.

**Files:**
- Create: three files each under fourteen folders under `Endpoints/`
- Modify: `Endpoints/ApiEndpoints.cs`
- Test: `src/FellowshipExtractor.Api.Tests/EndpointTests.cs`

**Interfaces:**
- Produces: `Map<Member>()` for each of the fourteen, and the durable guarantee that a type added
  later cannot ship without its routes.

- [ ] **Step 1: Write the failing test**

Create `src/FellowshipExtractor.Api.Tests/EndpointTests.cs`:

```csharp
using Microsoft.AspNetCore.Routing;

namespace FellowshipExtractor.Api.Tests;

public sealed class EndpointTests : IClassFixture<RouteTests.Factory>
{
    private readonly IReadOnlyList<RouteEndpoint> endpoints;

    public EndpointTests(RouteTests.Factory factory) =>
        endpoints = [.. factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()];

    [Fact]
    public void Every_entity_type_registers_a_record_route_and_a_tooltip_route()
    {
        var named = endpoints
            .Select(endpoint => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

        var missing = new List<string>();

        foreach (var type in Enum.GetValues<EntityType>())
        {
            if (!named.Contains($"Get{type}")) missing.Add($"Get{type}");
            if (!named.Contains($"Get{type}Tooltip")) missing.Add($"Get{type}Tooltip");
        }

        Assert.True(missing.Count == 0, "No endpoint file names " + string.Join(", ", missing) + ".");
    }

    [Fact]
    public void Every_entity_type_is_addressed_by_a_literal_path_naming_it()
    {
        var patterns = endpoints
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var type in Enum.GetValues<EntityType>())
        {
            var slug = EntityTypes.Slug(type);

            Assert.Contains($"/api/{slug}/{{id:int}}", patterns);
            Assert.Contains($"/api/{slug}/{{id:int}}/tooltip", patterns);
        }
    }
}
```

If the second test fails because `RawText` reads as the group relative `/{id:int}` rather than the
combined path, replace its body with a walk over `endpoint.DisplayName`, which carries the full
template. Print one `RawText` first to see which it is.

- [ ] **Step 2: Run it and watch it fail**

```powershell
dotnet test src/FellowshipExtractor.Api.Tests --nologo --filter "FullyQualifiedName~EndpointTests"
```

Expected: FAIL, naming the fourteen types with no routes.

- [ ] **Step 3: Scaffold the fourteen**

Write this to the scratchpad and run it from the repository root. It reads the member list out of
`EntityType.cs`, so the list is never copied.

```powershell
$root = "src/FellowshipExtractor.Api/Endpoints"
$source = Get-Content "src/Fellowship.SDK/Models/EntityType.cs" -Raw
$block = [regex]::Match($source, '(?s)public enum EntityType\s*\{(.*?)\}').Groups[1].Value

$members = $block -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ -match '^[A-Za-z]+$' }
$already = @('Item', 'Ability', 'WeaponPower', 'TalentSlot', 'Blessing', 'Trait', 'Gem', 'Currency')

foreach ($member in $members) {
    if ($already -contains $member) { continue }

    $slug = ($member -creplace '(?<!^)([A-Z])', '-$1').ToLowerInvariant()
    $folder = Join-Path $root $member
    New-Item -ItemType Directory -Force -Path $folder | Out-Null

    @"
using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public static class ${member}Group
{
    public static RouteGroupBuilder Map$member(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/$slug").WithTags(PublicTypes.Label(EntityType.$member));

        group.Map${member}Record();
        group.Map${member}Tooltip();

        return group;
    }
}
"@ | Set-Content -Path (Join-Path $folder "${member}Group.cs") -Encoding utf8NoBOM

    @"
using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public sealed record ${member}RecordRequest
{
    public int Id { get; init; }
}

public static class ${member}RecordEndpoint
{
    public static RouteHandlerBuilder Map${member}Record(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("Get$member")
            .WithSummary(`$"One {PublicTypes.Noun(EntityType.$member)}'s record.")
            .Produces<PublicEntity>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] ${member}RecordRequest request, IEntityStoreService store, ICodexViewService view) =>
        Records.One(EntityType.$member, request.Id, null, store, view);
}
"@ | Set-Content -Path (Join-Path $folder "${member}RecordEndpoint.cs") -Encoding utf8NoBOM

    @"
using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public sealed record ${member}TooltipRequest
{
    public int Id { get; init; }

    public TooltipQuery ToQuery(IQueryCollection raw, StatParameters stats, out string? error) =>
        StatQuery.Read(this, raw, stats, out error);
}

public static class ${member}TooltipEndpoint
{
    public static RouteHandlerBuilder Map${member}Tooltip(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}/tooltip", Handle)
            .WithName("Get${member}Tooltip")
            .WithSummary(`$"One {PublicTypes.Noun(EntityType.$member)}'s tooltip as a self-contained HTML fragment.")
            .WithStats()
            .Produces<string>(StatusCodes.Status200OK, "text/html")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static Task<IResult> Handle(
        [AsParameters] ${member}TooltipRequest request,
        HttpContext http,
        ITooltipService tooltips,
        IEntityStoreService store)
    {
        var query = request.ToQuery(http.Request.Query, store.Stats, out var error);

        return error is null
            ? tooltips.Get(EntityType.$member, request.Id, query)
            : Task.FromResult(StatQuery.Unreadable(error, store.Stats));
    }
}
"@ | Set-Content -Path (Join-Path $folder "${member}TooltipEndpoint.cs") -Encoding utf8NoBOM
}

Get-ChildItem $root -Directory | Select-Object -ExpandProperty Name
```

Expected: 22 folder names. Read three of the generated files before going on, checking the slug, the
`EntityType` member and that no stray backtick survived the here-strings.

- [ ] **Step 4: Map all twenty two**

Rewrite `MapApiEndpoints` so it names each group once, in the enum's own order:

```csharp
    public static void MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapAbility();
        app.MapBlessing();
        app.MapCompanionPet();
        app.MapCreature();
        app.MapCurrency();
        app.MapEffect();
        app.MapGem();
        app.MapGemTrait();
        app.MapGemType();
        app.MapItem();
        app.MapLegendaryPower();
        app.MapLegendaryStat();
        app.MapMount();
        app.MapMountPower();
        app.MapRelicPower();
        app.MapSetBonus();
        app.MapSetBonusPower();
        app.MapTalent();
        app.MapTalentSlot();
        app.MapTrait();
        app.MapTraitPower();
        app.MapWeaponPower();
    }
```

- [ ] **Step 5: Run it and watch it pass**

```powershell
dotnet build src/FellowshipExtractor.slnx
dotnet test src/FellowshipExtractor.Api.Tests --nologo --filter "FullyQualifiedName~EndpointTests"
```

Expected: 2 passed.

- [ ] **Step 6: Run the whole suite and commit**

```powershell
dotnet test src/FellowshipExtractor.Api.Tests --nologo -v q
```

```bash
git add -A
git commit -m "Give every remaining type a folder and two routes"
```

---

### Task 11: The browsing endpoints

**Files:**
- Create: `Endpoints/Browsing/BrowsingGroup.cs`, `SearchEndpoint.cs`, `EntitiesEndpoint.cs`,
  `CatalogEndpoint.cs`, `Endpoints/Documentation/BrowsingDocument.cs`
- Modify: `Models/CodexQuery.cs` (`Parse` takes the declared names, `Parameters` goes),
  `Endpoints/ApiEndpoints.cs`, `Program.cs` (delete the three browsing routes and `PublicRoutes`)

**Interfaces:**
- Produces: `MapSearch()`, `MapEntities()`, `MapCatalog()`, `SearchRequest` (`Q`, `Type`, `Limit`),
  `EntitiesRequest` (`Q`, `Type`, `Sort`, `Dir`, `Group`, `Page`, `Size`) with
  `ToQuery(IQueryCollection raw, ICodexService codex, out string? error)`.
  `CodexQuery.Parse(IQueryCollection query, ICodexService codex, IReadOnlySet<string> declared, out string? error)`.

- [ ] **Step 1: Write the failing test**

Add to `RouteTests`:

```csharp
    [Fact]
    public async Task A_type_naming_no_entity_type_is_refused_by_search()
    {
        var response = await client.GetAsync(
            "/api/search?q=boot&type=no-such-type", Xunit.TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(PublicTypes.NoSuchType("no-such-type"), body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_three_cross_type_routes_keep_their_paths()
    {
        foreach (var address in new[] { "/api/search?q=boot", "/api/entities?size=1", "/api/catalog" })
        {
            var response = await client.GetAsync(address, Xunit.TestContext.Current.CancellationToken);

            Assert.True(response.IsSuccessStatusCode, $"{address} answered {(int)response.StatusCode}.");
        }
    }
```

- [ ] **Step 2: Run it and watch it fail**

The second test passes against the old routes. Run it after Step 3 deletes them.

```powershell
dotnet test src/FellowshipExtractor.Api.Tests --nologo --filter "FullyQualifiedName~A_type_naming_no_entity_type"
```

Expected: PASS today, and it is the assertion that must keep holding once `PublicRoutes` is gone.

- [ ] **Step 3: Write the four files**

`Endpoints/Browsing/BrowsingGroup.cs`:

```csharp
namespace FellowshipExtractor.Api.Endpoints;

public static class BrowsingGroup
{
    public const string Tag = "Browsing";

    public static RouteGroupBuilder MapBrowsing(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").WithTags(Tag);

        group.MapSearch();
        group.MapEntities();
        group.MapCatalog();

        return group;
    }
}
```

`Endpoints/Browsing/SearchEndpoint.cs`:

```csharp
using System.ComponentModel;

namespace FellowshipExtractor.Api.Endpoints;

public sealed record SearchRequest
{
    [Description("The text to match. An empty one answers no results.")]
    public string? Q { get; init; }

    [Description("One entity type slug, to narrow the results to it.")]
    public string? Type { get; init; }

    [Description("How many results to answer, 1 to 100. Defaults to 10.")]
    public int? Limit { get; init; }
}

public static class SearchEndpoint
{
    public static RouteHandlerBuilder MapSearch(this IEndpointRouteBuilder group) =>
        group.MapGet("/search", Handle)
            .WithName("Search")
            .WithSummary("Ranked name matches, across every type or the one the type= slug names.")
            .Produces<SearchResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

    private static IResult Handle([AsParameters] SearchRequest request, ISuggestionService suggestions)
    {
        var narrowed = StatQuery.Given(request.Type);

        if (narrowed is not null && !EntityTypes.TryParse(narrowed, out _))
        {
            return Results.Problem(
                title: "No such type.",
                detail: PublicTypes.NoSuchType(narrowed),
                statusCode: StatusCodes.Status400BadRequest);
        }

        var limit = SuggestionService.Limit(request.Limit?.ToString(
            System.Globalization.CultureInfo.InvariantCulture));

        return Results.Ok(suggestions.For(request.Q ?? string.Empty, narrowed, limit));
    }
}
```

The `Limit` description repeats the bounds `SuggestionService` states. Build it from them so the two
cannot drift: give `SuggestionService` nothing new and write the attribute text as
`$"How many results to answer, 1 to {SuggestionService.MaximumLimit}. Defaults to {SuggestionService.DefaultLimit}."`
only if the executor confirms `[Description]` accepts a constant interpolation. It does not, because
an attribute argument must be a constant expression, so leave the literal and add to
`ApiDocumentTests` an assertion that the two numbers the sentence names are those constants.

`Endpoints/Browsing/EntitiesEndpoint.cs`:

```csharp
using System.ComponentModel;

namespace FellowshipExtractor.Api.Endpoints;

public sealed record EntitiesRequest
{
    [Description("Free text matched against every name.")]
    public string? Q { get; init; }

    [Description("Entity type slug, comma separated for several.")]
    public string? Type { get; init; }

    [Description("The column to order by. Defaults to name.")]
    public string? Sort { get; init; }

    [Description("Given as desc to order the other way.")]
    public string? Dir { get; init; }

    [Description("The facet to group the page by.")]
    public string? Group { get; init; }

    [Description("Which page to answer, from 1.")]
    public int? Page { get; init; }

    [Description("How many rows a page holds, up to 200. Defaults to 50.")]
    public int? Size { get; init; }

    public CodexQuery ToQuery(IQueryCollection raw, ICodexService codex, out string? error) =>
        CodexQuery.Parse(raw, codex, RequestProperties.Of(typeof(EntitiesRequest)), out error);
}

public static class EntitiesEndpoint
{
    public static RouteHandlerBuilder MapEntities(this IEndpointRouteBuilder group) =>
        group.MapGet("/entities", Handle)
            .WithName("GetEntities")
            .WithSummary("Every entity a filter leaves, paged.")
            .Produces<EntitiesResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

    private static IResult Handle(
        [AsParameters] EntitiesRequest request,
        HttpContext http,
        ICodexService codex,
        ICodexSearchService search,
        ICodexViewService view)
    {
        var query = request.ToQuery(http.Request.Query, codex, out var error);

        if (error is not null)
        {
            return Results.Problem(
                title: "The filter could not be read.", detail: error, statusCode: StatusCodes.Status400BadRequest);
        }

        var results = search.Run(query);

        return Results.Ok(new EntitiesResponse(
            results.Total,
            results.Page,
            results.Pages,
            [.. results.Rows.Select(row => SearchResult.From(row.Shown, view))]));
    }
}
```

`Endpoints/Browsing/CatalogEndpoint.cs` holds the body of the old `/api/catalog` handler, taking
`ICodexService codex, IEntityStoreService store`, named `GetCatalog`, with no request model.

- [ ] **Step 4: Let `CodexQuery.Parse` take the declared names**

In `Models/CodexQuery.cs`, delete the `Parameters` list and change the signature and the two places
that read it:

```csharp
    public static CodexQuery Parse(
        IQueryCollection query, ICodexService codex, IReadOnlySet<string> declared, out string? error)
```

`Declared(key)` becomes `declared.Contains(key) ? key.ToLowerInvariant() : null`, and the switch keeps
its `default:` throw, which now fires when `EntitiesRequest` grows a property nothing reads. The error
message's parameter list reads `declared` instead of `Parameters.Select(p => p.Name)`. `MaximumPageSize`
becomes `public const int MaximumPageSize = 200;` so a test can name it.

- [ ] **Step 5: Write the browsing operation transformer**

Create `src/FellowshipExtractor.Api/Endpoints/Documentation/BrowsingDocument.cs` holding
`BrowsingParameters : IOpenApiOperationTransformer`, taking `ICodexService codex`. It acts only on
the operation whose endpoint name is `GetEntities`, adding one query parameter per
`codex.Facets` entry (string, `$"{facet.Label}, comma separated for several."`), one per
`codex.Flags` entry (boolean, `flag.Label + "."`), and appending the measures sentence the old
`ApiParameters.Note` wrote for `ApiQuerySet.Catalogue`. Register it beside the other transformers in
`Program.cs`.

- [ ] **Step 6: Map them and delete the old routes**

Add `app.MapBrowsing();` to `MapApiEndpoints`. Delete `app.MapGet("/api/search", ...)`,
`app.MapGet("/api/entities", ...)`, `app.MapGet("/api/catalog", ...)` and the whole
`public static class PublicRoutes` from `Program.cs`. `PublicTypes.NoSuchType` keeps its callers in
`NotFound.razor`, `CodexQuery` and now `SearchEndpoint`.

- [ ] **Step 7: Run it and watch it pass**

```powershell
dotnet build src/FellowshipExtractor.slnx
dotnet test src/FellowshipExtractor.Api.Tests --nologo --filter "FullyQualifiedName~A_type_naming_no_entity_type|FullyQualifiedName~The_three_cross_type_routes|FullyQualifiedName~CodexTests"
```

Expected: all passed.

- [ ] **Step 8: Run the whole suite and commit**

```powershell
dotnet test src/FellowshipExtractor.Api.Tests --nologo -v q
```

```bash
git add -A
git commit -m "Give the three cross type routes files of their own"
```

---

### Task 12: The cutover

The two routes that took a slug go, `Tip` moves to the new address, and the parser that read the
whole query goes with them.

**Files:**
- Modify: `Program.cs`, `Models/CodexView.cs` and `Services/CodexViewService.cs` (`Tip`),
  `Components/Pages/EntityPage.razor` (`Address`), `Models/TooltipQuery.cs` (`Parse`, `Parameters`),
  `ApiDocument.cs` (`ApiQuerySet`, `ApiParameter`, `WithQuery`, `ApiParameters`),
  `Services/SuggestionService.cs` (`Parameters`), `Services/TooltipService.cs` (the query overload
  becomes the only one)
- Modify: `RouteTests.cs`, `TooltipQueryTests.cs`, `BlessingCardTests.cs`, `StateTests.cs`,
  `TooltipTests.cs`, `Sections/BorrowedTests.cs`, `Sections/PageSectionTests.cs`,
  `Sections/ParticularsTests.cs`, `ScopedTests.cs`, `Cx/FactTests.cs`, `Cx/NoteTests.cs`

- [ ] **Step 1: Write the failing test**

Replace the two walking tests in `RouteTests` and the two address tests:

```csharp
    [Fact]
    public async Task Both_routes_answer_for_one_id_of_every_type()
    {
        foreach (var type in Enum.GetValues<EntityType>())
        {
            var (id, hero) = Sample(type);
            var slug = EntityTypes.Slug(type);
            var wearer = hero is null ? string.Empty : $"?hero={hero}";

            foreach (var address in new[] { $"/api/{slug}/{id}{wearer}", $"/api/{slug}/{id}/tooltip{wearer}" })
            {
                var response = await client.GetAsync(address, Xunit.TestContext.Current.CancellationToken);
                var body = await response.Content.ReadAsStringAsync(Xunit.TestContext.Current.CancellationToken);

                Assert.True(
                    response.StatusCode == HttpStatusCode.OK,
                    $"{address} answered {(int)response.StatusCode}, not 200. Body: {body}");
            }
        }
    }

    [Fact]
    public async Task A_record_answers_the_type_the_slug_asked_for()
    {
        foreach (var type in Enum.GetValues<EntityType>())
        {
            var (id, hero) = Sample(type);
            var slug = EntityTypes.Slug(type);
            var wearer = hero is null ? string.Empty : $"?hero={hero}";

            var body = await client.GetStringAsync(
                $"/api/{slug}/{id}{wearer}", Xunit.TestContext.Current.CancellationToken);

            using var reading = JsonDocument.Parse(body);

            Assert.Equal(slug, reading.RootElement.GetProperty("type").GetString());
        }
    }

    [Fact]
    public async Task An_address_naming_no_type_is_a_routing_404()
    {
        var response = await client.GetAsync("/api/no-such-type/1", Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task An_entity_page_carries_the_hero_into_its_refetch_address()
    {
        var (id, hero) = Shared();
        var slug = EntityTypes.Slug(EntityType.Item);
        var wearer = Uri.EscapeDataString(hero);

        var page = await client.GetStringAsync(
            $"/entity/{slug}/{id}?hero={wearer}", Xunit.TestContext.Current.CancellationToken);

        Assert.Contains(
            $"data-stage=\"/api/{slug}/{id}/tooltip?hero={wearer}\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_entity_page_carries_the_state_into_the_refetch_address_of_a_card_that_draws_one()
    {
        var trait = Factory.Store.Entities.OfType<TraitDocument>().First(row => row.Icon is not null);
        var slug = EntityTypes.Slug(EntityType.Trait);

        var page = await client.GetStringAsync(
            $"/entity/{slug}/{trait.Id}?state=unavailable", Xunit.TestContext.Current.CancellationToken);

        Assert.Contains(
            $"data-stage=\"/api/{slug}/{trait.Id}/tooltip?state=unavailable\"", page, StringComparison.Ordinal);
    }
```

- [ ] **Step 2: Run them and watch them fail**

```powershell
dotnet test src/FellowshipExtractor.Api.Tests --nologo --filter "FullyQualifiedName~RouteTests"
```

Expected: FAIL on the two address tests, which still read `/api/tooltip/...`.

- [ ] **Step 3: Move `Tip` and the page address**

In `CodexViewService`:

```csharp
    public string Tip(EntityDocument document)
    {
        var where = $"/api/{EntityTypes.Slug(EntityTypes.TypeOf(document))}/" +
            document.Id.ToString(CultureInfo.InvariantCulture) + "/tooltip";

        return Reading(document) is { } hero ? $"{where}?hero={Uri.EscapeDataString(hero)}" : where;
    }
```

In `EntityPage.razor`, inject `ITooltipViewService Cards` and read:

```csharp
    private string Address
    {
        get
        {
            var where = Ui.Tip(Shown);

            return Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(
                where,
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["hero"] = where.Contains('?') ? null : Drawn,
                    ["state"] = Cards.DrawsState(Shown) && !string.IsNullOrWhiteSpace(State) ? State : null,
                });
        }
    }
```

- [ ] **Step 4: Delete the two slug routes and the old parser**

From `Program.cs`, delete `app.MapGet("/api/tooltip/{slug}/{id:int}", ...)` and
`app.MapGet("/api/entity/{slug}/{id:int}", ...)`. `Program.cs` now builds the host, registers the
services, sets up the middleware, maps the Razor components and calls `app.MapApiEndpoints()`, and
nothing else. `public partial class Program;` stays where it is, so `WebApplicationFactory<Program>`
still finds it.

From `Models/TooltipQuery.cs`, delete `Parse`, `Parameters`, `Reserved`, `Given`, `TryInt`,
`TryDecimal`, `LevelCeiling` and `StatCeiling`. The record keeps its properties, `Total`,
`Attribute`, `Haste`, `Expertise`, `GemPower`, `ImbuedTrait`, `ImbuedTraits` and `GemPowers`.

From `ApiDocument.cs`, delete `ApiValue`, `ApiQuerySet`, `ApiParameter`, `WithQuery` and the whole
`ApiParameters` transformer, and remove `options.AddOperationTransformer<ApiParameters>()` and the
`Tooltips`, `Records` and `Browsing` constants. `ApiDocument` keeps `Name`, `Route`, `Title`, `Ui`,
`Describe` and `ApiInfo`.

From `Services/SuggestionService.cs`, delete `Parameters`.

From `Services/TooltipService.cs`, delete the `IQueryCollection` overload from both the interface and
the class.

- [ ] **Step 5: Move the tests that named the old shapes**

`TooltipQueryTests` becomes a walk over the request models rather than over `Parse`. A declared
property reaches `ToQuery` through the model, and only a stat reaches it through `raw`, so each case
sets the property it is about and passes `raw` for the stats alone. Replace the file's body with:

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class TooltipQueryTests(Export export)
{
    private static IQueryCollection Raw(params (string Key, string[] Values)[] asked) =>
        new QueryCollection(asked.ToDictionary(a => a.Key, a => new StringValues(a.Values)));

    [Fact]
    public void An_item_reads_the_armor_the_caller_states()
    {
        var query = new ItemTooltipRequest { Id = 1, Armor = 303m }
            .ToQuery(Raw(), export.Store.Stats, out var error);

        Assert.Null(error);
        Assert.Equal(303m, query.Armor);
    }

    [Fact]
    public void An_item_reads_each_imbued_trait_and_the_rank_it_sits_at()
    {
        Assert.True(ImbuedTraits.TryParse("12:2,7", out var traits));

        var query = new ItemTooltipRequest { Id = 1, Trait = traits, MaxTraits = 5 }
            .ToQuery(Raw(), export.Store.Stats, out _);

        Assert.Equal([new ImbuedTrait(12, 2), new ImbuedTrait(7, 1)], query.Traits);
        Assert.Equal(5, query.MaxTraits);
        Assert.Equal(2, query.AllocatedTraits);
    }

    [Fact]
    public void An_item_counts_no_traits_when_the_caller_names_none()
    {
        var query = new ItemTooltipRequest { Id = 1 }.ToQuery(Raw(), export.Store.Stats, out _);

        Assert.Null(query.AllocatedTraits);
        Assert.Empty(query.Traits);
        Assert.Empty(query.GemPowers);
        Assert.False(query.NoSocket);
    }

    [Fact]
    public void An_item_keeps_armor_for_the_printed_line_and_still_reads_a_stat()
    {
        var query = new ItemTooltipRequest { Id = 1, Armor = 303m }
            .ToQuery(Raw(("stamina", ["36"])), export.Store.Stats, out var error);

        Assert.Null(error);
        Assert.Equal(303m, query.Armor);
        Assert.Equal(36m, query.Total("Stamina"));
    }

    [Fact]
    public void An_item_refuses_a_key_that_is_neither_its_property_nor_a_stat()
    {
        new ItemTooltipRequest { Id = 1 }.ToQuery(Raw(("nonesuch", ["1"])), export.Store.Stats, out var error);

        Assert.Equal("Unknown parameter 'nonesuch'.", error);
    }

    [Fact]
    public void An_ability_carries_the_hero_and_nothing_more()
    {
        var query = new AbilityTooltipRequest { Id = 1, Hero = "Barbarian" }
            .ToQuery(Raw(), export.Store.Stats, out var error);

        Assert.Null(error);
        Assert.Equal("Barbarian", query.Hero);
        Assert.Null(query.Rarity);
        Assert.Null(query.ItemLevel);
    }

    [Fact]
    public void An_ability_reads_a_blank_hero_as_none_given()
    {
        var query = new AbilityTooltipRequest { Id = 1, Hero = string.Empty }
            .ToQuery(Raw(), export.Store.Stats, out _);

        Assert.Null(query.Hero);
    }

    [Fact]
    public void A_blessing_carries_the_state_and_the_tier()
    {
        var query = new BlessingTooltipRequest { Id = 1, State = "allocated", EquippedTier = 2 }
            .ToQuery(Raw(), export.Store.Stats, out _);

        Assert.Equal("allocated", query.State);
        Assert.Equal(2, query.EquippedTier);
    }

    [Fact]
    public void A_trait_carries_the_state()
    {
        var query = new TraitTooltipRequest { Id = 1, State = "unavailable" }
            .ToQuery(Raw(), export.Store.Stats, out _);

        Assert.Equal("unavailable", query.State);
    }

    [Fact]
    public void A_gem_and_a_currency_carry_the_rung()
    {
        Assert.Equal(
            "3",
            new GemTooltipRequest { Id = 1, Rarity = "3" }.ToQuery(Raw(), export.Store.Stats, out _).Rarity);

        Assert.Equal(
            "3",
            new CurrencyTooltipRequest { Id = 1, Rarity = "3" }.ToQuery(Raw(), export.Store.Stats, out _).Rarity);
    }

    [Fact]
    public void A_talent_slot_carries_the_hero()
    {
        var query = new TalentSlotTooltipRequest { Id = 1, Hero = "Barbarian" }
            .ToQuery(Raw(), export.Store.Stats, out _);

        Assert.Equal("Barbarian", query.Hero);
    }

    [Fact]
    public void A_bare_type_carries_the_stats_and_nothing_else()
    {
        var query = new EffectTooltipRequest { Id = 1 }
            .ToQuery(Raw(("Stamina", ["40"])), export.Store.Stats, out var error);

        Assert.Null(error);
        Assert.Equal(40m, query.Total("Stamina"));
        Assert.Null(query.Hero);
    }
}
```

`BlessingCardTests:68`, `StateTests:134` and `TooltipTests:203` call `TooltipQuery.Parse`. Replace
each with the request model that endpoint binds, calling `ToQuery` with the same raw query.

The literal `/api/tooltip/{slug}/{id}` strings in `Sections/BorrowedTests.cs`,
`Sections/PageSectionTests.cs`, `Sections/ParticularsTests.cs`, `ScopedTests.cs`, `Cx/FactTests.cs`
and `Cx/NoteTests.cs` are parameters fed in rather than addresses produced, so they still pass. Move
them to `/api/{slug}/{id}/tooltip` so no test file states an address the app never writes.

- [ ] **Step 6: Build and run the whole suite**

```powershell
dotnet build src/FellowshipExtractor.slnx
dotnet test src/FellowshipExtractor.Api.Tests --nologo -v q
```

Expected: build clean, only the two `PlaceholderReadingTests` failures.

- [ ] **Step 7: Confirm the old shapes are gone**

```powershell
Select-String -Path src/FellowshipExtractor.Api -Include *.cs,*.razor -Recurse `
    -Pattern "ApiQuerySet|ApiParameter|WithQuery|PublicRoutes|TooltipQuery\.Parse|api/tooltip/"
```

Expected: no matches.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "Retire the routes that took a slug and the parser that read the whole query"
```

---

### Task 13: The document the endpoints publish

**Files:**
- Modify: `src/FellowshipExtractor.Api.Tests/ApiDocumentTests.cs`,
  `src/FellowshipExtractor.Api.Tests/ParameterTests.cs`,
  `src/FellowshipExtractor.Api.Tests/StatParameterTests.cs`

**Interfaces:**
- Consumes: `RouteTests.Factory`, `/openapi/v1.json`, `EndpointDataSource`.

- [ ] **Step 1: Write the failing document tests**

Add a second class to `ParameterTests.cs`, reading the published document through the factory. It
needs four usings the file does not carry yet: `System.Reflection`, `System.Text.Json`,
`Microsoft.AspNetCore.Routing` and `Microsoft.AspNetCore.Http` (already present).

```csharp
public sealed class PublishedParameterTests : IClassFixture<RouteTests.Factory>
{
    private readonly JsonDocument document;

    private readonly IReadOnlyList<RouteEndpoint> endpoints;

    public PublishedParameterTests(RouteTests.Factory factory)
    {
        var client = factory.CreateClient();

        document = JsonDocument.Parse(
            client.GetStringAsync(ApiDocument.Route).GetAwaiter().GetResult());

        endpoints =
            [.. factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()];
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
    public void The_item_alone_declares_the_rung_the_level_the_tempering_and_the_potential()
    {
        string[] owned = ["Rarity", "ItemLevel", "Upgrades", "MaxUpgrades", "PotentialPower"];

        foreach (var (path, operation) in document.RootElement.GetProperty("paths").EnumerateObject()
            .Where(entry => entry.Name.EndsWith("/tooltip", StringComparison.Ordinal))
            .Select(entry => (entry.Name, entry.Value.GetProperty("get"))))
        {
            var names = Parameters(operation).Select(parameter => parameter.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

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
                Assert.True(declared.Contains(name), $"{published} declares '{name}' and its model has no such property.");
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
```

Add to `StatParameterTests` the two cases that read through a live endpoint:

```csharp
public sealed class PublishedStatTests : IClassFixture<RouteTests.Factory>
{
    private readonly HttpClient client;

    public PublishedStatTests(RouteTests.Factory factory) => client = factory.CreateClient();

    [Fact]
    public async Task Every_stat_the_table_names_is_accepted_on_an_ability_tooltip()
    {
        var id = RouteTests.Factory.Store.Entities.First(row => row is AbilityDocument).Id;

        foreach (var stat in RouteTests.Factory.Store.Stats.Names)
        {
            var key = stat.Replace(" ", string.Empty, StringComparison.Ordinal);

            var response = await client.GetAsync(
                $"/api/ability/{id}/tooltip?{key}=10", Xunit.TestContext.Current.CancellationToken);

            Assert.True(response.IsSuccessStatusCode, $"?{key}=10 answered {(int)response.StatusCode}.");
        }
    }

    [Fact]
    public async Task A_name_the_table_does_not_carry_is_refused()
    {
        var id = RouteTests.Factory.Store.Entities.First(row => row is AbilityDocument).Id;

        var response = await client.GetAsync(
            $"/api/ability/{id}/tooltip?Nonesuch=10", Xunit.TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Nonesuch", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Armor_on_an_item_reads_as_the_printed_line()
    {
        var id = RouteTests.Factory.Store.Entities
            .OfType<ItemDocument>()
            .First(row => row.Stats is { Count: > 0 })
            .Id;

        var drawn = await client.GetStringAsync(
            $"/api/item/{id}/tooltip?armor=303", Xunit.TestContext.Current.CancellationToken);

        Assert.Contains("303", drawn, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run them and watch them fail**

```powershell
dotnet test src/FellowshipExtractor.Api.Tests --nologo --filter "FullyQualifiedName~PublishedParameterTests|FullyQualifiedName~PublishedStatTests"
```

Expected: FAIL where a real gap exists. Read each failure before changing anything: these tests
measure what Tasks 7 to 12 built, so a failure here names a bug in an endpoint file, not in the test.

- [ ] **Step 3: Rewrite `ApiDocumentTests` to read the document**

`ApiDocumentTests` no longer has `ApiParameter` lists to walk. Replace it with a class taking
`RouteTests.Factory`, reading `ApiDocument.Route`, and asserting:

- Every parameter in the document carries a description ending in `"."`.
- The `Limit` description on `/api/search` names `SuggestionService.MaximumLimit` and
  `SuggestionService.DefaultLimit`, and the `Size` description on `/api/entities` names
  `CodexQuery.MaximumPageSize` and `CodexQuery.DefaultPageSize`.
- The document's `info.description` names the entity count `ApiDocument.Describe` was given.
- Every tag on every operation is a `PublicTypes.Label` or `BrowsingGroup.Tag`.
- `The_search_limit_holds_between_the_bounds_the_document_states` keeps its five assertions against
  `SuggestionService.Limit`.

Keep `Every_filter_parameter_the_document_declares_is_read_rather_than_refused`, changing it to walk
the parameters the `/api/entities` operation publishes and to call
`CodexQuery.Parse(query, codex, RequestProperties.Of(typeof(EntitiesRequest)), out var error)`.

- [ ] **Step 4: Run the whole suite**

```powershell
dotnet test src/FellowshipExtractor.Api.Tests --nologo -v q
```

Expected: only the two `PlaceholderReadingTests` failures.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Read the published document rather than the lists that used to fill it"
```

---

### Task 14: Confirm the move drew nothing new

**Files:** none changed unless a difference is found.

- [ ] **Step 1: Draw the cards again**

```powershell
$env:FELLOWSHIP_TIP_DUMP = "$env:TEMP\fx-tip-after"
Remove-Item "$env:TEMP\fx-tip-after" -Recurse -Force -ErrorAction SilentlyContinue
dotnet test src/FellowshipExtractor.Api.Tests --nologo --filter "FullyQualifiedName~TooltipDumpTests"
$env:FELLOWSHIP_TIP_DUMP = $null
```

- [ ] **Step 2: Diff the two folders**

```powershell
$before = Get-ChildItem "$env:TEMP\fx-tip-before" -Filter *.html | Sort-Object Name
$after = Get-ChildItem "$env:TEMP\fx-tip-after" -Filter *.html | Sort-Object Name

"before: $($before.Count), after: $($after.Count)"
if ($before.Count -eq 0 -or $before.Count -ne $after.Count) { throw "The two runs drew different counts." }

Compare-Object $before.Name $after.Name

foreach ($file in $before) {
    $other = Join-Path "$env:TEMP\fx-tip-after" $file.Name
    if (-not (Test-Path $other)) { continue }

    $left = Get-FileHash $file.FullName -Algorithm SHA256
    $right = Get-FileHash $other -Algorithm SHA256

    if ($left.Hash -ne $right.Hash) { $file.Name }
}
```

Expected: no output from either. This move changes how a request is bound and where a route is
declared, not what a card draws, so an empty diff is what says the move preserved the rendering. Any
file that does differ is a defect to fix before finishing.

- [ ] **Step 3: Read the published document by hand**

Start the app in the shell tool's background mode with `FELLOWSHIP_EXPORT` pointing at
`output/export`, fetch `/openapi/v1.json`, and read the item and ability tooltip operations. The item
operation carries its twelve parameters, the ability operation carries `id` and `Hero`, and `Hero` is
the only name the two have in common. Stop the process.

- [ ] **Step 4: Run everything one last time**

```powershell
dotnet build src/FellowshipExtractor.slnx
dotnet test src/FellowshipExtractor.Api.Tests --nologo -v q
dotnet test src/FellowshipExtractor.Tests --nologo -v q
```

Expected: build clean with no warnings, the API suite failing only the two `PlaceholderReadingTests`
cases the stale export causes, and the extractor suite green.

- [ ] **Step 5: Confirm the shape on disk**

```powershell
(Get-ChildItem src/FellowshipExtractor.Api/Endpoints -Directory | Where-Object Name -ne 'Browsing' |
    Where-Object Name -ne 'Documentation').Count
(Get-ChildItem src/FellowshipExtractor.Api/Services -File).Count
Get-Content src/FellowshipExtractor.Api/Program.cs | Measure-Object -Line
```

Expected: 22 type folders, 15 service files, and a `Program.cs` that holds composition and nothing
else.

- [ ] **Step 6: Commit**

```bash
git commit --allow-empty -m "Confirm the move drew nothing new"
```
