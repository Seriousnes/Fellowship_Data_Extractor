# Blessings and hero trait node states Implementation Plan

**Goal:** A finesse is called a blessing everywhere the app speaks, a blessing and a hero trait carry
their art inside the card the way the game draws them, and each wears the frame its own family gives
it: a hero trait one of three node states and a blessing one of two plates, at the state the reader
is looking at unless they ask for another.

**Architecture:** `EntityTypes.Document(type)` resolves a document class by the name
`$"{type}Document"` and `TypeOf` parses that name back, so renaming the enum member and the class
together carries the slug, the deserialisation and the type resolution with them and nothing else has
to be told. The tooltip already branches on whether a row states a rung; it gains one further
question, whether a row draws its own art, and the rows that answer yes answer a third, which of
their own family's states to wear.

**Tech Stack:** .NET 10, C# 14, Blazor static server rendering, xunit.v3 with bunit in
`src/FellowshipExtractor.Api.Tests`, xunit 2 in `src/FellowshipExtractor.Tests`, SCSS compiled to
`wwwroot/css/tooltip.css`, CUE4Parse in the extractor.

The three in-game screenshots this plan is measured against are a blessing (*The Mystic*), a hero
trait (*Amethyst Splinters*) and the hero trait tree, pasted by the user on 2026-08-07 and
recoverable from the session transcripts under
`~/.claude/projects/G--source-Fellowship-Data-Extractor/*.jsonl`.

## Global Constraints

- .NET 10, C# 14. `net10.0` everywhere except `Fellowship.SDK.Generators`, which stays
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
- **A blessing is drawn on a plate of its own, and its state moves two assets.** The shipped family
  is `T_Buttons_BlessingButton_Allocated` and `T_Buttons_BlessingButton_Unallocated`, each with a
  `Hover`. Allocated is a gold stone plate and unallocated a slate one. The rune moves with it: every
  blessing names `T_Icons_BlessingRuneIcon_Unallocated_NN` and the build ships
  `T_Icons_BlessingRuneIcon_Allocated_NN` beside it, fourteen of each. A hero trait differs here: its
  art is one plain asset such as `Berserker16.png` and only the frame around it changes.
- **A blessing's rune is not its plate.** `T_Icons_BlessingRuneIcon_Unallocated_010.png`, which *The
  Mystic* names, is the bare glyph on transparency. The hexagonal stone behind it in the shipped
  tooltip is the button texture, so a blessing drawn without one is drawn wrong.
- **`export-all` writes the icon a document names and no other.** Blessing rows name only their
  unallocated rune, so the allocated fourteen reach the app as chrome rather than as entity art.
- **A row that states no rung already wears the right window.** Commit `36fbb73` gave it
  `T_Borders_Rounded_16`, gold at `#A38558` with its own 16px corner and no header plate.
  `UW_BlessingTooltip` names that same window, `T_Borders_Thin` and
  `T_Borders_WindowBackground_Windowed`, so the window is settled and this plan draws inside it.
- **The rule under the title is a shipped gold texture.** `UW_BlessingTooltip` draws `T_GoldDivider`
  where commit `36fbb73` put a flat `core.secondary-600` line. Task 2 pulls it and Task 3 hangs it.

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
| `src/FellowshipExtractor/Textures/UiCommand.cs` | Pulls both state families, the allocated runes, the hexagon, the padlock and the gold divider. |
| `src/FellowshipExtractor.Api/Models/ChromeAssets.cs` | Answers each family's state texture, the allocated rune, the padlock and the divider. |
| `src/FellowshipExtractor.Api/Models/TooltipQuery.cs` | Reads and documents `node`. |
| `src/FellowshipExtractor.Api/Models/TooltipView.cs` | Answers which rows draw their own art, the state frame and the state's art. |
| `src/FellowshipExtractor.Api/Components/Tooltip/Tooltip.razor` | Draws the art inside the card, on its state's frame. |
| `src/FellowshipExtractor.Api/Components/Tooltip/_Tooltip.razor.scss` | Styles the inset art, its frame and the gold divider. |
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
- Produces, all on `ChromeAssets`: `public static readonly IReadOnlyList<string> NodeStates`, ordered
  `selected`, `available`, `unavailable`; `public static readonly IReadOnlyList<string>
  BlessingStates`, ordered `allocated`, `unallocated`; `public string TraitNode(string? state)`;
  `public string BlessingPlate(string? state)`; `public string? AllocatedRune(string? icon)`;
  `public string LockGlyph`; `public string GoldDivider`. Both state members answer their first
  listed state for null or for a word they do not know. Task 3 calls `GoldDivider` and Task 4 calls
  the rest.

The tree draws a hero trait node at one of three states and a blessing at one of two, and the shipped
textures name both sets, so the words this app accepts are read off the texture names rather than
chosen. Each family's first state is what a tooltip shows when nothing is asked for, because a
tooltip is showing the row the reader is looking at rather than one they have yet to earn: a hero
trait is `selected` and a blessing `allocated`.

A blessing's state moves its rune as well as its plate, and the two runes are named alike, so
`AllocatedRune` answers the allocated sibling of an unallocated rune's file name and null for any
other art. The allocated runes are pulled as chrome and so are addressed under `ui/`, while the
unallocated one a document names is entity art at the container root.

- [ ] **Step 1: Add the textures to the extractor's list**

In `src/FellowshipExtractor/Textures/UiCommand.cs`, add to the `Chrome` array, after the
`T_Borders_RarityFrame` line. The hover and pressed states are pulled with the rest because the
reason for holding these at all is a tree the reader can work, and `Color` and `Mask` are the layers
that family composes from:

```csharp
        "T_Buttons_TraitNode_SelectedDefault",
        "T_Buttons_TraitNode_SelectedHover",
        "T_Buttons_TraitNode_SelectedPressed",
        "T_Buttons_TraitNode_AvailableDefault",
        "T_Buttons_TraitNode_AvailableHover",
        "T_Buttons_TraitNode_UnavailableDefault",
        "T_Buttons_TraitNode_UnavailableHover",
        "T_Buttons_TraitNode_Color",
        "T_Buttons_TraitNode_Mask",

        "T_Buttons_BlessingButton_Allocated",
        "T_Buttons_BlessingButton_AllocatedHover",
        "T_Buttons_BlessingButton_Unallocated",
        "T_Buttons_BlessingButton_UnallocatedHover",

        "T_Borders_Modifier_Frame_Hexagon",
        "T_Icons_Locked",
        "T_GoldDivider",
```

The fourteen allocated runes are a family rather than a list, so pull them by their prefix rather
than by spelling each name. Add the field beside `Chrome`:

```csharp
    private static readonly string[] Families = ["T_Icons_BlessingRuneIcon_Allocated_"];
```

and widen the line in `RunAsync` that builds the texture list, which today reads
`string[] textures = [.. Chrome, .. slotIcons.Values.Order(StringComparer.Ordinal)];`:

```csharp
        var kin = provider.Files.Values
            .Select(file => Path.GetFileNameWithoutExtension(file.Name))
            .Where(name => Families.Any(family => name.StartsWith(family, StringComparison.Ordinal)))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        string[] textures = [.. Chrome, .. kin, .. slotIcons.Values.Order(StringComparer.Ordinal)];
```

Read `RunAsync` first: it already holds `provider` and builds `byName` from `provider.Files`, so
place this after `provider` is created and before the list is used.

- [ ] **Step 2: Pull them**

```bash
FELLOWSHIP_USMAP="G:/source/Fellowship_Data_Extractor/output/fellowship.usmap" dotnet run --project src/FellowshipExtractor -- ui
```

Then confirm every one landed:

```bash
ls output/export/ui | grep -cE "TraitNode"
ls output/export/ui | grep -cE "BlessingButton"
ls output/export/ui | grep -cE "BlessingRuneIcon_Allocated"
ls output/export/ui | grep -E "Frame_Hexagon|T_Icons_Locked|T_GoldDivider"
```

Expected: 9 trait nodes, 4 blessing buttons, 14 allocated runes, and the three named files.

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
        Assert.EndsWith("T_GoldDivider.png", export.Chrome.GoldDivider, StringComparison.Ordinal);
    }

    [Fact]
    public void A_blessing_answers_the_plate_its_state_names()
    {
        Assert.Equal(["allocated", "unallocated"], ChromeAssets.BlessingStates);

        Assert.EndsWith(
            "T_Buttons_BlessingButton_Allocated.png", export.Chrome.BlessingPlate("allocated"),
            StringComparison.Ordinal);

        Assert.EndsWith(
            "T_Buttons_BlessingButton_Unallocated.png", export.Chrome.BlessingPlate("unallocated"),
            StringComparison.Ordinal);

        Assert.Equal(export.Chrome.BlessingPlate("allocated"), export.Chrome.BlessingPlate(null));
    }

    [Fact]
    public void Every_blessing_rune_has_the_allocated_twin_the_build_ships()
    {
        var blessings = export.Store.Entities.OfType<BlessingDocument>().ToArray();

        Assert.NotEmpty(blessings);

        Assert.All(blessings, blessing =>
        {
            var allocated = export.Chrome.AllocatedRune(blessing.Icon);

            Assert.NotNull(allocated);
            Assert.EndsWith(
                blessing.Icon!.Replace("Unallocated", "Allocated", StringComparison.Ordinal),
                allocated,
                StringComparison.Ordinal);

            Assert.True(File.Exists(Path.Combine(
                export.Paths.Ui,
                blessing.Icon.Replace("Unallocated", "Allocated", StringComparison.Ordinal))));
        });

        Assert.Null(export.Chrome.AllocatedRune("Berserker16.png"));
        Assert.Null(export.Chrome.AllocatedRune(null));
    }
}
```

`export.Paths` does not exist on the fixture yet; Step 4 adds it beside `Chrome`.

`export.Chrome` does not exist on the fixture yet; Step 4 adds it.

- [ ] **Step 4: Give the fixture its chrome**

In `src/FellowshipExtractor.Api.Tests/Export.cs`, add both properties beside `View` and `Tooltip`:

```csharp
    public ChromeAssets Chrome { get; }

    public ExportPaths Paths { get; }
```

set the paths where the fixture already builds them, changing that line to:

```csharp
        var paths = Paths = new ExportPaths(configuration);
```

and set the chrome where the fixture already builds one, changing that line to:

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
    private const string BlessingFamily = "T_Buttons_BlessingButton_";
    private const string RuneUnallocated = "Unallocated";
    private const string RuneAllocated = "Allocated";
    private const string RuneFamily = "T_Icons_BlessingRuneIcon_";
    private const string LockTexture = "T_Icons_Locked";
    private const string DividerTexture = "T_GoldDivider";
```

and the members, beside `SlotPlate`:

```csharp
    public static readonly IReadOnlyList<string> NodeStates = ["selected", "available", "unavailable"];

    public static readonly IReadOnlyList<string> BlessingStates = ["allocated", "unallocated"];

    public string TraitNode(string? state) =>
        Url(Name(NodeFamily + Chosen(NodeStates, state) + NodeSuffix));

    public string BlessingPlate(string? state) =>
        Url(Name(BlessingFamily + Chosen(BlessingStates, state)));

    public string? AllocatedRune(string? icon) =>
        icon is not null &&
        icon.StartsWith(RuneFamily + RuneUnallocated, StringComparison.Ordinal)
            ? Url($"{Prefix}/{icon.Replace(RuneUnallocated, RuneAllocated, StringComparison.Ordinal)}")
            : null;

    public string LockGlyph => Url(Name(LockTexture));

    public string GoldDivider => Url(Name(DividerTexture));

    private static string Chosen(IReadOnlyList<string> states, string? state) =>
        states.FirstOrDefault(known => string.Equals(known, state, StringComparison.OrdinalIgnoreCase))
            is { } found
            ? char.ToUpperInvariant(found[0]) + found[1..]
            : char.ToUpperInvariant(states[0][0]) + states[0][1..];
```

`AllocatedRune` builds its blob path from the icon's file name, which already ends `.png`, so it
calls `Url` directly rather than `Name`, which would append a second extension.

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

- [ ] **Step 5: Hang the gold divider under the title**

Commit `36fbb73` ruled the title off with a flat `core.secondary-600` line, and `UW_BlessingTooltip`
draws `T_GoldDivider` there. Serve it once alongside the other shared chrome in
`src/FellowshipExtractor.Api/Models/TooltipView.cs`, in the `.{{Root}}{...}` block that already
declares `--fx-slot-plate` and `--fx-window`, by adding to that declaration:

```csharp
--fx-divider:url({{chrome.GoldDivider}});
```

Then in `src/FellowshipExtractor.Api/Components/Tooltip/_Tooltip.razor.scss`, replace the
`border-bottom` the `.plain .head` rule carries with the texture:

```scss
    .head {
      margin: 0 14px;
      border-bottom: 0;
      background: var(--fx-divider) bottom center/100% $divider-height no-repeat;
    }
```

and with the other measurements at the top of the file:

```scss
$divider-height: 3px;
```

- [ ] **Step 6: Style the inset art**

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

- [ ] **Step 7: Run it and watch it pass**

```bash
dotnet build src/FellowshipExtractor.slnx
dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~InsetArtTests"
```

Expected: PASS, all three, and `0 Warning(s)` from the build.

- [ ] **Step 8: Look at it**

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

- [ ] **Step 9: Commit**

```bash
git add src/FellowshipExtractor.Api/Models/TooltipView.cs src/FellowshipExtractor.Api/Components/Tooltip/Tooltip.razor src/FellowshipExtractor.Api/Components/Tooltip/_Tooltip.razor.scss src/FellowshipExtractor.Api.Tests/InsetArtTests.cs
git commit -m "Carry a blessing's and a hero trait's art inside the card"
```

---

### Task 4: A row that carries its art wears the state the tree draws it at

**Files:**
- Modify: `src/FellowshipExtractor.Api/Models/TooltipQuery.cs:49-107`
- Modify: `src/FellowshipExtractor.Api/Models/TooltipView.cs`
- Modify: `src/FellowshipExtractor.Api/Components/Tooltip/Tooltip.razor`
- Modify: `src/FellowshipExtractor.Api/Components/Tooltip/_Tooltip.razor.scss`
- Create: `src/FellowshipExtractor.Api.Tests/NodeStateTests.cs`

**Interfaces:**
- Consumes: `ChromeAssets.TraitNode(string?)`, `ChromeAssets.BlessingPlate(string?)`,
  `ChromeAssets.AllocatedRune(string?)`, `ChromeAssets.LockGlyph`, `ChromeAssets.NodeStates` and
  `ChromeAssets.BlessingStates` from Task 2; `TooltipView.DrawsItsOwnArt(EntityDocument)` and the
  `.crest` figure from Task 3.
- Produces: `public string? Node { get; init; }` on `TooltipQuery`, read from the `node` query
  parameter; `public string? NodeFrame(EntityDocument, TooltipQuery)`,
  `public string? StateArt(EntityDocument, TooltipQuery)` and
  `public bool Locked(EntityDocument, TooltipQuery)` on `TooltipView`.

A hero trait's art sits in the hexagonal node the tree draws it in and a blessing's rune sits on the
stone plate the hero page draws it on, and in both cases the tooltip is showing the row the reader is
looking at, so each wears its family's first state unless the reader asks otherwise: a hero trait
`selected` and a blessing `allocated`. One parameter serves both, because a row belongs to one family
and so can only be asked for a state that family names; a word the row's family does not name leaves
it at that family's first state rather than erroring, which is how `rarity` already behaves.

A blessing's state moves its rune as well as its plate, so `StateArt` answers the allocated rune when
a blessing is allocated and null everywhere else, and null means the tooltip draws the art the
document itself names. A hero trait's art never moves, so `StateArt` is null for it and only its
frame changes. The padlock is drawn over an unavailable hero trait exactly as the tree draws it, and
over nothing else.

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
    public void A_blessing_wears_its_own_plate_and_is_allocated_unless_asked()
    {
        Assert.Equal(export.Chrome.BlessingPlate("allocated"), export.Tooltip.NodeFrame(Blessing, Asked(null)));

        Assert.Equal(
            export.Chrome.BlessingPlate("unallocated"),
            export.Tooltip.NodeFrame(Blessing, Asked("unallocated")));

        Assert.False(export.Tooltip.Locked(Blessing, Asked("unallocated")));
    }

    [Fact]
    public void A_word_the_rows_family_does_not_name_leaves_it_at_the_state_it_had()
    {
        Assert.Equal(export.Chrome.BlessingPlate("allocated"), export.Tooltip.NodeFrame(Blessing, Asked("available")));
        Assert.Equal(export.Chrome.TraitNode("selected"), export.Tooltip.NodeFrame(Trait, Asked("allocated")));
    }

    [Fact]
    public void An_allocated_blessing_shows_the_allocated_rune_and_a_hero_traits_art_never_moves()
    {
        Assert.Equal(export.Chrome.AllocatedRune(Blessing.Icon), export.Tooltip.StateArt(Blessing, Asked(null)));
        Assert.Equal(
            export.Chrome.AllocatedRune(Blessing.Icon),
            export.Tooltip.StateArt(Blessing, Asked("allocated")));

        Assert.Null(export.Tooltip.StateArt(Blessing, Asked("unallocated")));
        Assert.Null(export.Tooltip.StateArt(Trait, Asked(null)));
        Assert.Null(export.Tooltip.StateArt(Trait, Asked("available")));
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

        Assert.Contains(
            "T_Buttons_BlessingButton_Allocated.png",
            Draw(Blessing, Asked(null)).Find(".crest").GetAttribute("style") ?? string.Empty,
            StringComparison.Ordinal);
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
    public string? NodeFrame(EntityDocument document, TooltipQuery query) => document switch
    {
        TraitDocument => chrome.TraitNode(query.Node),
        BlessingDocument => chrome.BlessingPlate(query.Node),
        _ => null,
    };

    public string? StateArt(EntityDocument document, TooltipQuery query) =>
        document is BlessingDocument blessing && Allocated(query)
            ? chrome.AllocatedRune(blessing.Icon)
            : null;

    public bool Locked(EntityDocument document, TooltipQuery query) =>
        document is TraitDocument &&
        string.Equals(query.Node, Unavailable, StringComparison.OrdinalIgnoreCase);

    private static bool Allocated(TooltipQuery query) =>
        !string.Equals(query.Node, Unallocated, StringComparison.OrdinalIgnoreCase);

    private const string Unavailable = "unavailable";

    private const string Unallocated = "unallocated";

    public string LockGlyph => chrome.LockGlyph;
```

`Allocated` asks whether the reader turned the blessing off rather than whether they turned it on,
because allocated is what a blessing wears when nothing is asked and when a word its family does not
name is asked, which is the same rule `ChromeAssets.BlessingPlate` follows for the plate.

- [ ] **Step 5: Draw the node behind the art**

`src/FellowshipExtractor.Api/Components/Tooltip/Tooltip.razor`. Replace the crest block Task 3 added
with one that carries its node as a background and its padlock as a child:

```razor
        @if (Art is { } crest && TooltipView.DrawsItsOwnArt(Entity))
        {
            <figure class="crest" style="@(Ui.NodeFrame(Entity, Query) is { } node ? $"--fx-node:url({node})" : null)">
                <img alt="" src="@(Ui.StateArt(Entity, Query) ?? crest)">
                @if (Ui.Locked(Entity, Query))
                {
                    <img class="lock" alt="" src="@Ui.LockGlyph">
                }
            </figure>
        }
```

`StateArt` answers null for every row whose art does not move, so the `?? crest` falls back to the
art `OnParametersSet` already resolved and only an allocated blessing swaps.

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

Then open the blessing at both of its states and confirm the plate and the rune move together, gold
at the first and slate at the second:

- `/entity/blessing/706`
- `/entity/blessing/706?node=unallocated`

Confirm `/api/tooltip/trait/1?node=unavailable` answers the padlocked fragment. Stop the app and the
container, and record what you saw. Blessing 706 is *The Mystic*, which is the row the shipped
screenshot this plan is measured against shows.

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
