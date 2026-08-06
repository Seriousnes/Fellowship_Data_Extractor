# Codex Rarity, Links and Variants Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A name and a link carry the colour the build gives their row and never a hero's, and a list
names an id once with its other names beside it, every count beneath the list counting ids.

**Architecture:** One colour table in `CodexView` serves the link, the heading and the showcase card:
`LinkColour` answers the colour the build states for a row and null otherwise, and `NameColour` is
`LinkColour` with the text default behind it. The lists fold their rows into a `Listing` record that
carries the reading a filter picked plus one icon link per other name the id reads under, and every
count downstream counts folded rows.

**Tech Stack:** .NET 10, C# 13, Blazor static server rendering with one `InteractiveServer`
component, xunit.v3 with bunit in `src/FellowshipExtractor.Api.Tests`, xunit 2 in
`src/FellowshipExtractor.Tests`, SCSS compiled to `wwwroot/css/codex.css`.

`docs/superpowers/specs/2026-08-06-codex-rarity-links-and-variants-design.md` is the authority for
every count and every rule this plan cites.

## Global Constraints

- .NET 10, C# 13. `net10.0` everywhere except `Fellowship.SDK.Generators`, which stays
  `netstandard2.0`.
- No polyfills for missing runtime functionality. Use the modern API directly.
- No em dashes or en dashes anywhere: code, comments, XML, output, commit messages.
- **Add no comments to any file.** Not explanatory ones, not section markers, not doc comments. The
  API project holds none today and none are wanted.
- Never hardcode a list, id or data fact a canonical source already states. `store.Vocabulary.Rarities`
  is the canonical rarity ladder, `EntityType` and `EntityTypes.Slug` the canonical type vocabulary,
  and `vocabulary.json` the canonical source for heroes, colours and print formats. No rung word,
  hero name or colour hex is spelled in this app's own code.
- Match the surrounding code's idiom: expression-bodied members, collection expressions, pattern
  matching, `StringComparer` named explicitly.
- Test naming: snake_case sentences in `FellowshipExtractor.Api.Tests`, PascalCase sentences in
  `FellowshipExtractor.Tests`. `Api.Tests` uses `[Fact]` only; it has no `[Theory]` anywhere and this
  plan adds none.
- **Do not commit** unless the task says to. **Do not run `export-all`** unless a task says to; it
  takes about seven minutes.
- This checkout is shared with other sessions. Before editing a file, read it as it stands. Never run
  `git stash`.

## Prerequisite

Task 1 reads `ItemDocument.Rarities`, the rungs an item comes in, which
`docs/superpowers/specs/2026-08-05-item-level-rarity-and-tempering-design.md` section 4 defines and
which is in flight in this checkout. Before starting Task 1, confirm the export carries it:

```bash
grep -c '"rarities"' output/export/entities.jsonl
```

A non-zero count means the field has landed and the tasks run in the order they are numbered.

A zero means the item rarity work has not regenerated the export yet. In that case run Tasks 3, 4, 5
and 6 first, in that order, and come back to Tasks 1 and 2 when the grep answers. Tasks 3 through 6
read neither `Rarities` nor `LinkColour`: Task 5's one rarity-shaped test reads the scalar
`ItemDocument.Rarity`, which 37 rows over 11 ids carry today. Task 2 waits with Task 1 rather than
with the others, because the `--cx-name` it serves is Task 1's colour and would carry the plain text
colour on every item until Task 1 lands.

## File structure

| File | What happens |
|---|---|
| `src/FellowshipExtractor.Api/Models/CodexView.cs` | Gains `Rung` and `LinkColour`. `NameColour` reads them. `HeroTint` goes. |
| `src/FellowshipExtractor.Api/Components/Shared/EntityLink.razor` | Writes a colour only when given one. `Tint` goes. |
| `src/FellowshipExtractor.Api/Components/Pages/Home.razor` | The showcase card stops passing `Tint`. |
| `src/FellowshipExtractor.Api/Components/Pages/EntityPage.razor` | Serves `--cx-name`; the heading reads `--cx-rung` over it. |
| `src/FellowshipExtractor.Api/wwwroot/js/codex.js` | Sets `--cx-rung` from the pressed rung, clears it on arrival. |
| `src/FellowshipExtractor.Api/Models/EntityStore.cs` | Exposes the count of distinct `(type, id)` keys. |
| `src/FellowshipExtractor.Api/Models/Codex.cs` | `Count` and every per-type count answer ids. `View` counts the ids a facet value reaches. |
| `src/FellowshipExtractor.Api/Models/Suggestions.cs` | Folds hits to one per id before grouping and limiting. |
| `src/FellowshipExtractor.Api/Models/CodexQuery.cs` | `Listing` is declared here beside `CodexResults`, whose `Rows` becomes listings. `CodexSearch` folds and builds the variants. |
| `src/FellowshipExtractor.Api/Models/BrowseView.cs` | `Columns` reads `Listing.Shown`. |
| `src/FellowshipExtractor.Api/Components/Pages/Browse.razor` | Draws a listing and its variant links. |
| `src/FellowshipExtractor.Api/Program.cs` | `/api/entities` maps `Listing.Shown`. |
| `src/FellowshipExtractor.Api/Styles/codex.scss` | Styles the variant links. |
| `src/FellowshipExtractor.Api.Tests/**` | Covers the colour table, the link, the fold and the counts. |

`EntityPage.razor` appears twice above because Task 2 changes how it serves its heading colour and
Task 6 changes how `Nearby` counts; the two edits do not touch the same lines.

---

### Task 1: One colour table, and no hero on a name or a link

**Files:**
- Modify: `src/FellowshipExtractor.Api/Models/CodexView.cs:34-74`
- Modify: `src/FellowshipExtractor.Api/Components/Shared/EntityLink.razor`
- Modify: `src/FellowshipExtractor.Api/Components/Pages/Home.razor:61-64`
- Create: `src/FellowshipExtractor.Api.Tests/ColourTests.cs`
- Create: `src/FellowshipExtractor.Api.Tests/EntityLinkTests.cs`

**Interfaces:**
- Consumes: `DesignTokens.Rarity(string internalName)`, `DesignTokens.Gem(string type)`,
  `DesignTokens.Color(string name)`, all returning `string?` and all returning null on a miss;
  `store.Vocabulary.Rarities`, an `IReadOnlyList<RarityEntry>` of
  `RarityEntry(int Tier, string Name, string DisplayName, string GemDisplayName)`;
  `ItemDocument.Rarities`, an `IReadOnlyList<Rarity>?` ordered lowest rung first.
- Produces: `public string? LinkColour(EntityDocument document)` and the unchanged signature
  `public string NameColour(EntityDocument document)` on `CodexView`. `EntityLink` is the only caller
  of `LinkColour`. Task 2 renders `NameColour` into a custom property. `CodexView.HeroColour(string)`
  survives unchanged and Task 5 gives it a new caller.

Section 2 of the spec. `NameColour` reads a hero for a single-hero item and for every talent slot,
and `EntityLink` paints every link with it, so a link is hero coloured today and an item that states
no hero is drawn in the plain text colour rather than its rung's. One table answers both: the colour
the build states for a row, and the text default behind it.

- [ ] **Step 1: Write the failing colour tests**

`src/FellowshipExtractor.Api.Tests/ColourTests.cs`. The fixture builds `CodexView` with
`chrome: null!` and `icons: null!`, so touch nothing on this view but the colour members. Read the
expected colours out of the shipped ladder rather than spelling a hex.

```csharp
namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class ColourTests(Export export)
{
    private string? Shipped(int tier) =>
        CodexView.Readable(export.View.Tokens.Rarity(
            export.Store.Vocabulary.Rarities.Single(rung => rung.Tier == tier).Name));

    [Fact]
    public void A_dropped_item_is_drawn_at_the_top_of_its_own_ladder()
    {
        var dropped = export.Store.Entities.OfType<ItemDocument>()
            .First(item => item.Rarities is { Count: > 1 });

        Assert.Equal(Shipped((int)dropped.Rarities![^1]), export.View.NameColour(dropped));
    }

    [Fact]
    public void A_legendary_item_is_drawn_at_the_legendary_rung()
    {
        var legendary = export.Store.Entities.OfType<ItemDocument>()
            .First(item => item.Rarity is { } rung && (int)rung == 6);

        Assert.Equal(Shipped(6), export.View.NameColour(legendary));
    }

    [Fact]
    public void A_gem_tier_is_drawn_at_the_rung_it_states()
    {
        var tier = export.Store.Entities.OfType<GemTierDocument>().First();

        Assert.Equal(Shipped((int)tier.Rarity), export.View.NameColour(tier));
    }

    [Fact]
    public void A_gem_type_and_its_traits_are_drawn_in_the_stones_colour()
    {
        var type = export.Store.Entities.OfType<GemTypeDocument>().First();
        var trait = export.Store.Entities.OfType<GemTraitDocument>().First(t => t.Gem == type.Gem);
        var stone = CodexView.Readable(export.View.Tokens.Gem(type.Gem.ToString()));

        Assert.Equal(stone, export.View.NameColour(type));
        Assert.Equal(stone, export.View.NameColour(trait));
    }

    [Fact]
    public void An_item_carrying_one_hero_is_not_drawn_in_that_heros_colour()
    {
        var worn = export.Store.Entities.OfType<ItemDocument>()
            .First(item => item.Heroes is { Count: 1 });

        Assert.NotEqual(export.View.HeroColour(worn.Heroes![0].ToString()), export.View.NameColour(worn));
    }

    [Fact]
    public void A_talent_slot_is_not_drawn_in_its_heros_colour()
    {
        var slot = export.Store.Entities.OfType<TalentSlotDocument>().First();

        Assert.NotEqual(export.View.HeroColour(slot.Hero.ToString()), export.View.NameColour(slot));
    }

    [Fact]
    public void A_row_the_build_gives_no_colour_asks_for_none()
    {
        var ability = export.Store.Entities.OfType<AbilityDocument>().First();

        Assert.Null(export.View.LinkColour(ability));
        Assert.Equal(export.View.Tokens.Color("core.text-primary"), export.View.NameColour(ability));
    }

    [Fact]
    public void A_row_with_no_name_is_drawn_in_the_secondary_text_colour()
    {
        var nameless = export.Store.Entities.OfType<TalentSlotDocument>()
            .First(slot => PublicTypes.DisplayNameOf(slot) is null);

        Assert.Equal(export.View.Tokens.Color("core.text-secondary"), export.View.NameColour(nameless));
    }
}
```

The last test picks a talent slot because it must find a row that carries no name and that none of
rules 1 through 3 answers: 208 of the 216 talent slot rows carry no name, and a talent slot states
neither a rung nor a stone.

- [ ] **Step 2: Run the colour tests and watch them fail**

```bash
dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~ColourTests"
```

Expected: the build fails on `LinkColour`, which does not exist yet. Once it compiles, expect the
dropped-item, gem-type and two hero tests to fail.

- [ ] **Step 3: Write the colour table**

Replace `NameColour` and `HeroTint` in `src/FellowshipExtractor.Api/Models/CodexView.cs`. `Rung`
joins the ladder on the ordinal the generated `Rarity` enum carries, and asks the palette for the
entry's filed `Name`, which is the word the colour keys are built from.

```csharp
    public string? LinkColour(EntityDocument document)
    {
        Rarity? rung = document switch
        {
            ItemDocument { Rarities: { Count: > 0 } rungs } => rungs[^1],
            ItemDocument { Rarity: { } stated } => stated,
            GemTierDocument tier => tier.Rarity,
            _ => null,
        };

        if (rung is { } found && Rung(found) is { } shade) return shade;

        GemTypeName? gem = document switch
        {
            GemTypeDocument type => type.Gem,
            GemTierDocument tier => tier.Gem,
            GemTraitDocument trait => trait.Gem,
            _ => null,
        };

        return gem is { } stone ? Readable(tokens.Gem(stone.ToString())) : null;
    }

    private string? Rung(Rarity rarity) =>
        store.Vocabulary.Rarities.FirstOrDefault(entry => entry.Tier == (int)rarity) is { } rung
            ? Readable(tokens.Rarity(rung.Name))
            : null;

    public string NameColour(EntityDocument document) =>
        LinkColour(document)
        ?? (PublicTypes.DisplayNameOf(document) is null
            ? tokens.Color("core.text-secondary") ?? "#909BA6"
            : tokens.Color("core.text-primary") ?? "#EAE3DB");
```

The second arm answers a legendary that states its rung but no ladder, which is the shape the export
carries until the item rarity work regenerates it, and is harmless once it does.

Delete `HeroTint` entirely. `HeroColour` and `HeroSwatch` stay, because section 2.3 keeps the chips,
pills and rosters hero coloured and section 4.3 gives `HeroColour` a new caller.

- [ ] **Step 4: Run the colour tests and watch them pass**

```bash
dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~ColourTests"
```

Expected: PASS, all eight.

- [ ] **Step 5: Write the failing link tests**

`src/FellowshipExtractor.Api.Tests/EntityLinkTests.cs`. `EntityLink` injects `CodexView`, so
register the fixture's view.

```csharp
namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class EntityLinkTests : BunitContext
{
    private readonly EntityStore Store;

    public EntityLinkTests(Export export)
    {
        Store = export.Store;
        Services.AddSingleton(export.View);
    }

    private IElement Draw(EntityDocument document) =>
        Render<EntityLink>(p => p.Add(x => x.Entity, document)).Find("a");

    [Fact]
    public void A_link_to_a_row_the_build_colours_carries_that_colour()
    {
        EntityDocument[] coloured =
        [
            Store.Entities.OfType<ItemDocument>().First(item => item.Rarities is { Count: > 0 }),
            Store.Entities.OfType<GemTypeDocument>().First(),
            Store.Entities.OfType<GemTierDocument>().First(),
            Store.Entities.OfType<GemTraitDocument>().First(),
        ];

        Assert.All(coloured, document => Assert.StartsWith(
            "color:", Draw(document).GetAttribute("style"), StringComparison.Ordinal));
    }

    [Fact]
    public void A_link_to_every_other_row_asks_for_no_colour_at_all()
    {
        EntityDocument[] plain =
        [
            Store.Entities.OfType<AbilityDocument>().First(),
            Store.Entities.OfType<EffectDocument>().First(),
            Store.Entities.OfType<TalentDocument>().First(),
            Store.Entities.OfType<TalentSlotDocument>().First(),
            Store.Entities.OfType<CreatureDocument>().First(),
            Store.Entities.OfType<TraitDocument>().First(),
        ];

        Assert.All(plain, document => Assert.False(Draw(document).HasAttribute("style")));
    }
}
```

- [ ] **Step 6: Run the link tests and watch them fail**

```bash
dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~EntityLinkTests"
```

Expected: the second test fails, because `Tone` writes `style="color:..."` on every link.

- [ ] **Step 7: Let the stylesheet colour the links it should**

`src/FellowshipExtractor.Api/Components/Shared/EntityLink.razor` in full:

```razor
@inject CodexView Ui

<a class="@Class" href="@Ui.Href(Entity)" data-tip="@Ui.Tip(Entity)" style="@Tone">
    @if (ChildContent is null)
    {
        @Ui.Name(Entity)
    }
    else
    {
        @ChildContent
    }
</a>

@code {
    [Parameter, EditorRequired] public EntityDocument Entity { get; set; } = default!;

    [Parameter] public string? Class { get; set; }

    [Parameter] public RenderFragment? ChildContent { get; set; }

    private string? Tone => Ui.LinkColour(Entity) is { } colour ? $"color:{colour}" : null;
}
```

Blazor omits an attribute whose value is null, so a row the build gives no colour renders no `style`
at all and `codex.scss`'s `a { color: var(--gold-bright); }` inside `@scope (body) to (.fx-tip)`
stands.

Then in `src/FellowshipExtractor.Api/Components/Pages/Home.razor`, drop `Tint="false"` from the
showcase card's `<EntityLink>`. The card already draws its own name span through `NameColour`, so it
keeps the colour it has; the anchor around it now carries the row's own colour instead of nothing.

- [ ] **Step 8: Run both new files and the whole suite**

```bash
dotnet build src/FellowshipExtractor.slnx
dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj
```

Expected: `0 Warning(s)` and `0 Error(s)` from the build, and every test passing. `Tint` had one
caller, so a compile error naming any other is a caller this plan did not know about; fix it by
deleting the attribute and say so in your report.

- [ ] **Step 9: Commit**

```bash
git add src/FellowshipExtractor.Api/Models/CodexView.cs src/FellowshipExtractor.Api/Components/Shared/EntityLink.razor src/FellowshipExtractor.Api/Components/Pages/Home.razor src/FellowshipExtractor.Api.Tests/ColourTests.cs src/FellowshipExtractor.Api.Tests/EntityLinkTests.cs
git commit -m "Draw a name and a link at the rung the build gives the row"
```

---

### Task 2: The heading follows the rarity knob

**Files:**
- Modify: `src/FellowshipExtractor.Api/Components/Pages/EntityPage.razor:12`
- Modify: `src/FellowshipExtractor.Api/wwwroot/js/codex.js:186-207,232-234`
- Modify: `src/FellowshipExtractor.Api.Tests/Sections/PageSectionTests.cs`

**Interfaces:**
- Consumes: `CodexView.NameColour(EntityDocument)` from Task 1.
- Produces: nothing later tasks read.

Section 2.4. The heading is computed once on the server and holds still while the tooltip beside it
re-tints through `--fx-title`. Two custom properties let the knob override the served colour without
the server having to know a rung was pressed.

- [ ] **Step 1: Serve the name colour as a custom property**

In `src/FellowshipExtractor.Api/Components/Pages/EntityPage.razor`, find the wrapper the `<h1>` sits
in and add `--cx-name` to it, then let the heading read a rung over the top. Read the file first:
the wrapper element is whatever encloses the heading and the knobs today.

```razor
<div class="cx-entity" style="--cx-name:@Ui.NameColour(Shown)">
    <h1 style="color:var(--cx-rung, var(--cx-name))">@Name</h1>
```

- [ ] **Step 2: Set the rung from the pressed button**

In `src/FellowshipExtractor.Api/wwwroot/js/codex.js`, the rarity click handler already finds
`event.target.closest?.('button[data-knob]')`, clears `aria-pressed` and `dataset.value` across
`[data-knob="${rung.dataset.knob}"]`, sets `rung.dataset.value = rung.dataset.choice` on a fresh
press, and then calls `stage()`. Add the property alongside that, before the `stage()` call:

```javascript
    const heading = document.querySelector('.cx-entity');
    if (heading && rung.dataset.knob === 'rarity') {
        const pressed = rung.getAttribute('aria-pressed') === 'true';
        if (pressed) {
            heading.style.setProperty('--cx-rung', getComputedStyle(rung).color);
        } else {
            heading.style.removeProperty('--cx-rung');
        }
    }
```

`Knobs.razor` paints each rarity button with an inline colour from
`CodexView.Readable(Ui.Tokens.Rarity(rung.Name))`, so `getComputedStyle(rung).color` reads the value
the button was painted from. `data-choice` carries the rung's ordinal and not its colour, so do not
read it here.

Then in `arrived()`, which today is `function arrived() { hide(); }`, clear the property so a rung
pressed before a hero is chosen does not carry across the navigation the hero knob performs:

```javascript
function arrived() {
    hide();
    document.querySelector('.cx-entity')?.style.removeProperty('--cx-rung');
}
```

`arrived` is wired to `DOMContentLoaded` and to Blazor's `enhancedload`, so it covers both a replaced
document and an element the enhanced navigation keeps.

- [ ] **Step 3: Assert the heading serves a property rather than a colour**

`EntityPage` is a routed page rather than a section component, so `RouteTests` is where its markup is
reachable. That file is `src/FellowshipExtractor.Api.Tests/RouteTests.cs`, a
`WebApplicationFactory<Program>` fixture, and its facts are `async Task` taking their cancellation
token from `Xunit.TestContext.Current.CancellationToken`. Add one fact there, over an item that
states the rungs it comes in:

```csharp
    [Fact]
    public async Task An_entity_page_serves_its_name_colour_as_a_custom_property()
    {
        var client = factory.CreateClient();
        var page = await client.GetStringAsync("/entity/item/5250", TestContext.Current.CancellationToken);

        Assert.Contains("--cx-name:#", page, StringComparison.Ordinal);
        Assert.Contains("var(--cx-rung, var(--cx-name))", page, StringComparison.Ordinal);
    }
```

Read the class first: it names its factory field its own way, and this fact must build its client the
way the three facts already there do.

- [ ] **Step 4: Build, test, and look at it**

```bash
dotnet build src/FellowshipExtractor.slnx
dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj
```

Then run the app and press a rung on an item page, watching the heading change colour and change
back when the rung is released:

```bash
dotnet run --project src/FellowshipExtractor.Api
```

Stop the app when done. Record in your report which item you opened and what you saw.

- [ ] **Step 5: Commit**

```bash
git add src/FellowshipExtractor.Api/Components/Pages/EntityPage.razor src/FellowshipExtractor.Api/wwwroot/js/codex.js src/FellowshipExtractor.Api.Tests/Sections/PageSectionTests.cs
git commit -m "Re-tint the entity heading as the reader turns the rarity knob"
```

---

### Task 3: The codex counts ids

**Files:**
- Modify: `src/FellowshipExtractor.Api/Models/EntityStore.cs:76,90,147,170`
- Modify: `src/FellowshipExtractor.Api/Models/Codex.cs:86`
- Create: `src/FellowshipExtractor.Api.Tests/CountTests.cs`

**Interfaces:**
- Consumes: `EntityStore.byId`, a private
  `Dictionary<(Fellowship.SDK.EntityType Type, int Id), EntityDocument[]>`.
- Produces: `public int Ids { get; }` on `EntityStore`, and `Codex.Count` answering it. Task 4 and
  Task 5 assert against both.

Section 4.5. `Codex.Count` is `store.Count`, which is incremented once per line of the export, so the
front page says "12,437 entries" where 10,285 ids stand behind them. `byId.Count` is the number
wanted and exists already, inside a log message argument list, with no accessor.

- [ ] **Step 1: Write the failing count test**

`src/FellowshipExtractor.Api.Tests/CountTests.cs`.

```csharp
namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class CountTests(Export export)
{
    [Fact]
    public void The_store_counts_rows_and_the_codex_counts_ids()
    {
        var ids = export.Store.Entities
            .Select(document => (EntityTypes.TypeOf(document), document.Id))
            .Distinct()
            .Count();

        Assert.Equal(export.Store.Entities.Count, export.Store.Count);
        Assert.Equal(ids, export.Store.Ids);
        Assert.Equal(ids, export.View.Codex.Count);
        Assert.True(export.Store.Ids < export.Store.Count);
    }

    [Fact]
    public void Every_per_type_count_counts_ids()
    {
        foreach (var type in export.View.Codex.Types)
        {
            var ids = export.Store.Entities
                .Where(document => EntityTypes.TypeOf(document) == type.Type)
                .Select(document => document.Id)
                .Distinct()
                .Count();

            Assert.Equal(ids, type.Count);
        }

        Assert.Equal(export.View.Codex.Count, export.View.Codex.Types.Sum(type => type.Count));
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

```bash
dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~CountTests"
```

Expected: the build fails on `Store.Ids`, which does not exist.

- [ ] **Step 3: Expose the id count and read it**

In `src/FellowshipExtractor.Api/Models/EntityStore.cs`, add `public int Ids { get; }` beside
`public int Count { get; }` and set it from `byId.Count` at the point the index is built, which is
the `foreach (var (key, list) in groups)` loop. Then let the log line at the end of the constructor
read `Ids` rather than recomputing `byId.Count`.

In `src/FellowshipExtractor.Api/Models/Codex.cs`, `public int Count => store.Ids;`, and the per-type
counts the constructor builds count ids too. They read
`store.Entities.GroupBy(EntityTypes.TypeOf).ToDictionary(group => group.Key, group => group.Count())`
today, which is the number behind every `TypeView.Count`:

```csharp
        var counts = store.Entities
            .GroupBy(EntityTypes.TypeOf)
            .ToDictionary(group => group.Key, group => group.Select(row => row.Id).Distinct().Count());
```

That one line moves the front page's menu-card counts, `MegaMenu`'s badges and `NotFound.razor`'s
offer onto ids together, because all three read `TypeView.Count`.

- [ ] **Step 4: Run it and watch it pass**

```bash
dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~CountTests"
```

Expected: PASS.

- [ ] **Step 5: Check the four sentences that print these numbers**

These numbers reach four sentences that no test covers. Open each and confirm it still reads true
with an id count in it, changing the wording where it does not:

- `src/FellowshipExtractor.Api/Components/Pages/Home.razor:10`, "N entries read straight out of the
  shipped build", from `Codex.Count`.
- `src/FellowshipExtractor.Api/Components/Pages/Home.razor:38`, "N entities" on each menu card, from
  `TypeView.Count`.
- `src/FellowshipExtractor.Api/ApiDocument.cs:40,53`, the OpenAPI description "Every ability, item,
  talent, trait and gem the shipped build declares: {entities} of them", from `Codex.Count`.
- `src/FellowshipExtractor.Api/Components/Pages/NotFound.razor:31`, "Browse the {kind.Count:N0} it
  does have", from `TypeView.Count`.

- [ ] **Step 6: Build, test, commit**

```bash
dotnet build src/FellowshipExtractor.slnx
dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj
git add src/FellowshipExtractor.Api/Models/EntityStore.cs src/FellowshipExtractor.Api/Models/Codex.cs src/FellowshipExtractor.Api.Tests/CountTests.cs
git commit -m "Count the ids the corpus holds, not the readings"
```

---

### Task 4: The omnibox suggests ids

**Files:**
- Modify: `src/FellowshipExtractor.Api/Models/Suggestions.cs:24-49`
- Create: `src/FellowshipExtractor.Api.Tests/SuggestionsTests.cs`

**Interfaces:**
- Consumes: `SearchIndex.Search(string text, int limit)` returning `IReadOnlyList<Hit>` where
  `Hit` is `readonly record struct Hit(EntityDocument Document, int Score)`, already ordered by score
  then name length then name then id.
- Produces: no new type. `SearchResponse.Total` and `SearchGroup.Count` become id counts.

Section 4.2. `SearchIndex` keeps answering readings, because `CodexSearch.Run` narrows what `Search`
hands it by the reader's own facets and a fold before that narrowing would drop the reading the
facets would have kept. The omnibox has no such narrowing, so it folds as soon as the hits arrive.

- [ ] **Step 1: Write the failing suggestion test**

`src/FellowshipExtractor.Api.Tests/SuggestionsTests.cs`. Item 5191 reads under four names over twelve
rows, so a search for a word its readings share answers one suggestion.

```csharp
namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class SuggestionsTests(Export export)
{
    private Suggestions Box => new(new SearchIndex(export.Store, NullLogger<SearchIndex>.Instance), export.View);

    [Fact]
    public void A_word_matching_many_readings_of_one_id_suggests_it_once()
    {
        var found = Box.For("Betrayer's", null, 50);
        var ids = found.Results.Select(hit => (hit.Type, hit.Id)).ToArray();

        Assert.Equal(ids.Length, ids.Distinct().Count());
        Assert.Equal(ids.Length, found.Total);
    }

    [Fact]
    public void The_per_type_counts_count_ids()
    {
        var found = Box.For("Betrayer's", null, 200);

        Assert.Equal(found.Total, found.Groups.Sum(group => group.Count));
    }
}
```

`NullLogger<SearchIndex>` comes from `Microsoft.Extensions.Logging.Abstractions`; add the using to
this file, since the global usings do not carry it. If building a second `SearchIndex` over the whole
corpus is slow enough to notice, add a `Suggestions` property to
`src/FellowshipExtractor.Api.Tests/Export.cs` alongside `View` and `Tooltip` and share it, which is
what the fixture exists for.

- [ ] **Step 2: Run it and watch it fail**

```bash
dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~SuggestionsTests"
```

Expected: FAIL. Twelve readings of item 5191 arrive as twelve results, so the distinct count is
lower than the length.

- [ ] **Step 3: Fold the hits**

In `src/FellowshipExtractor.Api/Models/Suggestions.cs`, `For` currently reads
`var hits = index.Search(text, Ceiling);` and then groups, counts and narrows. Fold immediately
after the search, keeping the first hit per key, which is the highest-scoring reading because
`Search` has already ordered them:

```csharp
        var hits = index.Search(text, Ceiling)
            .GroupBy(hit => (EntityTypes.TypeOf(hit.Document), hit.Document.Id))
            .Select(readings => readings.First())
            .ToArray();
```

Everything below it is unchanged: `Groups`, `Total` and `chosen` all read `hits` and now all count
ids. `Ceiling` stays 2,000 readings, which is the ceiling on what is folded rather than on what is
answered.

- [ ] **Step 4: Run it and watch it pass**

```bash
dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~SuggestionsTests"
```

Expected: PASS.

- [ ] **Step 5: Build, test, commit**

```bash
dotnet build src/FellowshipExtractor.slnx
dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj
git add src/FellowshipExtractor.Api/Models/Suggestions.cs src/FellowshipExtractor.Api.Tests/SuggestionsTests.cs
git commit -m "Suggest an id once, however many readings match the typed words"
```

---

### Task 5: A browse row is an id

**Files:**
- Modify: `src/FellowshipExtractor.Api/Models/CodexQuery.cs:349-390,465`
- Modify: `src/FellowshipExtractor.Api/Models/BrowseView.cs:30`
- Modify: `src/FellowshipExtractor.Api/Components/Pages/Browse.razor:134,177-220,286,303-309`
- Modify: `src/FellowshipExtractor.Api/Components/Pages/Home.razor:81-90`
- Modify: `src/FellowshipExtractor.Api/Program.cs:117-133`
- Modify: `src/FellowshipExtractor.Api/Styles/codex.scss`
- Create: `src/FellowshipExtractor.Api.Tests/ListingTests.cs`

**Interfaces:**
- Consumes: `EntityStore.All(EntityType, int)` returning the readings of one id in export order;
  `CodexView.Icon(string?)`, `CodexView.Href(EntityType, int)`, `CodexView.HeroColour(string)`;
  `EntityStore.Wearer(EntityDocument)` returning the `HeroEntry` a reading is for.
- Produces: `public sealed record Listing(EntityDocument Shown, IReadOnlyList<Listing.Variant> Variants)`
  with `public sealed record Variant(string Name, string Icon, string Href, string? Colour)` nested
  inside it, and `CodexResults.Rows` typed `IReadOnlyList<Listing>`. Task 6 reads neither.

Sections 4.1 through 4.5. This is the task the whole plan is for: 5,744 item rows collapse to 3,789,
216 talent slot rows collapse to 19, and every count beneath the list counts what the list draws.

- [ ] **Step 1: Write the failing listing tests**

`src/FellowshipExtractor.Api.Tests/ListingTests.cs`. Item 5250 is *Sinbinding Stone*, nine readings
under one name. Item 5191 is four names over twelve readings, and Meiko reads it as
*Betrayer's Dragonhide Gloves*.

```csharp
using Microsoft.AspNetCore.Http;

namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class ListingTests(Export export)
{
    private const int Sinbinding = 5250;
    private const int Betrayers = 5191;

    private CodexResults Run(params (string Key, string Value)[] asked)
    {
        var query = CodexQuery.Parse(
            new QueryCollection(asked.ToDictionary(
                pair => pair.Key,
                pair => new Microsoft.Extensions.Primitives.StringValues(pair.Value))),
            export.View.Codex,
            out var error);

        Assert.Null(error);

        return new CodexSearch(
            export.Store,
            export.View.Codex,
            new SearchIndex(export.Store, NullLogger<SearchIndex>.Instance),
            export.View).Run(query with { PageSize = 200 });
    }

    private ItemDocument Legendary => export.Store.Entities.OfType<ItemDocument>()
        .First(item => item.Rarity is { } rung && (int)rung == 6
            && export.Store.All(EntityType.Item, item.Id).Count > 1);

    private static Listing Find(CodexResults results, int id) =>
        results.Rows.Single(row => row.Shown.Id == id);

    [Fact]
    public void An_id_is_drawn_once_however_many_readings_it_has()
    {
        var results = Run(("type", "item"));
        var ids = results.Rows.Select(row => row.Shown.Id).ToArray();

        Assert.Equal(ids.Length, ids.Distinct().Count());
        Assert.Equal(3789, results.Total);
    }

    [Fact]
    public void A_talent_slot_is_drawn_once_per_id()
    {
        Assert.Equal(19, Run(("type", "talent-slot")).Total);
    }

    [Fact]
    public void An_id_reading_under_one_name_draws_no_variant_link()
    {
        Assert.Empty(Find(Run(("type", "item"), ("q", "Sinbinding")), Sinbinding).Variants);
    }

    [Fact]
    public void An_id_reading_under_four_names_draws_the_other_three()
    {
        var row = Find(Run(("type", "item"), ("q", "Betrayer's")), Betrayers);

        Assert.Equal(3, row.Variants.Count);
        Assert.DoesNotContain(export.View.Name(row.Shown), row.Variants.Select(variant => variant.Name));
        Assert.Equal(row.Variants.Count, row.Variants.Select(variant => variant.Name).Distinct().Count());
    }

    [Fact]
    public void A_filter_decides_which_reading_a_row_is_named_by()
    {
        var row = Find(Run(("type", "item"), ("hero", "Meiko")), Betrayers);

        Assert.Equal("Betrayer's Dragonhide Gloves", export.View.Name(row.Shown));
    }

    [Fact]
    public void A_variant_link_opens_the_reading_whose_name_it_shows()
    {
        var row = Find(Run(("type", "item"), ("q", "Betrayer's")), Betrayers);

        Assert.All(row.Variants, variant =>
        {
            Assert.StartsWith($"/entity/item/{Betrayers}?hero=", variant.Href, StringComparison.Ordinal);

            var hero = Uri.UnescapeDataString(variant.Href.Split("?hero=")[1]);
            var reading = export.Store.All(EntityType.Item, Betrayers)
                .Single(read => export.Store.Wearer(read)?.Name == hero);

            Assert.Equal(export.View.Name(reading), variant.Name);
        });
    }

    [Fact]
    public void A_name_one_hero_reads_carries_that_heros_colour()
    {
        var legendary = Legendary;
        var row = Find(Run(("type", "item"), ("q", legendary.Name!)), legendary.Id);

        Assert.NotEmpty(row.Variants);
        Assert.All(row.Variants, variant => Assert.NotNull(variant.Colour));
    }

    [Fact]
    public void A_name_several_heroes_read_carries_none()
    {
        var shared = Find(Run(("type", "item"), ("q", "Betrayer's")), Betrayers).Variants;

        Assert.All(shared, variant => Assert.Null(variant.Colour));
    }

    [Fact]
    public void Every_count_beneath_the_list_counts_ids()
    {
        var results = Run(("type", "item"), ("hero", "Meiko"));

        Assert.Equal(results.Total, results.Types[EntityType.Item]);
        Assert.Equal(results.Total, results.AcrossTypes);

        var slot = results.Facets.Single(facet => facet.Key == "slot");
        var chosen = slot.Values[0];

        Assert.Equal(chosen.Count, Run(("type", "item"), ("hero", "Meiko"), ("slot", chosen.Value)).Total);
    }
}
```

The rarity facet's accepted value spelling is the printed word, so if `("rarity", "Legendary")` is
refused by `Parse`, read `Codex.Offered` for the rarity facet and use the spelling it lists.

- [ ] **Step 2: Run them and watch them fail**

```bash
dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~ListingTests"
```

Expected: the build fails on `Listing` and on `row.Shown`.

- [ ] **Step 3: Declare the listing and fold the results**

In `src/FellowshipExtractor.Api/Models/CodexQuery.cs`, beside `CodexResults`:

```csharp
public sealed record Listing(EntityDocument Shown, IReadOnlyList<Listing.Variant> Variants)
{
    public sealed record Variant(string Name, string? Icon, string Href, string? Colour);
}
```

`Variant` is nested because `Fellowship.SDK.Variant` is in scope in every file of this assembly
through the project's global `using Fellowship.SDK`, which is the same reason `Versions.razor` nests
its own `Version`. `Icon` is the art's file name as the reading states it, so the browser resolves it
through `CodexView.Icon` exactly as it resolves a row's own art.

`CodexResults.Rows` becomes `IReadOnlyList<Listing>`.

`CodexSearch`'s primary constructor gains the view, becoming
`public sealed class CodexSearch(EntityStore store, Codex codex, SearchIndex index, CodexView view)`.
It needs `view.Name` for the names an id reads under and `view.HeroColour` for the 36 that belong to
one hero. `CodexView` does not take `CodexSearch`, so nothing circles; `Suggestions` already takes
the view the same way. `Program.cs` registers both as singletons and needs no edit.

Fold after `Order` and before the page slice, so `Total` and `Pages` are counts of ids and a page
holds a fixed number of rows:

```csharp
        var ordered = Fold(Order(matched, query));
        var pages = Math.Max(1, (ordered.Length + query.PageSize - 1) / query.PageSize);
```

`Fold` keeps the first reading of each key, which after `Order` is the one the filter and the sort
put first, and that is the primary of spec section 4.2:

```csharp
    private Listing[] Fold(EntityDocument[] rows) =>
    [
        .. rows.GroupBy(row => (EntityTypes.TypeOf(row), row.Id))
            .Select(readings => Listed(readings.First())),
    ];
```

The three arrays `Run` builds beside `ordered` are counted rather than drawn, so they fold to a
number rather than to listings, and the lines that build them stay as they are.

`Codex.View` is where the facet counts are made. It walks rows and adds one to a bucket per facet
value, so it counts a reading rather than an id. Give each bucket the set of ids that reached it,
which leaves its signature, its case-variant folding and its ordering alone:

```csharp
    public FacetView View(Facet facet, IReadOnlyList<EntityDocument> rows)
    {
        var counted = new Dictionary<string, HashSet<(EntityType, int)>>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            var key = (EntityTypes.TypeOf(row), row.Id);

            foreach (var value in facet.Values(row))
            {
                if (value.Length == 0) continue;
                if (!counted.TryGetValue(value, out var ids)) counted[value] = ids = [];

                ids.Add(key);
            }
        }

        return new FacetView(facet.Key, facet.Label,
        [
            .. counted
                .GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(spellings => (
                    Value: Written(spellings),
                    Count: spellings.SelectMany(pair => pair.Value).Distinct().Count()))
                .OrderByDescending(value => value.Count)
                .ThenBy(value => value.Value, StringComparer.OrdinalIgnoreCase)
                .Select(value => new FacetValue(
                    value.Value,
                    Printed(facet, value.Value),
                    value.Count,
                    CodexView.Readable(facet.Colour?.Invoke(tokens, value.Value)))),
        ]);
    }
```

`Written` picks the dominant spelling of a case-variant group and takes
`IEnumerable<KeyValuePair<string, int>>`, ordering by the count. It moves with the dictionary:

```csharp
    private static string Written(IEnumerable<KeyValuePair<string, HashSet<(EntityType, int)>>> spellings) =>
        spellings
            .OrderByDescending(pair => pair.Value.Count)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .First()
            .Key;
```

The `Distinct` over the flattened sets matters, because two spellings of one value can hold the same
id and the union is what a click on that facet returns.

`Written` picks the dominant spelling by count, so a value whose two spellings differ in reading
count but not in id count can now settle on the other spelling. `Codex.Offered`, the whitelist
`CodexQuery.Parse` validates a query against, is built from `View(facet, store.Entities)`, so a
flipped spelling changes which query strings are accepted. Before moving on, print the offered values
for every facet and confirm they are the spellings the browser's own links generate: open `/browse`,
click through one value of each facet in the rail, and confirm none is refused.

Then `across` and `types` in `CodexSearch.Run`:

```csharp
        var across = rows.Where(row => Matches(row, query, skipTypes: true)).ToArray();
        var types = across
            .GroupBy(EntityTypes.TypeOf)
            .ToDictionary(group => group.Key, group => group.Select(row => row.Id).Distinct().Count());
        var acrossTypes = across.Select(row => (EntityTypes.TypeOf(row), row.Id)).Distinct().Count();
```

`unbounded` feeds the measure rail, which asks only whether any row carries a measure, so it needs no
fold.

- [ ] **Step 4: Build the variant links**

Still in `CodexSearch`, a listing's variants are the other distinct names the id reads under, each
carrying that name's first reading in export order:

```csharp
    private Listing Listed(EntityDocument shown)
    {
        var type = EntityTypes.TypeOf(shown);
        var drawn = view.Name(shown);

        var variants = store.All(type, shown.Id)
            .GroupBy(view.Name, StringComparer.Ordinal)
            .Where(readings => !string.Equals(readings.Key, drawn, StringComparison.Ordinal))
            .Select(readings => Variant(type, shown.Id, readings.Key, [.. readings]))
            .ToArray();

        return new Listing(shown, variants);
    }

    private Listing.Variant Variant(EntityType type, int id, string name, EntityDocument[] readings)
    {
        var heroes = readings
            .Select(reading => store.Wearer(reading)?.Name)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var address = $"/entity/{EntityTypes.Slug(type)}/{id.ToString(CultureInfo.InvariantCulture)}";

        return new Listing.Variant(
            name,
            (readings[0] as ItemDocument)?.Icon,
            heroes is [var first, ..] ? $"{address}?hero={Uri.EscapeDataString(first)}" : address,
            heroes is [{ } one] ? view.HeroColour(one) : null);
    }
```

Every variant addresses a hero, because the entity page shows `Readings[0]` when no hero is asked
for and that is the name the row already draws; naming the first hero of this name is what makes the
page open on the name the icon showed. The colour is the narrower question: a name one hero reads
takes that hero's colour, and a name three heroes read takes none, because it belongs to an armour
type and the shipped palette names no colour for one.

The address is built the way `CodexView.Href(EntityType, int)` builds it, and calling that member
instead is equally correct. Do not call `CodexView.Icon` here: it reads the blob client the test
fixture passes as null, and the browser resolves the file name anyway.

`store.All` is a dictionary lookup on `(type, id)`, and every rendered row already performs one
through `CodexView.Tip`, so this adds no scan.

- [ ] **Step 5: Draw the row**

`src/FellowshipExtractor.Api/Components/Pages/Browse.razor`. The row loop's `row` becomes a
`Listing`; introduce `var shown = row.Shown;` at the top of the loop body and read every existing
expression off it, including `var item = shown as ItemDocument;`, the group header's
`group.Values(shown)`, the `<EntityLink Entity="shown" Class="cx-nm" />`, the type cell and the
measure cells. Then draw the variants in the name cell, after the link:

```razor
<td>
    <EntityLink Entity="shown" Class="cx-nm" />
    @if (row.Variants.Count > 0)
    {
        <span class="cx-variants">
            @foreach (var variant in row.Variants)
            {
                <a href="@variant.Href" title="@variant.Name" aria-label="@variant.Name"
                   style="@(variant.Colour is null ? null : $"color:{variant.Colour}")">
                    <Icon Src="@variant.Icon" />
                </a>
            }
        </span>
    }
</td>
```

Then the three predicates and the empty state, all in the same file:

```csharp
    private bool ShowSlot => Results.Rows.Any(r => (r.Shown as ItemDocument)?.Slot is not null);

    private bool ShowWorn => Results.Rows.Any(r =>
        r.Shown is ItemDocument { ArmorType: not null } or ItemDocument { PrimaryStat: not null }
            or ItemDocument { Heroes: { Count: > 0 } });

    private bool ShowSchools => Results.Rows.Any(r => Codex.Schools(r.Shown).Any());
```

The empty state at `Browse.razor:286` is `new(0, 1, 1, [], [], new Dictionary<EntityType, int>(), 0, [])`
and still compiles, because `[]` infers the new element type. Read it and confirm rather than
changing it.

`src/FellowshipExtractor.Api/Models/BrowseView.cs:30`, `Columns` tests the page window for a measure:

```csharp
    public IReadOnlyList<Measure> Columns =>
        [.. codex.Measures.Where(m => m.Column && (m.Key == "id" || results.Rows.Any(row => m.Value(row.Shown) is not null)))];
```

`src/FellowshipExtractor.Api/Program.cs:117-133`, `/api/entities` maps the reading a listing shows:
`[.. results.Rows.Select(row => SearchResult.From(row.Shown, view))]`.

`src/FellowshipExtractor.Api/Components/Pages/Home.razor:81-90`, the showcase groups by type and
takes two per group with no id dedupe; add `.DistinctBy(entity => (EntityTypes.TypeOf(entity), entity.Id))`
before the grouping so one id cannot fill both places.

- [ ] **Step 6: Style the variant links**

`src/FellowshipExtractor.Api/Styles/codex.scss`, inside the existing page scope. Match the
surrounding rules rather than inventing a new idiom; read the file's other `.cx-` rules first. The
links sit inline after the name, are icon sized, and carry no underline.

```scss
    .cx-variants {
        display: inline-flex;
        gap: 0.25rem;
        margin-left: 0.5rem;
        vertical-align: middle;

        img {
            width: 1.1rem;
            height: 1.1rem;
        }
    }
```

The project compiles SCSS to `wwwroot/css/codex.css` as part of the build, so build once and confirm
the rule reached the compiled file.

- [ ] **Step 7: Run everything**

```bash
dotnet build src/FellowshipExtractor.slnx
dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj
```

Expected: `0 Warning(s)` and `0 Error(s)`, and every test passing, including
`CodexTests.The_cost_measure_reads_every_talent_slots_own_int_cost`, which asserts 216 talent slot
documents over `Store.Entities` rather than over a result set and is unaffected by the fold.

- [ ] **Step 8: Look at it**

```bash
dotnet run --project src/FellowshipExtractor.Api
```

Open `/browse?type=item`, search for Sinbinding Stone, and confirm it appears once. Open item 5191
and confirm three icon links sit beside its name and that each opens the hero whose name it shows.
Stop the app. Record the headline count you saw and what the tabs said.

- [ ] **Step 9: Commit**

```bash
git add src/FellowshipExtractor.Api/Models/CodexQuery.cs src/FellowshipExtractor.Api/Models/Codex.cs src/FellowshipExtractor.Api/Models/BrowseView.cs src/FellowshipExtractor.Api/Components/Pages/Browse.razor src/FellowshipExtractor.Api/Components/Pages/Home.razor src/FellowshipExtractor.Api/Program.cs src/FellowshipExtractor.Api/Styles/codex.scss src/FellowshipExtractor.Api.Tests/ListingTests.cs
git commit -m "Draw an id once, with its other names beside it"
```

---

### Task 6: More like this counts ids

**Files:**
- Modify: `src/FellowshipExtractor.Api/Components/Pages/EntityPage.razor:117-146`
- Modify: `src/FellowshipExtractor.Api.Tests/Sections/PageSectionTests.cs`

**Interfaces:**
- Consumes: `Bearings.Related`, typed `IReadOnlyList<(string Label, string Href, int Count)>`.
- Produces: nothing later tasks read.

Section 4.5's last line. `Nearby` counts rows matching a facet value and suppresses a link below two,
so its badge disagrees with the list the link opens.

- [ ] **Step 1: Count the ids a link reaches**

In `src/FellowshipExtractor.Api/Components/Pages/EntityPage.razor`, `Add(string facet, string? value)`
computes `var count = Store.Entities.Count(row => EntityTypes.TypeOf(row) == type && definition.Matches(row, [value]));`.
Count the ids instead:

```csharp
        var count = Store.Entities
            .Where(row => EntityTypes.TypeOf(row) == type && definition.Matches(row, [value]))
            .Select(row => row.Id)
            .Distinct()
            .Count();
```

The type is already fixed in the predicate, so the id alone is the key here.

- [ ] **Step 2: Assert no link is lost and every badge agrees**

`NearbyTests` in `src/FellowshipExtractor.Api.Tests/Sections/PageSectionTests.cs` renders the section
from a hand-built `Bearings`, so it cannot see counts the page computed. `Nearby()` is a private
method of `EntityPage`, so assert its arithmetic where it is reachable, in
`src/FellowshipExtractor.Api.Tests/RouteTests.cs`, over the page itself. Measured over the current
export, no facet-and-value pair `Nearby` offers falls from two or more rows to fewer than two ids, so
the same links are offered as before:

```csharp
    [Fact]
    public async Task Every_more_like_this_badge_counts_the_ids_its_link_opens()
    {
        var client = factory.CreateClient();
        var page = await client.GetStringAsync("/entity/item/5191", TestContext.Current.CancellationToken);

        Assert.Contains("More like this", page, StringComparison.Ordinal);

        var browse = await client.GetStringAsync(
            "/browse?type=item&slot=Hands", TestContext.Current.CancellationToken);

        Assert.Contains("entities", browse, StringComparison.Ordinal);
    }
```

That fact proves the page still offers its links and the browse page it points at still answers. To
prove the badge equals what the link opens, add the arithmetic to `ListingTests` from Task 5 instead,
where `CodexResults.Total` is directly readable: for the `slot` value item 5191 carries, assert
`Run(("type", "item"), ("slot", value)).Total` equals the count `Nearby` would compute, which is the
same `Distinct` expression this step just wrote. Read `RouteTests` first for how it names its factory
field and builds its client.

- [ ] **Step 3: Build, test, commit**

```bash
dotnet build src/FellowshipExtractor.slnx
dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj
git add src/FellowshipExtractor.Api/Components/Pages/EntityPage.razor src/FellowshipExtractor.Api.Tests/Sections/PageSectionTests.cs
git commit -m "Count the ids a More like this link opens"
```

---

## Notes for whoever executes this

- `src/FellowshipExtractor.Api.Tests/Export.cs` builds its `CodexView` with `chrome: null!` and
  `icons: null!`. `CodexView.Icon(string?)` reads both, so any test reaching a variant's `Icon`
  through the fixture view throws. Task 5's tests assert names, hrefs and colours and never the icon
  URL; keep it that way, or give the fixture a real `BlobContainerClient` pointed at
  `http://localhost/icons`, which is what the `TooltipView` in the same fixture already gets.
- Two `Export` classes share the name. `src/FellowshipExtractor.Api.Tests/Export.cs` is the xunit
  collection fixture injected into a constructor; `src/FellowshipExtractor.Tests/Export.cs` is a
  static lazy singleton read as `Export.Loaded`. Every test this plan adds uses the first.
- `FellowshipExtractor.Api.Tests` is xunit.v3 and `FellowshipExtractor.Tests` is xunit 2. A
  cancellation token in the first comes from `Xunit.TestContext.Current.CancellationToken`, which
  does not exist in the second.
- `DesignTokens` is never in DI. A test needing it reads `export.View.Tokens`.
- `Codex.View` is called once per facet per request and again for the whole corpus when `Codex` is
  constructed, so the counting change in Task 5 lands on both. `Codex.Offered`, which validates a
  query's facet values, reads only the value spellings and is unaffected by what the counts say.
- The rarity facet colour has a defect this plan does not touch: `Codex.Facet` builds an item's
  printed rarity word with `gem: false` and looks it back up with `gem: true`, so an item at ordinal
  4 takes ordinal 5's colour and one at ordinal 5 takes none. Report it; fixing it belongs with the
  item rarity work, not here.
