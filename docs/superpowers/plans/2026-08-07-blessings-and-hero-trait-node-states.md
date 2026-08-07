# Blessings and hero trait node states Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A finesse is called a blessing everywhere the app speaks, a blessing and a hero trait carry
their art inside the card the way the game draws them, and a hero trait's art wears the node frame
the tree gives it, selected unless the reader asks for another state.

**Architecture:** `EntityTypes.Document(type)` resolves a document class by the name
`$"{type}Document"` and `TypeOf` parses that name back, so renaming the enum member and the class
together carries the slug, the deserialisation and the type resolution with them and nothing else has
to be told. The tooltip already branches on whether a row states a rung; it gains one further
question, whether a row draws its own art, and hero traits answer a third, which node state to wear.

**Tech Stack:** .NET 10, C# 13, Blazor static server rendering, xunit.v3 with bunit in
`src/FellowshipExtractor.Api.Tests`, xunit 2 in `src/FellowshipExtractor.Tests`, SCSS compiled to
`wwwroot/css/tooltip.css`, CUE4Parse in the extractor.

The three in-game screenshots this plan is measured against are a blessing (*The Mystic*), a hero
trait (*Amethyst Splinters*) and the hero trait tree, pasted by the user on 2026-08-07 and
recoverable from the session transcripts under
`~/.claude/projects/G--source-Fellowship-Data-Extractor/*.jsonl`.

## Global Constraints

- .NET 10, C# 13. `net10.0` everywhere except `Fellowship.SDK.Generators`, which stays
  `netstandard2.0`.
- No polyfills for missing runtime functionality. Use the modern API directly.
- No em dashes or en dashes anywhere: code, comments, XML, output, commit messages.
- **Add no comments to any file.** Not explanatory ones, not section markers, not doc comments.
- Never hardcode a list, id or data fact a canonical source already states. The node state words come
  from the shipped texture names, `store.Vocabulary` is the canonical source for heroes, colours and
  rungs, and `EntityTypes.Slug` is the only place a slug is spelled.
- Match the surrounding code's idiom: expression-bodied members, collection expressions, pattern
  matching, `StringComparer` named explicitly.
- Test naming: snake_case sentences in `FellowshipExtractor.Api.Tests`, PascalCase sentences in
  `FellowshipExtractor.Tests`. `Api.Tests` uses `[Fact]` only.
- The store reads `output/export/entities.jsonl`. Regenerating it is `export-all`, which takes about
  seven minutes; Task 1 is the only task that runs it.
- This checkout is shared with other sessions. Before editing a file, read it as it stands. Never run
  `git stash`.

## What the build states

Measured on 2026-08-07 against `output/files.txt` and the extracted textures.

- **A finesse is a blessing.** All 14 finesse rows carry an icon named
  `T_Icons_BlessingRuneIcon_Unallocated_*`, and the game's own tooltip heads them "BLESSING" over the
  name and labels them "Hero Blessing".
- **Two names in the extractor are the build's, not ours, and do not move.**
  `src/FellowshipExtractor/Textures/UiCommand.cs` lists the shipped texture
  `T_Icons_Generic_TooltipFinesse`, and `src/FellowshipExtractor/Tooltip/TypeResolver.cs` matches the
  shipped content path `/Game/Abilities/Finesses/`.
- **A hero trait is a `TraitDocument`**, carrying `Category`, `MaxRank`, `Icon` and `Description`.
  The tree draws each node hexagonal at one of three states, and the shipped textures name them:
  `T_Buttons_TraitNode_SelectedDefault`, `T_Buttons_TraitNode_AvailableDefault` and
  `T_Buttons_TraitNode_UnavailableDefault`, which is the state the padlock is drawn over.
  `T_Icons_Locked` is that padlock and `T_Borders_Modifier_Frame_Hexagon` the hexagon.
- **A row that states no rung already wears the right window.** Commit `36fbb73` gave it
  `T_Borders_Rounded_16`, gold at `#A38558` with its own 16px corner, no header plate and a rule
  under the title. This plan draws inside that card and does not touch it.

## File structure

| File | What happens |
|---|---|
| `src/Fellowship.SDK/Documents/FinesseDocument.cs` | Becomes `BlessingDocument.cs` holding `BlessingDocument`. |
| `src/Fellowship.SDK/Models/EntityType.cs` | `Finesse` becomes `Blessing`, in alphabetical place. |
| `src/FellowshipExtractor/Tooltip/DocumentProjection.cs` | Projects a `BlessingDocument`. |
| `src/FellowshipExtractor/Tooltip/TypeResolver.cs` | The shipped path answers `EntityType.Blessing`. |
| `src/FellowshipExtractor.Api/Models/PublicTypes.cs` | The row reads Blessings, blessing. |
| `src/FellowshipExtractor.Api/Models/CodexView.cs` | Reads a `BlessingDocument`'s placeholders. |
| `src/FellowshipExtractor.Tests/TypeResolverTests.cs` | Asserts `EntityType.Blessing`. |
| `src/FellowshipExtractor/Textures/UiCommand.cs` | Pulls the node, hexagon and padlock textures. |
| `src/FellowshipExtractor.Api/Models/ChromeAssets.cs` | Answers a node state's texture and the padlock. |
| `src/FellowshipExtractor.Api/Models/TooltipQuery.cs` | Reads and documents `node`. |
| `src/FellowshipExtractor.Api/Models/TooltipView.cs` | Answers which rows draw their own art, and the node. |
| `src/FellowshipExtractor.Api/Components/Tooltip/Tooltip.razor` | Draws the art inside the card. |
| `src/FellowshipExtractor.Api/Components/Tooltip/_Tooltip.razor.scss` | Styles the inset art and its node. |
| `src/FellowshipExtractor.Api.Tests/**` | Covers the rename, the inset art and the node states. |

---

### Task 1: A finesse is a blessing

**Files:**
- Rename: `src/Fellowship.SDK/Documents/FinesseDocument.cs` to `BlessingDocument.cs`
- Modify: `src/Fellowship.SDK/Models/EntityType.cs:11-31`
- Modify: `src/FellowshipExtractor/Tooltip/DocumentProjection.cs:202`
- Modify: `src/FellowshipExtractor/Tooltip/TypeResolver.cs:21`
- Modify: `src/FellowshipExtractor.Api/Models/PublicTypes.cs:9-31`
- Modify: `src/FellowshipExtractor.Api/Models/CodexView.cs:157`
- Modify: `src/FellowshipExtractor.Tests/TypeResolverTests.cs:34,196`
- Create: `src/FellowshipExtractor.Api.Tests/BlessingTests.cs`

**Interfaces:**
- Consumes: `EntityTypes.Slug(EntityType)`, which kebab-cases the member name;
  `EntityTypes.Document(EntityType)`, which resolves `$"{type}Document"` by reflection;
  `EntityTypes.TypeOf(EntityDocument)`, which parses the class name back to the member.
- Produces: `Fellowship.SDK.Documents.BlessingDocument` with the unchanged members
  `Description`, `Icon`, `Placeholders`, `ScalesOff`; `EntityType.Blessing`; the slug `blessing`.
  Tasks 3 and 4 both read `BlessingDocument`.

The enum member and the class name must move together: `Document` builds the class name from the
member and `TypeOf` reads the member from the class name, so renaming one alone throws at startup.
Everything else follows, because the slug is derived rather than spelled and `PublicTypes` takes its
slug from `EntityTypes.Slug`.

- [ ] **Step 1: Write the failing rename test**

`src/FellowshipExtractor.Api.Tests/BlessingTests.cs`.

```csharp
namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class BlessingTests(Export export)
{
    [Fact]
    public void The_corpus_reads_blessings_and_no_finesse_survives()
    {
        var blessings = export.Store.Entities.OfType<BlessingDocument>().ToArray();

        Assert.NotEmpty(blessings);
        Assert.All(blessings, blessing =>
            Assert.Equal(EntityType.Blessing, EntityTypes.TypeOf(blessing)));

        Assert.Equal("blessing", EntityTypes.Slug(EntityType.Blessing));
        Assert.Equal(typeof(BlessingDocument), EntityTypes.Document(EntityType.Blessing));
    }

    [Fact]
    public void A_blessing_is_printed_and_addressed_as_one()
    {
        var blessing = PublicTypes.All.Single(type => type.Type == EntityType.Blessing);

        Assert.Equal("Blessings", blessing.Label);
        Assert.Equal("blessing", blessing.Noun);
        Assert.Equal("blessing", blessing.Slug);
    }

    [Fact]
    public void Every_blessing_carries_the_art_the_build_names_a_blessing_rune()
    {
        var blessings = export.Store.Entities.OfType<BlessingDocument>().ToArray();

        Assert.All(blessings, blessing => Assert.Contains(
            "BlessingRuneIcon", blessing.Icon ?? string.Empty, StringComparison.Ordinal));
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

```bash
dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~BlessingTests"
```

Expected: the build fails on `BlessingDocument` and `EntityType.Blessing`, neither of which exists.

- [ ] **Step 3: Rename the document and the member**

Rename the file and the class:

```bash
git mv src/Fellowship.SDK/Documents/FinesseDocument.cs src/Fellowship.SDK/Documents/BlessingDocument.cs
```

`src/Fellowship.SDK/Documents/BlessingDocument.cs` in full:

```csharp
namespace Fellowship.SDK.Documents;

public sealed class BlessingDocument : EntityDocument
{
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public IReadOnlyList<Placeholder>? Placeholders { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }
}
```

In `src/Fellowship.SDK/Models/EntityType.cs`, delete the `Finesse,` line and add `Blessing,` after
`Ability,`, keeping the members alphabetical as they already are.

- [ ] **Step 4: Move the four remaining readers**

`src/FellowshipExtractor/Tooltip/DocumentProjection.cs:202`:

```csharp
        EntityType.Blessing => new BlessingDocument
```

`src/FellowshipExtractor/Tooltip/TypeResolver.cs:21`, where the shipped path stays exactly as it is
because it is the build's own spelling:

```csharp
        ("/Game/Abilities/Finesses/", EntityType.Blessing),
```

`src/FellowshipExtractor.Api/Models/CodexView.cs:157`:

```csharp
        BlessingDocument blessing => blessing.Placeholders ?? [],
```

`src/FellowshipExtractor.Api/Models/PublicTypes.cs`: delete the `EntityType.Finesse` row and add this
one immediately after the `EntityType.Ability` row, so the Abilities group stays alphabetical:

```csharp
        (EntityType.Blessing, "Blessings", "blessing", "Abilities"),
```

`src/FellowshipExtractor.Tests/TypeResolverTests.cs:34` and `:196`:

```csharp
    [InlineData("/Game/Abilities/Finesses/BasicToAoe/CAA_Finesse_BasicToAoe", EntityType.Blessing)]
```

```csharp
    [InlineData(EntityType.Blessing, 14)]
```

- [ ] **Step 5: Confirm nothing in the API or the SDK still says finesse**

```bash
grep -rn "Finesse\|finesse" src/Fellowship.SDK src/FellowshipExtractor.Api --include=*.cs --include=*.razor --include=*.scss | grep -v "/obj/\|/bin/"
```

Expected: no output at all. Then confirm the extractor keeps exactly the two the build spells:

```bash
grep -rn "Finesse" src/FellowshipExtractor --include=*.cs | grep -v "/obj/\|/bin/"
```

Expected: exactly two lines, `UiCommand.cs` naming `T_Icons_Generic_TooltipFinesse` and
`TypeResolver.cs` naming `/Game/Abilities/Finesses/`.

- [ ] **Step 6: Build**

```bash
dotnet build src/FellowshipExtractor.slnx
```

Expected: `0 Warning(s)` and `0 Error(s)`.

- [ ] **Step 7: Regenerate the export**

The export writes each row's type through `EntityTypes.Slug`, so every finesse row still reads
`"type":"finesse"` until it is rewritten. This takes about seven minutes.

```bash
FELLOWSHIP_USMAP="G:/source/Fellowship_Data_Extractor/output/fellowship.usmap" dotnet run --project src/FellowshipExtractor -- export-all
```

Then confirm the corpus moved:

```bash
grep -c '"type":"blessing"' output/export/entities.jsonl
grep -c '"type":"finesse"' output/export/entities.jsonl
```

Expected: 14 blessings and 0 finesses.

- [ ] **Step 8: Run both suites**

```bash
dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj
dotnet test src/FellowshipExtractor.Tests/FellowshipExtractor.Tests.csproj
```

Expected: every `Api.Tests` test passes. `FellowshipExtractor.Tests` carries 32 failures that predate
this work, in `TypeResolverTests` and `StatParameterTests`; the count must still be 32 and none of
them may name a blessing.

- [ ] **Step 9: Commit**

```bash
git add -A src/Fellowship.SDK src/FellowshipExtractor src/FellowshipExtractor.Api src/FellowshipExtractor.Tests src/FellowshipExtractor.Api.Tests/BlessingTests.cs
git commit -m "Call a finesse what the game calls it, a blessing"
```

---

### Task 2: Pull the node frames the tree draws

**Files:**
- Modify: `src/FellowshipExtractor/Textures/UiCommand.cs:10-50`
- Modify: `src/FellowshipExtractor.Api/Models/ChromeAssets.cs`
- Create: `src/FellowshipExtractor.Api.Tests/NodeChromeTests.cs`

**Interfaces:**
- Consumes: `ChromeAssets.Url(string blob)` and `ChromeAssets.Name(string texture)`, both private,
  which build `{container}/ui/{texture}.png`.
- Produces: `public static readonly IReadOnlyList<string> NodeStates` on `ChromeAssets`, ordered
  `selected`, `available`, `unavailable`; `public string TraitNode(string? state)`, which answers the
  selected frame for null or for a state it does not know; `public string LockGlyph`. Task 4 calls
  all three.

The tree draws a node at one of three states and the shipped textures name them, so the words this
app accepts are read off the texture names rather than chosen. `Selected` is the state a hero trait
wears when nothing is asked for, because a tooltip is showing the trait the reader is looking at.

- [ ] **Step 1: Add the textures to the extractor's list**

In `src/FellowshipExtractor/Textures/UiCommand.cs`, add to the `Chrome` array, after the
`T_Borders_RarityFrame` line:

```csharp
        "T_Buttons_TraitNode_SelectedDefault",
        "T_Buttons_TraitNode_AvailableDefault",
        "T_Buttons_TraitNode_UnavailableDefault",
        "T_Borders_Modifier_Frame_Hexagon",
        "T_Icons_Locked",
```

- [ ] **Step 2: Pull them**

```bash
FELLOWSHIP_USMAP="G:/source/Fellowship_Data_Extractor/output/fellowship.usmap" dotnet run --project src/FellowshipExtractor -- ui
```

Then confirm all five landed:

```bash
ls output/export/ui | grep -E "TraitNode|Frame_Hexagon|T_Icons_Locked"
```

Expected: five files.

- [ ] **Step 3: Write the failing chrome test**

`src/FellowshipExtractor.Api.Tests/NodeChromeTests.cs`.

```csharp
namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class NodeChromeTests(Export export)
{
    [Fact]
    public void A_node_answers_the_frame_the_state_names()
    {
        Assert.EndsWith(
            "T_Buttons_TraitNode_SelectedDefault.png", export.Chrome.TraitNode("selected"),
            StringComparison.Ordinal);

        Assert.EndsWith(
            "T_Buttons_TraitNode_AvailableDefault.png", export.Chrome.TraitNode("available"),
            StringComparison.Ordinal);

        Assert.EndsWith(
            "T_Buttons_TraitNode_UnavailableDefault.png", export.Chrome.TraitNode("unavailable"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_node_nothing_asks_for_is_the_one_the_reader_is_looking_at()
    {
        Assert.Equal(export.Chrome.TraitNode("selected"), export.Chrome.TraitNode(null));
        Assert.Equal(export.Chrome.TraitNode("selected"), export.Chrome.TraitNode("no such state"));
        Assert.Equal(export.Chrome.TraitNode("selected"), export.Chrome.TraitNode("SELECTED"));
    }

    [Fact]
    public void The_states_are_the_ones_the_build_ships_and_the_padlock_is_there()
    {
        Assert.Equal(["selected", "available", "unavailable"], ChromeAssets.NodeStates);
        Assert.EndsWith("T_Icons_Locked.png", export.Chrome.LockGlyph, StringComparison.Ordinal);
    }
}
```

`export.Chrome` does not exist on the fixture yet; Step 4 adds it.

- [ ] **Step 4: Give the fixture its chrome**

In `src/FellowshipExtractor.Api.Tests/Export.cs`, add the property beside `View` and `Tooltip`:

```csharp
    public ChromeAssets Chrome { get; }
```

and set it where the fixture already builds one, changing that line to:

```csharp
        var chrome = Chrome = new ChromeAssets(icons, paths, Store, tokens);
```

- [ ] **Step 5: Run it and watch it fail**

```bash
dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~NodeChromeTests"
```

Expected: the build fails on `TraitNode`, `NodeStates` and `LockGlyph`.

- [ ] **Step 6: Answer the node frames**

In `src/FellowshipExtractor.Api/Models/ChromeAssets.cs`, add beside the other texture constants:

```csharp
    private const string NodeFamily = "T_Buttons_TraitNode_";
    private const string NodeSuffix = "Default";
    private const string LockTexture = "T_Icons_Locked";
```

and the members, beside `SlotPlate`:

```csharp
    public static readonly IReadOnlyList<string> NodeStates = ["selected", "available", "unavailable"];

    public string TraitNode(string? state) =>
        Url(Name(NodeFamily + Chosen(state) + NodeSuffix));

    public string LockGlyph => Url(Name(LockTexture));

    private static string Chosen(string? state) =>
        NodeStates.FirstOrDefault(known => string.Equals(known, state, StringComparison.OrdinalIgnoreCase))
            is { } found
            ? char.ToUpperInvariant(found[0]) + found[1..]
            : Capitalised;

    private const string Capitalised = "Selected";
```

- [ ] **Step 7: Run it and watch it pass**

```bash
dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~NodeChromeTests"
```

Expected: PASS, all three.

- [ ] **Step 8: Build, test, commit**

```bash
dotnet build src/FellowshipExtractor.slnx
dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj
git add src/FellowshipExtractor/Textures/UiCommand.cs src/FellowshipExtractor.Api/Models/ChromeAssets.cs src/FellowshipExtractor.Api.Tests/Export.cs src/FellowshipExtractor.Api.Tests/NodeChromeTests.cs
git commit -m "Read the three states the trait tree draws a node at"
```

---

### Task 3: A blessing and a hero trait carry their own art

**Files:**
- Modify: `src/FellowshipExtractor.Api/Models/TooltipView.cs`
- Modify: `src/FellowshipExtractor.Api/Components/Tooltip/Tooltip.razor:5-17`
- Modify: `src/FellowshipExtractor.Api/Components/Tooltip/_Tooltip.razor.scss`
- Create: `src/FellowshipExtractor.Api.Tests/InsetArtTests.cs`

**Interfaces:**
- Consumes: `TooltipView.Art(EntityDocument, RarityEntry?)`, which answers the icon URL or null;
  `BlessingDocument` from Task 1.
- Produces: `public static bool DrawsItsOwnArt(EntityDocument document)` on `TooltipView`. Task 4
  draws the node behind the art this task insets.

The game draws an item, a talent, a spell, an effect and a gem trait with no art inside the tooltip,
so the app keeps drawing those as the separate plate beside the card that it draws today. A blessing
and a hero trait carry their art inside the card, centred under the title, and this task moves only
those two.

- [ ] **Step 1: Write the failing inset test**

`src/FellowshipExtractor.Api.Tests/InsetArtTests.cs`. `Tooltip` injects `TooltipView`, so register
the fixture's.

```csharp
namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class InsetArtTests : BunitContext
{
    private readonly EntityStore Store;

    public InsetArtTests(Export export)
    {
        Store = export.Store;
        Services.AddSingleton(export.Tooltip);
    }

    private IRenderedComponent<Tooltip> Draw(EntityDocument document) =>
        Render<Tooltip>(p => p
            .Add(x => x.Entity, document)
            .Add(x => x.Query, new TooltipQuery()));

    [Fact]
    public void A_blessing_and_a_hero_trait_carry_their_art_inside_the_card()
    {
        EntityDocument[] carried =
        [
            Store.Entities.OfType<BlessingDocument>().First(row => row.Icon is not null),
            Store.Entities.OfType<TraitDocument>().First(row => row.Icon is not null),
        ];

        Assert.All(carried, document =>
        {
            var drawn = Draw(document);

            Assert.NotNull(drawn.Find(".card .crest img"));
            Assert.Empty(drawn.FindAll("figure.slot"));
        });
    }

    [Fact]
    public void Every_other_row_keeps_its_art_beside_the_card()
    {
        EntityDocument[] beside =
        [
            Store.Entities.OfType<ItemDocument>().First(row => row.Icon is not null),
            Store.Entities.OfType<TalentDocument>().First(row => row.Icon is not null),
            Store.Entities.OfType<AbilityDocument>().First(row => row.Icon is not null),
            Store.Entities.OfType<EffectDocument>().First(row => row.Icon is not null),
            Store.Entities.OfType<GemTraitDocument>().First(),
        ];

        Assert.All(beside, document =>
        {
            var drawn = Draw(document);

            Assert.Empty(drawn.FindAll(".card .crest"));
        });
    }

    [Fact]
    public void The_question_is_asked_of_the_document_and_not_of_its_art()
    {
        Assert.True(TooltipView.DrawsItsOwnArt(
            Store.Entities.OfType<BlessingDocument>().First()));

        Assert.True(TooltipView.DrawsItsOwnArt(
            Store.Entities.OfType<TraitDocument>().First()));

        Assert.False(TooltipView.DrawsItsOwnArt(
            Store.Entities.OfType<ItemDocument>().First()));
    }
}
```

`GemTraitDocument` is in the second list because the game draws a gem trait with no art at all, which
is the reading the third screenshot settles.

- [ ] **Step 2: Run it and watch it fail**

```bash
dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~InsetArtTests"
```

Expected: the build fails on `TooltipView.DrawsItsOwnArt`.

- [ ] **Step 3: Answer which rows carry their art**

In `src/FellowshipExtractor.Api/Models/TooltipView.cs`, beside `Art`:

```csharp
    public static bool DrawsItsOwnArt(EntityDocument document) =>
        document is BlessingDocument or TraitDocument;
```

- [ ] **Step 4: Draw it inside the card**

`src/FellowshipExtractor.Api/Components/Tooltip/Tooltip.razor`. Read the file first: the art is a
`<figure class="slot">` sitting before `<div class="card">`, and `.fx-tip` lays the two out in a row.
Guard that figure and add the inset one as the first child of the card, after the header:

```razor
    @if (Art is { } art && !TooltipView.DrawsItsOwnArt(Entity))
    {
        <figure class="slot"><img class="art" alt="" src="@art"></figure>
    }
    <div class="card">
        <header class="head">
```

and immediately after the `</header>` line:

```razor
        @if (Art is { } crest && TooltipView.DrawsItsOwnArt(Entity))
        {
            <figure class="crest"><img alt="" src="@crest"></figure>
        }
```

- [ ] **Step 5: Style the inset art**

`src/FellowshipExtractor.Api/Components/Tooltip/_Tooltip.razor.scss`, beside the `.orn` rule. Read
the file's other rules first and match them; the crest is centred, sits under the title and is drawn
at the size the tree draws a node.

```scss
  .crest {
    position: relative;
    display: flex;
    align-items: center;
    justify-content: center;
    padding: 12px 0 4px;

    img {
      display: block;
      width: $node-size;
      height: $node-size;
      object-fit: contain;
    }
  }
```

and with the other measurements at the top of the file:

```scss
$node-size: 72px;
```

- [ ] **Step 6: Run it and watch it pass**

```bash
dotnet build src/FellowshipExtractor.slnx
dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~InsetArtTests"
```

Expected: PASS, all three, and `0 Warning(s)` from the build.

- [ ] **Step 7: Look at it**

Start Azurite and the app, which needs both because the tooltip resolves its art and chrome from blob
storage:

```bash
docker run -d --rm --name fellowship-look-azurite -p 10577:10000 mcr.microsoft.com/azure-storage/azurite:latest azurite-blob --blobHost 0.0.0.0 --skipApiVersionCheck
```

```bash
ASPNETCORE_ENVIRONMENT=Development FELLOWSHIP_EXPORT="G:/source/Fellowship_Data_Extractor/output/export" ConnectionStrings__icons="DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10577/devstoreaccount1;ContainerName=icons" ASPNETCORE_URLS="http://localhost:5199" Logging__LogLevel__Azure=Warning dotnet run --project src/FellowshipExtractor.Api --no-build --no-launch-profile
```

`ASPNETCORE_ENVIRONMENT=Development` is not optional: without it the framework's own static assets
answer 500 and the page renders unstyled.

Open a blessing at `/entity/blessing/699` and a hero trait at `/entity/trait/1`, and confirm each
draws its art centred inside the gold card with nothing beside it. Open `/entity/item/5250` and
confirm the item still draws its art on the plate beside the card. Stop the app and the container
when done, and record what you saw.

- [ ] **Step 8: Commit**

```bash
git add src/FellowshipExtractor.Api/Models/TooltipView.cs src/FellowshipExtractor.Api/Components/Tooltip/Tooltip.razor src/FellowshipExtractor.Api/Components/Tooltip/_Tooltip.razor.scss src/FellowshipExtractor.Api.Tests/InsetArtTests.cs
git commit -m "Carry a blessing's and a hero trait's art inside the card"
```

---

### Task 4: A hero trait wears the node the tree gives it

**Files:**
- Modify: `src/FellowshipExtractor.Api/Models/TooltipQuery.cs:49-107`
- Modify: `src/FellowshipExtractor.Api/Models/TooltipView.cs`
- Modify: `src/FellowshipExtractor.Api/Components/Tooltip/Tooltip.razor`
- Modify: `src/FellowshipExtractor.Api/Components/Tooltip/_Tooltip.razor.scss`
- Create: `src/FellowshipExtractor.Api.Tests/NodeStateTests.cs`

**Interfaces:**
- Consumes: `ChromeAssets.TraitNode(string?)`, `ChromeAssets.LockGlyph` and `ChromeAssets.NodeStates`
  from Task 2; `TooltipView.DrawsItsOwnArt(EntityDocument)` and the `.crest` figure from Task 3.
- Produces: `public string? Node { get; init; }` on `TooltipQuery`, read from the `node` query
  parameter; `public string? NodeFrame(EntityDocument, TooltipQuery)` and
  `public bool Locked(EntityDocument, TooltipQuery)` on `TooltipView`.

A hero trait's art sits in the hexagonal node the tree draws it in, and the tooltip shows the trait
the reader is looking at, so the node is selected unless the reader asks otherwise. A reader building
a tree asks for another state through `node=available` or `node=unavailable`, and the padlock is
drawn over the unavailable one exactly as the tree draws it. A blessing carries its art with no node,
because the tree draws blessings as runes rather than as nodes.

- [ ] **Step 1: Write the failing node test**

`src/FellowshipExtractor.Api.Tests/NodeStateTests.cs`.

```csharp
using Microsoft.AspNetCore.Http;

namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class NodeStateTests : BunitContext
{
    private readonly Export export;

    public NodeStateTests(Export export)
    {
        this.export = export;
        Services.AddSingleton(export.Tooltip);
    }

    private TraitDocument Trait => export.Store.Entities.OfType<TraitDocument>().First(t => t.Icon is not null);

    private BlessingDocument Blessing =>
        export.Store.Entities.OfType<BlessingDocument>().First(b => b.Icon is not null);

    private static TooltipQuery Asked(string? node) => new() { Node = node };

    private IRenderedComponent<Tooltip> Draw(EntityDocument document, TooltipQuery query) =>
        Render<Tooltip>(p => p.Add(x => x.Entity, document).Add(x => x.Query, query));

    [Fact]
    public void A_hero_trait_nothing_asks_about_wears_the_selected_node()
    {
        Assert.Equal(export.Chrome.TraitNode("selected"), export.Tooltip.NodeFrame(Trait, Asked(null)));
    }

    [Fact]
    public void A_reader_can_ask_for_the_state_the_tree_would_draw()
    {
        Assert.Equal(export.Chrome.TraitNode("available"), export.Tooltip.NodeFrame(Trait, Asked("available")));
        Assert.Equal(
            export.Chrome.TraitNode("unavailable"), export.Tooltip.NodeFrame(Trait, Asked("unavailable")));
    }

    [Fact]
    public void Only_the_unavailable_node_is_padlocked()
    {
        Assert.True(export.Tooltip.Locked(Trait, Asked("unavailable")));
        Assert.False(export.Tooltip.Locked(Trait, Asked("available")));
        Assert.False(export.Tooltip.Locked(Trait, Asked(null)));
    }

    [Fact]
    public void A_blessing_carries_its_rune_and_wears_no_node()
    {
        Assert.Null(export.Tooltip.NodeFrame(Blessing, Asked(null)));
        Assert.Null(export.Tooltip.NodeFrame(Blessing, Asked("unavailable")));
        Assert.False(export.Tooltip.Locked(Blessing, Asked("unavailable")));
    }

    [Fact]
    public void The_drawn_card_shows_the_node_and_its_padlock()
    {
        var padlocked = Draw(Trait, Asked("unavailable"));

        Assert.Contains(
            "T_Buttons_TraitNode_UnavailableDefault.png",
            padlocked.Find(".crest").GetAttribute("style") ?? string.Empty,
            StringComparison.Ordinal);

        Assert.NotNull(padlocked.Find(".crest .lock"));
        Assert.Empty(Draw(Trait, Asked(null)).FindAll(".crest .lock"));
        Assert.Empty(Draw(Blessing, Asked(null)).FindAll(".crest .lock"));
    }

    [Fact]
    public void The_node_a_query_string_names_reaches_the_query()
    {
        var query = TooltipQuery.Parse(
            new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                ["node"] = "available",
            }),
            export.Store.Stats,
            out var error);

        Assert.Null(error);
        Assert.Equal("available", query.Node);
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

```bash
dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~NodeStateTests"
```

Expected: the build fails on `TooltipQuery.Node`, `TooltipView.NodeFrame` and `TooltipView.Locked`.

- [ ] **Step 3: Read the node off the query**

In `src/FellowshipExtractor.Api/Models/TooltipQuery.cs`, add the property beside `Rarity`:

```csharp
    public string? Node { get; init; }
```

add its row to `Parameters`, after the `rarity` row, so the OpenAPI document describes it:

```csharp
        new("node", ApiValue.Text,
            "The state the trait tree draws this node at: selected, available or unavailable."),
```

and read it where `Parse` reads the other named values. `Parse` ends by returning `result with { ... }`
at `TooltipQuery.cs:240-255`, filling each named value through the `text` local function declared at
line 223 and passing the string ones through `Given`. Add one line to that initialiser, beside
`Rarity`:

```csharp
            Node = Given(text("node")),
```

Nothing else in `Parse` changes. Adding `node` to `Parameters` in the previous edit puts it in
`Reserved`, so the loop over unreserved keys no longer tries to resolve it as a stat name, which is
what would otherwise make `node=available` an unknown-stat error.

- [ ] **Step 4: Answer the node and the padlock**

In `src/FellowshipExtractor.Api/Models/TooltipView.cs`, beside `DrawsItsOwnArt`:

```csharp
    public string? NodeFrame(EntityDocument document, TooltipQuery query) =>
        document is TraitDocument ? chrome.TraitNode(query.Node) : null;

    public bool Locked(EntityDocument document, TooltipQuery query) =>
        document is TraitDocument &&
        string.Equals(query.Node, Unavailable, StringComparison.OrdinalIgnoreCase);

    private const string Unavailable = "unavailable";

    public string LockGlyph => chrome.LockGlyph;
```

- [ ] **Step 5: Draw the node behind the art**

`src/FellowshipExtractor.Api/Components/Tooltip/Tooltip.razor`. Replace the crest block Task 3 added
with one that carries its node as a background and its padlock as a child:

```razor
        @if (Art is { } crest && TooltipView.DrawsItsOwnArt(Entity))
        {
            <figure class="crest" style="@(Ui.NodeFrame(Entity, Query) is { } node ? $"--fx-node:url({node})" : null)">
                <img alt="" src="@crest">
                @if (Ui.Locked(Entity, Query))
                {
                    <img class="lock" alt="" src="@Ui.LockGlyph">
                }
            </figure>
        }
```

- [ ] **Step 6: Style the node and its padlock**

`src/FellowshipExtractor.Api/Components/Tooltip/_Tooltip.razor.scss`. Extend the `.crest` rule Task 3
added so the node sits behind the art at the node's own size, and the padlock sits at the top of it
the way the tree draws it:

```scss
  .crest {
    position: relative;
    display: flex;
    align-items: center;
    justify-content: center;
    padding: 12px 0 4px;

    &::before {
      content: "";
      position: absolute;
      top: 12px;
      width: $node-size;
      height: $node-size;
      background: var(--fx-node, none) 0 0/100% 100% no-repeat;
      pointer-events: none;
    }

    img {
      display: block;
      width: $node-art-size;
      height: $node-art-size;
      object-fit: contain;
    }

    .lock {
      position: absolute;
      top: 8px;
      width: $node-lock-size;
      height: $node-lock-size;
    }
  }
```

and with the other measurements at the top of the file, beside `$node-size`:

```scss
$node-art-size: 56px;
$node-lock-size: 16px;
```

- [ ] **Step 7: Run it and watch it pass**

```bash
dotnet build src/FellowshipExtractor.slnx
dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj
```

Expected: `0 Warning(s)`, `0 Error(s)` and every test passing.

- [ ] **Step 8: Look at all three states**

Start Azurite and the app as Task 3 Step 7 does, then open a hero trait at each state and confirm the
frame changes and only the last is padlocked:

- `/entity/trait/1`
- `/entity/trait/1?node=available`
- `/entity/trait/1?node=unavailable`

Confirm `/entity/blessing/699` draws its rune with no node behind it, and `/api/tooltip/trait/1?node=unavailable`
answers the padlocked fragment. Stop the app and the container, and record what you saw.

- [ ] **Step 9: Commit**

```bash
git add src/FellowshipExtractor.Api/Models/TooltipQuery.cs src/FellowshipExtractor.Api/Models/TooltipView.cs src/FellowshipExtractor.Api/Components/Tooltip/Tooltip.razor src/FellowshipExtractor.Api/Components/Tooltip/_Tooltip.razor.scss src/FellowshipExtractor.Api.Tests/NodeStateTests.cs
git commit -m "Wear the node the trait tree would draw, selected unless asked"
```

---

## Notes for whoever executes this

- `src/FellowshipExtractor.Api.Tests/Export.cs` builds one `CodexView` and one `TooltipView` over the
  real export, both with a `BlobContainerClient` pointed at `http://localhost/icons`. Task 2 Step 4
  gives it the `ChromeAssets` those two already share; do not build a second one.
- Two `Export` classes share the name. `src/FellowshipExtractor.Api.Tests/Export.cs` is the xunit
  collection fixture injected into a constructor; `src/FellowshipExtractor.Tests/Export.cs` is a
  static lazy singleton read as `Export.Loaded`. Every test this plan adds uses the first.
- `FellowshipExtractor.Api.Tests` is xunit.v3 and `FellowshipExtractor.Tests` is xunit 2. A
  cancellation token in the first comes from `Xunit.TestContext.Current.CancellationToken`.
- `FellowshipExtractor.Tests` carries 32 failures that predate this plan, measured on 2026-08-07
  against commit `9c7a0db` in a detached worktree with the same export. They are in
  `TypeResolverTests` and `StatParameterTests`. Task 1 changes two `TypeResolverTests` rows, so
  confirm the count is still 32 rather than assuming it.
- The `ui` and `export-all` commands both need mappings:
  `FELLOWSHIP_USMAP="G:/source/Fellowship_Data_Extractor/output/fellowship.usmap"`. Without it the
  extractor stops and says so.
- `IconSeeder` uploads every file in `output/export/ui` on startup and skips what is already there by
  content hash, so a texture added in Task 2 reaches the browser on the next app start with no
  further command.
- The gold window a row with no rung wears is `T_Borders_Rounded_16`, and
  `_Tooltip.razor.scss`'s `.plain` modifier carries its geometry. The crest this plan adds sits
  inside that card, so check both a blessing and a hero trait against `.plain` rather than only the
  item card.
