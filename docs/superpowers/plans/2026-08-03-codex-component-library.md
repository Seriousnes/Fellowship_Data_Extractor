# Codex Component Library Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Express the shipped `cx-` design system as sixteen primitive components and thirteen section components, adopt them across every page of `src/FellowshipExtractor.Api`, and cover them with a bUnit test project that asserts rendered markup.

**Architecture:** Two layers. Primitives live in `Components/Cx/`, take plain values, inject nothing, and render one fixed skeleton of `cx-` markup each. Sections live in `Components/Sections/`, own exactly one `@if` block that is inlined in `Detail.razor` or `EntityPage.razor` today, read their record from a cascading parameter, and compose primitives. `Detail` re-supplies the `Entity` cascade at every nesting level; `EntityPage` supplies a second cascade of type `Bearings` carrying the page's own addresses.

**Tech Stack:** .NET 10 (`net10.0`), Blazor Web App with static server rendering, bUnit 2.8.6, xunit.v3 3.2.2, AngleSharp (through bUnit), PowerShell, Aspire AppHost with an Azurite container.

## Global Constraints

- Every command in this plan runs from the repo root `G:\source\Fellowship_Data_Extractor`, in PowerShell. Python is not installed on this machine; do not reach for it.
- Target framework `net10.0`. The installed SDK is `10.0.400-preview.0.26322.102`, so `NETSDK1057` is the one informational message a build is allowed to print.
- The repo has no `Directory.Packages.props` and no central package management. Every `PackageReference` carries its own literal `Version=` attribute.
- Exact package versions: `bunit` `2.8.6`, `xunit.v3` `3.2.2`, `Microsoft.NET.Test.Sdk` `18.8.1`, `xunit.runner.visualstudio` `3.1.5`.
- **Never anchor an edit on a line number.** A concurrent session has been rewriting `Detail.razor`, `EntityPage.razor`, `Browse.razor`, `Home.razor`, `NotFound.razor`, `CodexView.cs` and `Codex.cs`. Locate every edit site with the `Select-String` command the step gives, and anchor on CSS class names, component names, guard expressions and HTML structure.
- **The static-SSR trap.** Every page except `Omnibox.razor` is statically server rendered: `Routes.razor` declares no render mode and `Omnibox.razor` is the only file carrying `@rendermode InteractiveServer`. On a statically rendered component `@onclick`, `@onchange`, `@oninput` and `@bind` compile without error and do nothing at runtime. `data-stage`, `data-knob`, `data-choice`, `aria-pressed`, `data-tip` and the hero select's literal `onchange="location.search = '?hero=' + encodeURIComponent(this.value)"` are plain HTML attributes and are emitted verbatim in markup. Never route one through a component parameter and never convert one to a Blazor directive.
- **`@preservewhitespace` appears nowhere**, in any `.razor`, `.csproj` or `.props` file. Whitespace-only text nodes containing a newline are removed by the Razor compiler; whitespace on the same line as its neighbours is kept. That is why `@label <CountBadge …/>` renders a space and a tag on its own line does not.
- **Assertion rule.** Use `cut.MarkupMatches(expected)` for structure: it is a semantic HTML comparison that normalises insignificant whitespace, so it survives razor indentation and serialises `<br />` and `<hr />` the same on both sides. Use `Assert.Equal(expected, cut.Markup)` or `Assert.Equal(expected, cut.Find(selector).OuterHtml)` wherever **whitespace itself is the subject**: anything rendering into a `cx-prose` context (`.cx-prose` is `white-space: pre-line`, so a stray newline is a rendered blank line), and the three "no literal space before the badge/unit" cases.
- **Prose assertions are scoped to the prose element.** `Prose` and `KeyValue` with `Class="cx-prose"` are their own root, so `Assert.Equal(expected, cut.Markup)` stands. `Wording`, `Overflow` and `Ranks` also render a `<Note>` whose literal text spans several source lines and therefore carries the implementer's own indentation into `cut.Markup`; assert those three with `cut.MarkupMatches` for structure plus `Assert.Equal(expected, cut.Find("p.cx-prose").OuterHtml)` or `Assert.Equal(expected, cut.Find("span.cx-v.cx-prose").OuterHtml)` for the prose. If an exact-assertion expected string in this plan differs from what the component actually renders, **the actual output is the answer: record it and keep `Assert.Equal`.** Never relax one of these to `MarkupMatches`.
- **`cx-prose` is a generated-CSS scope name, not merely a class.** `App.razor` emits `Ui.RichTextCss("cx-prose")` and `RichText.cs` writes every style rule as `.cx-prose .rt-xxx`. The element that directly contains the rich-text `MarkupString` must itself carry `class="cx-prose"`.
- **The injection rule.** No component under `Components/Cx/` contains an `@inject` directive. Seven sections inject `CodexView` (`Wording`, `Particulars`, `StatSlots`, `Ranks`, `Overflow`, `Knobs`, `Readings`), `Knobs` also injects `EntityStore`, and the remaining six inject nothing.
- **No section component reads `Entity.Table` or `Entity.NetId`.** Those are absent from every nested row. `Detail` reads `Entity.Table` in exactly one place, `Filter(school)`, and nowhere else.
- **A component parameter takes one expression, never mixed content.** Compute every interpolated value into a local first, as the spec's call sites do.
- **Naming.** Component and member names come from the spec verbatim: terse domain nouns with no `-Component`, `-View`, `-Panel` or `-Section` suffix. Markup stays comment-free; `@code` blocks carry XML doc comments on parameters and on any choice that is not obvious from reading them.
- **Test namespace.** Every test file declares `namespace FellowshipExtractor.Api.Tests;` regardless of the folder it sits in. A namespace like `FellowshipExtractor.Api.Tests.Cx` makes `FellowshipExtractor.Api` an enclosing namespace and invites exactly the type-resolution trap the `Cx` folder name was chosen to avoid.
- **Every task ends green.** Run `dotnet build src/FellowshipExtractor.slnx` expecting `0 Warning(s)` and `0 Error(s)`, and `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj` expecting every test to pass, before committing.
- **Git.** `git add -A`, `git commit -a` and `git stash` in any form are forbidden: the working tree carries another session's uncommitted work. Every commit step lists explicit paths. `docs/` is untracked and no implementation task stages it. All commits land on the branch that is checked out when Task 1 runs; do not create or switch branches.
- Every commit message ends with the trailer `Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>`, supplied through a PowerShell here-string whose closing `'@` sits at column 0.
- **Running the app** needs Docker running and `output/export/` populated (it is, at `G:\source\Fellowship_Data_Extractor\output\export`). Start it with `aspire run` from the repo root and `FELLOWSHIP_PORT_OFFSET=0` so the URL is literally `http://localhost:5280`. Run it in a tracked background shell and stop that shell when done.

---

### Task 1: Reconcile the spec against the working tree and capture the baseline

**Files:**
- Read: `src/FellowshipExtractor.Api/Components/Shared/Detail.razor`
- Read: `src/FellowshipExtractor.Api/Components/Pages/EntityPage.razor`
- Read: `src/FellowshipExtractor.Api/Components/Pages/Browse.razor`
- Read: `src/FellowshipExtractor.Api/Components/Pages/Home.razor`
- Read: `src/FellowshipExtractor.Api/Components/Pages/NotFound.razor`
- Read: `src/FellowshipExtractor.Api/Components/_Imports.razor`
- Create: `C:\Users\Sean\AppData\Local\Temp\claude\G--source-Fellowship-Data-Extractor\2109da8e-31a6-4995-a9b0-5036194d1636\scratchpad\baseline\*.html`
- Create: `C:\Users\Sean\AppData\Local\Temp\claude\G--source-Fellowship-Data-Extractor\2109da8e-31a6-4995-a9b0-5036194d1636\scratchpad\reconciliation.md`

**Interfaces:**
- Produces: a written reconciliation note listing, for each of the thirteen sections named in the spec, whether its guard and head still exist in the working tree; and eight baseline HTML files that Task 22 diffs against. Nothing in the repository changes.

**The reconciliation rule, stated plainly.** If a section named in the spec has gone from the working tree, **its task is dropped, not resurrected**: strike the task from the plan and record the strike in the note. If a section has appeared that the spec does not name, **add a new task modelled on its nearest neighbour** in this plan: same file layout, same guard-false and guard-true test pair, same assertion rule. If a guard or head text has changed wording, the section keeps its task and the new wording is the one implemented and asserted.

- [ ] **Step 1: Confirm the tree builds before anything is measured**

Run: `dotnet build src/FellowshipExtractor.slnx --no-incremental`
Expected: `0 Warning(s)`, `0 Error(s)`, plus `NETSDK1057`.

- [ ] **Step 2: List the panels each consuming page still emits**

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components/Shared/Detail.razor,src/FellowshipExtractor.Api/Components/Pages/EntityPage.razor,src/FellowshipExtractor.Api/Components/Pages/Browse.razor,src/FellowshipExtractor.Api/Components/Pages/Home.razor,src/FellowshipExtractor.Api/Components/Pages/NotFound.razor -Pattern 'cx-panel-head|class="cx-panel"|cx-rail cx-panel'
```

- [ ] **Step 3: Confirm each of the eight `Detail` sections by its guard**

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components/Shared/Detail.razor -Pattern 'Entity\.Description is not null|Facts\.Count > 0|Entity\.Stats is|Entity\.RankRuns is|Entity\.GemOverflow is|Entity\.IconInheritedFrom is|Entity\.Grants is|Entity\.LegendaryStats is'
```

Expected: eight matches, one per section, in the order `Wording`, `Particulars`, `StatSlots`, `Ranks`, `Overflow`, `BorrowedArt`, `Grants`, `Legendaries`. Record any that is missing.

- [ ] **Step 4: Confirm each of the five `EntityPage` sections by its anchor**

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components/Pages/EntityPage.razor -Pattern 'data-stage|cx-knob|>Identity<|Every reading|More like this'
```

Expected: `data-stage` once (`Stage`), `cx-knob` four times (`Knobs`), `>Identity<` once (`Identity`), `Every reading` once (`Readings`), `More like this` once (`Nearby`).

- [ ] **Step 5: Confirm the members the sections and pages will read still exist**

```powershell
Select-String -Path src/FellowshipExtractor.Api/CodexView.cs -Pattern 'public .*(Facts|Addresses|Tip|Href|Reading|Name|Text|HeroColour)\('
Select-String -Path src/FellowshipExtractor.Api/PublicTypes.cs -Pattern 'public static .*(NameOf|SlugOf|TableOf|Label)\('
Select-String -Path src/FellowshipExtractor.Api/Codex.cs -Pattern 'public static string (Spaced|Number)\(|public Facet\? Facet\('
```

Expected: `Ui.Facts`, `Ui.Addresses`, `Ui.Tip`, `Ui.Href`, `Ui.Reading`, `Ui.Name`, `Ui.Text`, `Ui.HeroColour`; `PublicTypes.NameOf`, `SlugOf`, `TableOf`, `Label`; `Codex.Spaced`, `Codex.Number`, `Codex.Facet`. Record any that has been renamed and use the new name throughout.

- [ ] **Step 6: Read the current `_Imports.razor` so the two new `@using` lines land in the alphabetical run**

Run: `Get-Content src/FellowshipExtractor.Api/Components/_Imports.razor`
Expected: a list of `@using` lines including `@using FellowshipExtractor.Api.Components.Shared`. Note where `Cx` and `Sections` sort into it.

- [ ] **Step 7: Confirm no page has grown a Blazor event directive**

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components -Recurse -Pattern '@onclick|@onchange|@oninput|@bind|@preservewhitespace'
```

Expected: matches only in `Components/Layout/Omnibox.razor`. Any match elsewhere is a dead control that must be reported in the note before the refactor starts.

- [ ] **Step 8: Confirm every class the primitive catalogue hardcodes still has a CSS rule**

Tasks 3 to 9 write exact markup for sixteen primitives. That markup is only correct while `codex.css` still styles the classes it carries, and `codex.css` is one of the files the concurrent session edits: it has already deleted `table.cx-rows .cx-tag`, which had no razor user.

```powershell
$classes = 'cx-panel','cx-panel-head','cx-num','cx-pills','cx-kv','cx-k','cx-v','cx-miss',
           'cx-facts','cx-unit','cx-rule','cx-sub-head','cx-note','cx-empty','cx-chip',
           'cx-knob','cx-rungs','cx-prose','cx-stat-lines','cx-pairs','cx-linked'

foreach ($c in $classes) {
    $hit = Select-String -Path src/FellowshipExtractor.Api/wwwroot/css/codex.css -Pattern "\.$c\b" -Quiet
    if (-not $hit) { "MISSING RULE: $c" }
}
```

Expected: no output. A class named here with no rule left in `codex.css` means the design system moved under the spec; record it in the note and correct that primitive's markup in its own task rather than emitting a class that styles nothing.

Also diff the stylesheet against the revision the spec was written from, so a changed rule is seen rather than inferred:

```powershell
git diff --stat HEAD -- src/FellowshipExtractor.Api/wwwroot/css/codex.css
```

- [ ] **Step 9: Find the three entity addresses the baseline needs**

```powershell
$rows = Get-Content G:\source\Fellowship_Data_Extractor\output\export\entities.jsonl

function Address($row) {
    $o = $row | ConvertFrom-Json
    $hit = Select-String -Path G:\source\Fellowship_Data_Extractor\src\FellowshipExtractor.Api\PublicTypes.cs -Pattern "new PublicType\(""$($o.table)"", ""([^""]+)""" | Select-Object -First 1
    "/entity/$($hit.Matches[0].Groups[1].Value)/$($o.netId)"
}

$legendary = Address ($rows | Where-Object { $_ -match '"legendaryStats"' } | Select-Object -First 1)
$ranks     = Address ($rows | Where-Object { $_ -match '"rankRuns"' }       | Select-Object -First 1)
$knobs     = Address ($rows | Where-Object { $_ -match '"stats"' -and $_ -match '"heroes"' } | Select-Object -First 1)

$legendary; $ranks; $knobs
```

Expected: three addresses of the form `/entity/items/5146`. Write all three into the reconciliation note, because Task 22 visits the same three.

- [ ] **Step 10: Start the app in a tracked background shell**

```powershell
$env:FELLOWSHIP_PORT_OFFSET = "0"
aspire run
```

Run it in the background. Docker must be running. Then wait for the app:

```powershell
while (-not (Test-NetConnection -ComputerName localhost -Port 5280 -InformationLevel Quiet)) { }
(Invoke-WebRequest -Uri http://localhost:5280/health -UseBasicParsing).StatusCode
```

Expected: `200`.

- [ ] **Step 11: Capture the eight baseline pages**

```powershell
$baseline = "C:\Users\Sean\AppData\Local\Temp\claude\G--source-Fellowship-Data-Extractor\2109da8e-31a6-4995-a9b0-5036194d1636\scratchpad\baseline"
New-Item -ItemType Directory -Force $baseline | Out-Null

$pages = [ordered]@{
    "home.html"             = "/"
    "browse.html"           = "/browse"
    "browse-grouped.html"   = "/browse?group=slot"
    "not-found.html"        = "/not-found"
    "entity-knobs.html"     = $knobs
    "entity-legendary.html" = $legendary
    "entity-rankruns.html"  = $ranks
    "entity-unknown.html"   = "/entity/items/999999"
}

foreach ($name in $pages.Keys) {
    (Invoke-WebRequest -Uri ("http://localhost:5280" + $pages[$name]) -UseBasicParsing).Content |
        Set-Content -Path (Join-Path $baseline $name) -Encoding utf8
}

Get-ChildItem $baseline | Select-Object Name, Length
```

Expected: eight files, each non-empty.

- [ ] **Step 12: Look at the three entity pages in a browser and note what the tests cannot see**

Open `http://localhost:5280` plus each of the three addresses. Confirm and write down: the hero select, the four rarity rungs row, the item level and tempered inputs; that clicking a rung repaints the tooltip stage; that changing a number repaints it; that the Printed row shows its values separated by ` / ` on one line with no blank lines and with coloured rich-text runs.

- [ ] **Step 13: Stop the background shell running `aspire run`**

Terminate the tracked background shell. Confirm with:

```powershell
Test-NetConnection -ComputerName localhost -Port 5280 -InformationLevel Quiet
```

Expected: `False`.

- [ ] **Step 14: Write the reconciliation note**

Write `C:\Users\Sean\AppData\Local\Temp\claude\G--source-Fellowship-Data-Extractor\2109da8e-31a6-4995-a9b0-5036194d1636\scratchpad\reconciliation.md` containing: the thirteen section names with present/absent and any changed guard or head wording; any section found in the pages that the spec does not name, with the neighbour its new task is modelled on; any renamed `CodexView`, `PublicTypes` or `Codex` member; the three entity addresses; and the eight baseline file names.

- [ ] **Step 15: Apply the reconciliation to this plan**

Strike the task of any absent section. Add a task for any new section, modelled on its nearest neighbour. Update every head string, guard expression and member name in the remaining tasks to the wording the note records. Nothing is committed by this task.

---

### Task 2: The bUnit test project

**Files:**
- Create: `src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`
- Create: `src/FellowshipExtractor.Api.Tests/GlobalUsings.cs`
- Create: `src/FellowshipExtractor.Api.Tests/_Imports.razor`
- Create: `src/FellowshipExtractor.Api.Tests/Harness.razor`
- Create: `src/FellowshipExtractor.Api.Tests/HarnessTests.cs`
- Modify: `src/FellowshipExtractor.slnx`

**Interfaces:**
- Produces: a test project that `dotnet test` runs. Every later task adds `.cs` test files to it and asserts through `BunitContext`, `Render<T>`, `p.Add`, `p.AddChildContent`, `p.AddCascadingValue`, `cut.Render`, `cut.Markup`, `cut.MarkupMatches`, `cut.Find` and `cut.FindAll`.

**How the components become visible to the test project.** `FellowshipExtractor.Api` is a `Microsoft.NET.Sdk.Web` project. A `ProjectReference` to it from the test project works normally: the Razor SDK compiles each `.razor` file to a `public partial class` in the Api assembly, named after the file and namespaced as the root namespace plus the folder path. So `Components/Shared/Detail.razor` is `FellowshipExtractor.Api.Components.Shared.Detail`, and a `using` of that namespace makes `Render<Detail>` bind to the generic component overload. Without the `using`, `Render` binds to the `RenderFragment` overload instead and produces `CS0246` plus a cascade of overload-resolution errors. The test project itself uses `Microsoft.NET.Sdk.Razor` so that its own `.razor` fixtures are compiled; `Microsoft.NET.Sdk` leaves them out of the compilation with no error.

**`GlobalUsings.cs` grows in two later steps.** A `global using` naming a namespace that contains no types is `CS0246`, so `FellowshipExtractor.Api.Components.Cx` is added in Task 3 when `Cx/` gains its first component and `FellowshipExtractor.Api.Components.Sections` in Task 10.

- [ ] **Step 1: Write the csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <ProjectReference Include="..\FellowshipExtractor.Api\FellowshipExtractor.Api.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="bunit" Version="2.8.6" />
    <PackageReference Include="xunit.v3" Version="3.2.2" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.8.1" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Write the global usings**

`src/FellowshipExtractor.Api.Tests/GlobalUsings.cs`:

```csharp
global using AngleSharp.Dom;
global using Bunit;
global using FellowshipExtractor.Api;
global using FellowshipExtractor.Api.Components.Shared;
global using FellowshipExtractor.Tooltip;
global using Xunit;
```

- [ ] **Step 3: Write the test project's `_Imports.razor`**

```razor
@using Microsoft.AspNetCore.Components
@using Microsoft.AspNetCore.Components.Web
@using FellowshipExtractor.Api
@using FellowshipExtractor.Api.Components.Shared
@using FellowshipExtractor.Tooltip
```

- [ ] **Step 4: Write the razor canary fixture**

`src/FellowshipExtractor.Api.Tests/Harness.razor`:

```razor
<p class="cx-probe">@Text</p>

@code {
    /// <summary>
    /// What the canary draws. The fixture exists so that a build which stops compiling the test
    /// project's own razor files fails a test rather than passing silently.
    /// </summary>
    [Parameter] public string Text { get; set; } = string.Empty;
}
```

- [ ] **Step 5: Write the harness test**

`src/FellowshipExtractor.Api.Tests/HarnessTests.cs`:

```csharp
namespace FellowshipExtractor.Api.Tests;

public class HarnessTests : BunitContext
{
    [Fact]
    public void Renders_a_razor_fixture_from_the_test_project()
    {
        var label = PublicTypes.Label("ItemID");

        var cut = Render<Harness>(p => p.Add(x => x.Text, label));

        Assert.Equal($"<p class=\"cx-probe\">{label}</p>", cut.Markup);
    }
}
```

- [ ] **Step 6: Add the project to the solution**

`src/FellowshipExtractor.slnx` becomes:

```xml
<Solution>
  <Project Path="FellowshipExtractor/FellowshipExtractor.csproj" />
  <Project Path="FellowshipExtractor.Api/FellowshipExtractor.Api.csproj" />
  <Project Path="FellowshipExtractor.AppHost/FellowshipExtractor.AppHost.csproj" />
  <Project Path="FellowshipExtractor.ServiceDefaults/FellowshipExtractor.ServiceDefaults.csproj" />
  <Project Path="FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj" />
</Solution>
```

- [ ] **Step 7: Restore and build**

Run: `dotnet build src/FellowshipExtractor.slnx`
Expected: `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 8: Run the harness test**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~HarnessTests"`
Expected: PASS, 1 test.

- [ ] **Step 9: Commit**

```powershell
git add src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj src/FellowshipExtractor.Api.Tests/GlobalUsings.cs src/FellowshipExtractor.Api.Tests/_Imports.razor src/FellowshipExtractor.Api.Tests/Harness.razor src/FellowshipExtractor.Api.Tests/HarnessTests.cs src/FellowshipExtractor.slnx
git commit -m @'
test: add the bUnit test project

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 3: `CountBadge`, `Rule`, `SubHead`, `Note` and `Icon`

**Files:**
- Create: `src/FellowshipExtractor.Api/Components/Cx/CountBadge.razor`
- Create: `src/FellowshipExtractor.Api/Components/Cx/Rule.razor`
- Create: `src/FellowshipExtractor.Api/Components/Cx/SubHead.razor`
- Create: `src/FellowshipExtractor.Api/Components/Cx/Note.razor`
- Create: `src/FellowshipExtractor.Api/Components/Cx/Icon.razor`
- Modify: `src/FellowshipExtractor.Api/Components/_Imports.razor`
- Modify: `src/FellowshipExtractor.Api.Tests/GlobalUsings.cs`
- Test: `src/FellowshipExtractor.Api.Tests/Cx/CountBadgeTests.cs`, `RuleTests.cs`, `SubHeadTests.cs`, `NoteTests.cs`, `IconTests.cs`

**Interfaces:**
- Produces: `CountBadge(string Text)`, `Rule()`, `SubHead(RenderFragment? ChildContent)`, `Note(RenderFragment? ChildContent)`, `Icon(string Src)`. `Panel` and `Pill` render `CountBadge` internally in Tasks 4 and 5.

These five are the design system's fixed skeletons: each renders exactly one element with no conditional inside it, so they form one reviewer gate.

- [ ] **Step 1: Add the folder's `@using` to the app's imports**

Add to `src/FellowshipExtractor.Api/Components/_Imports.razor`, in the existing alphabetical run of `FellowshipExtractor.Api.*` lines, immediately after `@using FellowshipExtractor.Api.Components`:

```razor
@using FellowshipExtractor.Api.Components.Cx
```

- [ ] **Step 2: Add the folder's `global using` to the test project**

Add to `src/FellowshipExtractor.Api.Tests/GlobalUsings.cs`, after the `FellowshipExtractor.Api` line:

```csharp
global using FellowshipExtractor.Api.Components.Cx;
```

- [ ] **Step 3: Write the five failing tests**

`src/FellowshipExtractor.Api.Tests/Cx/CountBadgeTests.cs`:

```csharp
namespace FellowshipExtractor.Api.Tests;

public class CountBadgeTests : BunitContext
{
    [Fact]
    public void Renders_a_num_span()
    {
        var cut = Render<CountBadge>(p => p.Add(x => x.Text, "12"));

        cut.MarkupMatches("<span class=\"cx-num\">12</span>");
    }

    [Fact]
    public void Renders_text_that_is_not_a_count()
    {
        var cut = Render<CountBadge>(p => p.Add(x => x.Text, "Cloth / Intellect"));

        cut.MarkupMatches("<span class=\"cx-num\">Cloth / Intellect</span>");
    }
}
```

`src/FellowshipExtractor.Api.Tests/Cx/RuleTests.cs`:

```csharp
namespace FellowshipExtractor.Api.Tests;

public class RuleTests : BunitContext
{
    [Fact]
    public void Renders_a_rule()
    {
        var cut = Render<Rule>();

        cut.MarkupMatches("<hr class=\"cx-rule\" />");
    }
}
```

`src/FellowshipExtractor.Api.Tests/Cx/SubHeadTests.cs`:

```csharp
namespace FellowshipExtractor.Api.Tests;

public class SubHeadTests : BunitContext
{
    [Fact]
    public void Renders_a_sub_head_paragraph()
    {
        var cut = Render<SubHead>(p => p.AddChildContent("Heroes"));

        cut.MarkupMatches("<p class=\"cx-sub-head\">Heroes</p>");
    }

    [Fact]
    public void Renders_composed_child_content()
    {
        var cut = Render<SubHead>(p => p.AddChildContent("Set (4 pieces)"));

        cut.MarkupMatches("<p class=\"cx-sub-head\">Set (4 pieces)</p>");
    }
}
```

`src/FellowshipExtractor.Api.Tests/Cx/NoteTests.cs`:

```csharp
namespace FellowshipExtractor.Api.Tests;

public class NoteTests : BunitContext
{
    [Fact]
    public void Renders_a_note_paragraph()
    {
        var cut = Render<Note>(p => p.AddChildContent("A slot's weight is its share of the budget."));

        cut.MarkupMatches("<p class=\"cx-note\">A slot's weight is its share of the budget.</p>");
    }

    [Fact]
    public void Renders_inline_markup_inside_the_note()
    {
        var cut = Render<Note>(p => p.AddChildContent("belongs to <a href=\"/entity/items/12\" data-tip=\"/api/tooltip/items/12\">Old Boots</a>"));

        cut.MarkupMatches("<p class=\"cx-note\">belongs to <a href=\"/entity/items/12\" data-tip=\"/api/tooltip/items/12\">Old Boots</a></p>");
    }
}
```

`src/FellowshipExtractor.Api.Tests/Cx/IconTests.cs`:

```csharp
namespace FellowshipExtractor.Api.Tests;

public class IconTests : BunitContext
{
    [Fact]
    public void Renders_a_lazy_image_with_an_empty_alt()
    {
        var cut = Render<Icon>(p => p.Add(x => x.Src, "http://localhost/icons/boots.png"));

        cut.MarkupMatches("<img alt=\"\" loading=\"lazy\" src=\"http://localhost/icons/boots.png\" />");
    }
}
```

- [ ] **Step 4: Run the tests to verify they fail**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`
Expected: build FAILS with `CS0246` on `CountBadge`, `Rule`, `SubHead`, `Note` and `Icon`.

- [ ] **Step 5: Write `CountBadge.razor`**

```razor
<span class="cx-num">@Text</span>

@code {
    /// <summary>
    /// What the badge reads. A string rather than a number because four of the twelve call sites
    /// carry a name or a composed phrase rather than a count.
    /// </summary>
    [Parameter, EditorRequired] public string Text { get; set; } = string.Empty;
}
```

- [ ] **Step 6: Write `Rule.razor`**

```razor
<hr class="cx-rule" />
```

- [ ] **Step 7: Write `SubHead.razor`**

```razor
<p class="cx-sub-head">@ChildContent</p>

@code {
    /// <summary>
    /// The sub-head's text. Child content rather than a string because two call sites compose it
    /// from an expression: the school count picks singular or plural, and a set trails its piece
    /// count.
    /// </summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }
}
```

- [ ] **Step 8: Write `Note.razor`**

```razor
<p class="cx-note">@ChildContent</p>

@code {
    /// <summary>
    /// The note's prose. Child content rather than a string because the borrowed-art note carries
    /// an inline anchor.
    /// </summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }
}
```

- [ ] **Step 9: Write `Icon.razor`**

```razor
<img alt="" loading="lazy" src="@Src" />

@code {
    /// <summary>The image address, already resolved by the caller.</summary>
    [Parameter, EditorRequired] public string Src { get; set; } = string.Empty;
}
```

- [ ] **Step 10: Run the tests to verify they pass**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`
Expected: PASS, 9 tests.

- [ ] **Step 11: Build the solution clean**

Run: `dotnet build src/FellowshipExtractor.slnx`
Expected: `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 12: Commit**

```powershell
git add src/FellowshipExtractor.Api/Components/Cx/CountBadge.razor src/FellowshipExtractor.Api/Components/Cx/Rule.razor src/FellowshipExtractor.Api/Components/Cx/SubHead.razor src/FellowshipExtractor.Api/Components/Cx/Note.razor src/FellowshipExtractor.Api/Components/Cx/Icon.razor src/FellowshipExtractor.Api/Components/_Imports.razor src/FellowshipExtractor.Api.Tests/GlobalUsings.cs src/FellowshipExtractor.Api.Tests/Cx/CountBadgeTests.cs src/FellowshipExtractor.Api.Tests/Cx/RuleTests.cs src/FellowshipExtractor.Api.Tests/Cx/SubHeadTests.cs src/FellowshipExtractor.Api.Tests/Cx/NoteTests.cs src/FellowshipExtractor.Api.Tests/Cx/IconTests.cs
git commit -m @'
feat: add the cx design system's fixed skeletons

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 4: `Panel`

**Files:**
- Create: `src/FellowshipExtractor.Api/Components/Cx/Panel.razor`
- Test: `src/FellowshipExtractor.Api.Tests/Cx/PanelTests.cs`

**Interfaces:**
- Consumes: `CountBadge(string Text)` from Task 3.
- Produces: `Panel(string? Head, string? Badge, string? Class, string As = "div", string? Aria, RenderFragment? ChildContent)`. Every section in Tasks 10 to 18 and every panel in Tasks 19 to 21 renders it.

`cx-panel` is always the outermost class, `cx-panel-head` is always the first child of it, and the badge is always the last child of the head, because `.cx-panel-head .cx-num { margin-left: auto }` is what pushes it to the right edge.

- [ ] **Step 1: Write the failing tests**

`src/FellowshipExtractor.Api.Tests/Cx/PanelTests.cs`:

```csharp
namespace FellowshipExtractor.Api.Tests;

public class PanelTests : BunitContext
{
    [Fact]
    public void Renders_a_headless_panel()
    {
        var cut = Render<Panel>(p => p.AddChildContent("<p>body</p>"));

        cut.MarkupMatches("<div class=\"cx-panel\"><p>body</p></div>");
    }

    [Fact]
    public void Renders_a_head_with_no_badge()
    {
        var cut = Render<Panel>(p => p.Add(x => x.Head, "Identity").AddChildContent("<p>body</p>"));

        cut.MarkupMatches("<div class=\"cx-panel\"><div class=\"cx-panel-head\">Identity</div><p>body</p></div>");
    }

    [Fact]
    public void Renders_a_head_with_a_badge_and_no_space_before_it()
    {
        var cut = Render<Panel>(p => p.Add(x => x.Head, "Stat slots").Add(x => x.Badge, "3"));

        Assert.Equal(
            "<div class=\"cx-panel\"><div class=\"cx-panel-head\">Stat slots<span class=\"cx-num\">3</span></div></div>",
            cut.Markup);
    }

    [Fact]
    public void Renders_the_badge_as_the_last_child_of_the_head()
    {
        var cut = Render<Panel>(p => p.Add(x => x.Head, "Legendary stats").Add(x => x.Badge, "2"));

        var head = cut.Find("div.cx-panel-head");

        Assert.Contains("cx-num", head.LastElementChild!.ClassList);
    }

    [Fact]
    public void Renders_an_aside_with_an_extra_class_and_an_aria_label()
    {
        var cut = Render<Panel>(p => p
            .Add(x => x.As, "aside")
            .Add(x => x.Class, "cx-rail")
            .Add(x => x.Aria, "Filters")
            .Add(x => x.Head, "Refine"));

        cut.MarkupMatches(
            "<aside class=\"cx-panel cx-rail\" aria-label=\"Filters\">" +
            "<div class=\"cx-panel-head\">Refine</div></aside>");
    }

    [Fact]
    public void Renders_a_details_root_with_a_summary_head()
    {
        var cut = Render<Panel>(p => p
            .Add(x => x.As, "details")
            .Add(x => x.Head, "The exported record")
            .AddChildContent("<pre>{}</pre>"));

        cut.MarkupMatches(
            "<details class=\"cx-panel\">" +
            "<summary class=\"cx-panel-head\">The exported record</summary><pre>{}</pre></details>");
    }

    [Fact]
    public void Omits_the_aria_label_when_none_is_given()
    {
        var cut = Render<Panel>(p => p.Add(x => x.Head, "Details"));

        Assert.False(cut.Find("div.cx-panel").HasAttribute("aria-label"));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~PanelTests"`
Expected: build FAILS with `CS0246` on `Panel`.

- [ ] **Step 3: Write `Panel.razor`**

```razor
@if (As == "details")
{
    <details class="@Classes" aria-label="@Aria">
        @if (Head is not null)
        {
            <summary class="cx-panel-head">
                @Head
                @if (Badge is not null)
                {
                    <CountBadge Text="@Badge" />
                }
            </summary>
        }
        @ChildContent
    </details>
}
else if (As == "aside")
{
    <aside class="@Classes" aria-label="@Aria">
        @if (Head is not null)
        {
            <div class="cx-panel-head">
                @Head
                @if (Badge is not null)
                {
                    <CountBadge Text="@Badge" />
                }
            </div>
        }
        @ChildContent
    </aside>
}
else
{
    <div class="@Classes" aria-label="@Aria">
        @if (Head is not null)
        {
            <div class="cx-panel-head">
                @Head
                @if (Badge is not null)
                {
                    <CountBadge Text="@Badge" />
                }
            </div>
        }
        @ChildContent
    </div>
}

@code {
    /// <summary>The head's text. Null renders no head element at all.</summary>
    [Parameter] public string? Head { get; set; }

    /// <summary>
    /// The count badge trailing the head. A string rather than a number: one call site names the
    /// granted ability and another reads "12 heroes".
    /// </summary>
    [Parameter] public string? Badge { get; set; }

    /// <summary>Extra classes, appended after cx-panel.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>The root element: "div", "aside" or "details".</summary>
    [Parameter] public string As { get; set; } = "div";

    /// <summary>The aria-label. Null omits the attribute.</summary>
    [Parameter] public string? Aria { get; set; }

    [Parameter] public RenderFragment? ChildContent { get; set; }

    private string Classes => Class is null ? "cx-panel" : $"cx-panel {Class}";
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~PanelTests"`
Expected: PASS, 7 tests.

- [ ] **Step 5: Build the solution and confirm the nullable flow is clean**

Run: `dotnet build src/FellowshipExtractor.slnx`
Expected: `0 Warning(s)`, `0 Error(s)`. `Badge` is `string?` and `CountBadge.Text` is `string`; the `Badge is not null` guard sits in the same generated method as the parameter write, so no nullable warning is emitted. A warning here means the guard and the write have been separated and must be put back together.

- [ ] **Step 6: Run the whole suite**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`
Expected: PASS, 16 tests.

- [ ] **Step 7: Commit**

```powershell
git add src/FellowshipExtractor.Api/Components/Cx/Panel.razor src/FellowshipExtractor.Api.Tests/Cx/PanelTests.cs
git commit -m @'
feat: add the Panel primitive

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 5: `Pills` and `Pill`

**Files:**
- Create: `src/FellowshipExtractor.Api/Components/Cx/Pills.razor`
- Create: `src/FellowshipExtractor.Api/Components/Cx/Pill.razor`
- Test: `src/FellowshipExtractor.Api.Tests/Cx/PillsTests.cs`, `src/FellowshipExtractor.Api.Tests/Cx/PillTests.cs`

**Interfaces:**
- Consumes: `CountBadge(string Text)` from Task 3.
- Produces: `Pills(RenderFragment? ChildContent)` and `Pill(string? Href, string? Colour, string? Badge, RenderFragment? ChildContent)`.

`Pills` takes child content rather than an items list because the "Offered to" list is one `<ul>` fed by two collections: `Entity.AllowedRoles` first, then `Entity.AllowedAttributes`.

- [ ] **Step 1: Write the failing tests**

`src/FellowshipExtractor.Api.Tests/Cx/PillsTests.cs`:

```csharp
namespace FellowshipExtractor.Api.Tests;

public class PillsTests : BunitContext
{
    [Fact]
    public void Wraps_children_in_a_pills_list()
    {
        var cut = Render<Pills>(p => p.AddChildContent("<li><span>Fire</span></li>"));

        cut.MarkupMatches("<ul class=\"cx-pills\"><li><span>Fire</span></li></ul>");
    }

    [Fact]
    public void Holds_items_from_two_sources_in_one_list()
    {
        var cut = Render<Pills>(p => p.AddChildContent(
            "<li><span>Tank</span></li><li><span>Strength</span></li>"));

        Assert.Equal(2, cut.FindAll("ul.cx-pills > li").Count);
    }
}
```

`src/FellowshipExtractor.Api.Tests/Cx/PillTests.cs`:

```csharp
namespace FellowshipExtractor.Api.Tests;

public class PillTests : BunitContext
{
    [Fact]
    public void Renders_a_span_when_there_is_no_destination()
    {
        var cut = Render<Pill>(p => p.AddChildContent("Stat Crit"));

        cut.MarkupMatches("<li><span>Stat Crit</span></li>");
    }

    [Fact]
    public void Renders_an_anchor_when_there_is_a_destination()
    {
        var cut = Render<Pill>(p => p.Add(x => x.Href, "/browse?set=Ironhide").AddChildContent("Ironhide"));

        cut.MarkupMatches("<li><a href=\"/browse?set=Ironhide\">Ironhide</a></li>");
    }

    [Fact]
    public void Omits_the_style_attribute_when_there_is_no_colour()
    {
        var cut = Render<Pill>(p => p.AddChildContent("Fire"));

        Assert.False(cut.Find("li > span").HasAttribute("style"));
    }

    [Fact]
    public void Writes_the_colour_it_is_given_including_inherit()
    {
        var cut = Render<Pill>(p => p.Add(x => x.Colour, "inherit").AddChildContent("Tank"));

        cut.MarkupMatches("<li><span style=\"color:inherit\">Tank</span></li>");
    }

    [Fact]
    public void Renders_the_badge_with_no_space_before_it()
    {
        var cut = Render<Pill>(p => p
            .Add(x => x.Href, "/entity/items/5146")
            .Add(x => x.Badge, "Cloth / Intellect")
            .AddChildContent("Webbed Robe"));

        Assert.Equal(
            "<li><a href=\"/entity/items/5146\">Webbed Robe<span class=\"cx-num\">Cloth / Intellect</span></a></li>",
            cut.Markup);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~PillTests|FullyQualifiedName~PillsTests"`
Expected: build FAILS with `CS0246` on `Pills` and `Pill`.

- [ ] **Step 3: Write `Pills.razor`**

```razor
<ul class="cx-pills">
    @ChildContent
</ul>

@code {
    /// <summary>
    /// The list's items. Child content rather than a collection because the "Offered to" list is
    /// one list fed by two collections, roles first and then attributes.
    /// </summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }
}
```

- [ ] **Step 4: Write `Pill.razor`**

```razor
@if (Href is null)
{
    <li>
        <span style="@Tone">
            @ChildContent
            @if (Badge is not null)
            {
                <CountBadge Text="@Badge" />
            }
        </span>
    </li>
}
else
{
    <li>
        <a href="@Href" style="@Tone">
            @ChildContent
            @if (Badge is not null)
            {
                <CountBadge Text="@Badge" />
            }
        </a>
    </li>
}

@code {
    /// <summary>Where the pill leads. Null renders a span rather than an anchor.</summary>
    [Parameter] public string? Href { get; set; }

    /// <summary>
    /// The ink colour. Null omits the style attribute entirely, which is how the hero pills fall
    /// back to the stylesheet; a caller wanting the panel's own ink passes "inherit".
    /// </summary>
    [Parameter] public string? Colour { get; set; }

    /// <summary>The count badge trailing the label.</summary>
    [Parameter] public string? Badge { get; set; }

    [Parameter] public RenderFragment? ChildContent { get; set; }

    private string? Tone => Colour is null ? null : $"color:{Colour}";
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~PillTests|FullyQualifiedName~PillsTests"`
Expected: PASS, 7 tests.

- [ ] **Step 6: Build and run the whole suite**

Run: `dotnet build src/FellowshipExtractor.slnx`
Expected: `0 Warning(s)`, `0 Error(s)`.

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`
Expected: PASS, 23 tests.

- [ ] **Step 7: Commit**

```powershell
git add src/FellowshipExtractor.Api/Components/Cx/Pills.razor src/FellowshipExtractor.Api/Components/Cx/Pill.razor src/FellowshipExtractor.Api.Tests/Cx/PillsTests.cs src/FellowshipExtractor.Api.Tests/Cx/PillTests.cs
git commit -m @'
feat: add the Pills and Pill primitives

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 6: `Facts` and `Fact`

**Files:**
- Create: `src/FellowshipExtractor.Api/Components/Cx/Facts.razor`
- Create: `src/FellowshipExtractor.Api/Components/Cx/Fact.razor`
- Test: `src/FellowshipExtractor.Api.Tests/Cx/FactsTests.cs`, `src/FellowshipExtractor.Api.Tests/Cx/FactTests.cs`

**Interfaces:**
- Produces: `Facts(RenderFragment? ChildContent)` and `Fact(string Label, string? Unit, RenderFragment? ChildContent)`.

The rows are bare `<tr>` children of `<table>` with no `<thead>` and no `<tbody>`, which is what both existing tables emit and what `.cx-facts tr` and `.cx-facts th` select. `Fact` is asserted on `cut.Markup` rather than through `cut.Find`, because a `<tr>` at the top of a parsed fragment is not reliably addressable and the raw string is exactly what is under test anyway.

- [ ] **Step 1: Write the failing tests**

`src/FellowshipExtractor.Api.Tests/Cx/FactsTests.cs`:

```csharp
namespace FellowshipExtractor.Api.Tests;

public class FactsTests : BunitContext
{
    [Fact]
    public void Renders_a_facts_table()
    {
        var cut = Render<Facts>(p => p.AddChildContent("<tr><th>Type</th><td>Items</td></tr>"));

        cut.MarkupMatches("<table class=\"cx-facts\"><tr><th>Type</th><td>Items</td></tr></table>");
    }

    [Fact]
    public void Renders_an_empty_table_when_it_has_no_rows()
    {
        var cut = Render<Facts>();

        Assert.Equal("<table class=\"cx-facts\"></table>", cut.Markup);
    }
}
```

`src/FellowshipExtractor.Api.Tests/Cx/FactTests.cs`:

```csharp
namespace FellowshipExtractor.Api.Tests;

public class FactTests : BunitContext
{
    [Fact]
    public void Renders_a_label_cell_and_a_value_cell()
    {
        var cut = Render<Fact>(p => p.Add(x => x.Label, "Type").AddChildContent("Items"));

        Assert.Equal("<tr><th>Type</th><td>Items</td></tr>", cut.Markup);
    }

    [Fact]
    public void Renders_the_unit_with_no_space_before_it()
    {
        var cut = Render<Fact>(p => p.Add(x => x.Label, "Cooldown").Add(x => x.Unit, "s").AddChildContent("2.5"));

        Assert.Equal("<tr><th>Cooldown</th><td>2.5<span class=\"cx-unit\">s</span></td></tr>", cut.Markup);
    }

    [Fact]
    public void Renders_no_unit_span_when_there_is_no_unit()
    {
        var cut = Render<Fact>(p => p.Add(x => x.Label, "Network id").AddChildContent("5146"));

        Assert.DoesNotContain("cx-unit", cut.Markup);
    }

    [Fact]
    public void Renders_a_linked_value()
    {
        var cut = Render<Fact>(p => p
            .Add(x => x.Label, "Resource")
            .AddChildContent("<a href=\"/browse?resource=Mana\" data-tip=\"/api/tooltip/effect/9\">Mana</a>"));

        Assert.Equal(
            "<tr><th>Resource</th><td><a href=\"/browse?resource=Mana\" data-tip=\"/api/tooltip/effect/9\">Mana</a></td></tr>",
            cut.Markup);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~FactTests|FullyQualifiedName~FactsTests"`
Expected: build FAILS with `CS0246` on `Facts` and `Fact`.

- [ ] **Step 3: Write `Facts.razor`**

```razor
<table class="cx-facts">
    @ChildContent
</table>

@code {
    /// <summary>
    /// The rows, as bare table rows. The table carries no head or body section because that is what
    /// the stylesheet selects and what both existing tables emit, and it renders empty rather than
    /// not at all so the two-column pairs grid keeps its second column.
    /// </summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }
}
```

- [ ] **Step 4: Write `Fact.razor`**

```razor
<tr>
    <th>@Label</th>
    <td>
        @ChildContent
        @if (Unit is not null)
        {
            <span class="cx-unit">@Unit</span>
        }
    </td>
</tr>

@code {
    /// <summary>The row's heading cell.</summary>
    [Parameter, EditorRequired] public string Label { get; set; } = string.Empty;

    /// <summary>
    /// The unit trailing the value, drawn with no space before it: a duration reads 2.5s. Null
    /// renders no span at all.
    /// </summary>
    [Parameter] public string? Unit { get; set; }

    [Parameter] public RenderFragment? ChildContent { get; set; }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~FactTests|FullyQualifiedName~FactsTests"`
Expected: PASS, 6 tests.

- [ ] **Step 6: Build and run the whole suite**

Run: `dotnet build src/FellowshipExtractor.slnx`
Expected: `0 Warning(s)`, `0 Error(s)`.

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`
Expected: PASS, 29 tests.

- [ ] **Step 7: Commit**

```powershell
git add src/FellowshipExtractor.Api/Components/Cx/Facts.razor src/FellowshipExtractor.Api/Components/Cx/Fact.razor src/FellowshipExtractor.Api.Tests/Cx/FactsTests.cs src/FellowshipExtractor.Api.Tests/Cx/FactTests.cs
git commit -m @'
feat: add the Facts and Fact primitives

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 7: `KeyValues` and `KeyValue`

**Files:**
- Create: `src/FellowshipExtractor.Api/Components/Cx/KeyValues.razor`
- Create: `src/FellowshipExtractor.Api/Components/Cx/KeyValue.razor`
- Test: `src/FellowshipExtractor.Api.Tests/Cx/KeyValuesTests.cs`, `src/FellowshipExtractor.Api.Tests/Cx/KeyValueTests.cs`

**Interfaces:**
- Produces: `KeyValues(RenderFragment? ChildContent)` and `KeyValue(string Key, bool Missing, string? Class, RenderFragment? ChildContent)`.

`KeyValue`'s markup is one line with no literal text anywhere in it, because one of its callers renders into a `.cx-prose` context where a newline is a rendered line break.

- [ ] **Step 1: Write the failing tests**

`src/FellowshipExtractor.Api.Tests/Cx/KeyValuesTests.cs`:

```csharp
namespace FellowshipExtractor.Api.Tests;

public class KeyValuesTests : BunitContext
{
    [Fact]
    public void Wraps_children_in_a_key_value_list()
    {
        var cut = Render<KeyValues>(p => p.AddChildContent(
            "<li><span class=\"cx-k\">Printed</span><span class=\"cx-v\">10</span></li>"));

        cut.MarkupMatches(
            "<ul class=\"cx-kv\"><li><span class=\"cx-k\">Printed</span><span class=\"cx-v\">10</span></li></ul>");
    }
}
```

`src/FellowshipExtractor.Api.Tests/Cx/KeyValueTests.cs`:

```csharp
namespace FellowshipExtractor.Api.Tests;

public class KeyValueTests : BunitContext
{
    [Fact]
    public void Renders_a_key_and_a_value_on_one_line()
    {
        var cut = Render<KeyValue>(p => p.Add(x => x.Key, "Arithmetic").AddChildContent("Add over 5 ticks"));

        Assert.Equal(
            "<li><span class=\"cx-k\">Arithmetic</span><span class=\"cx-v\">Add over 5 ticks</span></li>",
            cut.Markup);
    }

    [Fact]
    public void Marks_a_missing_value()
    {
        var cut = Render<KeyValue>(p => p
            .Add(x => x.Key, "Note")
            .Add(x => x.Missing, true)
            .AddChildContent("printed and computed values disagree"));

        Assert.Equal(
            "<li><span class=\"cx-k\">Note</span><span class=\"cx-v cx-miss\">printed and computed values disagree</span></li>",
            cut.Markup);
    }

    [Fact]
    public void Renders_a_prose_value_span_with_no_whitespace_of_its_own()
    {
        var cut = Render<KeyValue>(p => p
            .Add(x => x.Key, "Printed")
            .Add(x => x.Class, "cx-prose")
            .AddChildContent("10<span> / </span>20"));

        Assert.Equal(
            "<li><span class=\"cx-k\">Printed</span><span class=\"cx-v cx-prose\">10<span> / </span>20</span></li>",
            cut.Markup);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~KeyValueTests|FullyQualifiedName~KeyValuesTests"`
Expected: build FAILS with `CS0246` on `KeyValues` and `KeyValue`.

- [ ] **Step 3: Write `KeyValues.razor`**

```razor
<ul class="cx-kv">
    @ChildContent
</ul>

@code {
    /// <summary>The rows.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }
}
```

- [ ] **Step 4: Write `KeyValue.razor`**

The markup is deliberately one line and carries no literal text: the rank-progression caller renders it inside a `white-space: pre-line` scope, where any newline this component contributed would become a blank line on screen.

```razor
<li><span class="cx-k">@Key</span><span class="@Classes">@ChildContent</span></li>

@code {
    /// <summary>The key span's text.</summary>
    [Parameter, EditorRequired] public string Key { get; set; } = string.Empty;

    /// <summary>Marks the value as absent or disputed rather than present.</summary>
    [Parameter] public bool Missing { get; set; }

    /// <summary>
    /// Extra classes on the value span. The rank-progression "Printed" row passes cx-prose, which
    /// is the generated style scope the game's rich text is written under.
    /// </summary>
    [Parameter] public string? Class { get; set; }

    [Parameter] public RenderFragment? ChildContent { get; set; }

    private string Classes =>
        "cx-v" + (Class is null ? null : " " + Class) + (Missing ? " cx-miss" : null);
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~KeyValueTests|FullyQualifiedName~KeyValuesTests"`
Expected: PASS, 4 tests.

- [ ] **Step 6: Build and run the whole suite**

Run: `dotnet build src/FellowshipExtractor.slnx`
Expected: `0 Warning(s)`, `0 Error(s)`.

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`
Expected: PASS, 33 tests.

- [ ] **Step 7: Commit**

```powershell
git add src/FellowshipExtractor.Api/Components/Cx/KeyValues.razor src/FellowshipExtractor.Api/Components/Cx/KeyValue.razor src/FellowshipExtractor.Api.Tests/Cx/KeyValuesTests.cs src/FellowshipExtractor.Api.Tests/Cx/KeyValueTests.cs
git commit -m @'
feat: add the KeyValues and KeyValue primitives

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 8: `Prose` and `Empty`

**Files:**
- Create: `src/FellowshipExtractor.Api/Components/Cx/Prose.razor`
- Create: `src/FellowshipExtractor.Api/Components/Cx/Empty.razor`
- Test: `src/FellowshipExtractor.Api.Tests/Cx/ProseTests.cs`, `src/FellowshipExtractor.Api.Tests/Cx/EmptyTests.cs`

**Interfaces:**
- Produces: `Prose(RenderFragment? ChildContent)` and `Empty(string? Said, RenderFragment? ChildContent)`.

`Prose` is the one primitive whose whole job is a contract: the element that directly contains the rich-text `MarkupString` must itself carry `class="cx-prose"`, because `App.razor` emits `Ui.RichTextCss("cx-prose")` and every rich-text style rule is written as `.cx-prose .rt-xxx`. Its markup is one line and contains no literal text.

- [ ] **Step 1: Write the failing tests**

`src/FellowshipExtractor.Api.Tests/Cx/ProseTests.cs`:

```csharp
namespace FellowshipExtractor.Api.Tests;

public class ProseTests : BunitContext
{
    [Fact]
    public void Renders_two_words_with_exactly_one_space_and_no_newline()
    {
        var cut = Render<Prose>(p => p.AddChildContent("one two"));

        Assert.Equal("<p class=\"cx-prose\">one two</p>", cut.Markup);
    }

    [Fact]
    public void Renders_a_rich_text_run_as_a_direct_child_of_the_prose_element()
    {
        var cut = Render<Prose>(p => p.AddChildContent(
            "grants you <span class=\"rt rt-warning\">+3%</span> haste"));

        var run = cut.Find("span.rt-warning");

        Assert.Equal("P", run.ParentElement!.TagName);
        Assert.Contains("cx-prose", run.ParentElement.ClassList);
    }

    [Fact]
    public void Keeps_a_newline_the_game_text_carries()
    {
        var cut = Render<Prose>(p => p.AddChildContent("first\nsecond"));

        Assert.Equal("<p class=\"cx-prose\">first\nsecond</p>", cut.Markup);
    }
}
```

`src/FellowshipExtractor.Api.Tests/Cx/EmptyTests.cs`:

```csharp
namespace FellowshipExtractor.Api.Tests;

public class EmptyTests : BunitContext
{
    [Fact]
    public void Renders_a_headline_and_a_detail_line()
    {
        var cut = Render<Empty>(p => p
            .Add(x => x.Said, "Nothing in the build answers that.")
            .AddChildContent("Remove a filter and try again."));

        cut.MarkupMatches(
            "<p class=\"cx-empty\"><b>Nothing in the build answers that.</b><br />" +
            "Remove a filter and try again.</p>");
    }

    [Fact]
    public void Renders_neither_the_headline_nor_its_break_when_none_is_said()
    {
        var cut = Render<Empty>(p => p.AddChildContent("Loading..."));

        Assert.Equal("<p class=\"cx-empty\">Loading...</p>", cut.Markup);
    }

    [Fact]
    public void Renders_a_link_as_the_detail_line()
    {
        var cut = Render<Empty>(p => p
            .Add(x => x.Said, "No such id.")
            .AddChildContent("<a href=\"/browse\">Browse everything</a>"));

        cut.MarkupMatches(
            "<p class=\"cx-empty\"><b>No such id.</b><br /><a href=\"/browse\">Browse everything</a></p>");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~ProseTests|FullyQualifiedName~EmptyTests"`
Expected: build FAILS with `CS0246` on `Prose` and `Empty`.

- [ ] **Step 3: Write `Prose.razor`**

The markup is one line and contains no literal text. `codex.css` sets `.cx-prose { white-space: pre-line }`, so a newline this component contributed would be a rendered line break.

```razor
<p class="cx-prose">@ChildContent</p>

@code {
    /// <summary>
    /// The rendered description. The paragraph itself carries cx-prose because that class is the
    /// scope every generated rich-text rule is written under, so a run nested one level deeper
    /// draws unstyled with no error.
    /// </summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }
}
```

- [ ] **Step 4: Write `Empty.razor`**

```razor
<p class="cx-empty">
    @if (Said is not null)
    {
        <b>@Said</b><br />
    }
    @ChildContent
</p>

@code {
    /// <summary>The bold headline and its line break. Null renders neither.</summary>
    [Parameter] public string? Said { get; set; }

    /// <summary>The detail line under the headline.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~ProseTests|FullyQualifiedName~EmptyTests"`
Expected: PASS, 6 tests. If an `Assert.Equal` here reports a difference, the rendered string is the answer: correct the expected string and keep `Assert.Equal`.

- [ ] **Step 6: Build and run the whole suite**

Run: `dotnet build src/FellowshipExtractor.slnx`
Expected: `0 Warning(s)`, `0 Error(s)`.

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`
Expected: PASS, 39 tests.

- [ ] **Step 7: Commit**

```powershell
git add src/FellowshipExtractor.Api/Components/Cx/Prose.razor src/FellowshipExtractor.Api/Components/Cx/Empty.razor src/FellowshipExtractor.Api.Tests/Cx/ProseTests.cs src/FellowshipExtractor.Api.Tests/Cx/EmptyTests.cs
git commit -m @'
feat: add the Prose and Empty primitives

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 9: `Chip` and `Knob`

**Files:**
- Create: `src/FellowshipExtractor.Api/Components/Cx/Chip.razor`
- Create: `src/FellowshipExtractor.Api/Components/Cx/Knob.razor`
- Test: `src/FellowshipExtractor.Api.Tests/Cx/ChipTests.cs`, `src/FellowshipExtractor.Api.Tests/Cx/KnobTests.cs`

**Interfaces:**
- Produces: `Chip(string? Colour, RenderFragment? ChildContent)` and `Knob(string Caption, string As = "label", RenderFragment? ChildContent)`.

A chip's entire appearance comes from `table.cx-rows .cx-chip`, so it is rendered only inside the browse results table. A knob's element genuinely varies: a `<label>` wrapping one form control is correct HTML and gives the caption its click target, and a `<label>` wrapping a group of buttons is not.

- [ ] **Step 1: Write the failing tests**

`src/FellowshipExtractor.Api.Tests/Cx/ChipTests.cs`:

```csharp
namespace FellowshipExtractor.Api.Tests;

public class ChipTests : BunitContext
{
    [Fact]
    public void Renders_a_chip_with_no_colour()
    {
        var cut = Render<Chip>(p => p.AddChildContent("Cloth"));

        cut.MarkupMatches("<span class=\"cx-chip\">Cloth</span>");
    }

    [Fact]
    public void Renders_a_chip_with_a_colour()
    {
        var cut = Render<Chip>(p => p.Add(x => x.Colour, "#8AC6FF").AddChildContent("Ardeos"));

        cut.MarkupMatches("<span class=\"cx-chip\" style=\"color:#8AC6FF\">Ardeos</span>");
    }

    [Fact]
    public void Omits_the_style_attribute_when_there_is_no_colour()
    {
        var cut = Render<Chip>(p => p.AddChildContent("+2"));

        Assert.False(cut.Find("span.cx-chip").HasAttribute("style"));
    }
}
```

`src/FellowshipExtractor.Api.Tests/Cx/KnobTests.cs`:

```csharp
namespace FellowshipExtractor.Api.Tests;

public class KnobTests : BunitContext
{
    [Fact]
    public void Renders_a_label_around_one_control()
    {
        var cut = Render<Knob>(p => p
            .Add(x => x.Caption, "Item level")
            .AddChildContent("<input type=\"number\" data-knob=\"itemLevel\" />"));

        cut.MarkupMatches(
            "<label class=\"cx-knob\"><span>Item level</span>" +
            "<input type=\"number\" data-knob=\"itemLevel\" /></label>");
    }

    [Fact]
    public void Renders_a_div_around_a_button_group()
    {
        var cut = Render<Knob>(p => p
            .Add(x => x.Caption, "Rarity")
            .Add(x => x.As, "div")
            .AddChildContent("<div class=\"cx-rungs\"></div>"));

        cut.MarkupMatches(
            "<div class=\"cx-knob\"><span>Rarity</span><div class=\"cx-rungs\"></div></div>");
    }

    [Fact]
    public void Renders_the_caption_as_the_first_child()
    {
        var cut = Render<Knob>(p => p.Add(x => x.Caption, "Hero").AddChildContent("<select></select>"));

        Assert.Equal("SPAN", cut.Find(".cx-knob").FirstElementChild!.TagName);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~ChipTests|FullyQualifiedName~KnobTests"`
Expected: build FAILS with `CS0246` on `Chip` and `Knob`.

- [ ] **Step 3: Write `Chip.razor`**

```razor
<span class="cx-chip" style="@Tone">@ChildContent</span>

@code {
    /// <summary>The ink colour. Null omits the style attribute.</summary>
    [Parameter] public string? Colour { get; set; }

    /// <summary>
    /// The chip's label. The chip's border, padding and size come from table.cx-rows .cx-chip
    /// alone, so it is drawn inside the browse results table and nowhere else.
    /// </summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    private string? Tone => Colour is null ? null : $"color:{Colour}";
}
```

- [ ] **Step 4: Write `Knob.razor`**

```razor
@if (As == "div")
{
    <div class="cx-knob">
        <span>@Caption</span>
        @ChildContent
    </div>
}
else
{
    <label class="cx-knob">
        <span>@Caption</span>
        @ChildContent
    </label>
}

@code {
    /// <summary>The caption, which .cx-knob > span styles.</summary>
    [Parameter, EditorRequired] public string Caption { get; set; } = string.Empty;

    /// <summary>
    /// "label" when the knob holds one form control, which gives the caption its click target, and
    /// "div" when it holds a button group, which a label may not wrap.
    /// </summary>
    [Parameter] public string As { get; set; } = "label";

    [Parameter] public RenderFragment? ChildContent { get; set; }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~ChipTests|FullyQualifiedName~KnobTests"`
Expected: PASS, 6 tests.

- [ ] **Step 6: Build and confirm no primitive injects anything**

Run: `dotnet build src/FellowshipExtractor.slnx`
Expected: `0 Warning(s)`, `0 Error(s)`.

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components/Cx -Recurse -Pattern '@inject'
```

Expected: no matches. Sixteen files now exist under `Components/Cx/`:

```powershell
(Get-ChildItem src/FellowshipExtractor.Api/Components/Cx -Filter *.razor).Count
```

Expected: `16`.

- [ ] **Step 7: Run the whole suite**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`
Expected: PASS, 45 tests.

- [ ] **Step 8: Commit**

```powershell
git add src/FellowshipExtractor.Api/Components/Cx/Chip.razor src/FellowshipExtractor.Api/Components/Cx/Knob.razor src/FellowshipExtractor.Api.Tests/Cx/ChipTests.cs src/FellowshipExtractor.Api.Tests/Cx/KnobTests.cs
git commit -m @'
feat: add the Chip and Knob primitives

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 10: The export fixture and `Wording`

**Files:**
- Create: `src/FellowshipExtractor.Api/Components/Sections/Wording.razor`
- Create: `src/FellowshipExtractor.Api.Tests/Export.cs`
- Create: `src/FellowshipExtractor.Api.Tests/Corpus.cs`
- Modify: `src/FellowshipExtractor.Api/Components/_Imports.razor`
- Modify: `src/FellowshipExtractor.Api.Tests/GlobalUsings.cs`
- Test: `src/FellowshipExtractor.Api.Tests/Sections/WordingTests.cs`

**Interfaces:**
- Consumes: `Panel(string? Head, …)` from Task 4, `Prose(RenderFragment?)` from Task 8, `Rule()` from Task 3.
- Produces: `Export` with `CodexView View` and `EntityStore Store`; `Corpus.Name`, the collection every fixture-taking test class opts into; and `Wording`, a section reading `[CascadingParameter] private Entity Entity`.

**Why a collection fixture rather than a class fixture.** Spec section 8.4 says the `CodexView` helper is shared through an xUnit class fixture. A class fixture is scoped to one test class and eight test classes need this one instance, so `[CollectionDefinition]` plus `ICollectionFixture<Export>` is the mechanism used: the 4 MB `entities.jsonl` is then parsed once per run rather than once per class. Everything else about section 8.4 is implemented exactly as written. Tests of the sixteen primitives and of the six sections that inject nothing take no fixture and do no I/O.

- [ ] **Step 1: Add the folder's `@using` to the app's imports**

Add to `src/FellowshipExtractor.Api/Components/_Imports.razor`, in the alphabetical run, immediately after `@using FellowshipExtractor.Api.Components.Pages`:

```razor
@using FellowshipExtractor.Api.Components.Sections
```

- [ ] **Step 2: Add the folder's `global using` to the test project**

Add to `src/FellowshipExtractor.Api.Tests/GlobalUsings.cs`, after the `Components.Cx` line:

```csharp
global using FellowshipExtractor.Api.Components.Sections;
```

- [ ] **Step 3: Write the export fixture**

`src/FellowshipExtractor.Api.Tests/Export.cs`:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace FellowshipExtractor.Api.Tests;

/// <summary>
/// A CodexView over the export the app itself reads, built once for the whole run. Chrome and the
/// blob client are left null: they are reached only by CodexView.Icon and CodexView.Chrome, no
/// section draws art, and constructing ChromeAssets measures the PNG corpus in the export's ui
/// folder.
/// </summary>
public sealed class Export
{
    public CodexView View { get; }

    public EntityStore Store { get; }

    public Export()
    {
        var folder = Environment.GetEnvironmentVariable("FELLOWSHIP_EXPORT") ?? Found();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["FELLOWSHIP_EXPORT"] = folder })
            .Build();

        var paths = new ExportPaths(configuration);

        Store = new EntityStore(paths, NullLogger<EntityStore>.Instance);

        var tokens = new DesignTokens(Store.Vocabulary.Colors, Store.Vocabulary.TextStyles);
        var richText = new RichTextRenderer(Store.Vocabulary.TextStyles);

        View = new CodexView(Store, new Codex(Store, tokens), tokens, chrome: null!, richText,
            new Placeholders(Store, richText), icons: null!);
    }

    /// <summary>
    /// The repo's own export, found by walking up from the test binary. The same folder the AppHost
    /// hands the API, so a section is tested against the corpus it renders in the browser rather
    /// than against a second copy of the shapes that could drift from it.
    /// </summary>
    private static string Found()
    {
        for (var here = new DirectoryInfo(AppContext.BaseDirectory); here is not null; here = here.Parent)
        {
            var export = Path.Combine(here.FullName, "output", "export");

            if (File.Exists(Path.Combine(export, "entities.jsonl"))) return export;
        }

        throw new InvalidOperationException(
            "No output/export above the test binary and FELLOWSHIP_EXPORT is not set. It names the " +
            "folder the extractor's export-all command wrote, which is the same folder the app reads.");
    }
}
```

- [ ] **Step 4: Write the collection definition**

`src/FellowshipExtractor.Api.Tests/Corpus.cs`:

```csharp
namespace FellowshipExtractor.Api.Tests;

/// <summary>
/// The collection that shares one <see cref="Export"/> across every class needing a real CodexView,
/// so the entity stream is read once per run rather than once per class.
/// </summary>
[CollectionDefinition(Name)]
public sealed class Corpus : ICollectionFixture<Export>
{
    public const string Name = "corpus";
}
```

- [ ] **Step 5: Write the failing tests**

`src/FellowshipExtractor.Api.Tests/Sections/WordingTests.cs`:

```csharp
namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class WordingTests : BunitContext
{
    public WordingTests(Export export) => Services.AddSingleton(export.View);

    [Fact]
    public void Renders_nothing_without_text()
    {
        var cut = Render<Wording>(p => p.AddCascadingValue(new Entity()));

        Assert.Empty(cut.Markup);
    }

    [Fact]
    public void Renders_the_description_in_a_text_panel()
    {
        var cut = Render<Wording>(p => p.AddCascadingValue(new Entity { Description = "one two" }));

        cut.MarkupMatches(
            "<div class=\"cx-panel\"><div class=\"cx-panel-head\">Text</div>" +
            "<p class=\"cx-prose\">one two</p></div>");
    }

    [Fact]
    public void Renders_the_description_with_exactly_one_space_and_no_newline()
    {
        var cut = Render<Wording>(p => p.AddCascadingValue(new Entity { Description = "one two" }));

        Assert.Equal("<p class=\"cx-prose\">one two</p>", cut.Find("p.cx-prose").OuterHtml);
    }

    [Fact]
    public void Renders_a_leading_rule_before_each_detail_line()
    {
        var entity = new Entity { Description = "head", Detail = ["first", "second"] };

        var cut = Render<Wording>(p => p.AddCascadingValue(entity));

        Assert.Equal(3, cut.FindAll("p.cx-prose").Count);
        Assert.Equal(2, cut.FindAll("hr.cx-rule").Count);
    }

    [Fact]
    public void Renders_detail_lines_with_no_description()
    {
        var cut = Render<Wording>(p => p.AddCascadingValue(new Entity { Detail = ["only"] }));

        Assert.Single(cut.FindAll("p.cx-prose"));
        Assert.Equal("<p class=\"cx-prose\">only</p>", cut.Find("p.cx-prose").OuterHtml);
    }

    [Fact]
    public void Renders_a_rich_text_run_as_a_direct_child_of_the_prose_element()
    {
        var entity = new Entity { Description = "grants you <rt.warning>+3%</> haste" };

        var cut = Render<Wording>(p => p.AddCascadingValue(entity));

        var run = cut.Find("p.cx-prose span.rt");

        Assert.Equal("P", run.ParentElement!.TagName);
        Assert.Contains("cx-prose", run.ParentElement.ClassList);
    }
}
```

- [ ] **Step 6: Run the tests to verify they fail**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~WordingTests"`
Expected: build FAILS with `CS0246` on `Wording`.

- [ ] **Step 7: Write `Wording.razor`**

```razor
@inject CodexView Ui

@if (Entity.Description is not null || Entity.Detail is { Count: > 0 })
{
    <Panel Head="Text">
        @if (Entity.Description is not null)
        {
            <Prose>@Ui.Text(Entity.Description, Entity.Placeholders, Entity.Tags)</Prose>
        }
        @foreach (var line in Entity.Detail ?? [])
        {
            <Rule />
            <Prose>@Ui.Text(line, Entity.Placeholders, Entity.Tags)</Prose>
        }
    </Panel>
}

@code {
    /// <summary>
    /// The record this panel draws, re-supplied by every Detail so a nested row draws its own text.
    /// </summary>
    [CascadingParameter] private Entity Entity { get; set; } = new();
}
```

The `?? []` on the loop is kept: the guard is an `||`, so the loop must tolerate a null `Detail` when `Description` alone is set.

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~WordingTests"`
Expected: PASS, 6 tests. If an `Assert.Equal` reports a difference, the rendered string is the answer: correct the expected string and keep `Assert.Equal`.

- [ ] **Step 9: Build and run the whole suite**

Run: `dotnet build src/FellowshipExtractor.slnx`
Expected: `0 Warning(s)`, `0 Error(s)`.

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`
Expected: PASS, 51 tests.

- [ ] **Step 10: Commit**

```powershell
git add src/FellowshipExtractor.Api/Components/Sections/Wording.razor src/FellowshipExtractor.Api/Components/_Imports.razor src/FellowshipExtractor.Api.Tests/GlobalUsings.cs src/FellowshipExtractor.Api.Tests/Export.cs src/FellowshipExtractor.Api.Tests/Corpus.cs src/FellowshipExtractor.Api.Tests/Sections/WordingTests.cs
git commit -m @'
feat: add the Wording section and the export test fixture

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 11: `Particulars`

**Files:**
- Create: `src/FellowshipExtractor.Api/Components/Sections/Particulars.razor`
- Test: `src/FellowshipExtractor.Api.Tests/Sections/ParticularsTests.cs`

**Interfaces:**
- Consumes: `Panel`, `Facts`, `Fact`, `Rule`, `SubHead`, `Pills`, `Pill` from Tasks 3 to 6; `Export` and `Corpus.Name` from Task 10.
- Produces: `Particulars` with `[Parameter] IReadOnlyList<(string Label, string Value, string? Unit, string? Href, string? Tip)> Facts` and `[Parameter] IReadOnlyList<(string Label, string Href)> Schools`, both defaulting to `[]`. `Detail` supplies both in Task 15.

`Particulars` has a `Facts` parameter and renders a `<Facts>` tag. The Razor compiler emits component types fully qualified and C# type-name lookup considers only types, so the tag resolves to `FellowshipExtractor.Api.Components.Cx.Facts` and the expression resolves to the parameter. If the tag ever fails to resolve, the fix is the namespace-qualified tag `<Cx.Facts>`, keeping both names as they are.

- [ ] **Step 1: Write the failing tests**

`src/FellowshipExtractor.Api.Tests/Sections/ParticularsTests.cs`:

```csharp
namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class ParticularsTests : BunitContext
{
    public ParticularsTests(Export export) => Services.AddSingleton(export.View);

    [Fact]
    public void Renders_nothing_when_the_entity_carries_none_of_it()
    {
        var cut = Render<Particulars>(p => p.AddCascadingValue(new Entity()));

        Assert.Empty(cut.Markup);
    }

    [Fact]
    public void Renders_the_facts_in_two_tables_with_the_odd_row_in_the_first()
    {
        IReadOnlyList<(string Label, string Value, string? Unit, string? Href, string? Tip)> facts =
        [
            ("Cooldown", "2.5", "s", null, null),
            ("Range", "30", "m", null, null),
            ("Cost", "40", null, null, null),
        ];

        var cut = Render<Particulars>(p => p.AddCascadingValue(new Entity()).Add(x => x.Facts, facts));

        var tables = cut.FindAll("table.cx-facts");

        Assert.Equal(2, tables.Count);
        Assert.Equal(2, tables[0].QuerySelectorAll("tr").Length);
        Assert.Single(tables[1].QuerySelectorAll("tr"));
    }

    [Fact]
    public void Renders_an_empty_second_table_for_a_single_fact()
    {
        IReadOnlyList<(string Label, string Value, string? Unit, string? Href, string? Tip)> facts =
        [
            ("Cooldown", "2.5", "s", null, null),
        ];

        var cut = Render<Particulars>(p => p.AddCascadingValue(new Entity()).Add(x => x.Facts, facts));

        var tables = cut.FindAll("table.cx-facts");

        Assert.Equal(2, tables.Count);
        Assert.Empty(tables[1].Children);
    }

    [Fact]
    public void Renders_a_linked_fact_cell_with_its_tip()
    {
        IReadOnlyList<(string Label, string Value, string? Unit, string? Href, string? Tip)> facts =
        [
            ("Resource", "Mana", null, "/browse?resource=Mana", "/api/tooltip/effect/9"),
        ];

        var cut = Render<Particulars>(p => p.AddCascadingValue(new Entity()).Add(x => x.Facts, facts));

        var link = cut.Find("table.cx-facts a");

        Assert.Equal("/browse?resource=Mana", link.GetAttribute("href"));
        Assert.Equal("/api/tooltip/effect/9", link.GetAttribute("data-tip"));
        Assert.Equal("Mana", link.TextContent);
    }

    [Fact]
    public void Opens_with_a_rule_when_there_are_no_facts()
    {
        IReadOnlyList<(string Label, string Href)> schools = [("Fire", "/browse?type=ability&school=Fire")];

        var cut = Render<Particulars>(p => p.AddCascadingValue(new Entity()).Add(x => x.Schools, schools));

        var head = cut.Find("div.cx-panel-head");

        Assert.Equal("HR", head.NextElementSibling!.TagName);
        Assert.Contains("cx-rule", head.NextElementSibling.ClassList);
    }

    [Fact]
    public void Names_one_school_in_the_singular()
    {
        IReadOnlyList<(string Label, string Href)> schools = [("Fire", "/browse?type=ability&school=Fire")];

        var cut = Render<Particulars>(p => p.AddCascadingValue(new Entity()).Add(x => x.Schools, schools));

        Assert.Equal("School", cut.Find("p.cx-sub-head").TextContent);
        Assert.Equal("/browse?type=ability&school=Fire", cut.Find("ul.cx-pills a").GetAttribute("href"));
    }

    [Fact]
    public void Names_two_schools_in_the_plural()
    {
        IReadOnlyList<(string Label, string Href)> schools =
        [
            ("Fire", "/browse?type=ability&school=Fire"),
            ("Frost", "/browse?type=ability&school=Frost"),
        ];

        var cut = Render<Particulars>(p => p.AddCascadingValue(new Entity()).Add(x => x.Schools, schools));

        Assert.Equal("Schools", cut.Find("p.cx-sub-head").TextContent);
    }

    [Fact]
    public void Links_each_hero_into_the_browser_with_an_escaped_name()
    {
        var cut = Render<Particulars>(p => p.AddCascadingValue(new Entity { Heroes = ["Lady Sunbloom"] }));

        Assert.Equal("Heroes", cut.Find("p.cx-sub-head").TextContent);
        Assert.Equal("/browse?hero=Lady%20Sunbloom", cut.Find("ul.cx-pills a").GetAttribute("href"));
    }

    [Fact]
    public void Names_the_set_and_its_piece_count()
    {
        var entity = new Entity { SetBonus = ["Ironhide"], SetCount = 4 };

        var cut = Render<Particulars>(p => p.AddCascadingValue(entity));

        Assert.Equal("Set (4 pieces)", cut.Find("p.cx-sub-head").TextContent);
        Assert.Equal("/browse?set=Ironhide", cut.Find("ul.cx-pills a").GetAttribute("href"));
    }

    [Fact]
    public void Names_the_set_without_a_piece_count()
    {
        var cut = Render<Particulars>(p => p.AddCascadingValue(new Entity { SetBonus = ["Ironhide"] }));

        Assert.Equal("Set", cut.Find("p.cx-sub-head").TextContent);
    }

    [Fact]
    public void Spaces_the_stats_it_scales_off_and_gives_them_no_destination()
    {
        var cut = Render<Particulars>(p => p.AddCascadingValue(new Entity { ScalesOff = ["StatCrit"] }));

        Assert.Equal("Scales off", cut.Find("p.cx-sub-head").TextContent);
        Assert.Equal("Stat Crit", cut.Find("ul.cx-pills li > span").TextContent);
    }

    [Fact]
    public void Offers_roles_then_attributes_in_one_list()
    {
        var entity = new Entity
        {
            AllowedRoles = ["Tank", "Healer"],
            AllowedAttributes = ["Strength", "Intellect"],
        };

        var cut = Render<Particulars>(p => p.AddCascadingValue(entity));

        Assert.Single(cut.FindAll("ul.cx-pills"));

        var items = cut.FindAll("ul.cx-pills > li");

        Assert.Equal(4, items.Count);
        Assert.Equal("Tank", items[0].TextContent);
        Assert.Equal("Healer", items[1].TextContent);
        Assert.Equal("Strength", items[2].TextContent);
        Assert.Equal("Intellect", items[3].TextContent);
        Assert.True(items[0].FirstElementChild!.HasAttribute("style"));
        Assert.True(items[1].FirstElementChild!.HasAttribute("style"));
        Assert.False(items[2].FirstElementChild!.HasAttribute("style"));
        Assert.False(items[3].FirstElementChild!.HasAttribute("style"));
    }

    [Fact]
    public void Opens_the_panel_and_draws_only_its_head_for_attributes_alone()
    {
        var cut = Render<Particulars>(p => p.AddCascadingValue(new Entity { AllowedAttributes = ["Strength"] }));

        cut.MarkupMatches("<div class=\"cx-panel\"><div class=\"cx-panel-head\">Details</div></div>");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~ParticularsTests"`
Expected: build FAILS with `CS0246` on `Particulars`.

- [ ] **Step 3: Write `Particulars.razor`**

```razor
@inject CodexView Ui

@if (Facts.Count > 0 || Schools.Count > 0 || Entity.Heroes is { Count: > 0 } ||
     Entity.SetBonus is { Count: > 0 } || Entity.ScalesOff is { Count: > 0 } ||
     Entity.AllowedRoles is { Count: > 0 } || Entity.AllowedAttributes is { Count: > 0 })
{
    <Panel Head="Details">
        @if (Facts.Count > 0)
        {
            <div class="cx-pairs">
                @for (var half = 0; half < 2; half++)
                {
                    var split = (Facts.Count + 1) / 2;
                    var slice = half == 0 ? Facts.Take(split) : Facts.Skip(split);

                    <Facts>
                        @foreach (var fact in slice)
                        {
                            <Fact Label="@fact.Label" Unit="@fact.Unit">
                                @if (fact.Href is null)
                                {
                                    @fact.Value
                                }
                                else
                                {
                                    <a href="@fact.Href" data-tip="@fact.Tip">@fact.Value</a>
                                }
                            </Fact>
                        }
                    </Facts>
                }
            </div>
        }

        @if (Schools.Count > 0)
        {
            <Rule />
            <SubHead>@(Schools.Count == 1 ? "School" : "Schools")</SubHead>
            <Pills>
                @foreach (var school in Schools)
                {
                    <Pill Href="@school.Href">@school.Label</Pill>
                }
            </Pills>
        }

        @if (Entity.Heroes is { Count: > 0 } heroes)
        {
            <Rule />
            <SubHead>Heroes</SubHead>
            <Pills>
                @foreach (var hero in heroes)
                {
                    var where = $"/browse?hero={Uri.EscapeDataString(hero)}";
                    <Pill Href="@where" Colour="@Ui.HeroColour(hero)">@hero</Pill>
                }
            </Pills>
        }

        @if (Entity.SetBonus is { Count: > 0 } sets)
        {
            <Rule />
            <SubHead>Set@(Entity.SetCount is { } pieces ? $" ({pieces} pieces)" : "")</SubHead>
            <Pills>
                @foreach (var set in sets)
                {
                    var where = $"/browse?set={Uri.EscapeDataString(set)}";
                    <Pill Href="@where">@set</Pill>
                }
            </Pills>
        }

        @if (Entity.ScalesOff is { Count: > 0 } scaling)
        {
            <Rule />
            <SubHead>Scales off</SubHead>
            <Pills>
                @foreach (var stat in scaling)
                {
                    <Pill>@Codex.Spaced(stat)</Pill>
                }
            </Pills>
        }

        @if (Entity.AllowedRoles is { Count: > 0 } roles)
        {
            <Rule />
            <SubHead>Offered to</SubHead>
            <Pills>
                @foreach (var role in roles)
                {
                    var tone = Ui.Tokens.Role(role) ?? "inherit";
                    <Pill Colour="@tone">@role</Pill>
                }
                @foreach (var stat in Entity.AllowedAttributes ?? [])
                {
                    <Pill>@stat</Pill>
                }
            </Pills>
        }
    </Panel>
}

@code {
    /// <summary>The record this panel draws, re-supplied by every Detail.</summary>
    [CascadingParameter] private Entity Entity { get; set; } = new();

    /// <summary>
    /// The scalar facts, already formatted and already carrying any address they link to, because
    /// a nested row carries no table and no id of its own to build one from.
    /// </summary>
    [Parameter] public IReadOnlyList<(string Label, string Value, string? Unit, string? Href, string? Tip)> Facts { get; set; } = [];

    /// <summary>
    /// The damage schools this entry is, each with the filter it links into, built by Detail
    /// through the school facet's own selector.
    /// </summary>
    [Parameter] public IReadOnlyList<(string Label, string Href)> Schools { get; set; } = [];
}
```

Every subsection except Facts opens with a `<Rule />` inside its own guard, so an entity with no facts but with schools shows a rule immediately under the panel head. `(Facts.Count + 1) / 2` puts the extra row in the first column on an odd count and renders an empty second table when there is exactly one fact. `AllowedAttributes` is in the panel guard and shares the "Offered to" list with `AllowedRoles`, roles first, so an entity carrying attributes and no roles opens the panel and renders only the head. All three are the rendering today and all three are preserved.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~ParticularsTests"`
Expected: PASS, 13 tests.

- [ ] **Step 5: Build and run the whole suite**

Run: `dotnet build src/FellowshipExtractor.slnx`
Expected: `0 Warning(s)`, `0 Error(s)`.

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`
Expected: PASS, 64 tests.

- [ ] **Step 6: Commit**

```powershell
git add src/FellowshipExtractor.Api/Components/Sections/Particulars.razor src/FellowshipExtractor.Api.Tests/Sections/ParticularsTests.cs
git commit -m @'
feat: add the Particulars section

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 12: `StatSlots`

**Files:**
- Create: `src/FellowshipExtractor.Api/Components/Sections/StatSlots.razor`
- Test: `src/FellowshipExtractor.Api.Tests/Sections/StatSlotsTests.cs`

**Interfaces:**
- Consumes: `Panel`, `Note` from Tasks 3 and 4; `Export` and `Corpus.Name` from Task 10.
- Produces: `StatSlots`, a section reading `[CascadingParameter] private Entity Entity` and injecting `CodexView Ui` for `Ui.Store.PrimaryAttributes`.

`ul.cx-stat-lines` has one call site and stays as markup inside this section.

- [ ] **Step 1: Write the failing tests**

`src/FellowshipExtractor.Api.Tests/Sections/StatSlotsTests.cs`:

```csharp
namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class StatSlotsTests : BunitContext
{
    public StatSlotsTests(Export export) => Services.AddSingleton(export.View);

    [Fact]
    public void Renders_nothing_without_stats()
    {
        var cut = Render<StatSlots>(p => p.AddCascadingValue(new Entity()));

        Assert.Empty(cut.Markup);
    }

    [Fact]
    public void Renders_the_panel_its_badge_its_note_and_its_lines()
    {
        var entity = new Entity
        {
            Stats =
            [
                new StatSlot("Stamina", 40) { Kind = "Primary" },
                new StatSlot("StatCrit", 20) { Kind = "Secondary" },
            ],
        };

        var cut = Render<StatSlots>(p => p.AddCascadingValue(entity));

        cut.MarkupMatches(
            "<div class=\"cx-panel\">" +
            "<div class=\"cx-panel-head\">Stat slots<span class=\"cx-num\">2</span></div>" +
            "<p class=\"cx-note\">A slot's weight is its share of the item's stat budget. The magnitude " +
            "is not in the build: a dynamic item rolls it from its level and rarity when it drops, " +
            "which is what the tooltip above computes.</p>" +
            "<ul class=\"cx-stat-lines\">" +
            "<li class=\"cx-primary\"><span>Stamina</span><span class=\"cx-w\">Primary &middot; weight 40</span></li>" +
            "<li class=\"cx-secondary\"><span>Stat Crit</span><span class=\"cx-w\">Secondary &middot; weight 20</span></li>" +
            "</ul></div>");
    }

    [Fact]
    public void Marks_a_primary_attribute_as_primary()
    {
        var entity = new Entity { Stats = [new StatSlot("Stamina", 40) { Kind = "Primary" }] };

        var cut = Render<StatSlots>(p => p.AddCascadingValue(entity));

        Assert.Contains("cx-primary", cut.Find("ul.cx-stat-lines > li").ClassList);
    }

    [Fact]
    public void Draws_the_slots_in_export_order()
    {
        var entity = new Entity
        {
            Stats =
            [
                new StatSlot("StatCrit", 20) { Kind = "Secondary" },
                new StatSlot("Stamina", 40) { Kind = "Primary" },
            ],
        };

        var cut = Render<StatSlots>(p => p.AddCascadingValue(entity));

        var lines = cut.FindAll("ul.cx-stat-lines > li");

        Assert.Equal("Stat Crit", lines[0].FirstElementChild!.TextContent);
        Assert.Equal("Stamina", lines[1].FirstElementChild!.TextContent);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~StatSlotsTests"`
Expected: build FAILS with `CS0246` on `StatSlots`.

- [ ] **Step 3: Write `StatSlots.razor`**

```razor
@inject CodexView Ui

@if (Entity.Stats is { Count: > 0 } slots)
{
    var badge = slots.Count.ToString();
    <Panel Head="Stat slots" Badge="@badge">
        <Note>
            A slot's weight is its share of the item's stat budget. The magnitude is not in the build:
            a dynamic item rolls it from its level and rarity when it drops, which is what the tooltip
            above computes.
        </Note>
        <ul class="cx-stat-lines">
            @foreach (var slot in slots)
            {
                <li class="@(Ui.Store.PrimaryAttributes.Contains(slot.Stat) || slot.Stat == "Stamina" ? "cx-primary" : "cx-secondary")">
                    <span>@Codex.Spaced(slot.Stat)</span>
                    <span class="cx-w">@slot.Kind &middot; weight @slot.Weight</span>
                </li>
            }
        </ul>
    </Panel>
}

@code {
    /// <summary>The record this panel draws, re-supplied by every Detail.</summary>
    [CascadingParameter] private Entity Entity { get; set; } = new();
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~StatSlotsTests"`
Expected: PASS, 4 tests.

- [ ] **Step 5: Build and run the whole suite**

Run: `dotnet build src/FellowshipExtractor.slnx`
Expected: `0 Warning(s)`, `0 Error(s)`.

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`
Expected: PASS, 68 tests.

- [ ] **Step 6: Commit**

```powershell
git add src/FellowshipExtractor.Api/Components/Sections/StatSlots.razor src/FellowshipExtractor.Api.Tests/Sections/StatSlotsTests.cs
git commit -m @'
feat: add the StatSlots section

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 13: `Ranks`

**Files:**
- Create: `src/FellowshipExtractor.Api/Components/Sections/Ranks.razor`
- Test: `src/FellowshipExtractor.Api.Tests/Sections/RanksTests.cs`

**Interfaces:**
- Consumes: `Panel`, `Note`, `Rule`, `KeyValues`, `KeyValue` from Tasks 3, 4 and 7; `Export` and `Corpus.Name` from Task 10.
- Produces: `Ranks`, a section reading `[CascadingParameter] private Entity Entity` and injecting `CodexView Ui`.

The section is named `Ranks` rather than `RankRuns` because `FellowshipExtractor.Tooltip.RankRuns` is a static class and `FellowshipExtractor.Tooltip` is in `_Imports.razor`, so a second `RankRuns` in scope would be ambiguous.

The `Printed` row is the sharpest whitespace case in the app. `<span> / </span>` is a literal text node containing a non-whitespace character, so the spaces around the slash survive Razor's whitespace handling and are the rendered separator; it is not rewritten as bare text, as `&nbsp;/&nbsp;` or as a CSS `::before`. Every other text node in the `@for` body is whitespace containing a newline, so the compiler removes it and the source indentation never reaches the browser.

- [ ] **Step 1: Write the failing tests**

`src/FellowshipExtractor.Api.Tests/Sections/RanksTests.cs`:

```csharp
namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class RanksTests : BunitContext
{
    public RanksTests(Export export) => Services.AddSingleton(export.View);

    private static Entity WithOneRun() => new()
    {
        RankRuns =
        [
            new RankRun("10/20/30", ["10", "20", "30"])
            {
                Values = [1d, 2d, 3d],
                Transform = "Add",
                Ticks = 5,
                Disagrees = true,
            },
        ],
    };

    [Fact]
    public void Renders_nothing_without_runs()
    {
        var cut = Render<Ranks>(p => p.AddCascadingValue(new Entity()));

        Assert.Empty(cut.Markup);
    }

    [Fact]
    public void Renders_the_panel_its_badge_its_note_and_every_row()
    {
        var cut = Render<Ranks>(p => p.AddCascadingValue(WithOneRun()));

        cut.MarkupMatches(
            "<div class=\"cx-panel\">" +
            "<div class=\"cx-panel-head\">Rank progression<span class=\"cx-num\">1</span></div>" +
            "<p class=\"cx-note\">The slash-separated numbers the description prints, one entry per run " +
            "of them, matched to the curve behind it. The printed row is what the game shows; the " +
            "values are what it computes with.</p>" +
            "<hr class=\"cx-rule\" />" +
            "<ul class=\"cx-kv\">" +
            "<li><span class=\"cx-k\">Printed</span><span class=\"cx-v cx-prose\">10<span> / </span>20<span> / </span>30</span></li>" +
            "<li><span class=\"cx-k\">Shipped values</span><span class=\"cx-v\">1 / 2 / 3</span></li>" +
            "<li><span class=\"cx-k\">Arithmetic</span><span class=\"cx-v\">Add over 5 ticks</span></li>" +
            "<li><span class=\"cx-k\">Note</span><span class=\"cx-v cx-miss\">printed and computed values disagree</span></li>" +
            "</ul></div>");
    }

    [Fact]
    public void Separates_the_printed_values_with_a_slash_and_no_newline()
    {
        var cut = Render<Ranks>(p => p.AddCascadingValue(WithOneRun()));

        var printed = cut.Find("span.cx-v.cx-prose");

        Assert.Equal(
            "<span class=\"cx-v cx-prose\">10<span> / </span>20<span> / </span>30</span>",
            printed.OuterHtml);
        Assert.DoesNotContain("\n", printed.OuterHtml);
    }

    [Fact]
    public void Renders_only_the_printed_row_when_a_run_carries_nothing_else()
    {
        var entity = new Entity { RankRuns = [new RankRun("10", ["10"])] };

        var cut = Render<Ranks>(p => p.AddCascadingValue(entity));

        Assert.Single(cut.FindAll("ul.cx-kv > li"));
        Assert.Equal("Printed", cut.Find("span.cx-k").TextContent);
    }

    [Fact]
    public void Draws_a_missing_value_as_a_dash()
    {
        var entity = new Entity
        {
            RankRuns = [new RankRun("10/20", ["10", "20"]) { Values = [1d, null] }],
        };

        var cut = Render<Ranks>(p => p.AddCascadingValue(entity));

        Assert.Equal("1 / -", cut.FindAll("span.cx-v")[1].TextContent);
    }

    [Fact]
    public void Renders_a_rule_before_every_run()
    {
        var entity = new Entity
        {
            RankRuns = [new RankRun("10", ["10"]), new RankRun("20", ["20"])],
        };

        var cut = Render<Ranks>(p => p.AddCascadingValue(entity));

        Assert.Equal(2, cut.FindAll("hr.cx-rule").Count);
        Assert.Equal(2, cut.FindAll("ul.cx-kv").Count);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~RanksTests"`
Expected: build FAILS with `CS0246` on `Ranks`.

- [ ] **Step 3: Write `Ranks.razor`**

```razor
@inject CodexView Ui

@if (Entity.RankRuns is { Count: > 0 } runs)
{
    var badge = runs.Count.ToString();
    <Panel Head="Rank progression" Badge="@badge">
        <Note>
            The slash-separated numbers the description prints, one entry per run of them, matched to
            the curve behind it. The printed row is what the game shows; the values are what it
            computes with.
        </Note>
        @foreach (var run in runs)
        {
            <Rule />
            <KeyValues>
                <KeyValue Key="Printed" Class="cx-prose">
                    @for (var rank = 0; rank < run.Printed.Count; rank++)
                    {
                        @if (rank > 0)
                        {
                            <span> / </span>
                        }
                        @Ui.Text(run.Printed[rank], null, Entity.Tags)
                    }
                </KeyValue>
                @if (run.Values is { Count: > 0 })
                {
                    <KeyValue Key="Shipped values">@string.Join(" / ", run.Values.Select(v => v is null ? "-" : Codex.Number(v.Value)))</KeyValue>
                }
                @if (run.Transform is not null)
                {
                    <KeyValue Key="Arithmetic">@run.Transform@(run.Ticks is { } ticks ? $" over {ticks} ticks" : "")</KeyValue>
                }
                @if (run.Disagrees == true)
                {
                    <KeyValue Key="Note" Missing="true">printed and computed values disagree</KeyValue>
                }
            </KeyValues>
        }
    </Panel>
}

@code {
    /// <summary>The record this panel draws, re-supplied by every Detail.</summary>
    [CascadingParameter] private Entity Entity { get; set; } = new();
}
```

Each `<KeyValue …>…</KeyValue>` that carries literal text is written on one line, for the same reason `KeyValue.razor` itself is.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~RanksTests"`
Expected: PASS, 6 tests. If the `Assert.Equal` on the printed span reports a difference, the rendered string is the answer: correct the expected string and keep `Assert.Equal`.

- [ ] **Step 5: Build and run the whole suite**

Run: `dotnet build src/FellowshipExtractor.slnx`
Expected: `0 Warning(s)`, `0 Error(s)`.

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`
Expected: PASS, 74 tests.

- [ ] **Step 6: Commit**

```powershell
git add src/FellowshipExtractor.Api/Components/Sections/Ranks.razor src/FellowshipExtractor.Api.Tests/Sections/RanksTests.cs
git commit -m @'
feat: add the Ranks section

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 14: `Overflow` and `BorrowedArt`

**Files:**
- Create: `src/FellowshipExtractor.Api/Components/Sections/Overflow.razor`
- Create: `src/FellowshipExtractor.Api/Components/Sections/BorrowedArt.razor`
- Test: `src/FellowshipExtractor.Api.Tests/Sections/OverflowTests.cs`, `src/FellowshipExtractor.Api.Tests/Sections/BorrowedArtTests.cs`

**Interfaces:**
- Consumes: `Panel`, `Prose`, `Note` from Tasks 3, 4 and 8; `Export` and `Corpus.Name` from Task 10.
- Produces: `Overflow`, reading the entity cascade and injecting `CodexView Ui`; and `BorrowedArt` with `[Parameter] string? Href`, `[Parameter] string? Tip`, `[Parameter] string? Name`.

`BorrowedArt` declares no cascading parameter and injects nothing: spec section 4.6 states it reads nothing from the cascade, and its guard is a link-availability test on `Href`. The panel disappears when the lending row's table is not public, so no address is ever built from a shipped table name. `Detail` resolves the lender through `CodexView.Addresses` and passes the three strings in.

- [ ] **Step 1: Write the failing tests**

`src/FellowshipExtractor.Api.Tests/Sections/OverflowTests.cs`:

```csharp
namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class OverflowTests : BunitContext
{
    public OverflowTests(Export export) => Services.AddSingleton(export.View);

    [Fact]
    public void Renders_nothing_without_a_gem_overflow()
    {
        var cut = Render<Overflow>(p => p.AddCascadingValue(new Entity()));

        Assert.Empty(cut.Markup);
    }

    [Fact]
    public void Renders_the_overflow_text_in_its_own_panel()
    {
        var entity = new Entity { GemOverflow = new GemOverflow("beyond max") };

        var cut = Render<Overflow>(p => p.AddCascadingValue(entity));

        cut.MarkupMatches(
            "<div class=\"cx-panel\"><div class=\"cx-panel-head\">Past max power</div>" +
            "<p class=\"cx-prose\">beyond max</p></div>");
    }

    [Fact]
    public void Renders_the_overflow_text_with_no_whitespace_of_its_own()
    {
        var entity = new Entity { GemOverflow = new GemOverflow("beyond max") };

        var cut = Render<Overflow>(p => p.AddCascadingValue(entity));

        Assert.Equal("<p class=\"cx-prose\">beyond max</p>", cut.Find("p.cx-prose").OuterHtml);
    }
}
```

`src/FellowshipExtractor.Api.Tests/Sections/BorrowedArtTests.cs`:

```csharp
namespace FellowshipExtractor.Api.Tests;

public class BorrowedArtTests : BunitContext
{
    [Fact]
    public void Renders_nothing_when_the_lending_row_has_no_address()
    {
        var cut = Render<BorrowedArt>(p => p.Add(x => x.Name, "Old Boots"));

        Assert.Empty(cut.Markup);
    }

    [Fact]
    public void Names_the_row_the_art_belongs_to()
    {
        var cut = Render<BorrowedArt>(p => p
            .Add(x => x.Href, "/entity/items/12")
            .Add(x => x.Tip, "/api/tooltip/items/12")
            .Add(x => x.Name, "Old Boots"));

        cut.MarkupMatches(
            "<div class=\"cx-panel\"><div class=\"cx-panel-head\">Borrowed art</div>" +
            "<p class=\"cx-note\">This record ships no art of its own. The image above belongs to " +
            "<a href=\"/entity/items/12\" data-tip=\"/api/tooltip/items/12\">Old Boots</a>, " +
            "which is where it was taken from.</p></div>");
    }

    [Fact]
    public void Carries_the_tip_the_hover_layer_reads()
    {
        var cut = Render<BorrowedArt>(p => p
            .Add(x => x.Href, "/entity/items/12")
            .Add(x => x.Tip, "/api/tooltip/items/12")
            .Add(x => x.Name, "Old Boots"));

        Assert.Equal("/api/tooltip/items/12", cut.Find("p.cx-note a").GetAttribute("data-tip"));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~OverflowTests|FullyQualifiedName~BorrowedArtTests"`
Expected: build FAILS with `CS0246` on `Overflow` and `BorrowedArt`.

- [ ] **Step 3: Write `Overflow.razor`**

```razor
@inject CodexView Ui

@if (Entity.GemOverflow is { } overflow)
{
    <Panel Head="Past max power">
        <Prose>@Ui.Text(overflow.Description, overflow.Placeholders, Entity.Tags)</Prose>
    </Panel>
}

@code {
    /// <summary>The record this panel draws, re-supplied by every Detail.</summary>
    [CascadingParameter] private Entity Entity { get; set; } = new();
}
```

- [ ] **Step 4: Write `BorrowedArt.razor`**

```razor
@if (Href is not null)
{
    <Panel Head="Borrowed art">
        <Note>
            This record ships no art of its own. The image above belongs to
            <a href="@Href" data-tip="@Tip">@Name</a>,
            which is where it was taken from.
        </Note>
    </Panel>
}

@code {
    /// <summary>
    /// The lending row's page. Null renders nothing at all, which is how the panel disappears when
    /// the lender is filed under a table no reader can open.
    /// </summary>
    [Parameter] public string? Href { get; set; }

    /// <summary>The lending row's tooltip address, read by the hover layer.</summary>
    [Parameter] public string? Tip { get; set; }

    /// <summary>The lending row's public name, resolved by reading the row rather than its tag.</summary>
    [Parameter] public string? Name { get; set; }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~OverflowTests|FullyQualifiedName~BorrowedArtTests"`
Expected: PASS, 6 tests.

- [ ] **Step 6: Build and run the whole suite**

Run: `dotnet build src/FellowshipExtractor.slnx`
Expected: `0 Warning(s)`, `0 Error(s)`.

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`
Expected: PASS, 80 tests.

- [ ] **Step 7: Commit**

```powershell
git add src/FellowshipExtractor.Api/Components/Sections/Overflow.razor src/FellowshipExtractor.Api/Components/Sections/BorrowedArt.razor src/FellowshipExtractor.Api.Tests/Sections/OverflowTests.cs src/FellowshipExtractor.Api.Tests/Sections/BorrowedArtTests.cs
git commit -m @'
feat: add the Overflow and BorrowedArt sections

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 15: `Grants`, `Legendaries` and `Detail`'s cascade

**Files:**
- Create: `src/FellowshipExtractor.Api/Components/Sections/Grants.razor`
- Create: `src/FellowshipExtractor.Api/Components/Sections/Legendaries.razor`
- Modify: `src/FellowshipExtractor.Api/Components/Shared/Detail.razor`
- Test: `src/FellowshipExtractor.Api.Tests/Sections/GrantsTests.cs`, `src/FellowshipExtractor.Api.Tests/Sections/LegendariesTests.cs`, `src/FellowshipExtractor.Api.Tests/DetailTests.cs`

**Interfaces:**
- Consumes: `Panel`, `Rule`, `SubHead` from Tasks 3 and 4; `Wording`, `Particulars`, `StatSlots`, `Ranks`, `Overflow`, `BorrowedArt` from Tasks 10 to 14; `Export` and `Corpus.Name` from Task 10.
- Produces: `Grants` and `Legendaries`, both reading the entity cascade and injecting nothing; and a `Detail` whose markup is the eight section tags inside one `<CascadingValue Value="Entity">` and whose `@code` block holds only `Entity`, `Facts`, `Schools`, `Lender`, `SchoolFacet`, `OnParametersSet` and `Filter`. `EntityPage` renders `<Detail Entity="Shown" />` in Task 19.

**This is the highest-value task in the plan.** `Detail` renders itself recursively, once for `Entity.Grants` and once per `Entity.LegendaryStats` row, so the cascade has to be re-supplied at every nesting level: a single cascade declared on the page would flow past the nested `<Detail Entity="grants" />` unchanged, and the nested sections would draw the outer item's description, facts and stats while their guards were evaluated on the granted ability. The outer entity's `Grants` guard is truthy, so a nested `Detail` reading the outer entity also renders another Grants panel, and recurses until Blazor's recursion limit stops it. Re-supplying at each level makes each `Detail` instance the sole source of truth for everything below it.

**Neither cascade sets `IsFixed`.** Measured on bUnit 2.8.6 and .NET 10: with `IsFixed="true"`, a component re-rendered with a different value keeps drawing the first one. The optimisation buys nothing here and costs correctness on any re-render, so it is left off and the re-render test below is what holds that decision in place.

**The nested `<Detail>` inside the legendary loop is keyed on the loop index**, never on the roll: `Entity` is a `record`, whose synthesized value equality makes two structurally equal rolls produce the same key, and Blazor throws `More than one sibling of component 'Detail' has the same key value` from `RenderTreeDiffBuilder` on any keyed diff.

**`DetailTests` are characterization tests.** They describe behaviour the pre-rewrite `Detail` already has, so they are written first and are expected to **pass** against it, then to keep passing after the rewrite. Do not expect a red step from them and do not weaken one to produce a red step. If one fails against the pre-rewrite `Detail`, that is drift the Task 1 note must record and the guard or head it disagrees with must be reconciled before the rewrite starts.

**`GrantsTests` and `LegendariesTests` assert on heads, badges, sub-heads and occurrence counts rather than exact markup**, because the nested `<Detail>` inside them is rewritten within this same task and its inner markup changes underneath the tests. What the two sections own is their own panel, and that is what is asserted.

- [ ] **Step 1: Write the `Detail` characterization tests**

`src/FellowshipExtractor.Api.Tests/DetailTests.cs`:

```csharp
namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class DetailTests : BunitContext
{
    public DetailTests(Export export) => Services.AddSingleton(export.View);

    private static int Occurrences(string haystack, string needle)
    {
        var found = 0;

        for (var at = haystack.IndexOf(needle, StringComparison.Ordinal); at >= 0;
             at = haystack.IndexOf(needle, at + 1, StringComparison.Ordinal))
        {
            found++;
        }

        return found;
    }

    [Fact]
    public void A_nested_detail_draws_the_nested_entity_and_not_the_outer_one()
    {
        var inner = new Entity
        {
            DisplayName = "Flame Lash",
            Description = "inner text",
            Stats = [new StatSlot("Stamina", 10) { Kind = "Primary" }],
        };

        var outer = new Entity
        {
            Description = "outer text",
            Stats = [new StatSlot("StatCrit", 20) { Kind = "Secondary" }],
            Grants = inner,
        };

        var cut = Render<Detail>(p => p.Add(x => x.Entity, outer));

        Assert.Equal(1, Occurrences(cut.Markup, "outer text"));
        Assert.Equal(1, Occurrences(cut.Markup, "inner text"));
        Assert.Equal(1, Occurrences(cut.Markup, "Grants an ability"));

        var grants = cut.FindAll("div.cx-panel").First(panel =>
            panel.FirstElementChild is { } head &&
            head.ClassList.Contains("cx-panel-head") &&
            head.TextContent.StartsWith("Grants an ability", StringComparison.Ordinal));

        Assert.Contains("inner text", grants.InnerHtml);
        Assert.DoesNotContain("outer text", grants.InnerHtml);
        Assert.Contains("Stamina", grants.InnerHtml);
        Assert.DoesNotContain("Stat Crit", grants.InnerHtml);
    }

    [Fact]
    public void A_detail_re_rendered_with_a_different_entity_draws_the_new_one()
    {
        var cut = Render<Detail>(p => p.Add(x => x.Entity, new Entity { Description = "first" }));

        Assert.Contains("first", cut.Markup);

        cut.Render(p => p.Add(x => x.Entity, new Entity { Description = "second" }));

        Assert.Contains("second", cut.Markup);
        Assert.DoesNotContain("first", cut.Markup);
    }

    [Fact]
    public void Each_legendary_roll_draws_its_own_text_under_its_own_sub_head()
    {
        var entity = new Entity
        {
            LegendaryStats =
            [
                new Entity { DisplayName = "Fierce", Description = "one" },
                new Entity { DisplayName = "Keen", Description = "two" },
            ],
        };

        var cut = Render<Detail>(p => p.Add(x => x.Entity, entity));

        var heads = cut.FindAll("p.cx-sub-head");

        Assert.Equal(2, heads.Count);
        Assert.Equal("Fierce", heads[0].TextContent);
        Assert.Equal("Keen", heads[1].TextContent);
        Assert.Contains("one", cut.Markup);
        Assert.Contains("two", cut.Markup);
    }

    [Fact]
    public void Two_structurally_equal_rolls_survive_a_re_render()
    {
        var entity = new Entity
        {
            LegendaryStats =
            [
                new Entity { DisplayName = "Fierce", Description = "same" },
                new Entity { DisplayName = "Fierce", Description = "same" },
            ],
        };

        var cut = Render<Detail>(p => p.Add(x => x.Entity, entity));

        Assert.Equal(2, cut.FindAll("p.cx-sub-head").Count);

        cut.Render(p => p.Add(x => x.Entity, new Entity
        {
            LegendaryStats =
            [
                new Entity { DisplayName = "Fierce", Description = "same" },
                new Entity { DisplayName = "Fierce", Description = "same" },
            ],
        }));

        Assert.Equal(2, cut.FindAll("p.cx-sub-head").Count);
    }

    [Fact]
    public void Emits_its_panels_as_siblings_with_no_wrapper()
    {
        var entity = new Entity
        {
            Description = "text",
            Stats = [new StatSlot("Stamina", 40) { Kind = "Primary" }],
        };

        var cut = Render<Detail>(p => p.Add(x => x.Entity, entity));

        Assert.StartsWith("<div class=\"cx-panel\">", cut.Markup);

        var panels = cut.FindAll("div.cx-panel");

        Assert.Equal(2, panels.Count);
        Assert.All(panels, panel => Assert.Null(panel.ParentElement?.Closest(".cx-panel")));
    }
}
```

- [ ] **Step 2: Run the characterization tests against the pre-rewrite `Detail`**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~DetailTests"`
Expected: PASS, 5 tests. A failure here is drift in the current `Detail`, not a bad test: record it in the Task 1 note and reconcile the guard or head it disagrees with before continuing.

- [ ] **Step 3: Write the `Grants` and `Legendaries` failing tests**

`src/FellowshipExtractor.Api.Tests/Sections/GrantsTests.cs`:

```csharp
namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class GrantsTests : BunitContext
{
    public GrantsTests(Export export) => Services.AddSingleton(export.View);

    [Fact]
    public void Renders_nothing_when_the_entity_grants_no_ability()
    {
        var cut = Render<Grants>(p => p.AddCascadingValue(new Entity()));

        Assert.Empty(cut.Markup);
    }

    [Fact]
    public void Names_the_granted_ability_in_the_badge_and_draws_it_below()
    {
        var granted = new Entity { DisplayName = "Flame Lash", Description = "burns" };

        var cut = Render<Grants>(p => p.AddCascadingValue(new Entity { Grants = granted }));

        var head = cut.Find("div.cx-panel-head");

        Assert.StartsWith("Grants an ability", head.TextContent);
        Assert.Equal("Flame Lash", head.QuerySelector("span.cx-num")!.TextContent);
        Assert.Contains("burns", cut.Markup);
    }

    [Fact]
    public void Renders_no_badge_when_the_granted_ability_has_no_display_name()
    {
        var granted = new Entity { Description = "burns" };

        var cut = Render<Grants>(p => p.AddCascadingValue(new Entity { Grants = granted }));

        Assert.Null(cut.Find("div.cx-panel-head").QuerySelector("span.cx-num"));
    }
}
```

`src/FellowshipExtractor.Api.Tests/Sections/LegendariesTests.cs`:

```csharp
namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class LegendariesTests : BunitContext
{
    public LegendariesTests(Export export) => Services.AddSingleton(export.View);

    [Fact]
    public void Renders_nothing_without_legendary_rolls()
    {
        var cut = Render<Legendaries>(p => p.AddCascadingValue(new Entity()));

        Assert.Empty(cut.Markup);
    }

    [Fact]
    public void Counts_the_rolls_and_names_each_one()
    {
        var entity = new Entity
        {
            LegendaryStats =
            [
                new Entity { DisplayName = "Fierce", Description = "one" },
                new Entity { DisplayName = "Keen", Description = "two" },
            ],
        };

        var cut = Render<Legendaries>(p => p.AddCascadingValue(entity));

        var head = cut.Find("div.cx-panel-head");

        Assert.StartsWith("Legendary stats", head.TextContent);
        Assert.Equal("2", head.QuerySelector("span.cx-num")!.TextContent);

        var heads = cut.FindAll("p.cx-sub-head");

        Assert.Equal(2, heads.Count);
        Assert.Equal("Fierce", heads[0].TextContent);
        Assert.Equal("Keen", heads[1].TextContent);
    }

    [Fact]
    public void Draws_the_rolls_in_export_order_behind_a_rule_each()
    {
        var entity = new Entity
        {
            LegendaryStats =
            [
                new Entity { DisplayName = "Keen" },
                new Entity { DisplayName = "Fierce" },
            ],
        };

        var cut = Render<Legendaries>(p => p.AddCascadingValue(entity));

        var heads = cut.FindAll("p.cx-sub-head");

        Assert.Equal("Keen", heads[0].TextContent);
        Assert.Equal("Fierce", heads[1].TextContent);
        Assert.Equal(2, cut.FindAll("hr.cx-rule").Count);
    }
}
```

- [ ] **Step 4: Run the two section test classes to verify they fail**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~GrantsTests|FullyQualifiedName~LegendariesTests"`
Expected: build FAILS with `CS0246` on `Grants` and `Legendaries`.

- [ ] **Step 5: Write `Grants.razor`**

```razor
@if (Entity.Grants is { } grants)
{
    <Panel Head="Grants an ability" Badge="@grants.DisplayName">
        <Detail Entity="grants" />
    </Panel>
}

@code {
    /// <summary>
    /// The record this panel draws. The nested Detail re-supplies the cascade with the granted
    /// ability, so everything below this panel draws that ability and not the record granting it.
    /// </summary>
    [CascadingParameter] private Entity Entity { get; set; } = new();
}
```

- [ ] **Step 6: Write `Legendaries.razor`**

```razor
@if (Entity.LegendaryStats is { Count: > 0 } legendaries)
{
    var badge = legendaries.Count.ToString();
    <Panel Head="Legendary stats" Badge="@badge">
        @for (var index = 0; index < legendaries.Count; index++)
        {
            var roll = legendaries[index];

            <Rule />
            <SubHead>@PublicTypes.NameOf(roll)</SubHead>
            <Detail Entity="roll" @key="index" />
        }
    </Panel>
}

@code {
    /// <summary>
    /// The record this panel draws. The loop exists to give the key a value: the key is the index
    /// and never the roll, because Entity is a record whose value equality makes two structurally
    /// equal rolls produce the same key, and duplicate sibling keys throw out of the diff builder.
    /// The rolls are drawn in export order and never sorted.
    /// </summary>
    [CascadingParameter] private Entity Entity { get; set; } = new();
}
```

- [ ] **Step 7: Run the two section test classes to verify they pass**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~GrantsTests|FullyQualifiedName~LegendariesTests"`
Expected: PASS, 6 tests.

- [ ] **Step 8: Locate `Detail.razor`'s current members before replacing the file**

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components/Shared/Detail.razor -Pattern 'private|\[Parameter'
```

Confirm the names `Facts`, `Schools`, `Lender` or `SchoolFacet`, `OnParametersSet` and `Filter`, and note the exact expressions in `OnParametersSet` and `Filter` so the rewrite carries them through unchanged.

- [ ] **Step 9: Replace `Detail.razor` in full**

```razor
@inject CodexView Ui

<CascadingValue Value="Entity">
    <Wording />
    <Particulars Facts="Facts" Schools="Schools" />
    <StatSlots />
    <Ranks />
    <Overflow />
    <BorrowedArt Href="@(Lender?.Href)" Tip="@(Lender?.Tip)" Name="@(Lender?.Name)" />
    <Grants />
    <Legendaries />
</CascadingValue>

@code {
    [Parameter, EditorRequired] public Entity Entity { get; set; } = new();

    private IReadOnlyList<(string Label, string Value, string? Unit, string? Href, string? Tip)> Facts = [];

    /// <summary>
    /// The damage schools this entry is, each with the filter it links into. The schools are the one
    /// part of an ability's tag list a player meets; the rest of the list is the build's own
    /// classification and is drawn nowhere.
    /// </summary>
    private IReadOnlyList<(string Label, string Href)> Schools = [];

    /// <summary>
    /// The row this entry's art belongs to, resolved here because a section is given a record and
    /// never an address. Null when the record ships its own art, and null when the lending row is
    /// filed under a table no reader can open.
    /// </summary>
    private (string Href, string? Tip, string Name)? Lender;

    private const string SchoolFacet = "school";

    protected override void OnParametersSet()
    {
        Facts = Ui.Facts(Entity);

        // Read through the school facet's own selector rather than repeating the rule for what a
        // school is, because these pills link into that facet: a second copy could offer a value
        // the filter behind the link does not recognise.
        IEnumerable<string> schools = Ui.Codex.Facet(SchoolFacet)?.Values(Entity) ?? [];

        Schools = [.. schools.Select(school => (Codex.Spaced(school), Filter(school)))];

        Lender = Entity.IconInheritedFrom is { } source && Ui.Addresses(source.Table, source.NetId) is { } found
            ? (found.Href, found.Tip, Ui.Name(source.Table, source.NetId))
            : null;
    }

    /// <summary>
    /// A school pill's destination. A nested row - a granted ability, a legendary stat line -
    /// carries no table of its own, so the type half of the query is left off rather than sent
    /// empty, and a row of a non-public table can name no type at all.
    /// </summary>
    private string Filter(string school) =>
        (PublicTypes.SlugOf(Entity.Table) is { } slug ? $"/browse?type={slug}&" : "/browse?") +
        $"{SchoolFacet}={Uri.EscapeDataString(school)}";
}
```

`Detail` is the only component that reads `Entity.Table`, and it reads it only to build the school filter. `<CascadingValue>` emits no element, so the eight sections' panels remain siblings at the component root and `.cx-panel + .cx-panel { margin-top: 16px }` still supplies the gap between them; `Detail` never gains a wrapper element.

- [ ] **Step 10: Run the characterization tests against the rewritten `Detail`**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~DetailTests"`
Expected: PASS, 5 tests, the same five that passed in Step 2. `A_detail_re_rendered_with_a_different_entity_draws_the_new_one` is the test that fails if `IsFixed="true"` is ever added to the `<CascadingValue>`.

- [ ] **Step 11: Confirm no section reads an identity**

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components/Sections -Recurse -Pattern 'Entity\.Table|Entity\.NetId'
```

Expected: no matches.

- [ ] **Step 12: Build and run the whole suite**

Run: `dotnet build src/FellowshipExtractor.slnx`
Expected: `0 Warning(s)`, `0 Error(s)`.

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`
Expected: PASS, 91 tests.

- [ ] **Step 13: Commit**

```powershell
git add src/FellowshipExtractor.Api/Components/Sections/Grants.razor src/FellowshipExtractor.Api/Components/Sections/Legendaries.razor src/FellowshipExtractor.Api/Components/Shared/Detail.razor src/FellowshipExtractor.Api.Tests/Sections/GrantsTests.cs src/FellowshipExtractor.Api.Tests/Sections/LegendariesTests.cs src/FellowshipExtractor.Api.Tests/DetailTests.cs
git commit -m @'
feat: compose Detail from its eight sections over a re-supplied cascade

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 16: `Bearings`, `Stage` and `Nearby`

**Files:**
- Create: `src/FellowshipExtractor.Api/Bearings.cs`
- Create: `src/FellowshipExtractor.Api/Components/Sections/Stage.razor`
- Create: `src/FellowshipExtractor.Api/Components/Sections/Nearby.razor`
- Test: `src/FellowshipExtractor.Api.Tests/Sections/StageTests.cs`, `src/FellowshipExtractor.Api.Tests/Sections/NearbyTests.cs`

**Interfaces:**
- Consumes: `Panel`, `Empty`, `Pills`, `Pill` from Tasks 4, 5 and 8.
- Produces: `Bearings`, a `public sealed record` in namespace `FellowshipExtractor.Api` with `Shown`, `Readings`, `Reading`, `Address`, `Page`, `Under`, `Label`, `NetId`, `Kind` and `Related`; plus `Stage` and `Nearby`, both reading `[CascadingParameter] private Bearings Here` and injecting nothing. `Identity`, `Readings` and `Knobs` read the same cascade in Tasks 17 and 18, and `EntityPage` supplies it in Task 19.

`Bearings` is a distinct type from `Entity`, so the two cascades never collide: Blazor keys cascading values by type.

`codex.js` finds the stage with `document.querySelector('[data-stage]')`, splits `box.dataset.stage` on `?` into an address and a carried query, fetches the result, and attaches a shadow root to the div. So `data-stage` is a plain HTML attribute emitted verbatim and never a Blazor directive; `Here.Address` is the complete tooltip URL including the `?hero=` the server wants carried, computed once by the page; and the div is a real element with a stable identity across enhanced navigations, carrying no `@key` and never changing element type, so `host.shadowRoot ?? host.attachShadow(...)` finds the root it attached last time.

- [ ] **Step 1: Write the failing tests**

`src/FellowshipExtractor.Api.Tests/Sections/StageTests.cs`:

```csharp
namespace FellowshipExtractor.Api.Tests;

public class StageTests : BunitContext
{
    [Fact]
    public void Emits_the_stage_address_verbatim_including_a_carried_query()
    {
        var here = new Bearings { Address = "/api/tooltip/items/5146?hero=Ardeos" };

        var cut = Render<Stage>(p => p.AddCascadingValue(here));

        var box = cut.Find("div.cx-entity-tip");

        Assert.Equal("/api/tooltip/items/5146?hero=Ardeos", box.GetAttribute("data-stage"));
        Assert.Equal("polite", box.GetAttribute("aria-live"));
    }

    [Fact]
    public void Renders_the_loading_placeholder_as_its_only_child()
    {
        var here = new Bearings { Address = "/api/tooltip/items/5146" };

        var cut = Render<Stage>(p => p.AddCascadingValue(here));

        Assert.Equal("<p class=\"cx-empty\">Loading...</p>", cut.Find("div.cx-entity-tip").InnerHtml);
    }

    [Fact]
    public void Carries_no_key_and_stays_a_div()
    {
        var cut = Render<Stage>(p => p.AddCascadingValue(new Bearings()));

        Assert.StartsWith("<div class=\"cx-entity-tip\"", cut.Markup);
    }
}
```

`src/FellowshipExtractor.Api.Tests/Sections/NearbyTests.cs`:

```csharp
namespace FellowshipExtractor.Api.Tests;

public class NearbyTests : BunitContext
{
    [Fact]
    public void Renders_nothing_when_there_is_nowhere_to_go()
    {
        var cut = Render<Nearby>(p => p.AddCascadingValue(new Bearings()));

        Assert.Empty(cut.Markup);
    }

    [Fact]
    public void Renders_a_counted_pill_per_related_list()
    {
        var here = new Bearings
        {
            Related =
            [
                ("Slot: Head", "/browse?type=items&slot=Head", 9),
                ("Set: Ironhide", "/browse?type=items&set=Ironhide", 1200),
            ],
        };

        var cut = Render<Nearby>(p => p.AddCascadingValue(here));

        cut.MarkupMatches(
            "<div class=\"cx-panel\"><div class=\"cx-panel-head\">More like this</div>" +
            "<ul class=\"cx-pills\">" +
            "<li><a href=\"/browse?type=items&amp;slot=Head\">Slot: Head<span class=\"cx-num\">9</span></a></li>" +
            "<li><a href=\"/browse?type=items&amp;set=Ironhide\">Set: Ironhide<span class=\"cx-num\">1,200</span></a></li>" +
            "</ul></div>");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~StageTests|FullyQualifiedName~NearbyTests"`
Expected: build FAILS with `CS0246` on `Bearings`, `Stage` and `Nearby`.

- [ ] **Step 3: Write `Bearings.cs`**

`src/FellowshipExtractor.Api/Bearings.cs`:

```csharp
using FellowshipExtractor.Tooltip;

namespace FellowshipExtractor.Api;

/// <summary>
/// What the entity page knows about the address it is answering, cascaded to the sections that draw
/// its own chrome. Every address in it is built once by the page: a nested row carries no table and
/// no id of its own, so a section that computed one would link nowhere on half the records it is
/// given.
/// </summary>
public sealed record Bearings
{
    /// <summary>The reading on screen, which is the asked-for hero's or the first one read.</summary>
    public Entity Shown { get; init; } = new();

    /// <summary>Every reading of this id, in the order the export was written.</summary>
    public IReadOnlyList<Entity> Readings { get; init; } = [];

    /// <summary>The hero whose reading is shown, asked for or derived.</summary>
    public string? Reading { get; init; }

    /// <summary>The tooltip address, complete with any query the page wants carried into it.</summary>
    public string Address { get; init; } = string.Empty;

    /// <summary>This row's own page, which the reading pills hang their hero query off.</summary>
    public string Page { get; init; } = string.Empty;

    /// <summary>The browser, narrowed to this row's type.</summary>
    public string Under { get; init; } = string.Empty;

    /// <summary>The word this row's type is published under.</summary>
    public string Label { get; init; } = string.Empty;

    public int NetId { get; init; }

    /// <summary>The type this row is filed under, counted, or null when it is filed under none.</summary>
    public EntityType? Kind { get; init; }

    /// <summary>Where else a reader can be sent from here, already labelled and counted.</summary>
    public IReadOnlyList<(string Label, string Href, int Count)> Related { get; init; } = [];
}
```

- [ ] **Step 4: Write `Stage.razor`**

`data-stage` and `aria-live` are plain HTML attributes, emitted verbatim.

```razor
<div class="cx-entity-tip" data-stage="@Here.Address" aria-live="polite">
    <Empty>Loading...</Empty>
</div>

@code {
    /// <summary>
    /// The page's own facts. The stage address is read whole and split on its query by the script,
    /// so it arrives complete rather than being rebuilt here.
    /// </summary>
    [CascadingParameter] private Bearings Here { get; set; } = new();
}
```

- [ ] **Step 5: Write `Nearby.razor`**

```razor
@if (Here.Related.Count > 0)
{
    <Panel Head="More like this">
        <Pills>
            @foreach (var (label, href, count) in Here.Related)
            {
                var badge = count.ToString("N0");
                <Pill Href="@href" Badge="@badge">@label</Pill>
            }
        </Pills>
    </Panel>
}

@code {
    /// <summary>The page's own facts, carrying the related lists already labelled and counted.</summary>
    [CascadingParameter] private Bearings Here { get; set; } = new();
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~StageTests|FullyQualifiedName~NearbyTests"`
Expected: PASS, 5 tests.

- [ ] **Step 7: Build and run the whole suite**

Run: `dotnet build src/FellowshipExtractor.slnx`
Expected: `0 Warning(s)`, `0 Error(s)`.

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`
Expected: PASS, 96 tests.

- [ ] **Step 8: Commit**

```powershell
git add src/FellowshipExtractor.Api/Bearings.cs src/FellowshipExtractor.Api/Components/Sections/Stage.razor src/FellowshipExtractor.Api/Components/Sections/Nearby.razor src/FellowshipExtractor.Api.Tests/Sections/StageTests.cs src/FellowshipExtractor.Api.Tests/Sections/NearbyTests.cs
git commit -m @'
feat: add the page cascade record with the Stage and Nearby sections

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 17: `Identity` and `Readings`

**Files:**
- Create: `src/FellowshipExtractor.Api/Components/Sections/Identity.razor`
- Create: `src/FellowshipExtractor.Api/Components/Sections/Readings.razor`
- Test: `src/FellowshipExtractor.Api.Tests/Sections/IdentityTests.cs`, `src/FellowshipExtractor.Api.Tests/Sections/ReadingsTests.cs`

**Interfaces:**
- Consumes: `Panel`, `Facts`, `Fact`, `Rule`, `SubHead`, `Pills`, `Pill` from Tasks 3 to 6; `Bearings` from Task 16; `Export` and `Corpus.Name` from Task 10.
- Produces: `Identity`, which injects nothing and places `<Readings />` itself; and `Readings`, which injects `CodexView Ui` for `Ui.Reading`.

The readings list belongs inside the Identity panel, directly under the row that counts them, so `Identity` places `<Readings />` and `EntityPage` stays flat. The readings are drawn in export order, which is the order the store read them.

- [ ] **Step 1: Write the failing tests**

`src/FellowshipExtractor.Api.Tests/Sections/IdentityTests.cs`:

```csharp
namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class IdentityTests : BunitContext
{
    public IdentityTests(Export export) => Services.AddSingleton(export.View);

    [Fact]
    public void Renders_the_four_identity_rows()
    {
        var here = new Bearings
        {
            Under = "/browse?type=items",
            Label = "Items",
            NetId = 5146,
            Readings = [new Entity { DisplayName = "Webbed Robe" }],
            Address = "/api/tooltip/items/5146",
        };

        var cut = Render<Identity>(p => p.AddCascadingValue(here));

        cut.MarkupMatches(
            "<div class=\"cx-panel\"><div class=\"cx-panel-head\">Identity</div>" +
            "<table class=\"cx-facts\">" +
            "<tr><th>Type</th><td><a href=\"/browse?type=items\">Items</a></td></tr>" +
            "<tr><th>Network id</th><td>5146</td></tr>" +
            "<tr><th>Readings of this id</th><td>1</td></tr>" +
            "<tr><th>Tooltip</th><td><a href=\"/api/tooltip/items/5146\">/api/tooltip/items/5146</a></td></tr>" +
            "</table></div>");
    }

    [Fact]
    public void Counts_the_readings_against_the_type_when_the_type_is_known()
    {
        var here = new Bearings
        {
            Under = "/browse?type=items",
            Label = "Items",
            NetId = 5146,
            Readings = [new Entity(), new Entity()],
            Address = "/api/tooltip/items/5146",
            Kind = new EntityType("ItemID", "items", "Items", "Items", 2204),
        };

        var cut = Render<Identity>(p => p.AddCascadingValue(here));

        Assert.Equal("2 of 2,204 under Items", cut.FindAll("table.cx-facts td")[2].TextContent);
    }

    [Fact]
    public void Places_the_readings_list_inside_its_own_panel()
    {
        var first = new Entity { DisplayName = "Webbed Robe" };
        var second = new Entity { DisplayName = "Tribal Hauberk" };

        var here = new Bearings
        {
            Under = "/browse?type=items",
            Label = "Items",
            NetId = 5146,
            Readings = [first, second],
            Shown = first,
            Page = "/entity/items/5146",
            Address = "/api/tooltip/items/5146",
        };

        var cut = Render<Identity>(p => p.AddCascadingValue(here));

        Assert.NotNull(cut.Find("div.cx-panel ul.cx-pills"));
        Assert.Equal("Every reading", cut.Find("div.cx-panel p.cx-sub-head").TextContent);
    }
}
```

`EntityType` is the record `Codex.Types` holds, declared as `EntityType(string Table, string Slug, string Label, string Group, int Count)`. Confirm that shape before writing the second test:

```powershell
Select-String -Path src/FellowshipExtractor.Api/Codex.cs -Pattern 'record EntityType'
```

If its positional parameters have changed, construct it as the source declares and keep the assertion.

`src/FellowshipExtractor.Api.Tests/Sections/ReadingsTests.cs`:

```csharp
namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class ReadingsTests : BunitContext
{
    public ReadingsTests(Export export) => Services.AddSingleton(export.View);

    [Fact]
    public void Renders_nothing_for_a_single_reading()
    {
        var here = new Bearings { Readings = [new Entity { DisplayName = "Webbed Robe" }] };

        var cut = Render<Readings>(p => p.AddCascadingValue(here));

        Assert.Empty(cut.Markup);
    }

    [Fact]
    public void Renders_one_pill_per_reading_with_the_shown_one_in_gold()
    {
        var first = new Entity { DisplayName = "Webbed Robe", ArmorType = "Cloth", PrimaryStat = "Intellect" };
        var second = new Entity { DisplayName = "Tribal Hauberk", ArmorType = "Leather", PrimaryStat = "Agility" };

        var here = new Bearings
        {
            Readings = [first, second],
            Shown = first,
            Page = "/entity/items/5146",
        };

        var cut = Render<Readings>(p => p.AddCascadingValue(here));

        cut.MarkupMatches(
            "<hr class=\"cx-rule\" />" +
            "<p class=\"cx-sub-head\">Every reading</p>" +
            "<ul class=\"cx-pills\">" +
            "<li><a href=\"/entity/items/5146\" style=\"color:var(--gold-bright)\">Webbed Robe" +
            "<span class=\"cx-num\">Cloth / Intellect</span></a></li>" +
            "<li><a href=\"/entity/items/5146\">Tribal Hauberk" +
            "<span class=\"cx-num\">Leather / Agility</span></a></li>" +
            "</ul>");
    }

    [Fact]
    public void Reads_any_for_a_reading_that_names_no_armour_or_stat()
    {
        var first = new Entity { DisplayName = "One" };
        var second = new Entity { DisplayName = "Two" };

        var here = new Bearings { Readings = [first, second], Shown = first, Page = "/entity/items/5146" };

        var cut = Render<Readings>(p => p.AddCascadingValue(here));

        Assert.Equal("any / any", cut.FindAll("span.cx-num")[0].TextContent);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~IdentityTests|FullyQualifiedName~ReadingsTests"`
Expected: build FAILS with `CS0246` on `Identity` and `Readings`.

- [ ] **Step 3: Write `Readings.razor`**

```razor
@inject CodexView Ui

@if (Here.Readings.Count > 1)
{
    <Rule />
    <SubHead>Every reading</SubHead>
    <Pills>
        @foreach (var reading in Here.Readings)
        {
            var who = Ui.Reading(reading);
            var where = Here.Page + (who is null ? null : "?hero=" + Uri.EscapeDataString(who));
            var tone = reading == Here.Shown ? "var(--gold-bright)" : null;
            var badge = $"{reading.ArmorType ?? "any"} / {reading.PrimaryStat ?? "any"}";

            <Pill Href="@where" Colour="@tone" Badge="@badge">@PublicTypes.NameOf(reading)</Pill>
        }
    </Pills>
}

@code {
    /// <summary>
    /// The page's own facts. The readings are drawn in export order, which is the order the store
    /// read them, and each pill hangs its hero query off the page address the page supplied.
    /// </summary>
    [CascadingParameter] private Bearings Here { get; set; } = new();
}
```

- [ ] **Step 4: Write `Identity.razor`**

```razor
<Panel Head="Identity">
    <Facts>
        <Fact Label="Type"><a href="@Here.Under">@Here.Label</a></Fact>
        <Fact Label="Network id">@Here.NetId</Fact>
        <Fact Label="Readings of this id">@Here.Readings.Count@(Here.Kind is { } kind ? $" of {kind.Count:N0} under {Here.Label}" : null)</Fact>
        <Fact Label="Tooltip"><a href="@Here.Address">@Here.Address</a></Fact>
    </Facts>
    <Readings />
</Panel>

@code {
    /// <summary>
    /// The page's own facts. The readings list sits in this panel directly under the row that
    /// counts them, so this section places it rather than the page.
    /// </summary>
    [CascadingParameter] private Bearings Here { get; set; } = new();
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~IdentityTests|FullyQualifiedName~ReadingsTests"`
Expected: PASS, 6 tests.

- [ ] **Step 6: Build and run the whole suite**

Run: `dotnet build src/FellowshipExtractor.slnx`
Expected: `0 Warning(s)`, `0 Error(s)`.

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`
Expected: PASS, 102 tests.

- [ ] **Step 7: Commit**

```powershell
git add src/FellowshipExtractor.Api/Components/Sections/Identity.razor src/FellowshipExtractor.Api/Components/Sections/Readings.razor src/FellowshipExtractor.Api.Tests/Sections/IdentityTests.cs src/FellowshipExtractor.Api.Tests/Sections/ReadingsTests.cs
git commit -m @'
feat: add the Identity and Readings sections

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 18: `Knobs`

**Files:**
- Create: `src/FellowshipExtractor.Api/Components/Sections/Knobs.razor`
- Test: `src/FellowshipExtractor.Api.Tests/Sections/KnobsTests.cs`

**Interfaces:**
- Consumes: `Panel` from Task 4, `Knob` from Task 9, `Bearings` from Task 16, `Export` and `Corpus.Name` from Task 10.
- Produces: `Knobs`, a headless panel reading `[CascadingParameter] private Bearings Here` and injecting `CodexView Ui` and `EntityStore Store`.

**Every attribute in this section is a plain HTML attribute** and every one of them compiles cleanly if broken:

- `onchange="location.search = '?hero=' + encodeURIComponent(this.value)"` is written verbatim in markup. It is not `@onchange` and it is not passed through a component parameter. It performs a full navigation, which is the only way the hero can change on a statically rendered page, because the server has to re-resolve the shown reading, the name, the name colour, the detail and the related list.
- `data-knob`'s value is the query-parameter name sent to `/api/tooltip/…`: `hero`, `rarity`, `itemLevel`, `upgrades`. Those four strings are an API contract.
- The rarity rungs are `<button type="button">`, because `codex.js` selects `button[data-knob]` and `type="button"` keeps them out of form submission.
- `aria-pressed="false"` is present on the server-rendered HTML of every rung. It is the only visual state `.cx-rungs button[aria-pressed="true"]` has, and the toggle-off path reads the attribute.
- The whole rung family shares one `data-knob="rarity"` value, so `querySelectorAll` can clear its siblings.
- `<option value="">Any</option>` appears only when `Here.Readings.Count == 1`, which is the case where `PicksHero` was satisfied by the shown reading listing more than one hero, so clearing the hero is meaningful.
- `selected="@(who.Name == Here.Reading)"` compares against the reading actually on screen, so a fresh load selects what it is showing.
- The hero list is sorted ordinal-ignore-case; the rarity ladder is file order and is never sorted.
- The gem label switches on `IsGem` alone while the rung block renders for `Wearable || IsGem`: gems and items print the same tier under different words, and that is domain vocabulary.

- [ ] **Step 1: Write the failing tests**

`src/FellowshipExtractor.Api.Tests/Sections/KnobsTests.cs`:

```csharp
namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class KnobsTests : BunitContext
{
    public KnobsTests(Export export)
    {
        Services.AddSingleton(export.View);
        Services.AddSingleton(export.Store);
    }

    private static Entity Wearable() => new()
    {
        Stats = [new StatSlot("Stamina", 40) { Kind = "Primary" }],
        Heroes = ["Ardeos", "Brenna"],
    };

    [Fact]
    public void Renders_nothing_for_a_row_with_no_knobs()
    {
        var cut = Render<Knobs>(p => p.AddCascadingValue(new Bearings()));

        Assert.Empty(cut.Markup);
    }

    [Fact]
    public void Renders_the_hero_select_with_its_navigation_attribute_decoded()
    {
        var shown = Wearable();
        var here = new Bearings { Shown = shown, Readings = [shown], Reading = "Ardeos" };

        var cut = Render<Knobs>(p => p.AddCascadingValue(here));

        var select = cut.Find("select[data-knob=hero]");

        Assert.Equal(
            "location.search = '?hero=' + encodeURIComponent(this.value)",
            select.GetAttribute("onchange"));
    }

    [Fact]
    public void Renders_every_rarity_rung_as_a_pressable_button()
    {
        var shown = Wearable();
        var here = new Bearings { Shown = shown, Readings = [shown] };

        var cut = Render<Knobs>(p => p.AddCascadingValue(here));

        var rungs = cut.FindAll("div.cx-rungs button");

        Assert.NotEmpty(rungs);
        Assert.All(rungs, rung =>
        {
            Assert.Equal("button", rung.GetAttribute("type"));
            Assert.Equal("rarity", rung.GetAttribute("data-knob"));
            Assert.False(string.IsNullOrEmpty(rung.GetAttribute("data-choice")));
            Assert.Equal("false", rung.GetAttribute("aria-pressed"));
        });
    }

    [Fact]
    public void Renders_the_two_number_knobs_with_their_query_parameter_names()
    {
        var shown = Wearable();
        var here = new Bearings { Shown = shown, Readings = [shown] };

        var cut = Render<Knobs>(p => p.AddCascadingValue(here));

        Assert.Equal("number", cut.Find("input[data-knob=itemLevel]").GetAttribute("type"));
        Assert.Equal("number", cut.Find("input[data-knob=upgrades]").GetAttribute("type"));
        Assert.Equal(
            StatMagnitudes.MaximumItemLevel.ToString(),
            cut.Find("input[data-knob=itemLevel]").GetAttribute("value"));
    }

    [Fact]
    public void Renders_four_knobs_for_a_wearable_row_that_picks_a_hero()
    {
        var shown = Wearable();
        var here = new Bearings { Shown = shown, Readings = [shown] };

        var cut = Render<Knobs>(p => p.AddCascadingValue(here));

        Assert.Equal(4, cut.FindAll(".cx-knob").Count);
        Assert.Equal("SPAN", cut.Find(".cx-knob").FirstElementChild!.TagName);
    }

    [Fact]
    public void Offers_a_blank_hero_option_for_a_single_reading()
    {
        var shown = Wearable();
        var here = new Bearings { Shown = shown, Readings = [shown] };

        var cut = Render<Knobs>(p => p.AddCascadingValue(here));

        Assert.Single(cut.FindAll("select[data-knob=hero] option[value='']"));
    }

    [Fact]
    public void Offers_no_blank_hero_option_for_several_readings()
    {
        var shown = Wearable();
        var other = new Entity { Stats = shown.Stats, Heroes = ["Ardeos"] };
        var here = new Bearings { Shown = shown, Readings = [shown, other] };

        var cut = Render<Knobs>(p => p.AddCascadingValue(here));

        Assert.Empty(cut.FindAll("select[data-knob=hero] option[value='']"));
    }

    [Fact]
    public void Renders_the_rarity_rungs_for_a_gem_and_no_number_knobs()
    {
        var shown = new Entity { GemTier = 3 };
        var here = new Bearings { Shown = shown, Readings = [shown] };

        var cut = Render<Knobs>(p => p.AddCascadingValue(here));

        Assert.NotEmpty(cut.FindAll("div.cx-rungs button"));
        Assert.Empty(cut.FindAll("input[data-knob=itemLevel]"));
        Assert.Empty(cut.FindAll("select[data-knob=hero]"));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~KnobsTests"`
Expected: build FAILS with `CS0246` on `Knobs`.

- [ ] **Step 3: Write `Knobs.razor`**

```razor
@inject CodexView Ui
@inject EntityStore Store

@if (PicksHero || Wearable || IsGem)
{
    <Panel>
        @if (PicksHero)
        {
            <Knob Caption="Hero">
                <select data-knob="hero"
                        onchange="location.search = '?hero=' + encodeURIComponent(this.value)">
                    @if (Here.Readings.Count == 1)
                    {
                        <option value="">Any</option>
                    }
                    @foreach (var who in Store.Vocabulary.Heroes
                        .Where(h => Here.Readings.Any(r => r.Heroes is null || r.Heroes.Contains(h.Name)))
                        .OrderBy(h => h.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        <option value="@who.Name" selected="@(who.Name == Here.Reading)">
                            @who.Name (@who.ArmorType, @who.PrimaryStat)
                        </option>
                    }
                </select>
            </Knob>
        }

        @if (Wearable || IsGem)
        {
            <Knob Caption="Rarity" As="div">
                <div class="cx-rungs">
                    @foreach (var rung in Store.Vocabulary.Rarities)
                    {
                        var colour = CodexView.Readable(Ui.Tokens.Rarity(rung.Name));
                        <button type="button" data-knob="rarity" data-choice="@rung.Tier"
                                aria-pressed="false"
                                style="@(colour is null ? null : $"color:{colour}")">
                            @(IsGem ? rung.GemDisplayName : rung.DisplayName)
                        </button>
                    }
                </div>
            </Knob>
        }

        @if (Wearable)
        {
            <Knob Caption="Item level">
                <input type="number" min="1" max="1000" step="1"
                       value="@StatMagnitudes.MaximumItemLevel" data-knob="itemLevel" />
            </Knob>

            <Knob Caption="Tempered">
                <input type="number" min="0" max="20" step="1" placeholder="none" data-knob="upgrades" />
            </Knob>
        }
    </Panel>
}

@code {
    /// <summary>The page's own facts, carrying the reading on screen and every reading of this id.</summary>
    [CascadingParameter] private Bearings Here { get; set; } = new();

    private bool IsGem => Here.Shown.GemTier is not null;

    private bool Wearable => Here.Shown.Stats is { Count: > 0 };

    /// <summary>
    /// Whether a hero can be picked at all: either the id reads differently per hero, or the one
    /// reading on screen is offered to more than one of them.
    /// </summary>
    private bool PicksHero => Here.Readings.Count > 1 || Here.Shown.Heroes is { Count: > 1 };
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~KnobsTests"`
Expected: PASS, 8 tests. `Renders_the_hero_select_with_its_navigation_attribute_decoded` reads the attribute through the parsed DOM, so it holds whether or not the renderer escaped the apostrophes; if it fails, the attribute has been turned into a Blazor directive or routed through a component parameter and must be put back into markup verbatim.

- [ ] **Step 5: Build and run the whole suite**

Run: `dotnet build src/FellowshipExtractor.slnx`
Expected: `0 Warning(s)`, `0 Error(s)`.

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`
Expected: PASS, 110 tests.

- [ ] **Step 6: Commit**

```powershell
git add src/FellowshipExtractor.Api/Components/Sections/Knobs.razor src/FellowshipExtractor.Api.Tests/Sections/KnobsTests.cs
git commit -m @'
feat: add the Knobs section

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 19: `EntityPage` adopts the five page sections

**Files:**
- Modify: `src/FellowshipExtractor.Api/Components/Pages/EntityPage.razor`

**Interfaces:**
- Consumes: `Bearings` from Task 16; `Stage`, `Nearby`, `Identity`, `Readings`, `Knobs` from Tasks 16 to 18; `Detail` from Task 15.
- Produces: an `EntityPage` whose markup is the page title, the crumbs, the `<h1>` and the five section tags inside the `.cx-entity` grid, all inside one `<CascadingValue Value="Here">`.

This task's gate is the build, the structural checks below and the whole suite staying green; the page itself is confirmed in a browser in Task 22. `.cx-entity`, `.cx-entity-top` and `.cx-entity-side` stay as markup: they are the page's own grid and `.cx-entity-side .cx-panel + .cx-panel { margin-top: 0 }` selects through them.

- [ ] **Step 1: Record the members the current page computes**

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components/Pages/EntityPage.razor -Pattern 'private |\[Parameter|\[SupplyParameterFromQuery'
```

Note the exact expressions for `table`, `Readings`, `Shown`, `Name`, `Under`, `Reads` and `Nearby()` so the rewrite carries them through unchanged.

- [ ] **Step 2: Replace `EntityPage.razor` in full**

```razor
@page "/entity/{slug}/{netId:int}"
@inject CodexView Ui
@inject Codex Catalog
@inject EntityStore Store
@inject NavigationManager Nav

@if (Readings.Count > 0)
{
    <PageTitle>@Name - Fellowship Codex</PageTitle>
    <Crumbs Trail="@(new List<(string, string?)> { (PublicTypes.Label(table), Under), (Name, null) })" />

    <h1 style="color:@Ui.NameColour(Shown)">@Name</h1>

    <CascadingValue Value="Here">
        <div class="cx-entity">
            <div class="cx-entity-top">
                <Stage />

                <div class="cx-entity-side">
                    <Knobs />
                    <Identity />
                </div>
            </div>

            <Detail Entity="Shown" />

            <Nearby />
        </div>
    </CascadingValue>
}

@code {
    /// <summary>
    /// The type's public slug, which is the only spelling this route answers to. A slug naming
    /// nothing public leaves <see cref="table"/> null and the page answers 404, so a shipped table
    /// name in the address opens no page rather than opening the page under a second name.
    /// </summary>
    [Parameter] public string Slug { get; set; } = string.Empty;

    [Parameter] public int NetId { get; set; }

    [SupplyParameterFromQuery(Name = "hero")] public string? Asked { get; set; }

    /// <summary>The shipped table the slug names, resolved once and never drawn.</summary>
    private string? table;

    private IReadOnlyList<Entity> Readings = [];

    private Entity Shown = new();

    /// <summary>
    /// Everything the page's own sections draw, built in one place so that no section has to work
    /// out an address for itself.
    /// </summary>
    private Bearings Here = new();

    private string Name => Ui.Name(Shown);

    /// <summary>
    /// The browser, narrowed to this row's type. Built from the slug the type is filed under rather
    /// than from the route's own spelling, so the crumb and the type row agree with the menu whatever
    /// case a reader typed.
    /// </summary>
    private string Under => $"/browse?type={PublicTypes.SlugOf(table)}";

    protected override void OnParametersSet()
    {
        table = PublicTypes.TableOf(Slug);
        Readings = table is null ? [] : Store.All(table, NetId);

        if (Readings.Count == 0)
        {
            Shown = new Entity();
            Here = new Bearings();
            Nav.NotFound();
            return;
        }

        Shown = Asked is not null && Store.Hero(Asked) is { } named
            ? Readings.FirstOrDefault(e => Reads(e, named.Name, named.ArmorType, named.PrimaryStat)) ?? Readings[0]
            : Readings[0];

        Here = new Bearings
        {
            Shown = Shown,
            Readings = Readings,
            Reading = Asked ?? Ui.Reading(Shown),
            Address = Ui.Tip(Shown),
            Page = Ui.Href(table, NetId),
            Under = Under,
            Label = PublicTypes.Label(table),
            NetId = NetId,
            Kind = Catalog.Types.FirstOrDefault(t => t.Table == Shown.Table),
            Related = Nearby(),
        };
    }

    private static bool Reads(Entity entity, string hero, string? armor, string? stat) =>
        (entity.Heroes is null || entity.Heroes.Contains(hero)) &&
        (entity.ArmorType is null || entity.ArmorType == armor) &&
        (entity.PrimaryStat is null || entity.PrimaryStat == stat);

    private IReadOnlyList<(string, string, int)> Nearby()
    {
        var nearby = new List<(string, string, int)>();

        void Add(string facet, string? value)
        {
            if (value is null) return;
            if (Catalog.Facet(facet) is not { } definition) return;

            var count = Store.Entities.Count(row =>
                row.Table == Shown.Table && definition.Matches(row, [value]));

            if (count < 2) return;

            nearby.Add((
                $"{definition.Label}: {Codex.Spaced(value)}",
                $"{Under}&{facet}={Uri.EscapeDataString(value)}",
                count));
        }

        // Every axis a reader can be sent along is one the game itself prints: a slot, a set, a gem
        // type, a trait category, a hero, a school of magic. The loot rule set an item rolls under
        // is the one shared field that is a shipped spelling throughout, so it offers no way in.
        Add("slot", Shown.Slot);
        Add("set", Shown.SetBonus?.FirstOrDefault());
        Add("gem", Shown.GemType);
        Add("trait", Shown.TraitCategory);
        Add("hero", Shown.Heroes?.Count == 1 ? Shown.Heroes[0] : null);
        Add("school", Catalog.Facet("school")?.Values(Shown).FirstOrDefault());

        return nearby;
    }
}
```

If Step 1 showed a member whose expression differs from the above, keep the working tree's expression and change only the markup and the `Bearings` construction.

- [ ] **Step 3: Confirm the page's markup is the five section tags inside the grid**

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components/Pages/EntityPage.razor -Pattern '<Stage />|<Knobs />|<Identity />|<Nearby />|<Detail Entity="Shown" />|<CascadingValue Value="Here">'
```

Expected: six matches, one each. `<Readings />` is placed by `Identity` and does not appear here.

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components/Pages/EntityPage.razor -Pattern 'cx-knob|data-stage|cx-facts|cx-pills|cx-num'
```

Expected: no matches. Every one of those now lives in a section.

- [ ] **Step 4: Build and run the whole suite**

Run: `dotnet build src/FellowshipExtractor.slnx`
Expected: `0 Warning(s)`, `0 Error(s)`.

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`
Expected: PASS, 110 tests.

- [ ] **Step 5: Commit**

```powershell
git add src/FellowshipExtractor.Api/Components/Pages/EntityPage.razor
git commit -m @'
refactor: compose EntityPage from its five page sections

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 20: `Browse` adopts the primitives

**Files:**
- Modify: `src/FellowshipExtractor.Api/Components/Pages/Browse.razor`

**Interfaces:**
- Consumes: `Panel`, `Empty`, `Chip`, `Icon`, `CountBadge` from Tasks 3, 4, 8 and 9.
- Produces: no new type. `Browse` keeps every member it has.

**What this task keeps as markup**, deliberately and by name:

- **The results rows.** The group-head `<tr class="cx-group-head">` is a sibling emitted before a data row, and the decision to emit it depends on a `lastGroup` local carried across loop iterations. A child component can neither emit a sibling row nor carry that state, so one result row stays inline and the `@foreach` stays exactly as it is, `@{ string? lastGroup = null; }` and `colspan="12"` included.
- The `<details class="cx-facet">` blocks and their `open="…"` expressions: a facet opens when it is in use or has eight values or fewer, the Between panel opens when a range is set, and the grouping and flags panels are unconditionally open.
- `.cx-tabs`, `.cx-chosen`, `.cx-ranges`, `.cx-results-head`, `.cx-table-scroll` and `.cx-pager`.
- `ShowSlot`, `ShowWorn` and `ShowSchools`, each gating a `<th>` and a `<td>` that must stay in lockstep.

**The literal space before a `cx-num` inside a tab or a facet value stays.** In `.cx-panel-head` and `.cx-pills` the gap comes from CSS, which is why those two lose their space; in the tabs and the facet values the space is in the text and is preserved by writing `@label <CountBadge … />` on one line.

- [ ] **Step 1: Adopt `Panel` and `Empty` for the unreadable-filter panel**

Find it:

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components/Pages/Browse.razor -Pattern 'That filter could not be read'
```

Replace the surrounding `<div class="cx-panel">` and `<p class="cx-empty">` with, keeping the `@error` expression exactly as it is:

```razor
<Panel><Empty Said="That filter could not be read.">@error</Empty></Panel>
```

- [ ] **Step 2: Adopt `Panel` for the filter rail**

Find it:

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components/Pages/Browse.razor -Pattern 'cx-rail|aria-label="Filters"'
```

Replace the `<aside class="cx-rail cx-panel" aria-label="Filters">` opening and its `<div class="cx-panel-head">Refine</div>` first child with:

```razor
<Panel As="aside" Class="cx-rail" Aria="Filters" Head="Refine">
```

and its `</aside>` with `</Panel>`. The rail then renders `class="cx-panel cx-rail"` rather than `class="cx-rail cx-panel"`; class order is read by no selector in `codex.css`.

- [ ] **Step 3: Adopt `Panel` and `Empty` for the no-results panel**

Find it:

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components/Pages/Browse.razor -Pattern 'Nothing in the build answers that'
```

Replace with, keeping the ternary exactly as it is:

```razor
<Panel>
    <Empty Said="Nothing in the build answers that.">
        @(Query.IsEmpty
            ? "Pick a category from the menus above, or type a name into the search box."
            : "Remove a filter and try again.")
    </Empty>
</Panel>
```

- [ ] **Step 4: Adopt `Chip` for the five chip emissions**

Find them:

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components/Pages/Browse.razor -Pattern 'cx-chip'
```

Expected: five matches, all inside `table.cx-rows`. Replace each in place:

```razor
<Chip>@row.ArmorType</Chip>
```

```razor
<Chip>@Codex.Spaced(row.PrimaryStat)</Chip>
```

```razor
@foreach (var hero in (row.Heroes ?? []).Take(3))
{
    var tone = Ui.HeroColour(hero);
    <Chip Colour="@tone">@hero</Chip>
}
```

```razor
<Chip>+@(row.Heroes.Count - 3)</Chip>
```

```razor
<Chip>@school</Chip>
```

The `.Take(3)` and the `row.Heroes.Count - 3` overflow count stay tied to each other.

- [ ] **Step 5: Adopt `Icon` for the row art**

Find it:

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components/Pages/Browse.razor -Pattern 'loading="lazy"'
```

Replace with:

```razor
<td class="cx-art"><Icon Src="@Ui.Icon(row.Icon)" /></td>
```

- [ ] **Step 6: Adopt `CountBadge` for the tabs and the facet values**

Find them:

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components/Pages/Browse.razor -Pattern 'cx-num'
```

Replace each, keeping the literal space that precedes it:

```razor
All <CountBadge Text="@Results.AcrossTypes.ToString("N0")" />
```

```razor
@type.Label <CountBadge Text="@Results.Types.GetValueOrDefault(type.Table).ToString("N0")" />
```

```razor
@value.Label <CountBadge Text="@value.Count.ToString("N0")" />
```

`cx-numeric` is a different class, for table cell alignment, and is not touched.

- [ ] **Step 7: Confirm the results loop is intact**

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components/Pages/Browse.razor -Pattern 'lastGroup|cx-group-head|colspan="12"|ShowSlot|ShowWorn|ShowSchools'
```

Expected: `lastGroup` three times, `cx-group-head` once, `colspan="12"` once, and each of the three flags twice in the markup plus once in the `@code` block.

- [ ] **Step 8: Confirm no `cx-panel`, `cx-empty` or bare chip markup is left**

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components/Pages/Browse.razor -Pattern 'class="cx-panel"|class="cx-empty"|class="cx-chip"|class="cx-num"'
```

Expected: no matches.

- [ ] **Step 9: Build and run the whole suite**

Run: `dotnet build src/FellowshipExtractor.slnx`
Expected: `0 Warning(s)`, `0 Error(s)`.

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`
Expected: PASS, 110 tests.

- [ ] **Step 10: Commit**

```powershell
git add src/FellowshipExtractor.Api/Components/Pages/Browse.razor
git commit -m @'
refactor: adopt the cx primitives in Browse

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 21: `Home`, `NotFound`, `MegaMenu` and `Omnibox` adopt the primitives

**Files:**
- Modify: `src/FellowshipExtractor.Api/Components/Pages/Home.razor`
- Modify: `src/FellowshipExtractor.Api/Components/Pages/NotFound.razor`
- Modify: `src/FellowshipExtractor.Api/Components/Layout/MegaMenu.razor`
- Modify: `src/FellowshipExtractor.Api/Components/Layout/Omnibox.razor`

**Interfaces:**
- Consumes: `Panel`, `Empty`, `Icon`, `CountBadge` from Tasks 3, 4 and 8.
- Produces: a `Home` with a computed `private string Roster => $"{Store.Vocabulary.Heroes.Count} heroes";`. No other member changes anywhere.

`.cx-hero-band`, `.cx-roster`, `.cx-cards`, `.cx-card`, `.cx-card-name`, `.cx-card-count`, `.cx-card-links` and `.cx-linked` stay as markup: one call site each and no variants. `<EntityLink Entity="entity" Class="cx-card" Tint="false">` with child content is unchanged. In `MegaMenu`, `.cx-mega`, `.cx-mega-group` and `.cx-mega-open` are read by `codex.js` and `.cx-mega-group { position: static }` resolves the full-width dropdown against `.cx-masthead { position: sticky }`, so no wrapper element is introduced between the masthead and the panel. `Omnibox` keeps its `@rendermode InteractiveServer`, its `@bind`, its `@onclick` and its `@onkeydown`, which are legitimate only because of that render mode; a primitive placed inside it renders interactively and produces the same markup, because no primitive declares an event handler, a `@bind` or any state.

- [ ] **Step 1: Adopt `Panel` for `Home`'s roster**

Find it:

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components/Pages/Home.razor -Pattern 'The roster|cx-num'
```

Replace the roster `<div class="cx-panel">` and its head with:

```razor
<Panel Head="The roster" Badge="@Roster">
```

and its closing `</div>` with `</Panel>`, keeping the `<div class="cx-roster">` body unchanged. Add to the `@code` block:

```csharp
/// <summary>How many heroes the roster panel is showing, as its head's badge reads it.</summary>
private string Roster => $"{Store.Vocabulary.Heroes.Count} heroes";
```

- [ ] **Step 2: Adopt `Panel` for `Home`'s menu groups and showcase**

Replace each menu group's `<div class="cx-panel">` and `<div class="cx-panel-head">@group.Label</div>` with `<Panel Head="@group.Label">` and its closing `</div>` with `</Panel>`.

Replace the showcase panel's opening and head with `<Panel Head="A sample of what is in there">` and its closing `</div>` with `</Panel>`.

- [ ] **Step 3: Adopt `Icon` for `Home`'s showcase card image**

Find it:

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components/Pages/Home.razor -Pattern 'loading="lazy"'
```

Replace with:

```razor
<Icon Src="@Ui.Icon(entity.Icon)" />
```

- [ ] **Step 4: Adopt `Panel` and `Empty` for `NotFound`**

Find it:

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components/Pages/NotFound.razor -Pattern 'cx-panel|cx-empty'
```

Replace the panel and its paragraph with, leaving the `@code` block untouched:

```razor
<Panel>
    <Empty Said="@Said"><a href="@Where">@Offer</a></Empty>
</Panel>
```

- [ ] **Step 5: Adopt `CountBadge` for `MegaMenu`'s two counts**

Find them:

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components/Layout/MegaMenu.razor -Pattern 'cx-num'
```

Replace each, keeping the literal space that precedes it:

```razor
@entry.Label <CountBadge Text="@entry.Count.ToString("N0")" />
```

```razor
@link.Label <CountBadge Text="@link.Count.ToString("N0")" />
```

- [ ] **Step 6: Adopt `Icon` for `Omnibox`'s result image**

Find it:

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components/Layout/Omnibox.razor -Pattern 'loading="lazy"'
```

Replace with:

```razor
<Icon Src="@hit.IconUrl" />
```

- [ ] **Step 7: Confirm the shapes are gone from all four files**

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components/Pages/Home.razor,src/FellowshipExtractor.Api/Components/Pages/NotFound.razor,src/FellowshipExtractor.Api/Components/Layout/MegaMenu.razor,src/FellowshipExtractor.Api/Components/Layout/Omnibox.razor -Pattern 'class="cx-panel"|class="cx-empty"|class="cx-num"|loading="lazy"'
```

Expected: no matches.

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components/Layout/Omnibox.razor -Pattern '@rendermode InteractiveServer'
```

Expected: one match, on the first line.

- [ ] **Step 8: Build and run the whole suite**

Run: `dotnet build src/FellowshipExtractor.slnx`
Expected: `0 Warning(s)`, `0 Error(s)`.

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`
Expected: PASS, 110 tests.

- [ ] **Step 9: Commit**

```powershell
git add src/FellowshipExtractor.Api/Components/Pages/Home.razor src/FellowshipExtractor.Api/Components/Pages/NotFound.razor src/FellowshipExtractor.Api/Components/Layout/MegaMenu.razor src/FellowshipExtractor.Api/Components/Layout/Omnibox.razor
git commit -m @'
refactor: adopt the cx primitives in Home, NotFound, MegaMenu and Omnibox

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 22: Full acceptance verification

**Files:**
- Read: every file the plan created or modified
- Create: `C:\Users\Sean\AppData\Local\Temp\claude\G--source-Fellowship-Data-Extractor\2109da8e-31a6-4995-a9b0-5036194d1636\scratchpad\after\*.html`

**Interfaces:**
- Consumes: everything. This task adds no code.

The fourteen numbered checks below are spec section 9's acceptance criteria, in order. Every one of them is run and its output confirmed before the work is called done.

**The accepted differences.** A text diff of a baseline page against its post-refactor capture may show only these four, and each occurrence must be one of them:

- **(a)** the browse rail renders `class="cx-panel cx-rail"` rather than `class="cx-rail cx-panel"`; class order is read by no selector in `codex.css`;
- **(b)** no literal space before a panel-head badge; `.cx-panel-head` is `display: flex` with `gap: 10px` and the badge carries `margin-left: auto`, so the head text and the badge are separate flex items and a trailing space collapses;
- **(c)** no literal space before a pill badge; `.cx-pills .cx-num { padding: 0 0 0 4px }` supplies the gap, which is why the reading pills already render with no space today;
- **(d)** no empty `<span class="cx-unit"></span>` for a fact with no unit; an empty inline span with no padding and no content renders nothing, and the identity rows already emit none.

Any fifth kind of difference is a regression and is fixed before this task completes.

- [ ] **Step 1: Criterion 1, the build**

Run: `dotnet build src/FellowshipExtractor.slnx --no-incremental`
Expected: `0 Warning(s)`, `0 Error(s)`. The only informational message is `NETSDK1057`.

- [ ] **Step 2: Criterion 2, the tests**

Run: `dotnet test src/FellowshipExtractor.slnx`
Expected: the new test project runs and every test passes.

- [ ] **Step 3: Criterion 3, the two folders**

```powershell
Get-ChildItem src/FellowshipExtractor.Api/Components/Cx -Filter *.razor | Select-Object -ExpandProperty Name
Get-ChildItem src/FellowshipExtractor.Api/Components/Sections -Filter *.razor | Select-Object -ExpandProperty Name
```

Expected: sixteen names under `Cx` (`Panel`, `CountBadge`, `Pills`, `Pill`, `Facts`, `Fact`, `KeyValues`, `KeyValue`, `SubHead`, `Note`, `Empty`, `Rule`, `Prose`, `Chip`, `Knob`, `Icon`) and thirteen under `Sections` (`Wording`, `Particulars`, `StatSlots`, `Ranks`, `Overflow`, `BorrowedArt`, `Grants`, `Legendaries`, `Stage`, `Knobs`, `Identity`, `Readings`, `Nearby`), each in its own file named after the component. If Task 1 struck or added a section, the expected list is the reconciled one.

- [ ] **Step 4: Criterion 4, the imports**

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components/_Imports.razor -Pattern 'Components.Cx|Components.Sections'
```

Expected: `@using FellowshipExtractor.Api.Components.Cx` and `@using FellowshipExtractor.Api.Components.Sections`.

- [ ] **Step 5: Criterion 5, `Detail`'s shape**

```powershell
Get-Content src/FellowshipExtractor.Api/Components/Shared/Detail.razor
```

Expected: the markup is the eight section tags inside one `<CascadingValue Value="Entity">`, and the `@code` block holds only `Entity`, `Facts`, `Schools`, `Lender`, `SchoolFacet`, `OnParametersSet` and `Filter`.

- [ ] **Step 6: Criterion 6, `EntityPage`'s shape**

```powershell
Get-Content src/FellowshipExtractor.Api/Components/Pages/EntityPage.razor
```

Expected: the markup is the page title, the crumbs, the `<h1>` and the five section tags inside the `.cx-entity` grid, all inside one `<CascadingValue Value="Here">`.

- [ ] **Step 7: Criterion 7, no primitive injects**

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components/Cx -Recurse -Pattern '@inject'
```

Expected: no matches.

- [ ] **Step 8: Criterion 8, no section reads an identity**

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components/Sections -Recurse -Pattern 'Entity\.Table|Entity\.NetId'
```

Expected: no matches.

- [ ] **Step 9: Criterion 9, no event directives outside `Omnibox`**

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components -Recurse -Pattern '@onclick|@onchange|@oninput|@bind'
Select-String -Path src/FellowshipExtractor.Api/Components -Recurse -Pattern '@preservewhitespace'
```

Expected: the first finds matches only in `Components/Layout/Omnibox.razor`; the second finds nothing.

- [ ] **Step 10: Criterion 10, confirm the baseline exists and predates the refactor**

```powershell
$baseline = "C:\Users\Sean\AppData\Local\Temp\claude\G--source-Fellowship-Data-Extractor\2109da8e-31a6-4995-a9b0-5036194d1636\scratchpad\baseline"
Get-ChildItem $baseline | Select-Object Name, Length, LastWriteTime
```

Expected: the eight files from Task 1. If they are missing, the baseline cannot be recreated after the refactor and criterion 11 is answered by the browser comparison in Steps 13 and 14 alone.

- [ ] **Step 11: Criteria 13 and 14, run the app**

`output/export/` is populated at `G:\source\Fellowship_Data_Extractor\output\export`. Docker must be running. From the repo root, in a tracked background shell:

```powershell
$env:FELLOWSHIP_PORT_OFFSET = "0"
aspire run
```

Then:

```powershell
while (-not (Test-NetConnection -ComputerName localhost -Port 5280 -InformationLevel Quiet)) { }
(Invoke-WebRequest -Uri http://localhost:5280/health -UseBasicParsing).StatusCode
```

Expected: `200`. The API cannot be started standalone: `Program.cs` calls `AddAzureBlobContainerClient("icons")` and `IconSeeder` is an `IHostedService` calling `CreateIfNotExistsAsync`, so a missing or unreachable blob endpoint fails host start. The AppHost supplies the Azurite container, the `icons` connection string and `FELLOWSHIP_EXPORT`.

- [ ] **Step 12: Criterion 12, capture the same eight pages and diff them**

The three addresses are recomputed with the same command Task 1 used, so the two captures visit the same rows even if the shell has been restarted since. Confirm they match the three the Task 1 note recorded.

```powershell
$baseline  = "C:\Users\Sean\AppData\Local\Temp\claude\G--source-Fellowship-Data-Extractor\2109da8e-31a6-4995-a9b0-5036194d1636\scratchpad\baseline"
$after     = "C:\Users\Sean\AppData\Local\Temp\claude\G--source-Fellowship-Data-Extractor\2109da8e-31a6-4995-a9b0-5036194d1636\scratchpad\after"
New-Item -ItemType Directory -Force $after | Out-Null

$rows = Get-Content G:\source\Fellowship_Data_Extractor\output\export\entities.jsonl

function Address($row) {
    $o = $row | ConvertFrom-Json
    $hit = Select-String -Path G:\source\Fellowship_Data_Extractor\src\FellowshipExtractor.Api\PublicTypes.cs -Pattern "new PublicType\(""$($o.table)"", ""([^""]+)""" | Select-Object -First 1
    "/entity/$($hit.Matches[0].Groups[1].Value)/$($o.netId)"
}

$legendary = Address ($rows | Where-Object { $_ -match '"legendaryStats"' } | Select-Object -First 1)
$ranks     = Address ($rows | Where-Object { $_ -match '"rankRuns"' }       | Select-Object -First 1)
$knobs     = Address ($rows | Where-Object { $_ -match '"stats"' -and $_ -match '"heroes"' } | Select-Object -First 1)

$legendary; $ranks; $knobs

$pages = [ordered]@{
    "home.html"             = "/"
    "browse.html"           = "/browse"
    "browse-grouped.html"   = "/browse?group=slot"
    "not-found.html"        = "/not-found"
    "entity-knobs.html"     = $knobs
    "entity-legendary.html" = $legendary
    "entity-rankruns.html"  = $ranks
    "entity-unknown.html"   = "/entity/items/999999"
}

foreach ($name in $pages.Keys) {
    (Invoke-WebRequest -Uri ("http://localhost:5280" + $pages[$name]) -UseBasicParsing).Content |
        Set-Content -Path (Join-Path $after $name) -Encoding utf8
}

foreach ($name in $pages.Keys) {
    "=== $name ==="
    Compare-Object (Get-Content (Join-Path $baseline $name)) (Get-Content (Join-Path $after $name))
}
```

Expected: every reported difference is one of the four accepted differences (a) to (d) above. Read each one and say which of the four it is. Anything else is a regression to fix before this task completes.

- [ ] **Step 13: Criterion 11, compare the three entity pages in a browser**

Open each of `$knobs`, `$legendary` and `$ranks` at `http://localhost:5280` and confirm against the notes taken in Task 1 Step 11:

- the hero item shows the hero select, the rarity rungs, the item level and the tempered inputs; clicking a rung repaints the tooltip stage; changing a number repaints it;
- the legendary item's granted ability and each legendary roll draw their own text, facts and stats and not the item's;
- the trait or ability's Printed row shows its values separated by ` / ` on one line, with no blank lines, and with its coloured rich-text runs styled.

- [ ] **Step 14: Criterion 11, compare the remaining pages in a browser**

Open and confirm:

- `/browse` ungrouped and `/browse?group=slot` grouped, with the group-head rows, the chips and the facet rail intact;
- `/`, with the roster, each menu group and the showcase;
- `/not-found`, and `/entity/items/999999`.

- [ ] **Step 15: Stop the background shell running `aspire run`**

Terminate the tracked background shell. Confirm with:

```powershell
Test-NetConnection -ComputerName localhost -Port 5280 -InformationLevel Quiet
```

Expected: `False`.

- [ ] **Step 16: Confirm the working tree carries only this plan's changes**

```powershell
git status --porcelain
```

Expected: nothing from this plan is left unstaged. Any other session's uncommitted files are still there and are left exactly as they are.
