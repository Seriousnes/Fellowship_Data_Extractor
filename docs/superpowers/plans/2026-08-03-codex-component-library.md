# Codex Component Library Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Express the shipped `cx-` design system as sixteen primitive components and thirteen section components, move the one 540-line global stylesheet into a `.razor.scss` beside each component that draws a class of its own, adopt both across every page of `src/FellowshipExtractor.Api`, and cover them with a bUnit test project that asserts rendered markup.

**Architecture:** Two layers. Primitives live in `Components/Cx/`, take plain values, inject nothing, and render one fixed skeleton of `cx-` markup each. Sections live in `Components/Sections/`, own exactly one `@if` block that is inlined in `Detail.razor` or `EntityPage.razor` today, read their record from a cascading parameter, and compose primitives. `Detail` re-supplies the `Entity` cascade at every nesting level; `EntityPage` supplies a second cascade of type `Bearings` carrying the page's own addresses. Styling moves with the markup: twenty-seven `.razor.scss` files compiled by `AspNetCore.SassCompiler` and scoped by Blazor's CSS isolation, over a global `Styles/codex.scss` holding the design tokens, the resets, the `@scope (body) to (.fx-tip)` wrapper and the two classes more than one component renders.

**Tech Stack:** .NET 10 (`net10.0`), Blazor Web App with static server rendering, `AspNetCore.SassCompiler` 1.102.0 carrying its own dart-sass binaries, Blazor CSS isolation, bUnit 2.8.6, xunit.v3 3.2.2, AngleSharp (through bUnit), PowerShell, Aspire AppHost with an Azurite container.

## Global Constraints

- Every command in this plan runs from the repo root `G:\source\Fellowship_Data_Extractor`, in PowerShell. Python is not installed on this machine; do not reach for it.
- Target framework `net10.0`. The installed SDK is `10.0.400-preview.0.26322.102`, so `NETSDK1057` is the one informational message a build is allowed to print.
- The repo has no `Directory.Packages.props` and no central package management. Every `PackageReference` carries its own literal `Version=` attribute.
- Exact package versions: `AspNetCore.SassCompiler` `1.102.0`, `bunit` `2.8.6`, `xunit.v3` `3.2.2`, `Microsoft.NET.Test.Sdk` `18.8.1`, `xunit.runner.visualstudio` `3.1.5`.
- **Never anchor an edit on a line number.** A concurrent session has been rewriting `Detail.razor`, `EntityPage.razor`, `Browse.razor`, `Home.razor`, `NotFound.razor`, `CodexView.cs` and `Codex.cs`. Locate every edit site with the `Select-String` command the step gives, and anchor on CSS class names, component names, guard expressions and HTML structure.
- **The static-SSR trap.** Every page except `Omnibox.razor` is statically server rendered: `Routes.razor` declares no render mode and `Omnibox.razor` is the only file carrying `@rendermode InteractiveServer`. On a statically rendered component `@onclick`, `@onchange`, `@oninput` and `@bind` compile without error and do nothing at runtime. `data-stage`, `data-knob`, `data-choice`, `aria-pressed`, `data-tip` and the hero select's literal `onchange="location.search = '?hero=' + encodeURIComponent(this.value)"` are plain HTML attributes and are emitted verbatim in markup. Never route one through a component parameter and never convert one to a Blazor directive.
- **`@preservewhitespace` appears nowhere**, in any `.razor`, `.csproj` or `.props` file. Whitespace-only text nodes containing a newline are removed by the Razor compiler; whitespace on the same line as its neighbours is kept. That is why `@label <CountBadge …/>` renders a space and a tag on its own line does not.
- **Assertion rule.** Use `cut.MarkupMatches(expected)` for structure: it is a semantic HTML comparison that normalises insignificant whitespace, so it survives razor indentation and serialises `<br />` and `<hr />` the same on both sides. Use `Assert.Equal(expected, cut.Markup.Bare())` or `Assert.Equal(expected, cut.Find(selector).OuterHtml.Bare())` wherever **whitespace itself is the subject**: anything rendering into a `cx-prose` context (`.cx-prose` is `white-space: pre-line`, so a stray newline is a rendered blank line), and the three "no literal space before the badge/unit" cases.
- **Prose assertions are scoped to the prose element.** `Prose` and `KeyValue` with `Class="cx-prose"` are their own root, so `Assert.Equal(expected, cut.Markup.Bare())` stands. `Wording`, `Overflow` and `Ranks` also render a `<Note>` whose literal text spans several source lines and therefore carries the implementer's own indentation into `cut.Markup`; assert those three with `cut.MarkupMatches` for structure plus `Assert.Equal(expected, cut.Find("p.cx-prose").OuterHtml.Bare())` or `Assert.Equal(expected, cut.Find("span.cx-v.cx-prose").OuterHtml.Bare())` for the prose. If an exact-assertion expected string in this plan differs from what the component actually renders, **the actual output is the answer: record it and keep `Assert.Equal`.** Never relax one of these to `MarkupMatches`.
- **Styling is Sass and no `.css` file is hand-written or committed.** Every stylesheet is generated from a `.scss` by `AspNetCore.SassCompiler` 1.102.0, whose targets declare `BeforeTargets="Build;ResolveScopedCssInputs;BundleMinify;ResolveProjectStaticWebAssets"`, so Sass runs before the Razor SDK gathers scoped CSS and the chain needs no custom MSBuild. The package carries its own dart-sass binaries per RID, so there is no npm, no node and no PATH dependency. `.gitignore` carries `*.css` and `*.css.map`, and a fresh clone plus `dotnet build` produces every stylesheet.
- **A component carries a scope attribute if and only if a `.razor.scss` sits beside it.** Twenty-seven files do: fifteen of the sixteen primitives, four of the thirteen sections and eight of the components that already exist. `Prose`, `NotFound` and the other nine sections render elements with no attribute at all.
- **The scope attribute lands on the *last* element of a selector, and only on elements the Razor compiler itself emitted.** So a rule reaching `MarkupString` content or a child component's markup goes inside a `::deep` block, which moves the attribute to the *first* element and leaves the descendants unqualified. Three rules in the whole library are in that position, each a `<code>` inside content a caller passed in: `.cx-note code`, `.cx-facts code` and `.cx-kv code`. Written without `::deep` they compile, bundle and match nothing.
- **`MarkupMatches` already forgives the scope attribute, and only it.** Measured on bunit 2.8.6: it passes when the expected markup carries no `b-` attribute and when it carries a different one, and fails on any other extra attribute, `data-extra` and `data-extra="1"` both included. So every `MarkupMatches` assertion in this plan is written without the attribute and no hash is ever typed into a test. `cut.Markup`, `OuterHtml` and `InnerHtml` are raw strings and forgive nothing, so each exact-string assertion against a component that carries the attribute reads through `.Bare()`, the single helper the test project owns.
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
- Produces: a written reconciliation note listing, for each of the thirteen sections named in the spec, whether its guard and head still exist in the working tree; the rule-by-rule drift in `codex.css` since the revision the spec's stylesheet assignment was made against; and eight baseline HTML files that Task 23 diffs against. Nothing in the repository changes.

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

Tasks 4 to 10 write exact markup for sixteen primitives. That markup is only correct while `codex.css` still styles the classes it carries, and `codex.css` is one of the files the concurrent session edits: it has already deleted `table.cx-rows .cx-tag`, which had no razor user.

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

- [ ] **Step 9: Diff the stylesheet the spec's rule-by-rule assignment was made against**

The spec assigns every rule in `codex.css` to an owning `.scss`: sections 2.9, 3, 4 and 6 name one owner each, and acceptance criterion 21 is that every rule appears in exactly one of them. That census was taken against the file as it stood, and `codex.css` is one of the files the concurrent session edits. A rule that has appeared since has no owner, and a rule that has changed would be carried into a `.scss` in its old wording.

Read the whole diff rather than a summary, because the note needs the rules themselves:

```powershell
git diff HEAD -- src/FellowshipExtractor.Api/wwwroot/css/codex.css
```

Then take the selector census the later tasks are checked against, and keep the file as the refactor starts from it:

```powershell
$scratch = "C:\Users\Sean\AppData\Local\Temp\claude\G--source-Fellowship-Data-Extractor\2109da8e-31a6-4995-a9b0-5036194d1636\scratchpad"
New-Item -ItemType Directory -Force $scratch | Out-Null

Copy-Item src/FellowshipExtractor.Api/wwwroot/css/codex.css (Join-Path $scratch "codex.css.before") -Force

# Comments come out first. A block comment's interior lines start with neither / nor {, and the
# selector pattern's character class matches newlines, so a comment left in swallows the rule that
# follows it and lands in the census as a selector nothing will ever own.
$before = (Get-Content src/FellowshipExtractor.Api/wwwroot/css/codex.css -Raw) -replace '(?s)/\*.*?\*/', ''

$selectors = [regex]::Matches($before, '(?m)^\s*([^@{}/][^{}]*?)\s*\{') |
    ForEach-Object { $_.Groups[1].Value.Trim() -replace '\s+', ' ' } |
    Sort-Object -Unique

$selectors | Set-Content (Join-Path $scratch "selectors.before.txt") -Encoding utf8
$selectors.Count
```

Expected: a file of unique selectors, one per line, and a count. Read the file before going on: every line must look like a selector. A line carrying prose is a comment the strip missed, and it would become a phantom row in the census that no stylesheet can discharge.

Task 3 assigns each of these to a stylesheet, and Task 23 proves nothing was dropped.

For every rule the `git diff` reports as added, changed or removed, write into the note: the selector, whether it is new, changed or gone, and which stylesheet the spec's ownership rule puts it in. The rule is spec section 2.8: one component renders the element, so that component's `.razor.scss` owns it with no `::deep`; the element is `MarkupString` content or a descendant of child content, so the owning component reaches it through `::deep`; two or more components render it, so it stays in `Styles/codex.scss`. A rule with no owner under that test is reported in the note and assigned before any `.scss` is written.

- [ ] **Step 10: Find the three entity addresses the baseline needs**

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

Expected: three addresses of the form `/entity/items/5146`. Write all three into the reconciliation note, because Task 23 visits the same three.

- [ ] **Step 11: Start the app in a tracked background shell**

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

- [ ] **Step 12: Capture the eight baseline pages**

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

- [ ] **Step 13: Look at the three entity pages in a browser and note what the tests cannot see**

Open `http://localhost:5280` plus each of the three addresses. Confirm and write down: the hero select, the four rarity rungs row, the item level and tempered inputs; that clicking a rung repaints the tooltip stage; that changing a number repaints it; that the Printed row shows its values separated by ` / ` on one line with no blank lines and with coloured rich-text runs.

Two more, because they are what the move to scoped styling can break silently and no build or unit test sees either. Write down what they look like now, so Task 23 has something to compare against:

- **Rich text is coloured.** On the entity whose description carries coloured runs, note the colour each `rt-*` span draws in, that a `.sub` run has a dotted underline and a help cursor, that a `.tok` run is in the mono face and the negative colour, and that an `.rt-unstyled` run takes the surrounding ink. Note the same on the rank-progression Printed row, which draws its runs inside a `<span class="cx-v cx-prose">` rather than a `<p>`.
- **Links do not underline on hover.** Hover a browse type tab, a facet value, a pill, a crumb, a results-table column header and a pager link, and note the colour each takes.

- [ ] **Step 14: Stop the background shell running `aspire run`**

Terminate the tracked background shell. Confirm with:

```powershell
Test-NetConnection -ComputerName localhost -Port 5280 -InformationLevel Quiet
```

Expected: `False`.

- [ ] **Step 15: Write the reconciliation note**

Write `C:\Users\Sean\AppData\Local\Temp\claude\G--source-Fellowship-Data-Extractor\2109da8e-31a6-4995-a9b0-5036194d1636\scratchpad\reconciliation.md` containing: the thirteen section names with present/absent and any changed guard or head wording; any section found in the pages that the spec does not name, with the neighbour its new task is modelled on; any renamed `CodexView`, `PublicTypes` or `Codex` member; every `codex.css` rule that has appeared, changed or gone since the spec's census, each with the stylesheet that will own it; the selector count from Step 9; the three entity addresses; and the eight baseline file names.

- [ ] **Step 16: Apply the reconciliation to this plan**

Strike the task of any absent section. Add a task for any new section, modelled on its nearest neighbour. Update every head string, guard expression and member name in the remaining tasks to the wording the note records. Add each newly appeared or changed CSS rule to the `.razor.scss` its owner's task writes, in the wording the working tree carries. Nothing is committed by this task.

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

**`GlobalUsings.cs` grows in two later steps.** A `global using` naming a namespace that contains no types is `CS0246`, so `FellowshipExtractor.Api.Components.Cx` is added in Task 4 when `Cx/` gains its first component and `FellowshipExtractor.Api.Components.Sections` in Task 11.

**The test project references `AspNetCore.SassCompiler` nowhere and carries no `sasscompiler.json`.** Scoped styling is compiled and bundled by the Api project, and the scope attribute is baked into each component at that project's build, so a test renders exactly what a browser gets.

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

### Task 3: The Sass toolchain and the global stylesheet

**Files:**
- Create: `src/FellowshipExtractor.Api/sasscompiler.json`
- Create: `src/FellowshipExtractor.Api/Styles/codex.scss`
- Create: `src/FellowshipExtractor.Api/Styles/_shared.scss`
- Modify: `src/FellowshipExtractor.Api/FellowshipExtractor.Api.csproj`
- Modify: `src/FellowshipExtractor.Api/Components/App.razor`
- Modify: `.gitignore`
- Delete from the index: `src/FellowshipExtractor.Api/wwwroot/css/codex.css`
- Create: `C:\Users\Sean\AppData\Local\Temp\claude\G--source-Fellowship-Data-Extractor\2109da8e-31a6-4995-a9b0-5036194d1636\scratchpad\stylesheet-census.md`

**Interfaces:**
- Produces: a build in which `Styles/codex.scss` compiles to `wwwroot/css/codex.css` and any `Foo.razor.scss` under `Components/` compiles to a sibling `Foo.razor.css` that the Razor SDK scopes and bundles into `FellowshipExtractor.Api.styles.css`. Every task from Task 4 on writes `.razor.scss` files into that build. `Styles/_shared.scss` produces the Sass API those files consume: the variables `$gutter` and `$on-gold`, and the mixins `plated`, `sunken`, `edged`, `gold-slab` and `eyebrow`, reached with `@use "../../Styles/shared" as *;`.

This task lands before the first primitive because every primitive task from Task 4 on writes a `.razor.scss`, and with no `sasscompiler.json` those files compile to nothing and say nothing about it. Configuration is required: the package silently does no work without it.

**The single most consequential line in this task is the `App.razor` link.** Scoped styles are served as `{AssemblyName}.styles.css` at the web root. Without that link no scoped rule loads at all, every component renders unstyled, and the build stays green.

- [ ] **Step 1: Add the package reference**

Find the Api project's package group:

```powershell
Select-String -Path src/FellowshipExtractor.Api/FellowshipExtractor.Api.csproj -Pattern 'PackageReference'
```

Expected: three references, `Aspire.Azure.Storage.Blobs`, `Microsoft.AspNetCore.OpenApi` and `Microsoft.OpenApi`, in one `ItemGroup`. Add a fourth to that same group, keeping whatever ordering the group already reads in:

```xml
<PackageReference Include="AspNetCore.SassCompiler" Version="1.102.0" />
```

The repo has no `Directory.Packages.props` and no central package management, so the version is written literally here exactly as the other three are.

- [ ] **Step 2: Write `sasscompiler.json`**

`src/FellowshipExtractor.Api/sasscompiler.json`, at the project root beside the csproj:

```json
{
  "Source": "Styles",
  "Target": "wwwroot/css",
  "GenerateScopedCss": true,
  "ScopedCssFolders": [ "Components" ],
  "Configurations": {
    "Debug": { "Arguments": "--style=expanded --no-source-map" }
  }
}
```

`--no-source-map` is what keeps a `.css.map` from appearing beside every generated stylesheet.

- [ ] **Step 3: Write `Styles/_shared.scss`**

The partial. It declares Sass variables and mixins and emits no stylesheet of its own, so every sheet that `@use`s it costs nothing in output.

```scss
/// The page gutter: the masthead bar, the mega-menu columns, <main> and the footer all use it.
$gutter: clamp(12px, 2.4vw, 28px);

/// The ink a gold slab is printed in. A literal rather than a token, because the shipped palette
/// carries no colour for text on gold.
$on-gold: #0b141d;

/// The client's own nine-sliced texture, the same one the tooltip draws itself on: a 2px warm gold
/// border round a #121D28 interior. Worn by a panel and by a card.
@mixin plated {
  border: 2px solid transparent;
  border-image: var(--chrome-panel) 2 fill stretch;
  padding: 14px 16px;
}

/// A field sunk into the ground: the omnibox, the range inputs, the knobs, a roster entry.
@mixin sunken {
  background: var(--sunken);
  border: 1px solid var(--line);
  color: var(--ink);
}

/// What a sunken field does when it takes focus.
@mixin edged {
  border-color: var(--gold-deep);
  outline: none;
}

/// A gold slab: the apply button, the omnibox's view-all row, a pressed chip, the current page.
@mixin gold-slab {
  background: var(--gold-deep);
  color: $on-gold;
}

/// The small uppercase caption over a list, worn by a sub-head and by a mega-menu section head.
@mixin eyebrow {
  margin: 0 0 3px;
  font-size: 10px;
  letter-spacing: .14em;
  text-transform: uppercase;
  color: var(--faint);
}
```

**No Sass variable is named after a custom property and none precomputes one.** `--core-secondary-900`, `--core-primary-main`, `--semantic-negative`, `--game-stat-primary`, `--chrome-panel` and the rest are written into an inline `<style>` by `App.razor` out of data mined from the shipped build. Sass never sees a value for any of them. A `$gold: #d4af37` anywhere in this project would be a second, wrong palette the build cannot tell apart from the real one.

- [ ] **Step 4: Write `Styles/codex.scss`**

The global sheet: the custom properties, the `html` and `body` rules, the resets inside the `@scope (body) to (.fx-tip)` wrapper, and the handful of rules a single component cannot own.

```scss
@use "shared" as *;

/*
    Values for the properties and variables here are set in App.razor from data-mined tokens.
*/
:root {
  --ground: var(--core-secondary-1000);
  --panel: var(--core-secondary-900);
  --raised: var(--core-secondary-800);
  --sunken: #08111a;
  --line: var(--core-secondary-700);
  --line-soft: #16212c;
  --ink: var(--core-text-primary);
  --muted: var(--core-text-secondary);
  --faint: var(--core-text-info);
  --gold: var(--core-primary-main);
  --gold-bright: var(--core-primary-200);
  --gold-deep: var(--core-primary-500);

  --display: ArnoPro, "Arno Pro", "Iowan Old Style", "Palatino Linotype", Palatino, "Book Antiqua",
             "Times New Roman", serif;
  --sans: "Noto Sans", system-ui, -apple-system, "Segoe UI", Roboto, sans-serif;
  --mono: ui-monospace, "Cascadia Mono", "JetBrains Mono", Menlo, Consolas, monospace;

  --rail: 268px;
  --wide: 1440px;
}

html { scrollbar-gutter: stable; }

body {
  margin: 0;
  background:
    radial-gradient(1200px 600px at 50% -280px, #16222e 0%, transparent 70%),
    var(--ground);
  color: var(--ink);
  font: 400 14px/1.55 var(--sans);
  font-variant-numeric: tabular-nums;
  min-height: 100vh;
}

@scope (body) to (.fx-tip) {

  *, *::before, *::after { box-sizing: border-box; }

  a { color: var(--gold-bright); text-decoration: none; }
  a:hover { text-decoration: underline; }

  :focus-visible { outline: 2px solid var(--gold-bright); outline-offset: 2px; }

  h1 {
    font-family: var(--display); font-weight: 400; letter-spacing: .04em;
    font-size: clamp(26px, 4vw, 40px); line-height: 1.1; margin: 6px 0 14px;
  }
  h2 {
    font-family: var(--display); font-weight: 400; letter-spacing: .07em; text-transform: uppercase;
    font-size: 19px; margin: 0 0 10px; color: var(--core-secondary-000);
  }

  /* Two components draw a card: Home's own menu-entry anchor, and the EntityLink the showcase
     passes Class="cx-card" to. A class with two owners is styled here, where no scope attribute
     narrows it to one of them. */
  .cx-card {
    @include plated;
    display: block; color: var(--ink);
  }
  .cx-card:hover { text-decoration: none; }

  /* Prose is styled here for the same reason - a <p> from Prose and a <span class="cx-v cx-prose">
     from KeyValue both wear the class - and for a second one. App.razor emits
     Ui.RichTextCss("cx-prose") into the page's inline <style>, which writes one .cx-prose .rt-xxx
     rule per shipped text-style row at runtime. Those values are mined from the build and nothing
     here knows them, so the colour table cannot move into a stylesheet. Keeping the rest of the
     family beside it keeps one specificity story for the whole of .cx-prose. The runs themselves
     arrive as a MarkupString and carry no scope attribute, which is the other reason these three
     rules cannot be scoped. */
  .cx-prose { white-space: pre-line; color: var(--ink); }
  .cx-prose .sub { font-weight: 600; border-bottom: 1px dotted currentColor; cursor: help; }
  .cx-prose .tok { color: var(--semantic-negative); font-family: var(--mono); }
  .cx-prose .rt-unstyled { color: inherit; }

  /* Drawn by no component today. Kept whole, because the panels this markup belongs to come back
     with the shape they had: a Panel As="details" for the record, a token card for the
     substitutions. */
  .cx-token { border-bottom: 1px solid var(--line-soft); padding: 8px 0; }
  .cx-token:last-child { border-bottom: 0; }
  .cx-token-name { font-family: var(--mono); color: var(--gold-bright); font-size: 12.5px; }
  .cx-token-read { color: var(--ink); font-weight: 600; }
  .cx-token-work { color: var(--faint); font-size: 12px; }

  pre.cx-raw {
    margin: 0; padding: 10px 12px; background: var(--sunken); border: 1px solid var(--line);
    overflow-x: auto; font: 12px/1.5 var(--mono); color: var(--core-secondary-100);
  }
}
```

If Task 1 Step 9 recorded a rule in this family that has changed or appeared since, write it here in the wording the working tree carries.

- [ ] **Step 5: Link the scoped bundle from `App.razor`**

Find the existing link rather than a line number:

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components/App.razor -Pattern 'css/codex.css'
```

Expected: one match. Add immediately after it:

```html
<link rel="stylesheet" href="FellowshipExtractor.Api.styles.css" />
```

so the head reads:

```html
<link rel="stylesheet" href="css/codex.css" />
<link rel="stylesheet" href="FellowshipExtractor.Api.styles.css" />
```

- [ ] **Step 6: Ignore every generated stylesheet**

Append to `.gitignore`, after the existing `.vs/` line:

```
*.css
*.css.map
```

`.scss` is unaffected by the pattern and stays the only committed source of styling.

- [ ] **Step 7: Untrack the hand-written stylesheet and remove it from the working tree**

`wwwroot/css/codex.css` is tracked, and a pattern added to `.gitignore` does not untrack a tracked file. Task 1 Step 9 already copied it to the scratchpad as `codex.css.before`; confirm that copy exists before deleting anything.

```powershell
$scratch = "C:\Users\Sean\AppData\Local\Temp\claude\G--source-Fellowship-Data-Extractor\2109da8e-31a6-4995-a9b0-5036194d1636\scratchpad"

Test-Path (Join-Path $scratch "codex.css.before")

git rm --cached src/FellowshipExtractor.Api/wwwroot/css/codex.css
Remove-Item src/FellowshipExtractor.Api/wwwroot/css/codex.css
```

Expected: `True`, then `rm 'src/FellowshipExtractor.Api/wwwroot/css/codex.css'`, then the file gone from disk. Deleting it from disk is what makes Step 8 prove the build regenerates it rather than finding the old one still sitting there.

- [ ] **Step 8: Build and confirm Sass ran**

Run: `dotnet build src/FellowshipExtractor.slnx`
Expected: `0 Warning(s)`, `0 Error(s)`.

```powershell
Get-ChildItem src/FellowshipExtractor.Api/wwwroot/css -Filter *.css | Select-Object Name, Length
Get-ChildItem src/FellowshipExtractor.Api -Recurse -Filter *.css.map | Select-Object FullName
```

Expected: `codex.css` exists again and is non-empty, and no `.css.map` anywhere. A missing `codex.css` means `sasscompiler.json` was not found: it belongs beside the csproj, not beside the solution.

- [ ] **Step 9: Confirm the generated stylesheet against the one that was deleted**

The generated file is not byte-identical to the deleted one and is not meant to be: it is the global subset. Every rule the spec assigns to a component leaves this file and arrives in that component's `.razor.scss` in Tasks 4 to 22. Show exactly which rules those are, so the difference is enumerated rather than assumed:

```powershell
$scratch = "C:\Users\Sean\AppData\Local\Temp\claude\G--source-Fellowship-Data-Extractor\2109da8e-31a6-4995-a9b0-5036194d1636\scratchpad"

function Selectors($path) {
    # Comments first, for the reason Task 1 Step 9 gives: the selector pattern matches across
    # newlines, so a block comment left in is captured as part of the selector after it.
    $text = (Get-Content $path -Raw) -replace '(?s)/\*.*?\*/', ''

    [regex]::Matches($text, '(?m)^\s*([^@{}/][^{}]*?)\s*\{') |
        ForEach-Object { $_.Groups[1].Value.Trim() -replace '\s+', ' ' } |
        Sort-Object -Unique
}

$before = Get-Content (Join-Path $scratch "selectors.before.txt")
$after  = Selectors src/FellowshipExtractor.Api/wwwroot/css/codex.css

"kept:    " + (Compare-Object $before $after -IncludeEqual -ExcludeDifferent).Count
"departed:" + (Compare-Object $before $after | Where-Object SideIndicator -eq '<=').Count
"new:     " + (Compare-Object $before $after | Where-Object SideIndicator -eq '=>').Count

Compare-Object $before $after | Where-Object SideIndicator -eq '=>'
```

Expected: every selector still in the file is one written in Step 4, `new` is zero, and the only reported additions would be a Sass mistake. Read the kept list and confirm each entry is one of `:root`, `html`, `body`, `@scope`'s `*`, `a`, `a:hover`, `:focus-visible`, `h1`, `h2`, `.cx-card`, `.cx-card:hover`, the four `.cx-prose` rules, the five `.cx-token*` rules and `pre.cx-raw`.

- [ ] **Step 10: Write the census the later tasks are checked against**

Write `C:\Users\Sean\AppData\Local\Temp\claude\G--source-Fellowship-Data-Extractor\2109da8e-31a6-4995-a9b0-5036194d1636\scratchpad\stylesheet-census.md`: one line per departed selector, giving the selector and the `.razor.scss` the spec assigns it to, with the task number that writes that file.

```powershell
$scratch = "C:\Users\Sean\AppData\Local\Temp\claude\G--source-Fellowship-Data-Extractor\2109da8e-31a6-4995-a9b0-5036194d1636\scratchpad"

function Selectors($path) {
    # Comments first, for the reason Task 1 Step 9 gives: the selector pattern matches across
    # newlines, so a block comment left in is captured as part of the selector after it.
    $text = (Get-Content $path -Raw) -replace '(?s)/\*.*?\*/', ''

    [regex]::Matches($text, '(?m)^\s*([^@{}/][^{}]*?)\s*\{') |
        ForEach-Object { $_.Groups[1].Value.Trim() -replace '\s+', ' ' } |
        Sort-Object -Unique
}

$before = Get-Content (Join-Path $scratch "selectors.before.txt")
$after  = Selectors src/FellowshipExtractor.Api/wwwroot/css/codex.css

$departed = Compare-Object $before $after | Where-Object SideIndicator -eq '<=' |
    ForEach-Object { "| ``$($_.InputObject)`` | | |" }

@("# Stylesheet census", "", "| selector | owning .razor.scss | task |", "|---|---|---|") + $departed |
    Set-Content (Join-Path $scratch "stylesheet-census.md") -Encoding utf8
```

Then fill the two empty columns from the spec, which names an owner for every one: sections 3.1 to 3.16 for the primitives, 4.2, 4.3, 4.9 and 4.10 for the four sections with a sheet, and 6.2, 6.3, 6.4, 6.6 and 6.7 for the eight components that already exist. A row you cannot fill is a rule with no owner and is reported before Task 4 starts.

The table is the record of which file owns each rule, and Task 23 Step 12 reads it as such. Many of these selectors are deliberately rewritten on the way into their new home, because the scope attribute moves what a selector has to say: `.cx-facts tr` becomes a bare `tr` in `Fact.razor.scss`, `.cx-pills li a` becomes `.cx-pills a` in `Pill.razor.scss`, and `.cx-mega-sec-head, .cx-sub-head` becomes an `@include eyebrow` in two files. Record the new spelling in the owner column where it differs, so the table can be read against the files later.

- [ ] **Step 11: Confirm no `.css` is staged or untracked**

```powershell
git status --porcelain -- '*.css'
git ls-files '*.css'
git status --porcelain --ignored -- '*.css'
```

Expected: the first shows the staged deletion `D` on `src/FellowshipExtractor.Api/wwwroot/css/codex.css` and nothing else; the second returns nothing, because `git rm --cached` took that path out of the index; the third shows the regenerated `codex.css` with the `!!` ignored marker rather than `??`, which is what says the pattern is doing its job.

- [ ] **Step 12: Run the whole suite**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`
Expected: PASS, 1 test. Nothing in this task changes a component, so the harness canary is still the only test.

- [ ] **Step 13: Commit**

The staged deletion from Step 7 is already in the index and needs no path of its own here; the six other paths are named explicitly.

```powershell
git add src/FellowshipExtractor.Api/FellowshipExtractor.Api.csproj src/FellowshipExtractor.Api/sasscompiler.json src/FellowshipExtractor.Api/Styles/codex.scss src/FellowshipExtractor.Api/Styles/_shared.scss src/FellowshipExtractor.Api/Components/App.razor .gitignore
git commit -m @'
build: compile styling from Sass and scope it per component

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

Then confirm the commit carries the deletion and no generated file:

```powershell
git show --stat --name-status HEAD
```

Expected: `M` on the csproj, `App.razor` and `.gitignore`, `A` on `sasscompiler.json` and the two `.scss` files, and `D` on `wwwroot/css/codex.css`. No `.css` is added.

---

### Task 4: `CountBadge`, `Rule`, `SubHead`, `Note` and `Icon`

**Files:**
- Create: `src/FellowshipExtractor.Api/Components/Cx/CountBadge.razor`, `CountBadge.razor.scss`
- Create: `src/FellowshipExtractor.Api/Components/Cx/Rule.razor`, `Rule.razor.scss`
- Create: `src/FellowshipExtractor.Api/Components/Cx/SubHead.razor`, `SubHead.razor.scss`
- Create: `src/FellowshipExtractor.Api/Components/Cx/Note.razor`, `Note.razor.scss`
- Create: `src/FellowshipExtractor.Api/Components/Cx/Icon.razor`, `Icon.razor.scss`
- Modify: `src/FellowshipExtractor.Api/Components/_Imports.razor`
- Modify: `src/FellowshipExtractor.Api.Tests/GlobalUsings.cs`
- Test: `src/FellowshipExtractor.Api.Tests/Cx/CountBadgeTests.cs`, `RuleTests.cs`, `SubHeadTests.cs`, `NoteTests.cs`, `IconTests.cs`

**Interfaces:**
- Consumes: the Sass build from Task 3, including `Styles/_shared.scss`'s `eyebrow` mixin.
- Produces: `CountBadge(string Text)`, `Rule()`, `SubHead(RenderFragment? ChildContent)`, `Note(RenderFragment? ChildContent)`, `Icon(string Src)`. `Panel` and `Pill` render `CountBadge` internally in Tasks 5 and 6.

These five are the design system's fixed skeletons: each renders exactly one element with no conditional inside it, so they form one reviewer gate. A component and its stylesheet are one deliverable and are committed together.

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

- [ ] **Step 10: Write the five stylesheets**

`src/FellowshipExtractor.Api/Components/Cx/CountBadge.razor.scss`. All five `.cx-num` rules in the stylesheet this refactor starts from gather here, because every one of them targets this span and this component is the only thing that renders it. Each positional rule keeps its ancestor selector, which is matched on the class alone and needs no attribute of its own:

```scss
.cx-num { font-size: 10.5px; color: var(--faint); font-variant-numeric: tabular-nums; }

.cx-panel-head .cx-num { margin-left: auto; }
.cx-facet-values .cx-num { margin-left: auto; }
.cx-tab[aria-current] .cx-num { color: var(--gold-deep); }
.cx-pills .cx-num { border: 0; background: none; padding: 0 0 0 4px; }
```

The `.cx-pills` rule is what supplies the 4px gap before a pill's badge. Its `border: 0` and `background: none` had a second job in the flat stylesheet, undoing the pill's own border and ground; a `Pill` now styles only the elements it renders, so the badge never picks those up in the first place. Both declarations are kept: the rule reads as one thing and states what the badge is.

`src/FellowshipExtractor.Api/Components/Cx/Rule.razor.scss`:

```scss
.cx-rule {
  height: 8px; border: 0; margin: 12px 0;
  background: var(--chrome-rule) 0 0/100% 100% no-repeat;
}
```

`src/FellowshipExtractor.Api/Components/Cx/SubHead.razor.scss`:

```scss
@use "../../Styles/shared" as *;

.cx-sub-head { @include eyebrow; }
```

The stylesheet this refactor starts from writes that rule as `.cx-mega-sec-head, .cx-sub-head`, a selector list whose two halves belong to different components. The mixin is what keeps the two halves the same rule after they are split: `MegaMenu.razor.scss` includes it too, in Task 22.

`src/FellowshipExtractor.Api/Components/Cx/Note.razor.scss`:

```scss
.cx-note {
  color: var(--muted); font-size: 12px; margin: 0 0 8px;

  /* The note's body is child content, so a <code> in it carries the caller's scope attribute. */
  ::deep code { font: 12px/1.5 var(--mono); color: var(--core-secondary-100); }
}
```

The borrowed-art note's `<a data-tip>` is child content too, and needs no rule: the global anchor styling reaches it.

`src/FellowshipExtractor.Api/Components/Cx/Icon.razor.scss`. Three rules from three different pages converge here, because the `<img>` is this component's element wherever it is placed:

```scss
table.cx-rows .cx-art img {
  display: block; width: 34px; height: 34px; object-fit: contain;
  background: var(--chrome-slot) center/100% 100% no-repeat; padding: 2px;
}

.cx-omnibox-hit img {
  width: 30px; height: 30px; object-fit: contain; flex: 0 0 auto;
  background: var(--chrome-slot) center/100% 100% no-repeat;
}

.cx-linked img {
  width: 30px; height: 30px; object-fit: contain;
  background: var(--chrome-slot) center/100% 100% no-repeat;
}
```

The three sizes genuinely differ, so they stay three rules rather than one. Each ancestor class is still owned by the page that renders it and is matched on the class alone.

- [ ] **Step 11: Run the tests to verify they pass**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`
Expected: PASS, 9 tests.

- [ ] **Step 12: Build the solution clean and confirm the five sheets compiled**

Run: `dotnet build src/FellowshipExtractor.slnx`
Expected: `0 Warning(s)`, `0 Error(s)`.

```powershell
Get-ChildItem src/FellowshipExtractor.Api/Components/Cx -Filter *.razor.css | Select-Object Name, Length
```

Expected: `CountBadge.razor.css`, `Rule.razor.css`, `SubHead.razor.css`, `Note.razor.css` and `Icon.razor.css`, each non-empty. A missing one means the `.razor.scss` is misnamed: the double extension is what makes the Razor SDK scope it.

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components/Cx/Note.razor.css -Pattern 'code'
```

Expected: one match, of the form `.cx-note[b-…] code`, with the attribute on `.cx-note` and not on `code`. `code[b-` in that output means the `::deep` block was dropped and the rule matches nothing.

- [ ] **Step 13: Commit**

```powershell
git add src/FellowshipExtractor.Api/Components/Cx/CountBadge.razor src/FellowshipExtractor.Api/Components/Cx/CountBadge.razor.scss src/FellowshipExtractor.Api/Components/Cx/Rule.razor src/FellowshipExtractor.Api/Components/Cx/Rule.razor.scss src/FellowshipExtractor.Api/Components/Cx/SubHead.razor src/FellowshipExtractor.Api/Components/Cx/SubHead.razor.scss src/FellowshipExtractor.Api/Components/Cx/Note.razor src/FellowshipExtractor.Api/Components/Cx/Note.razor.scss src/FellowshipExtractor.Api/Components/Cx/Icon.razor src/FellowshipExtractor.Api/Components/Cx/Icon.razor.scss src/FellowshipExtractor.Api/Components/_Imports.razor src/FellowshipExtractor.Api.Tests/GlobalUsings.cs src/FellowshipExtractor.Api.Tests/Cx/CountBadgeTests.cs src/FellowshipExtractor.Api.Tests/Cx/RuleTests.cs src/FellowshipExtractor.Api.Tests/Cx/SubHeadTests.cs src/FellowshipExtractor.Api.Tests/Cx/NoteTests.cs src/FellowshipExtractor.Api.Tests/Cx/IconTests.cs
git commit -m @'
feat: add the cx design system's fixed skeletons

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

Confirm no generated stylesheet went in with them:

```powershell
git show --name-only --format="" HEAD | Select-String -Pattern '\.css$'
```

Expected: no output. `.gitignore`'s `*.css` is what keeps the generated `.razor.css` files out, and `.scss` is unaffected by the pattern.

---

### Task 5: `Panel` and the `Bare` helper

**Files:**
- Create: `src/FellowshipExtractor.Api/Components/Cx/Panel.razor`
- Create: `src/FellowshipExtractor.Api/Components/Cx/Panel.razor.scss`
- Create: `src/FellowshipExtractor.Api.Tests/Scoped.cs`
- Test: `src/FellowshipExtractor.Api.Tests/ScopedTests.cs`, `src/FellowshipExtractor.Api.Tests/Cx/PanelTests.cs`

**Interfaces:**
- Consumes: `CountBadge(string Text)` from Task 4, and `Styles/_shared.scss`'s `plated` mixin from Task 3.
- Produces: `Panel(string? Head, string? Badge, string? Class, string As = "div", string? Aria, RenderFragment? ChildContent)`. Every section in Tasks 11 to 19 and every panel in Tasks 20 to 22 renders it. Also `Scoped.Bare(this string markup)`, the extension every exact-string assertion in the rest of the plan reads through.

`cx-panel` is always the outermost class, `cx-panel-head` is always the first child of it, and the badge is always the last child of the head, because `.cx-panel-head .cx-num { margin-left: auto }` is what pushes it to the right edge.

**Why the helper lands here.** This is the first task whose assertion compares an exact rendered string against a component that carries a scope attribute: `Panel` and `CountBadge` each have a `.razor.scss`, so `cut.Markup` reads `<div class="cx-panel" b-…><div class="cx-panel-head" b-…>Stat slots<span class="cx-num" b-…>3</span></div></div>`. `MarkupMatches` forgives the attribute and needs nothing; a raw string does not.

- [ ] **Step 1: Write the failing tests**

`src/FellowshipExtractor.Api.Tests/ScopedTests.cs`. Two tests pin the helper's shape, because five whitespace assertions later in this plan read through it and a helper that ate one character too many would make each of them pass against the wrong string:

```csharp
namespace FellowshipExtractor.Api.Tests;

public class ScopedTests : BunitContext
{
    [Fact]
    public void Strips_the_scope_attribute_a_component_renders()
    {
        var cut = Render<Note>(p => p.AddChildContent("plain"));

        Assert.Matches(@"\sb-[a-z0-9]{10}", cut.Markup);
        Assert.Equal("<p class=\"cx-note\">plain</p>", cut.Markup.Bare());
    }

    [Fact]
    public void Leaves_a_data_attribute_a_class_and_the_text_between_two_elements_alone()
    {
        const string markup =
            "<p class=\"cx-note\">belongs to " +
            "<a href=\"/entity/items/12\" data-tip=\"/api/tooltip/items/12\">Old Boots</a>" +
            ", which is where it was taken from.</p>";

        Assert.Equal(markup, markup.Bare());
    }
}
```

`Note` is rendered rather than `Prose`, because `Prose` is the one primitive with no `.razor.scss` and so carries no attribute to strip. The `Assert.Matches` is what proves the attribute was there before `Bare` removed it, without ever writing a hash into a test.

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
            cut.Markup.Bare());
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

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~PanelTests|FullyQualifiedName~ScopedTests"`
Expected: build FAILS with `CS0246` on `Panel` and `CS1061` on `Bare`.

- [ ] **Step 3: Write the `Bare` helper**

`src/FellowshipExtractor.Api.Tests/Scoped.cs`:

```csharp
using System.Text.RegularExpressions;

namespace FellowshipExtractor.Api.Tests;

/// <summary>
/// The rendered markup without the scope attributes Blazor's CSS isolation puts on every element a
/// component with a stylesheet emits. Only the assertions that compare an exact string need it:
/// bUnit's semantic comparison already ignores the attribute, and an assertion that reads one
/// deliberately does it through Find and GetAttribute, which are unaffected.
/// </summary>
internal static partial class Scoped
{
    /// <summary>b- and ten base-36 characters, valueless, followed by a space or the tag's end.</summary>
    [GeneratedRegex(@"\sb-[a-z0-9]{10}(?=[\s>])")]
    private static partial Regex Attribute();

    public static string Bare(this string markup) => Attribute().Replace(markup, string.Empty);
}
```

The lookahead is what keeps it to attributes: the pattern can only match where a tag continues or closes. Stripping the attribute cannot disturb a whitespace assertion either, because the attribute is never inside a text node.

- [ ] **Step 4: Write `Panel.razor`**

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

- [ ] **Step 5: Write `Panel.razor.scss`**

`src/FellowshipExtractor.Api/Components/Cx/Panel.razor.scss`:

```scss
@use "../../Styles/shared" as *;

.cx-panel {
  position: relative;
  @include plated;
}

.cx-panel + .cx-panel { margin-top: 16px; }

/* The entity page's side column is a grid and spaces its own children, so the panels stacked in it
   drop the adjacency margin. Both elements in the selector are this component's, and the attribute
   lands on the second, which is the one being styled. */
.cx-entity-side .cx-panel + .cx-panel { margin-top: 0; }

.cx-panel-head {
  display: flex; align-items: center; gap: 10px; margin: -14px -16px 12px;
  padding: 9px 16px;
  background: var(--chrome-header) center/100% 100% no-repeat;
  font-family: var(--display); font-size: 15px; letter-spacing: .1em; text-transform: uppercase;
  color: var(--gold-bright);
}

/* Browse's filter rail is a Panel with As="aside" and Class="cx-rail". The class is the caller's
   word but the <aside> it lands on is this component's element, so the rule lives here. */
.cx-rail { position: sticky; top: 66px; max-height: calc(100vh - 82px); overflow-y: auto; }

/* One column below the rail's width, and the results first: a filter panel a reader has to scroll
   past to reach what they asked for is a filter panel in the way. The grid half of this rule is in
   Browse.razor.scss, which owns .cx-browse. */
@media (max-width: 1080px) {
  .cx-rail { position: static; max-height: none; order: 2; }
}

/* The As="details" variant: a summary that opens and shuts rather than a head that only names. */
.cx-raw-record > summary { cursor: pointer; list-style: none; }
.cx-raw-record > summary::-webkit-details-marker { display: none; }
.cx-raw-record > summary::after { content: "\002B"; margin-left: auto; color: var(--gold-deep); }
.cx-raw-record[open] > summary::after { content: "\2212"; }
.cx-raw-record[open] > summary { margin-bottom: 12px; }
```

`.cx-panel-head .cx-num` is not here. The badge is a `CountBadge`, a child component whose span carries its own hash, so the rule that pushes it to the right edge lives in `CountBadge.razor.scss`, written in Task 4.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~PanelTests|FullyQualifiedName~ScopedTests"`
Expected: PASS, 9 tests.

- [ ] **Step 7: Build the solution and confirm the nullable flow is clean**

Run: `dotnet build src/FellowshipExtractor.slnx`
Expected: `0 Warning(s)`, `0 Error(s)`. `Badge` is `string?` and `CountBadge.Text` is `string`; the `Badge is not null` guard sits in the same generated method as the parameter write, so no nullable warning is emitted. A warning here means the guard and the write have been separated and must be put back together.

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components/Cx/Panel.razor.css -Pattern 'cx-panel \+ \.cx-panel'
```

Expected: two matches, each with the scope attribute on the second `.cx-panel` of the pair, which is the element being styled.

- [ ] **Step 8: Run the whole suite**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`
Expected: PASS, 18 tests.

- [ ] **Step 9: Commit**

```powershell
git add src/FellowshipExtractor.Api/Components/Cx/Panel.razor src/FellowshipExtractor.Api/Components/Cx/Panel.razor.scss src/FellowshipExtractor.Api.Tests/Scoped.cs src/FellowshipExtractor.Api.Tests/ScopedTests.cs src/FellowshipExtractor.Api.Tests/Cx/PanelTests.cs
git commit -m @'
feat: add the Panel primitive

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 6: `Pills` and `Pill`

**Files:**
- Create: `src/FellowshipExtractor.Api/Components/Cx/Pills.razor`, `Pills.razor.scss`
- Create: `src/FellowshipExtractor.Api/Components/Cx/Pill.razor`, `Pill.razor.scss`
- Test: `src/FellowshipExtractor.Api.Tests/Cx/PillsTests.cs`, `src/FellowshipExtractor.Api.Tests/Cx/PillTests.cs`

**Interfaces:**
- Consumes: `CountBadge(string Text)` from Task 4, and `Scoped.Bare` from Task 5.
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
            cut.Markup.Bare());
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

- [ ] **Step 5: Write the two stylesheets**

`src/FellowshipExtractor.Api/Components/Cx/Pills.razor.scss`:

```scss
.cx-pills { display: flex; flex-wrap: wrap; gap: 5px; margin: 0; padding: 0; list-style: none; }
```

That is the whole of it. `.cx-pills li a` and `.cx-pills li span` are the `Pill`'s elements and are styled there; written here they would compile to `.cx-pills li a[b-…]` and match nothing.

`src/FellowshipExtractor.Api/Components/Cx/Pill.razor.scss`. `Pill` renders the `<li>`, the `<a>` and the `<span>`, so the two rules drop the `li` from their selector and keep the `.cx-pills` ancestor:

```scss
.cx-pills a, .cx-pills span {
  display: inline-block; padding: 2px 9px; border: 1px solid var(--line); border-radius: 999px;
  font-size: 12px; color: var(--muted); background: var(--sunken);
}

.cx-pills a:hover { border-color: var(--gold-deep); color: var(--gold-bright); text-decoration: none; }
```

The `<ul class="cx-pills">` is the `Pills` component's element and carries a different hash, so the ancestor is matched on its class alone and needs no attribute. It is kept rather than dropped for a bare `a, span`, because a pill is styled *in a pill list* and a bare selector would state something wider than that.

`.cx-pills span` reaches this component's own label span and not the `CountBadge` beside it, which carries a different hash again. That is the isolation doing the work the `.cx-pills .cx-num` reset used to do by hand.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~PillTests|FullyQualifiedName~PillsTests"`
Expected: PASS, 7 tests.

- [ ] **Step 7: Build and run the whole suite**

Run: `dotnet build src/FellowshipExtractor.slnx`
Expected: `0 Warning(s)`, `0 Error(s)`.

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components/Cx/Pill.razor.css -Pattern 'cx-pills'
```

Expected: three selectors, each with the attribute on the `a` or the `span` and none on `.cx-pills`, which is the `Pills` component's element.

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`
Expected: PASS, 25 tests.

- [ ] **Step 8: Commit**

```powershell
git add src/FellowshipExtractor.Api/Components/Cx/Pills.razor src/FellowshipExtractor.Api/Components/Cx/Pills.razor.scss src/FellowshipExtractor.Api/Components/Cx/Pill.razor src/FellowshipExtractor.Api/Components/Cx/Pill.razor.scss src/FellowshipExtractor.Api.Tests/Cx/PillsTests.cs src/FellowshipExtractor.Api.Tests/Cx/PillTests.cs
git commit -m @'
feat: add the Pills and Pill primitives

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 7: `Facts` and `Fact`

**Files:**
- Create: `src/FellowshipExtractor.Api/Components/Cx/Facts.razor`, `Facts.razor.scss`
- Create: `src/FellowshipExtractor.Api/Components/Cx/Fact.razor`, `Fact.razor.scss`
- Test: `src/FellowshipExtractor.Api.Tests/Cx/FactsTests.cs`, `src/FellowshipExtractor.Api.Tests/Cx/FactTests.cs`

**Interfaces:**
- Consumes: `Scoped.Bare` from Task 5.
- Produces: `Facts(RenderFragment? ChildContent)` and `Fact(string Label, string? Unit, RenderFragment? ChildContent)`.

The rows are bare `<tr>` children of `<table>` with no `<thead>` and no `<tbody>`, which is what both existing tables emit and what the shipped `.cx-facts tr` and `.cx-facts th` select. `Fact` is asserted on `cut.Markup.Bare()` rather than through `cut.Find`, because a `<tr>` at the top of a parsed fragment is not reliably addressable and the raw string is exactly what is under test anyway.

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

        Assert.Equal("<table class=\"cx-facts\"></table>", cut.Markup.Bare());
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

        Assert.Equal("<tr><th>Type</th><td>Items</td></tr>", cut.Markup.Bare());
    }

    [Fact]
    public void Renders_the_unit_with_no_space_before_it()
    {
        var cut = Render<Fact>(p => p.Add(x => x.Label, "Cooldown").Add(x => x.Unit, "s").AddChildContent("2.5"));

        Assert.Equal("<tr><th>Cooldown</th><td>2.5<span class=\"cx-unit\">s</span></td></tr>", cut.Markup.Bare());
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
            cut.Markup.Bare());
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

- [ ] **Step 5: Write the two stylesheets**

`src/FellowshipExtractor.Api/Components/Cx/Facts.razor.scss`:

```scss
.cx-facts { width: 100%; border-collapse: collapse; font-size: 13px; }
```

The row, cell and unit rules belong to `Fact`, which renders those elements.

`src/FellowshipExtractor.Api/Components/Cx/Fact.razor.scss`:

```scss
tr { border-bottom: 1px solid var(--line-soft); }

th {
  text-align: left; font-weight: 600; color: var(--muted); padding: 6px 12px 6px 0; width: 1%;
  white-space: nowrap;
}

td {
  padding: 6px 0;

  /* A <code> in a fact cell arrives as the caller's child content and carries the caller's scope
     attribute, so it is reached through ::deep. */
  ::deep code { font: 12px/1.5 var(--mono); color: var(--core-secondary-100); }
}

.cx-unit { color: var(--faint); }
```

The `<tr>`, `<th>`, `<td>` and unit span are all this component's, so the shipped `.cx-facts tr`, `.cx-facts th`, `.cx-facts td` and `.cx-facts .cx-unit` drop their ancestor selector. A `<tr>` outside a `.cx-facts` table is not a position this component can be rendered in, so the bare selectors state nothing wider than the shipped ones do.

`.cx-facts code` is the one rule of the five that cannot lose its ancestor: the `<code>` is not this component's element. It is written on the `<td>`, which narrows it from anywhere in the table to the value cell. That is the whole of its reach in practice: the `<th>` is rendered by `Fact` from the `Label` string, so no caller can put markup of any kind in a header cell.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~FactTests|FullyQualifiedName~FactsTests"`
Expected: PASS, 6 tests.

- [ ] **Step 7: Build and run the whole suite**

Run: `dotnet build src/FellowshipExtractor.slnx`
Expected: `0 Warning(s)`, `0 Error(s)`.

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components/Cx/Fact.razor.css -Pattern 'code'
```

Expected: one match, of the form `td[b-…] code`. `code[b-` there means the `::deep` block was dropped and the rule matches nothing; Task 8 adds the test that fails on it.

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`
Expected: PASS, 31 tests.

- [ ] **Step 8: Commit**

```powershell
git add src/FellowshipExtractor.Api/Components/Cx/Facts.razor src/FellowshipExtractor.Api/Components/Cx/Facts.razor.scss src/FellowshipExtractor.Api/Components/Cx/Fact.razor src/FellowshipExtractor.Api/Components/Cx/Fact.razor.scss src/FellowshipExtractor.Api.Tests/Cx/FactsTests.cs src/FellowshipExtractor.Api.Tests/Cx/FactTests.cs
git commit -m @'
feat: add the Facts and Fact primitives

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 8: `KeyValues`, `KeyValue` and the isolation guard

**Files:**
- Create: `src/FellowshipExtractor.Api/Components/Cx/KeyValues.razor`, `KeyValues.razor.scss`
- Create: `src/FellowshipExtractor.Api/Components/Cx/KeyValue.razor`, `KeyValue.razor.scss`
- Test: `src/FellowshipExtractor.Api.Tests/Cx/KeyValuesTests.cs`, `src/FellowshipExtractor.Api.Tests/Cx/KeyValueTests.cs`, `src/FellowshipExtractor.Api.Tests/IsolationTests.cs`

**Interfaces:**
- Consumes: `Scoped.Bare` from Task 5, and the generated `Note.razor.css` and `Fact.razor.css` from Tasks 4 and 7.
- Produces: `KeyValues(RenderFragment? ChildContent)` and `KeyValue(string Key, bool Missing, string? Class, RenderFragment? ChildContent)`, and `IsolationTests`, the one automated guard on the three `::deep` rules.

`KeyValue`'s markup is one line with no literal text anywhere in it, because one of its callers renders into a `.cx-prose` context where a newline is a rendered line break.

**This task closes the `::deep` set.** `.cx-note code`, `.cx-facts code` and `.cx-kv code` are the only three rules in the library that reach markup a caller wrote, and `KeyValue.razor.scss` is the last of the three to be written. A nested rule written without `::deep` compiles, bundles and matches nothing, so the guard is added here, where all three generated stylesheets exist for it to read.

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
            cut.Markup.Bare());
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
            cut.Markup.Bare());
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
            cut.Markup.Bare());
    }
}
```

`src/FellowshipExtractor.Api.Tests/IsolationTests.cs`:

```csharp
namespace FellowshipExtractor.Api.Tests;

/// <summary>
/// Reads the stylesheets the build generated from the .scss sources, which is the only place a
/// dropped ::deep is visible: the rule compiles, bundles and matches nothing, and no rendered
/// markup differs.
/// </summary>
public class IsolationTests
{
    [Fact]
    public void No_generated_selector_reaches_a_code_element_through_the_scope_attribute()
    {
        string[] sheets =
        [
            "Components/Cx/Note.razor.css",
            "Components/Cx/Fact.razor.css",
            "Components/Cx/KeyValue.razor.css",
        ];

        var project = Project();

        foreach (var sheet in sheets)
        {
            var path = Path.Combine(project, sheet);

            Assert.True(File.Exists(path), $"{path} was not generated. Sass compiles it on build.");
            Assert.DoesNotContain("code[b-", File.ReadAllText(path), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The Api project folder, found by walking up from the test binary the way Export.Found walks
    /// up to the export, so the test reads the stylesheets the build just wrote rather than a copy.
    /// </summary>
    private static string Project()
    {
        for (var here = new DirectoryInfo(AppContext.BaseDirectory); here is not null; here = here.Parent)
        {
            var project = Path.Combine(here.FullName, "src", "FellowshipExtractor.Api");

            if (File.Exists(Path.Combine(project, "FellowshipExtractor.Api.csproj"))) return project;
        }

        throw new InvalidOperationException(
            "No src/FellowshipExtractor.Api above the test binary. The generated .razor.css files sit " +
            "beside their .razor.scss sources in that folder.");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~KeyValueTests|FullyQualifiedName~KeyValuesTests|FullyQualifiedName~IsolationTests"`
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

- [ ] **Step 5: Write the two stylesheets**

`src/FellowshipExtractor.Api/Components/Cx/KeyValues.razor.scss`:

```scss
.cx-kv { list-style: none; margin: 0; padding: 0; font-size: 13px; }
```

`src/FellowshipExtractor.Api/Components/Cx/KeyValue.razor.scss`:

```scss
li {
  display: flex; gap: 10px; padding: 4px 0; border-bottom: 1px solid var(--line-soft);

  &:last-child { border-bottom: 0; }

  /* A <code> in a row is the caller's child content, so it is reached through ::deep. Written on
     the <li> rather than on .cx-v, which is what .cx-kv code reaches today. */
  ::deep code { font: 12px/1.5 var(--mono); color: var(--core-secondary-100); }
}

.cx-k { color: var(--muted); flex: 1 1 auto; min-width: 0; word-break: break-word; }
.cx-v { flex: 0 0 auto; font-weight: 600; }
.cx-v.cx-miss { font-weight: 400; color: var(--faint); font-style: italic; }
.cx-miss { color: var(--faint); font-style: italic; }
```

`Missing="true"` is the only way `cx-miss` reaches the page, so both `cx-miss` rules live here even though the second carries no `.cx-kv` ancestor in the stylesheet this refactor starts from.

`.cx-prose` is not here. When `Class="cx-prose"` is passed, the span wears a class two components render, so it is styled globally in `Styles/codex.scss`, written in Task 3.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~KeyValueTests|FullyQualifiedName~KeyValuesTests|FullyQualifiedName~IsolationTests"`
Expected: PASS, 5 tests. If `IsolationTests` fails, a `::deep` block was dropped from one of the three stylesheets it names: put it back rather than relaxing the assertion, because the rule it guards matches nothing without it.

- [ ] **Step 7: Build and run the whole suite**

Run: `dotnet build src/FellowshipExtractor.slnx`
Expected: `0 Warning(s)`, `0 Error(s)`.

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`
Expected: PASS, 36 tests.

- [ ] **Step 8: Commit**

```powershell
git add src/FellowshipExtractor.Api/Components/Cx/KeyValues.razor src/FellowshipExtractor.Api/Components/Cx/KeyValues.razor.scss src/FellowshipExtractor.Api/Components/Cx/KeyValue.razor src/FellowshipExtractor.Api/Components/Cx/KeyValue.razor.scss src/FellowshipExtractor.Api.Tests/Cx/KeyValuesTests.cs src/FellowshipExtractor.Api.Tests/Cx/KeyValueTests.cs src/FellowshipExtractor.Api.Tests/IsolationTests.cs
git commit -m @'
feat: add the KeyValues and KeyValue primitives

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 9: `Prose` and `Empty`

**Files:**
- Create: `src/FellowshipExtractor.Api/Components/Cx/Prose.razor`
- Create: `src/FellowshipExtractor.Api/Components/Cx/Empty.razor`, `Empty.razor.scss`
- Test: `src/FellowshipExtractor.Api.Tests/Cx/ProseTests.cs`, `src/FellowshipExtractor.Api.Tests/Cx/EmptyTests.cs`

**Interfaces:**
- Consumes: `Scoped.Bare` from Task 5, and the four `.cx-prose` rules written into `Styles/codex.scss` in Task 3.
- Produces: `Prose(RenderFragment? ChildContent)` and `Empty(string? Said, RenderFragment? ChildContent)`.

`Prose` is the one primitive whose whole job is a contract: the element that directly contains the rich-text `MarkupString` must itself carry `class="cx-prose"`, because `App.razor` emits `Ui.RichTextCss("cx-prose")` and every rich-text style rule is written as `.cx-prose .rt-xxx`. Its markup is one line and contains no literal text.

**`Prose` renders no stylesheet, and this is the one primitive of which that is true.** Its four rules live in `Styles/codex.scss`, written in Task 3, for two reasons that both come out of how the scope attribute lands. `.cx-prose` has two component owners, this `<p>` and `KeyValue`'s span, so a scoped rule would style one and silently drop the other. And every run inside it arrives as a `MarkupString` with no scope attribute, so `.cx-prose .sub` scoped here would compile to `.cx-prose[b-…] .sub` while the runtime colour table stayed at `.cx-prose .rt-fire`: one family of rules on the same runs, at two specificities, maintained in two places. Creating `Prose.razor.scss` is therefore the single edit that undoes this task, and the `<p>` it renders carries no scope attribute at all.

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

        Assert.Equal("<p class=\"cx-prose\">one two</p>", cut.Markup.Bare());
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

        Assert.Equal("<p class=\"cx-prose\">first\nsecond</p>", cut.Markup.Bare());
    }

    [Fact]
    public void Renders_no_scope_attribute_because_it_owns_no_stylesheet()
    {
        var cut = Render<Prose>(p => p.AddChildContent("one two"));

        Assert.DoesNotMatch(@"\sb-[a-z0-9]{10}", cut.Markup);
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

        Assert.Equal("<p class=\"cx-empty\">Loading...</p>", cut.Markup.Bare());
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

The markup is one line and contains no literal text. `Styles/codex.scss` sets `.cx-prose { white-space: pre-line }`, so a newline this component contributed would be a rendered line break.

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

- [ ] **Step 5: Write `Empty.razor.scss`**

`src/FellowshipExtractor.Api/Components/Cx/Empty.razor.scss`:

```scss
.cx-empty {
  padding: 40px 16px; text-align: center; color: var(--muted);

  b { color: var(--ink); }
}
```

The `<b>` is rendered by `Empty` from `Said`, not passed in, so it needs no `::deep`.

`Prose` gets no file. Its four rules are in `Styles/codex.scss`.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~ProseTests|FullyQualifiedName~EmptyTests"`
Expected: PASS, 7 tests. If an `Assert.Equal` here reports a difference, the rendered string is the answer: correct the expected string and keep `Assert.Equal`.

- [ ] **Step 7: Build and confirm `Prose` owns no stylesheet**

Run: `dotnet build src/FellowshipExtractor.slnx`
Expected: `0 Warning(s)`, `0 Error(s)`.

```powershell
Get-ChildItem src/FellowshipExtractor.Api/Components/Cx -Filter Prose.razor.*
```

Expected: nothing. `Prose.razor` alone, with no `.scss` and no generated `.css` beside it.

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`
Expected: PASS, 43 tests.

- [ ] **Step 8: Commit**

```powershell
git add src/FellowshipExtractor.Api/Components/Cx/Prose.razor src/FellowshipExtractor.Api/Components/Cx/Empty.razor src/FellowshipExtractor.Api/Components/Cx/Empty.razor.scss src/FellowshipExtractor.Api.Tests/Cx/ProseTests.cs src/FellowshipExtractor.Api.Tests/Cx/EmptyTests.cs
git commit -m @'
feat: add the Prose and Empty primitives

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 10: `Chip` and `Knob`

**Files:**
- Create: `src/FellowshipExtractor.Api/Components/Cx/Chip.razor`, `Chip.razor.scss`
- Create: `src/FellowshipExtractor.Api/Components/Cx/Knob.razor`, `Knob.razor.scss`
- Test: `src/FellowshipExtractor.Api.Tests/Cx/ChipTests.cs`, `src/FellowshipExtractor.Api.Tests/Cx/KnobTests.cs`

**Interfaces:**
- Produces: `Chip(string? Colour, RenderFragment? ChildContent)` and `Knob(string Caption, string As = "label", RenderFragment? ChildContent)`. This task completes the sixteen primitives and the fifteen primitive stylesheets.

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

- [ ] **Step 5: Write the two stylesheets**

`src/FellowshipExtractor.Api/Components/Cx/Chip.razor.scss`:

```scss
/* The chip's whole appearance is this one rule, and it selects only inside the results table.
   The ancestor is Browse's element and is matched on its class; the span is this component's and
   is what the scope attribute lands on. */
table.cx-rows .cx-chip {
  display: inline-block; padding: 0 6px; border: 1px solid var(--line); border-radius: 2px;
  font-size: 11px; color: var(--muted); margin: 1px 3px 1px 0; white-space: nowrap;
}
```

`src/FellowshipExtractor.Api/Components/Cx/Knob.razor.scss`:

```scss
.cx-knob {
  display: block; margin-bottom: 10px;

  > span {
    display: block; font-size: 11px; letter-spacing: .12em; text-transform: uppercase;
    color: var(--faint); margin-bottom: 3px;
  }
}
```

The caption span is this component's. The `<select>`, the `<input>`s and the rung buttons are the `Knobs` section's child content, so `.cx-knob select`, `.cx-knob input` and their focus rules live in `Knobs.razor.scss`, written on the elements that section renders in Task 19. That is the ownership rule and not `::deep`: the control has a component that owns it.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~ChipTests|FullyQualifiedName~KnobTests"`
Expected: PASS, 6 tests.

- [ ] **Step 7: Build and confirm no primitive injects anything**

Run: `dotnet build src/FellowshipExtractor.slnx`
Expected: `0 Warning(s)`, `0 Error(s)`.

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components/Cx -Recurse -Pattern '@inject'
```

Expected: no matches. Sixteen files now exist under `Components/Cx/`:

```powershell
$cx = Get-ChildItem src/FellowshipExtractor.Api/Components/Cx

($cx | Where-Object Name -like '*.razor').Count
($cx | Where-Object Name -like '*.razor.scss').Count
($cx | Where-Object Name -like '*.razor.css').Count
```

Expected: `16`, `15`, `15`. The one primitive with no stylesheet is `Prose`, and the `.css` count matching the `.scss` count is what says every one of them compiled. `Where-Object` rather than `-Filter`: `-Filter` is answered by the filesystem, which matches an 8.3 short name as well as the long one, so `-Filter *.razor` can return the `.razor.scss` files too and read `46`.

- [ ] **Step 8: Run the whole suite**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`
Expected: PASS, 49 tests.

- [ ] **Step 9: Commit**

```powershell
git add src/FellowshipExtractor.Api/Components/Cx/Chip.razor src/FellowshipExtractor.Api/Components/Cx/Chip.razor.scss src/FellowshipExtractor.Api/Components/Cx/Knob.razor src/FellowshipExtractor.Api/Components/Cx/Knob.razor.scss src/FellowshipExtractor.Api.Tests/Cx/ChipTests.cs src/FellowshipExtractor.Api.Tests/Cx/KnobTests.cs
git commit -m @'
feat: add the Chip and Knob primitives

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 11: The export fixture and `Wording`

**Files:**
- Create: `src/FellowshipExtractor.Api/Components/Sections/Wording.razor`
- Create: `src/FellowshipExtractor.Api.Tests/Export.cs`
- Create: `src/FellowshipExtractor.Api.Tests/Corpus.cs`
- Modify: `src/FellowshipExtractor.Api/Components/_Imports.razor`
- Modify: `src/FellowshipExtractor.Api.Tests/GlobalUsings.cs`
- Test: `src/FellowshipExtractor.Api.Tests/Sections/WordingTests.cs`

**Interfaces:**
- Consumes: `Panel(string? Head, …)` from Task 5, `Prose(RenderFragment?)` from Task 9, `Rule()` from Task 4, `Scoped.Bare` from Task 5.
- Produces: `Export` with `CodexView View` and `EntityStore Store`; `Corpus.Name`, the collection every fixture-taking test class opts into; and `Wording`, a section reading `[CascadingParameter] private Entity Entity`.

`Wording` draws no class of its own: it is a `Panel`, a `Prose` and a `Rule` per detail line, each of which owns its own styling. It renders no stylesheet, and the same is true of `Ranks`, `Overflow`, `BorrowedArt`, `Grants`, `Legendaries`, `Identity`, `Readings` and `Nearby`. Four sections do carry one, and each is written in its own task: `Particulars` in Task 12, `StatSlots` in Task 13, `Stage` in Task 17 and `Knobs` in Task 19.

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

        Assert.Equal("<p class=\"cx-prose\">one two</p>", cut.Find("p.cx-prose").OuterHtml.Bare());
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
        Assert.Equal("<p class=\"cx-prose\">only</p>", cut.Find("p.cx-prose").OuterHtml.Bare());
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

`Wording.razor` has no `.razor.scss` beside it. Every class it draws belongs to a primitive.

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~WordingTests"`
Expected: PASS, 6 tests. If an `Assert.Equal` reports a difference, the rendered string is the answer: correct the expected string and keep `Assert.Equal`.

- [ ] **Step 9: Build and run the whole suite**

Run: `dotnet build src/FellowshipExtractor.slnx`
Expected: `0 Warning(s)`, `0 Error(s)`.

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`
Expected: PASS, 55 tests.

- [ ] **Step 10: Commit**

```powershell
git add src/FellowshipExtractor.Api/Components/Sections/Wording.razor src/FellowshipExtractor.Api/Components/_Imports.razor src/FellowshipExtractor.Api.Tests/GlobalUsings.cs src/FellowshipExtractor.Api.Tests/Export.cs src/FellowshipExtractor.Api.Tests/Corpus.cs src/FellowshipExtractor.Api.Tests/Sections/WordingTests.cs
git commit -m @'
feat: add the Wording section and the export test fixture

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 12: `Particulars`

**Files:**
- Create: `src/FellowshipExtractor.Api/Components/Sections/Particulars.razor`, `Particulars.razor.scss`
- Test: `src/FellowshipExtractor.Api.Tests/Sections/ParticularsTests.cs`

**Interfaces:**
- Consumes: `Panel`, `Facts`, `Fact`, `Rule`, `SubHead`, `Pills`, `Pill` from Tasks 4 to 7; `Export` and `Corpus.Name` from Task 11.
- Produces: `Particulars` with `[Parameter] IReadOnlyList<(string Label, string Value, string? Unit, string? Href, string? Tip)> Facts` and `[Parameter] IReadOnlyList<(string Label, string Href)> Schools`, both defaulting to `[]`. `Detail` supplies both in Task 16.

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

- [ ] **Step 4: Write `Particulars.razor.scss`**

`div.cx-pairs` is the one class this section draws itself, so it is the whole of the file:

```scss
.cx-pairs { display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 0 28px; }
```

The two `<table class="cx-facts">` inside it are `Facts` components and are styled in `Facts.razor.scss`, written in Task 7.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~ParticularsTests"`
Expected: PASS, 13 tests.

- [ ] **Step 6: Build and run the whole suite**

Run: `dotnet build src/FellowshipExtractor.slnx`
Expected: `0 Warning(s)`, `0 Error(s)`.

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`
Expected: PASS, 68 tests.

- [ ] **Step 7: Commit**

```powershell
git add src/FellowshipExtractor.Api/Components/Sections/Particulars.razor src/FellowshipExtractor.Api/Components/Sections/Particulars.razor.scss src/FellowshipExtractor.Api.Tests/Sections/ParticularsTests.cs
git commit -m @'
feat: add the Particulars section

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 13: `StatSlots`

**Files:**
- Create: `src/FellowshipExtractor.Api/Components/Sections/StatSlots.razor`, `StatSlots.razor.scss`
- Test: `src/FellowshipExtractor.Api.Tests/Sections/StatSlotsTests.cs`

**Interfaces:**
- Consumes: `Panel`, `Note` from Tasks 4 and 5; `Export` and `Corpus.Name` from Task 11.
- Produces: `StatSlots`, a section reading `[CascadingParameter] private Entity Entity` and injecting `CodexView Ui` for `Ui.Store.PrimaryAttributes`.

`ul.cx-stat-lines` has one call site and stays as markup inside this section, so its whole rule family does too.

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

- [ ] **Step 4: Write `StatSlots.razor.scss`**

```scss
.cx-stat-lines {
  list-style: none; margin: 0; padding: 0; font-size: 13px;

  li { display: flex; gap: 8px; padding: 3px 0; }

  .cx-w { color: var(--faint); margin-left: auto; font-size: 11px; }
  .cx-primary { color: var(--game-stat-primary); }
  .cx-secondary { color: var(--game-stat-secondary); }
  li.cx-primary { color: var(--game-stat-primary); }
  li.cx-secondary { color: var(--game-stat-secondary); }
}
```

The list, the rows and both spans are this section's own elements, so nothing here needs `::deep`. The class pair is kept in both spellings, on the row and on a span inside it, exactly as the stylesheet this refactor starts from carries them.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~StatSlotsTests"`
Expected: PASS, 4 tests.

- [ ] **Step 6: Build and run the whole suite**

Run: `dotnet build src/FellowshipExtractor.slnx`
Expected: `0 Warning(s)`, `0 Error(s)`.

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`
Expected: PASS, 72 tests.

- [ ] **Step 7: Commit**

```powershell
git add src/FellowshipExtractor.Api/Components/Sections/StatSlots.razor src/FellowshipExtractor.Api/Components/Sections/StatSlots.razor.scss src/FellowshipExtractor.Api.Tests/Sections/StatSlotsTests.cs
git commit -m @'
feat: add the StatSlots section

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 14: `Ranks`

**Files:**
- Create: `src/FellowshipExtractor.Api/Components/Sections/Ranks.razor`
- Test: `src/FellowshipExtractor.Api.Tests/Sections/RanksTests.cs`

**Interfaces:**
- Consumes: `Panel`, `Note`, `Rule`, `KeyValues`, `KeyValue` from Tasks 4, 5 and 8; `Export` and `Corpus.Name` from Task 11; `Scoped.Bare` from Task 5.
- Produces: `Ranks`, a section reading `[CascadingParameter] private Entity Entity` and injecting `CodexView Ui`.

`Ranks` renders no stylesheet: every element it draws is a `Panel`, a `Note`, a `Rule`, a `KeyValues` or a `KeyValue`, each of which owns its own. That is also why the Printed row's value span carries `KeyValue`'s scope attribute and its exact assertion reads through `.Bare()`, while the `<span> / </span>` separators inside it are this section's own elements and carry none.

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

        var printed = cut.Find("span.cx-v.cx-prose").OuterHtml.Bare();

        Assert.Equal(
            "<span class=\"cx-v cx-prose\">10<span> / </span>20<span> / </span>30</span>",
            printed);
        Assert.DoesNotContain("\n", printed);
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

Each `<KeyValue …>…</KeyValue>` that carries literal text is written on one line, for the same reason `KeyValue.razor` itself is. `Ranks.razor` has no `.razor.scss` beside it.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~RanksTests"`
Expected: PASS, 6 tests. If the `Assert.Equal` on the printed span reports a difference, the rendered string is the answer: correct the expected string and keep `Assert.Equal`, and keep the `.Bare()` on it.

- [ ] **Step 5: Build and run the whole suite**

Run: `dotnet build src/FellowshipExtractor.slnx`
Expected: `0 Warning(s)`, `0 Error(s)`.

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`
Expected: PASS, 78 tests.

- [ ] **Step 6: Commit**

```powershell
git add src/FellowshipExtractor.Api/Components/Sections/Ranks.razor src/FellowshipExtractor.Api.Tests/Sections/RanksTests.cs
git commit -m @'
feat: add the Ranks section

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 15: `Overflow` and `BorrowedArt`

**Files:**
- Create: `src/FellowshipExtractor.Api/Components/Sections/Overflow.razor`
- Create: `src/FellowshipExtractor.Api/Components/Sections/BorrowedArt.razor`
- Test: `src/FellowshipExtractor.Api.Tests/Sections/OverflowTests.cs`, `src/FellowshipExtractor.Api.Tests/Sections/BorrowedArtTests.cs`

**Interfaces:**
- Consumes: `Panel`, `Prose`, `Note` from Tasks 4, 5 and 9; `Export` and `Corpus.Name` from Task 11; `Scoped.Bare` from Task 5.
- Produces: `Overflow`, reading the entity cascade and injecting `CodexView Ui`; and `BorrowedArt` with `[Parameter] string? Href`, `[Parameter] string? Tip`, `[Parameter] string? Name`.

Neither section renders a stylesheet: each is a `Panel` around a `Prose` or a `Note`. The borrowed-art note's inline `<a data-tip>` is child content and takes the global anchor styling from `Styles/codex.scss`.

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

        Assert.Equal("<p class=\"cx-prose\">beyond max</p>", cut.Find("p.cx-prose").OuterHtml.Bare());
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

```powershell
Get-ChildItem src/FellowshipExtractor.Api/Components/Sections -Filter *.razor.scss
```

Expected: `Particulars.razor.scss` and `StatSlots.razor.scss` only. `Wording`, `Ranks`, `Overflow` and `BorrowedArt` draw no class of their own and get no file.

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`
Expected: PASS, 84 tests.

- [ ] **Step 7: Commit**

```powershell
git add src/FellowshipExtractor.Api/Components/Sections/Overflow.razor src/FellowshipExtractor.Api/Components/Sections/BorrowedArt.razor src/FellowshipExtractor.Api.Tests/Sections/OverflowTests.cs src/FellowshipExtractor.Api.Tests/Sections/BorrowedArtTests.cs
git commit -m @'
feat: add the Overflow and BorrowedArt sections

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 16: `Grants`, `Legendaries` and `Detail`'s cascade

**Files:**
- Create: `src/FellowshipExtractor.Api/Components/Sections/Grants.razor`
- Create: `src/FellowshipExtractor.Api/Components/Sections/Legendaries.razor`
- Modify: `src/FellowshipExtractor.Api/Components/Shared/Detail.razor`
- Test: `src/FellowshipExtractor.Api.Tests/Sections/GrantsTests.cs`, `src/FellowshipExtractor.Api.Tests/Sections/LegendariesTests.cs`, `src/FellowshipExtractor.Api.Tests/DetailTests.cs`

**Interfaces:**
- Consumes: `Panel`, `Rule`, `SubHead` from Tasks 4 and 5; `Wording`, `Particulars`, `StatSlots`, `Ranks`, `Overflow`, `BorrowedArt` from Tasks 11 to 15; `Export` and `Corpus.Name` from Task 11; `Scoped.Bare` from Task 5.
- Produces: `Grants` and `Legendaries`, both reading the entity cascade and injecting nothing; and a `Detail` whose markup is the eight section tags inside one `<CascadingValue Value="Entity">` and whose `@code` block holds only `Entity`, `Facts`, `Schools`, `Lender`, `SchoolFacet`, `OnParametersSet` and `Filter`. `EntityPage` renders `<Detail Entity="Shown" />` in Task 20.

`Grants`, `Legendaries` and `Detail` render no stylesheet. `Detail` renders no element of its own at all: `<CascadingValue>` emits none.

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

        Assert.StartsWith("<div class=\"cx-panel\">", cut.Markup.Bare());

        var panels = cut.FindAll("div.cx-panel");

        Assert.Equal(2, panels.Count);
        Assert.All(panels, panel => Assert.Null(panel.ParentElement?.Closest(".cx-panel")));
    }
}
```

- [ ] **Step 2: Run the characterization tests against the pre-rewrite `Detail`**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~DetailTests"`
Expected: PASS, 5 tests. A failure here is drift in the current `Detail`, not a bad test: record it in the Task 1 note and reconcile the guard or head it disagrees with before continuing.

`Emits_its_panels_as_siblings_with_no_wrapper` reads through `.Bare()` so that it holds on both sides of the rewrite. The pre-rewrite `Detail` writes `<div class="cx-panel">` in its own markup and owns no stylesheet, so the string carries no attribute and `Bare` changes nothing; after the rewrite the div is a `Panel`, which does own one, and the attribute is there. That is the whole reason the helper exists.

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
Expected: PASS, 95 tests.

- [ ] **Step 13: Commit**

```powershell
git add src/FellowshipExtractor.Api/Components/Sections/Grants.razor src/FellowshipExtractor.Api/Components/Sections/Legendaries.razor src/FellowshipExtractor.Api/Components/Shared/Detail.razor src/FellowshipExtractor.Api.Tests/Sections/GrantsTests.cs src/FellowshipExtractor.Api.Tests/Sections/LegendariesTests.cs src/FellowshipExtractor.Api.Tests/DetailTests.cs
git commit -m @'
feat: compose Detail from its eight sections over a re-supplied cascade

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 17: `Bearings`, `Stage` and `Nearby`

**Files:**
- Create: `src/FellowshipExtractor.Api/Bearings.cs`
- Create: `src/FellowshipExtractor.Api/Components/Sections/Stage.razor`, `Stage.razor.scss`
- Create: `src/FellowshipExtractor.Api/Components/Sections/Nearby.razor`
- Test: `src/FellowshipExtractor.Api.Tests/Sections/StageTests.cs`, `src/FellowshipExtractor.Api.Tests/Sections/NearbyTests.cs`

**Interfaces:**
- Consumes: `Panel`, `Empty`, `Pills`, `Pill` from Tasks 5, 6 and 9; `Scoped.Bare` from Task 5.
- Produces: `Bearings`, a `public sealed record` in namespace `FellowshipExtractor.Api` with `Shown`, `Readings`, `Reading`, `Address`, `Page`, `Under`, `Label`, `NetId`, `Kind` and `Related`; plus `Stage` and `Nearby`, both reading `[CascadingParameter] private Bearings Here` and injecting nothing. `Identity`, `Readings` and `Knobs` read the same cascade in Tasks 18 and 19, and `EntityPage` supplies it in Task 20.

`Stage` carries a stylesheet because the tooltip stage div is its own element; `Nearby` carries none, being a `Panel` around a `Pills`.

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

        Assert.Equal("<p class=\"cx-empty\">Loading...</p>", cut.Find("div.cx-entity-tip").InnerHtml.Bare());
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

- [ ] **Step 6: Write `Stage.razor.scss`**

```scss
.cx-entity-tip { flex: 0 0 auto; min-width: 340px; }
```

The class reads as part of the entity page's grid and is listed with it in the stylesheet this refactor starts from, but the div is this component's, so the rule lives here. `EntityPage.razor.scss` owns the flex container round it, in Task 20.

The tooltip fragment that `codex.js` paints into this div goes into a shadow root, and neither this sheet nor any other in the bundle enters one. The fragment keeps supplying its own styling exactly as it does now.

`Nearby.razor` gets no stylesheet.

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~StageTests|FullyQualifiedName~NearbyTests"`
Expected: PASS, 5 tests.

- [ ] **Step 8: Build and run the whole suite**

Run: `dotnet build src/FellowshipExtractor.slnx`
Expected: `0 Warning(s)`, `0 Error(s)`.

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`
Expected: PASS, 100 tests.

- [ ] **Step 9: Commit**

```powershell
git add src/FellowshipExtractor.Api/Bearings.cs src/FellowshipExtractor.Api/Components/Sections/Stage.razor src/FellowshipExtractor.Api/Components/Sections/Stage.razor.scss src/FellowshipExtractor.Api/Components/Sections/Nearby.razor src/FellowshipExtractor.Api.Tests/Sections/StageTests.cs src/FellowshipExtractor.Api.Tests/Sections/NearbyTests.cs
git commit -m @'
feat: add the page cascade record with the Stage and Nearby sections

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 18: `Identity` and `Readings`

**Files:**
- Create: `src/FellowshipExtractor.Api/Components/Sections/Identity.razor`
- Create: `src/FellowshipExtractor.Api/Components/Sections/Readings.razor`
- Test: `src/FellowshipExtractor.Api.Tests/Sections/IdentityTests.cs`, `src/FellowshipExtractor.Api.Tests/Sections/ReadingsTests.cs`

**Interfaces:**
- Consumes: `Panel`, `Facts`, `Fact`, `Rule`, `SubHead`, `Pills`, `Pill` from Tasks 4 to 7; `Bearings` from Task 17; `Export` and `Corpus.Name` from Task 11.
- Produces: `Identity`, which injects nothing and places `<Readings />` itself; and `Readings`, which injects `CodexView Ui` for `Ui.Reading`.

The readings list belongs inside the Identity panel, directly under the row that counts them, so `Identity` places `<Readings />` and `EntityPage` stays flat. The readings are drawn in export order, which is the order the store read them.

Neither section renders a stylesheet: both are made entirely of primitives, and both test classes assert through `MarkupMatches` and `TextContent`, which see past the scope attribute already.

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
Expected: PASS, 106 tests.

- [ ] **Step 7: Commit**

```powershell
git add src/FellowshipExtractor.Api/Components/Sections/Identity.razor src/FellowshipExtractor.Api/Components/Sections/Readings.razor src/FellowshipExtractor.Api.Tests/Sections/IdentityTests.cs src/FellowshipExtractor.Api.Tests/Sections/ReadingsTests.cs
git commit -m @'
feat: add the Identity and Readings sections

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 19: `Knobs`

**Files:**
- Create: `src/FellowshipExtractor.Api/Components/Sections/Knobs.razor`, `Knobs.razor.scss`
- Test: `src/FellowshipExtractor.Api.Tests/Sections/KnobsTests.cs`

**Interfaces:**
- Consumes: `Panel` from Task 5, `Knob` from Task 10, `Bearings` from Task 17, `Export` and `Corpus.Name` from Task 11, and `Styles/_shared.scss`'s `sunken` and `edged` mixins from Task 3.
- Produces: `Knobs`, a headless panel reading `[CascadingParameter] private Bearings Here` and injecting `CodexView Ui` and `EntityStore Store`. This task completes the four section stylesheets.

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

- [ ] **Step 4: Write `Knobs.razor.scss`**

The `<select>`, the two `<input>`s and every rung button are this section's own elements even though a `Knob` wraps them, so the shipped rules keep their `.cx-knob` ancestor and need no `::deep`:

```scss
@use "../../Styles/shared" as *;

.cx-knob select, .cx-knob input {
  width: 100%; appearance: none;
  @include sunken;
  font: 400 13px/1 var(--sans); padding: 7px 9px; border-radius: 2px;
}
.cx-knob select:focus, .cx-knob input:focus { @include edged; }

.cx-rungs {
  display: flex; flex-wrap: wrap; gap: 4px;

  button {
    appearance: none; cursor: pointer; background: var(--sunken); border: 1px solid var(--line);
    border-radius: 2px; padding: 5px 9px; font: 600 12px/1 var(--sans); color: var(--muted);

    /* The only visual state a pressed rung has. codex.js writes the attribute; the server renders
       it false on every rung. */
    &[aria-pressed="true"] { border-color: currentColor; background: var(--raised); }
  }
}
```

A rung takes `color: var(--muted)` rather than the ink a sunken field takes, so it spells its ground and border out instead of including the mixin.

`.cx-knob` itself and `.cx-knob > span` are the `Knob` primitive's elements and are styled in `Knob.razor.scss`, written in Task 10. The `.cx-knob` ancestor here is matched on its class alone and carries no attribute of its own.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~KnobsTests"`
Expected: PASS, 8 tests. `Renders_the_hero_select_with_its_navigation_attribute_decoded` reads the attribute through the parsed DOM, so it holds whether or not the renderer escaped the apostrophes; if it fails, the attribute has been turned into a Blazor directive or routed through a component parameter and must be put back into markup verbatim.

- [ ] **Step 6: Build and run the whole suite**

Run: `dotnet build src/FellowshipExtractor.slnx`
Expected: `0 Warning(s)`, `0 Error(s)`.

```powershell
Get-ChildItem src/FellowshipExtractor.Api/Components/Sections -Filter *.razor.scss | Select-Object -ExpandProperty Name
```

Expected: `Knobs.razor.scss`, `Particulars.razor.scss`, `Stage.razor.scss` and `StatSlots.razor.scss`, and nothing else. The other nine sections compose primitives and draw no class of their own.

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`
Expected: PASS, 114 tests.

- [ ] **Step 7: Commit**

```powershell
git add src/FellowshipExtractor.Api/Components/Sections/Knobs.razor src/FellowshipExtractor.Api/Components/Sections/Knobs.razor.scss src/FellowshipExtractor.Api.Tests/Sections/KnobsTests.cs
git commit -m @'
feat: add the Knobs section

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 20: `EntityPage` adopts the five page sections

**Files:**
- Modify: `src/FellowshipExtractor.Api/Components/Pages/EntityPage.razor`
- Create: `src/FellowshipExtractor.Api/Components/Pages/EntityPage.razor.scss`

**Interfaces:**
- Consumes: `Bearings` from Task 17; `Stage`, `Nearby`, `Identity`, `Readings`, `Knobs` from Tasks 17 to 19; `Detail` from Task 16.
- Produces: an `EntityPage` whose markup is the page title, the crumbs, the `<h1>` and the five section tags inside the `.cx-entity` grid, all inside one `<CascadingValue Value="Here">`, and the first of the eight stylesheets that belong to components which already exist.

This task's gate is the build, the structural checks below and the whole suite staying green; the page itself is confirmed in a browser in Task 23. `.cx-entity`, `.cx-entity-top` and `.cx-entity-side` stay as markup: they are the page's own grid and `.cx-entity-side .cx-panel + .cx-panel { margin-top: 0 }` selects through them.

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

- [ ] **Step 3: Write `EntityPage.razor.scss`**

`src/FellowshipExtractor.Api/Components/Pages/EntityPage.razor.scss`:

```scss
.cx-entity { display: grid; grid-template-columns: minmax(0, 1fr); gap: 18px; }

.cx-entity-top { display: flex; flex-wrap: wrap; gap: 18px; align-items: flex-start; }

.cx-entity-side {
  flex: 1 1 420px; min-width: 0;
  display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); gap: 18px;
  align-items: start;
}
```

The two rules that reach through this grid are elsewhere, each with the component that renders the element it styles: `.cx-entity-tip` with `Stage` in Task 17, and `.cx-entity-side .cx-panel + .cx-panel` with `Panel` in Task 5. Both ancestors are matched on the class alone, so neither needs an attribute here.

- [ ] **Step 4: Confirm the page's markup is the five section tags inside the grid**

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components/Pages/EntityPage.razor -Pattern '<Stage />|<Knobs />|<Identity />|<Nearby />|<Detail Entity="Shown" />|<CascadingValue Value="Here">'
```

Expected: six matches, one each. `<Readings />` is placed by `Identity` and does not appear here.

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components/Pages/EntityPage.razor -Pattern 'cx-knob|data-stage|cx-facts|cx-pills|cx-num'
```

Expected: no matches. Every one of those now lives in a section.

- [ ] **Step 5: Build and run the whole suite**

Run: `dotnet build src/FellowshipExtractor.slnx`
Expected: `0 Warning(s)`, `0 Error(s)`.

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components/Pages/EntityPage.razor.css -Pattern 'cx-entity'
```

Expected: three selectors, each carrying the scope attribute. `EntityPage` is a routable page and is scoped like any other component.

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`
Expected: PASS, 114 tests.

- [ ] **Step 6: Commit**

```powershell
git add src/FellowshipExtractor.Api/Components/Pages/EntityPage.razor src/FellowshipExtractor.Api/Components/Pages/EntityPage.razor.scss
git commit -m @'
refactor: compose EntityPage from its five page sections

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 21: `Browse` adopts the primitives

**Files:**
- Modify: `src/FellowshipExtractor.Api/Components/Pages/Browse.razor`
- Create: `src/FellowshipExtractor.Api/Components/Pages/Browse.razor.scss`

**Interfaces:**
- Consumes: `Panel`, `Empty`, `Chip`, `Icon`, `CountBadge` from Tasks 4, 5, 9 and 10, and `Styles/_shared.scss`'s `sunken`, `edged` and `gold-slab` mixins from Task 3.
- Produces: no new type. `Browse` keeps every member it has, and gains the largest of the component stylesheets.

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

and its `</aside>` with `</Panel>`. The rail then renders `class="cx-panel cx-rail"` rather than `class="cx-rail cx-panel"`; class order is read by no selector in the design system. `.cx-rail` and its narrow-screen override are styled in `Panel.razor.scss`, written in Task 5, because the `<aside>` the class lands on is `Panel`'s element.

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

- [ ] **Step 9: Write `Browse.razor.scss`**

The rail's facets, the chosen-filter chips, the tabs, the results table and the pager are all this page's own markup, so this is the largest of the component sheets.

```scss
@use "../../Styles/shared" as *;

.cx-browse {
  display: grid; grid-template-columns: var(--rail) minmax(0, 1fr); gap: 18px; align-items: start;
}

/* One column below the rail's width, and the results first: a filter panel a reader has to scroll
   past to reach what they asked for is a filter panel in the way. The rail's half of this rule is
   in Panel.razor.scss, which owns the <aside> the cx-rail class lands on. */
@media (max-width: 1080px) {
  .cx-browse { grid-template-columns: minmax(0, 1fr); }
  .cx-browse > section { order: 1; }
}

.cx-facet + .cx-facet { border-top: 1px solid var(--line-soft); margin-top: 10px; padding-top: 10px; }
.cx-facet > summary {
  cursor: pointer; list-style: none; display: flex; align-items: center; gap: 6px;
  font-size: 11px; letter-spacing: .13em; text-transform: uppercase; color: var(--faint);
  padding: 2px 0;
}
.cx-facet > summary::-webkit-details-marker { display: none; }
.cx-facet > summary::after { content: "+"; margin-left: auto; color: var(--gold-deep); font-size: 14px; }
.cx-facet[open] > summary::after { content: "\2212"; }
.cx-facet > summary:hover { color: var(--gold-bright); }

.cx-facet-values { list-style: none; margin: 6px 0 0; padding: 0; max-height: 260px; overflow-y: auto; }
.cx-facet-values li a {
  display: flex; align-items: baseline; gap: 7px; padding: 2px 4px; border-radius: 2px;
  color: var(--core-secondary-000); font-size: 13px;
}
.cx-facet-values li a:hover { background: var(--raised); text-decoration: none; }
.cx-facet-values li a.on { background: var(--raised); color: var(--gold-bright); font-weight: 600; }
.cx-facet-values li a.on::before { content: "\2713"; color: var(--gold-bright); font-size: 11px; }

.cx-ranges { margin-top: 8px; }
.cx-range { display: grid; grid-template-columns: 1fr 1fr; gap: 4px; margin-bottom: 7px; }
.cx-range > span { grid-column: 1 / -1; font-size: 12px; color: var(--core-secondary-000); }
.cx-range input {
  width: 100%; min-width: 0; appearance: textfield;
  @include sunken;
  border-radius: 2px; font: 400 12px/1 var(--sans); padding: 5px 6px;
}
.cx-range input:focus { @include edged; }
.cx-ranges button {
  width: 100%; appearance: none; cursor: pointer; margin-top: 4px;
  @include gold-slab;
  border: 0; border-radius: 2px; font: 600 12px/1 var(--sans); letter-spacing: .06em; padding: 8px;
}
.cx-ranges button:hover { background: var(--gold); }

.cx-chosen { display: flex; flex-wrap: wrap; gap: 5px; margin: 0 0 12px; padding: 0; list-style: none; }
.cx-chosen li a {
  display: inline-flex; align-items: center; gap: 6px;
  background: var(--raised); border: 1px solid var(--line); border-radius: 2px;
  padding: 3px 8px; font-size: 12px; color: var(--ink);
}
.cx-chosen li a:hover {
  border-color: var(--semantic-negative); color: var(--semantic-negative); text-decoration: none;
}
.cx-chosen li a::after { content: "\00d7"; color: var(--faint); }
.cx-chosen .cx-clear-all a { color: var(--muted); font-size: 12px; background: none; border: 0; }
.cx-chosen .cx-clear-all a::after { content: none; }

.cx-tabs {
  display: flex; gap: 2px; overflow-x: auto; border-bottom: 1px solid var(--line); margin-bottom: 12px;
}
.cx-tab {
  display: inline-flex; align-items: baseline; gap: 7px; white-space: nowrap;
  padding: 9px 13px; font-size: 13px; color: var(--muted);
  border-bottom: 2px solid transparent;
}
.cx-tab:hover { color: var(--ink); text-decoration: none; }
.cx-tab[aria-current] { color: var(--gold-bright); border-bottom-color: var(--gold); font-weight: 600; }

.cx-results-head {
  display: flex; align-items: center; gap: 12px; flex-wrap: wrap;
  margin-bottom: 8px; font-size: 12px; color: var(--muted);
}
.cx-results-head .cx-count { margin-right: auto; }
.cx-results-head .cx-count b { color: var(--ink); }

.cx-table-scroll { overflow-x: auto; }

table.cx-rows {
  width: 100%; border-collapse: collapse; font-size: 13px;

  /* Not sticky. The scroll container round the table is what a sticky header would resolve against,
     and an offset inside it pushes the header down over the first row instead of holding it under
     the masthead, which is what it did. */
  thead th {
    text-align: left; font-weight: 600; font-size: 11px; letter-spacing: .1em; text-transform: uppercase;
    color: var(--faint); padding: 7px 10px; border-bottom: 1px solid var(--line);
    background: var(--sunken); white-space: nowrap;

    a { color: inherit; display: inline-flex; align-items: center; gap: 4px; }
    a:hover { color: var(--gold-bright); text-decoration: none; }
  }
  thead th[aria-sort] a { color: var(--gold-bright); }

  tbody td { padding: 5px 10px; border-bottom: 1px solid var(--line-soft); vertical-align: middle; }
  tbody tr:hover td { background: #131f2a; }

  .cx-numeric { text-align: right; font-variant-numeric: tabular-nums; color: var(--muted); }
  .cx-art { width: 40px; }
  .cx-group-head td {
    background: var(--raised); font-family: var(--display); letter-spacing: .09em; text-transform: uppercase;
    color: var(--gold-bright); font-size: 13px; padding: 7px 10px;
  }
  .cx-tags { max-width: 230px; }
  .cx-unit { color: var(--faint); }
}

.cx-pager {
  display: flex; align-items: center; gap: 6px; justify-content: flex-end; margin-top: 12px; font-size: 12px;
}
.cx-pager a, .cx-pager span {
  padding: 5px 10px; border: 1px solid var(--line); border-radius: 2px; color: var(--muted);
}
.cx-pager a:hover { border-color: var(--gold-deep); color: var(--gold-bright); text-decoration: none; }
.cx-pager [aria-current] {
  @include gold-slab;
  border-color: var(--gold-deep); font-weight: 600;
}
.cx-pager .cx-gap { border: 0; padding: 5px 2px; }
```

Four rules that read as this page's leave it, each following the element they style: `.cx-rail` and its narrow-screen override to `Panel.razor.scss` in Task 5, `table.cx-rows .cx-art img` to `Icon.razor.scss` in Task 4, `table.cx-rows .cx-chip` to `Chip.razor.scss` in Task 10, and `table.cx-rows .cx-nm` to `EntityLink.razor.scss` in Task 22, which is what the name cell is. `.cx-empty` goes with `Empty` and every `.cx-num` with `CountBadge`.

- [ ] **Step 10: Build and run the whole suite**

Run: `dotnet build src/FellowshipExtractor.slnx`
Expected: `0 Warning(s)`, `0 Error(s)`.

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components/Pages/Browse.razor.css -Pattern 'cx-chip|cx-art img|cx-nm|cx-rail'
```

Expected: no matches. Each of those four selectors belongs to the component that renders its element, and a copy left here would be a second definition of the same rule at a different specificity.

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`
Expected: PASS, 114 tests.

- [ ] **Step 11: Commit**

```powershell
git add src/FellowshipExtractor.Api/Components/Pages/Browse.razor src/FellowshipExtractor.Api/Components/Pages/Browse.razor.scss
git commit -m @'
refactor: adopt the cx primitives in Browse

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 22: `Home`, `NotFound`, `MegaMenu` and `Omnibox` adopt the primitives, and the page shell gains its stylesheets

**Files:**
- Modify: `src/FellowshipExtractor.Api/Components/Pages/Home.razor`
- Create: `src/FellowshipExtractor.Api/Components/Pages/Home.razor.scss`
- Modify: `src/FellowshipExtractor.Api/Components/Pages/NotFound.razor`
- Modify: `src/FellowshipExtractor.Api/Components/Layout/MegaMenu.razor`
- Create: `src/FellowshipExtractor.Api/Components/Layout/MegaMenu.razor.scss`
- Modify: `src/FellowshipExtractor.Api/Components/Layout/Omnibox.razor`
- Create: `src/FellowshipExtractor.Api/Components/Layout/Omnibox.razor.scss`
- Create: `src/FellowshipExtractor.Api/Components/Layout/MainLayout.razor.scss`
- Create: `src/FellowshipExtractor.Api/Components/Shared/Crumbs.razor.scss`
- Create: `src/FellowshipExtractor.Api/Components/Shared/EntityLink.razor.scss`

**Interfaces:**
- Consumes: `Panel`, `Empty`, `Icon`, `CountBadge` from Tasks 4, 5 and 9, and `Styles/_shared.scss`'s `$gutter`, `$on-gold`, `sunken`, `edged`, `gold-slab` and `eyebrow` from Task 3.
- Produces: a `Home` with a computed `private string Roster => $"{Store.Vocabulary.Heroes.Count} heroes";`. No other member changes anywhere, and the six stylesheets written here complete the twenty-seven.

`MainLayout.razor`, `Crumbs.razor` and `EntityLink.razor` keep their markup exactly as it is and gain a stylesheet each. They are grouped into this task because it is the one that owns the page shell and the menus, and because a stylesheet with no markup change is not a reviewer gate of its own. Without them the masthead, `<main>`, the footer, the hover-tooltip layer, the crumbs and the results-table name cell all render unstyled while the build stays green.

`NotFound` gets no stylesheet. Its `<h1>` takes the global heading rule from `Styles/codex.scss` and everything else it draws is a `Panel` and an `Empty`, each of which owns its own styling.

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

- [ ] **Step 7: Write `Home.razor.scss`**

```scss
@use "../../Styles/shared" as *;

.cx-hero-band { padding: clamp(22px, 5vw, 54px) 0 26px; text-align: center; }
.cx-hero-band h1 { margin: 0 0 8px; }
.cx-hero-band p { color: var(--muted); max-width: 62ch; margin: 0 auto; }

.cx-cards { display: grid; grid-template-columns: repeat(auto-fill, minmax(230px, 1fr)); gap: 14px; }

/* The card's own frame is global: this page draws one card itself and hands the class to
   EntityLink for the other. The name inside a card is always this page's element, so the hover
   rule that tints it lives here, matching the card ancestor on its class. */
.cx-card:hover .cx-card-name { color: var(--gold-bright); }
.cx-card-name {
  font-family: var(--display); font-size: 17px; letter-spacing: .05em; text-transform: uppercase;
}
.cx-card-count { font-size: 12px; color: var(--muted); }
.cx-card-links {
  margin: 8px 0 0; padding: 0; list-style: none; font-size: 12.5px; columns: 2; column-gap: 14px;
}
.cx-card-links li { break-inside: avoid; }

.cx-roster { display: flex; flex-wrap: wrap; gap: 8px; }
.cx-roster a {
  display: inline-flex; align-items: center; gap: 8px; padding: 6px 12px 6px 8px;
  @include sunken;
  border-radius: 2px; font-size: 13px;
}
.cx-roster a:hover { text-decoration: none; border-color: currentColor; }
/* The chosen hero, marked the way a pressed rarity rung is: the entity page draws its roster with
   one entry current, and the raised ground is what tells that apart from the hovered entry, which
   the border alone already means. Same specificity as the hover rule, so it is placed after it. */
.cx-roster a[aria-current] { border-color: currentColor; background: var(--raised); }
.cx-pip { width: 10px; height: 10px; border-radius: 50%; flex: 0 0 auto; }
.cx-who { font-weight: 600; }
.cx-code { color: var(--faint); font-family: var(--mono); font-size: 11px; }

.cx-linked { display: flex; align-items: center; gap: 9px; }
```

The hover and current rules on a roster entry are the one equal-specificity pair in the stylesheet this refactor starts from that source order decides, and they stay in one file in that order. `.cx-card` itself is global, in `Styles/codex.scss`, because two components draw one. `.cx-linked img` is an `Icon` and is styled in `Icon.razor.scss`.

- [ ] **Step 8: Write `MegaMenu.razor.scss`**

```scss
@use "../../Styles/shared" as *;

.cx-mega { flex: 0 1 auto; min-width: 0; }
.cx-mega-bar {
  display: flex; gap: 2px; list-style: none; margin: 0; padding: 0;
  overflow-x: auto; scrollbar-width: none;
}
.cx-mega-bar::-webkit-scrollbar { display: none; }

/* Static, so the panel below resolves its full width against the sticky masthead rather than
   against the group. */
.cx-mega-group { position: static; }

.cx-mega-open {
  appearance: none; background: transparent; border: 0; cursor: pointer;
  color: var(--muted); font: 600 12px/1 var(--sans); letter-spacing: .09em; text-transform: uppercase;
  padding: 18px 12px; white-space: nowrap;
  border-bottom: 2px solid transparent;
  transition: color .12s ease, border-color .12s ease;
}
.cx-mega-open:hover,
.cx-mega-group[data-open] .cx-mega-open { color: var(--gold-bright); border-bottom-color: var(--gold); }

.cx-mega-panel {
  position: absolute; left: 0; right: 0; top: 100%;
  display: none;
  background: linear-gradient(180deg, #101c27 0%, #0c1620 100%);
  border-top: 1px solid var(--gold-deep);
  border-bottom: 1px solid var(--line);
  box-shadow: 0 26px 40px -24px #000;
}
.cx-mega-group[data-open] .cx-mega-panel { display: block; }

.cx-mega-cols {
  max-width: var(--wide); margin: 0 auto; padding: 18px $gutter 22px;
  columns: 210px 5; column-gap: 26px;
  max-height: min(70vh, 620px); overflow-y: auto;
}

.cx-mega-head {
  display: flex; align-items: baseline; gap: 7px; break-inside: avoid; break-after: avoid;
  font-family: var(--display); font-size: 15px; letter-spacing: .06em; text-transform: uppercase;
  color: var(--gold-bright); padding-bottom: 6px; margin: 0 0 8px;
  border-bottom: 1px solid var(--line);
}
.cx-mega-head + .cx-mega-sec { break-before: avoid; }
.cx-mega-sec { margin: 0 0 14px; break-inside: avoid; }
.cx-mega-sec-head { @include eyebrow; }
.cx-mega-sec ul { list-style: none; margin: 0; padding: 0; }
.cx-mega-sec li a {
  display: flex; align-items: baseline; gap: 6px; padding: 1px 0;
  color: var(--core-secondary-000); font-size: 13px;
}
.cx-mega-sec li a:hover { color: var(--gold-bright); text-decoration: none; }
```

`data-open` is written only by `codex.js` and read only by these two rules, which is why it stays a plain attribute on a plain element. `.cx-mega-sec-head` includes the same `eyebrow` mixin `SubHead.razor.scss` does, which is what keeps the two halves of the shipped `.cx-mega-sec-head, .cx-sub-head` selector list the same rule after the split.

- [ ] **Step 9: Write `Omnibox.razor.scss`**

```scss
@use "../../Styles/shared" as *;

.cx-omnibox {
  position: relative; flex: 1 1 260px; min-width: 0;
  display: flex; align-items: center; margin-left: auto;
}
.cx-omnibox svg {
  position: absolute; left: 10px; width: 15px; height: 15px; color: var(--faint); pointer-events: none;
}
.cx-omnibox input {
  width: 100%; appearance: none;
  @include sunken;
  border-radius: 3px; font: 400 13px/1 var(--sans); padding: 9px 30px 9px 32px;
}
.cx-omnibox input::placeholder { color: var(--faint); }
.cx-omnibox input::-webkit-search-cancel-button { display: none; }
.cx-omnibox input:focus { @include edged; box-shadow: 0 0 0 1px var(--gold-deep); }
.cx-omnibox-clear {
  position: absolute; right: 4px; appearance: none; background: transparent; border: 0;
  color: var(--faint); font-size: 18px; line-height: 1; padding: 4px 8px; cursor: pointer;
}
.cx-omnibox-clear:hover { color: var(--ink); }

.cx-omnibox-drop {
  position: absolute; top: calc(100% + 6px); right: 0; width: min(640px, 92vw);
  background: #0d1721; border: 1px solid var(--line); border-top-color: var(--gold-deep);
  box-shadow: 0 26px 40px -22px #000; z-index: 70;
  max-height: min(70vh, 560px); overflow-y: auto;
}
.cx-omnibox-chips {
  display: flex; gap: 4px; padding: 8px; flex-wrap: wrap; border-bottom: 1px solid var(--line-soft);
}
.cx-omnibox-chip {
  appearance: none; cursor: pointer; border: 1px solid transparent; border-radius: 2px;
  background: var(--raised); color: var(--muted); font: 600 11px/1 var(--sans);
  padding: 6px 9px; letter-spacing: .04em;
}
.cx-omnibox-chip[aria-pressed="true"] { @include gold-slab; }
.cx-omnibox-chip:hover { color: var(--ink); }
.cx-omnibox-chip[aria-pressed="true"]:hover { color: $on-gold; }

.cx-omnibox-hit {
  display: flex; align-items: center; gap: 10px; padding: 6px 10px;
  border-bottom: 1px solid var(--line-soft); color: var(--ink);
}
.cx-omnibox-hit:hover,
.cx-omnibox-hit[data-active] { background: var(--raised); text-decoration: none; }
.cx-omnibox-hit .cx-t { flex: 1 1 auto; min-width: 0; }
.cx-omnibox-hit .cx-nm {
  display: block; font-weight: 600; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
}
.cx-omnibox-hit .cx-sub {
  display: block; font-size: 11px; color: var(--faint); font-family: var(--mono);
  overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
}
.cx-omnibox-hit .cx-ty { flex: 0 0 auto; font-size: 11px; color: var(--muted); }
.cx-omnibox-hit mark { background: transparent; color: var(--gold-bright); text-decoration: underline; }
.cx-omnibox-all {
  display: block; text-align: center; padding: 10px; font-weight: 600; letter-spacing: .05em;
  @include gold-slab;
}
.cx-omnibox-all:hover, .cx-omnibox-all[data-active] { background: var(--gold); text-decoration: none; }
.cx-omnibox-none { padding: 18px; text-align: center; color: var(--muted); }
```

The `<mark>` runs are emitted by this component from `Suggestions.Mark`, element by element rather than as one `MarkupString`, so they carry its attribute and need no `::deep`. `.cx-omnibox-hit img` is an `Icon` and is styled in `Icon.razor.scss`. `.cx-omnibox-hit .cx-nm` stays here and is a different element from the `.cx-nm` in the results table, which is an `EntityLink`.

Scoped styling is compile-time and is unaffected by render mode: this sheet is rewritten with the same kind of attribute as every other and works identically under `@rendermode InteractiveServer`.

- [ ] **Step 10: Write `MainLayout.razor.scss`**

`MainLayout.razor` keeps its markup exactly as it is. This sheet owns the page shell: the skip link, the masthead, `<main>`, the hover-tooltip layer and the footer.

```scss
@use "../../Styles/shared" as *;

.cx-skip {
  position: absolute; left: -9999px; top: 0; z-index: 100;
  background: var(--panel); color: var(--ink); padding: 10px 16px; border: 1px solid var(--gold);
}
.cx-skip:focus { left: 8px; top: 8px; }

.cx-masthead {
  position: sticky; top: 0; z-index: 60;
  background: linear-gradient(180deg, #101b26 0%, #0b141d 100%);
  border-bottom: 1px solid var(--line);
  box-shadow: 0 1px 0 #00000060, 0 10px 24px -18px #000;
}

.cx-bar {
  max-width: var(--wide); margin: 0 auto; padding: 0 $gutter;
  display: flex; align-items: center; gap: clamp(10px, 2vw, 26px); min-height: 54px;
}

.cx-brand { display: inline-flex; align-items: center; gap: 9px; flex: 0 0 auto; text-decoration: none; }
.cx-brand:hover { text-decoration: none; }
.cx-brand-mark {
  width: 18px; height: 18px;
  background: var(--chrome-arrow) center/contain no-repeat;
  filter: drop-shadow(0 0 4px #00000090);
  opacity: .9;
}
.cx-brand-name {
  font-family: var(--display); font-size: 18px; letter-spacing: .11em; text-transform: uppercase;
  color: var(--core-secondary-000); font-weight: 400;
}
.cx-brand-name b { color: var(--gold-bright); font-weight: 600; }

main { max-width: var(--wide); margin: 0 auto; padding: 14px $gutter 90px; }

/* Where the fragment a hovered link fetches is drawn. Only the layer is styled: what goes inside it
   is the API's own markup under .fx-tip, in a shadow root no stylesheet here enters. */
.cx-tip-layer {
  position: fixed; left: 0; top: 0; z-index: 90;
  pointer-events: none;
  filter: drop-shadow(0 16px 26px #000000b0);
}
.cx-tip-layer[hidden] { display: none; }

.cx-foot {
  border-top: 1px solid var(--line); margin-top: 40px;
  padding: 22px $gutter 40px; color: var(--faint); font-size: 12px;
}
.cx-foot p { max-width: var(--wide); margin: 0 auto 6px; }
.cx-foot b { color: var(--muted); font-weight: 600; }
.cx-foot-links { display: flex; gap: 14px; }
```

`main` is this layout's element, so the bare element selector reaches only the one `<main>` the layout renders.

- [ ] **Step 11: Write `Crumbs.razor.scss` and `EntityLink.razor.scss`**

Both components keep their markup exactly as it is.

`src/FellowshipExtractor.Api/Components/Shared/Crumbs.razor.scss`:

```scss
.cx-crumbs { margin: 0 0 4px; }
.cx-crumbs ol {
  list-style: none; display: flex; flex-wrap: wrap; gap: 6px; margin: 0; padding: 0; font-size: 12px;
}
.cx-crumbs li + li::before { content: "\203A"; color: var(--faint); margin-right: 6px; }
.cx-crumbs a { color: var(--muted); }
.cx-crumbs a:hover { color: var(--gold-bright); }
.cx-crumbs span { color: var(--ink); }
```

`src/FellowshipExtractor.Api/Components/Shared/EntityLink.razor.scss`:

```scss
/* The results table's name cell is an EntityLink carrying Class="cx-nm", so the anchor the weight
   is set on is this component's element and the rule follows it here. */
table.cx-rows .cx-nm { font-weight: 600; }
```

That is the whole file, and it is the clearest case of the ownership rule: read off the flat stylesheet the rule looks like part of the results table, and the element it styles is rendered three files away.

- [ ] **Step 12: Confirm the shapes are gone from all four files**

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components/Pages/Home.razor,src/FellowshipExtractor.Api/Components/Pages/NotFound.razor,src/FellowshipExtractor.Api/Components/Layout/MegaMenu.razor,src/FellowshipExtractor.Api/Components/Layout/Omnibox.razor -Pattern 'class="cx-panel"|class="cx-empty"|class="cx-num"|loading="lazy"'
```

Expected: no matches.

```powershell
Select-String -Path src/FellowshipExtractor.Api/Components/Layout/Omnibox.razor -Pattern '@rendermode InteractiveServer'
```

Expected: one match, on the first line.

- [ ] **Step 13: Confirm all twenty-seven stylesheets exist and every one compiled**

Run: `dotnet build src/FellowshipExtractor.slnx`
Expected: `0 Warning(s)`, `0 Error(s)`.

```powershell
$all  = Get-ChildItem src/FellowshipExtractor.Api/Components -Recurse -File
$scss = $all | Where-Object Name -like '*.razor.scss'
$css  = $all | Where-Object Name -like '*.razor.css'

$scss.Count
$css.Count

Compare-Object ($scss.Name -replace '\.scss$', '') $css.Name
```

Expected: `27`, `27`, and no output from `Compare-Object`. A `.scss` with no matching `.css` did not compile; a `.css` with no matching `.scss` is a leftover from a renamed file and is deleted. `Where-Object` rather than `-Filter`, because `-Filter` is answered by the filesystem and matches 8.3 short names as well as long ones.

```powershell
$scss | Where-Object { -not (Test-Path ($_.FullName -replace '\.razor\.scss$', '.razor')) } |
    Select-Object FullName
```

Expected: no output. A `.razor.scss` whose `.razor` is missing or misspelled compiles to a `.razor.css` the Razor SDK never scopes, so its rules land nowhere.

- [ ] **Step 14: Run the whole suite**

Run: `dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`
Expected: PASS, 114 tests.

- [ ] **Step 15: Commit**

```powershell
git add src/FellowshipExtractor.Api/Components/Pages/Home.razor src/FellowshipExtractor.Api/Components/Pages/Home.razor.scss src/FellowshipExtractor.Api/Components/Pages/NotFound.razor src/FellowshipExtractor.Api/Components/Layout/MegaMenu.razor src/FellowshipExtractor.Api/Components/Layout/MegaMenu.razor.scss src/FellowshipExtractor.Api/Components/Layout/Omnibox.razor src/FellowshipExtractor.Api/Components/Layout/Omnibox.razor.scss src/FellowshipExtractor.Api/Components/Layout/MainLayout.razor.scss src/FellowshipExtractor.Api/Components/Shared/Crumbs.razor.scss src/FellowshipExtractor.Api/Components/Shared/EntityLink.razor.scss
git commit -m @'
refactor: adopt the cx primitives in Home, NotFound, MegaMenu and Omnibox

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
'@
```

---

### Task 23: Full acceptance verification

**Files:**
- Read: every file the plan created or modified
- Create: `C:\Users\Sean\AppData\Local\Temp\claude\G--source-Fellowship-Data-Extractor\2109da8e-31a6-4995-a9b0-5036194d1636\scratchpad\after\*.html`
- Create: `C:\Users\Sean\AppData\Local\Temp\claude\G--source-Fellowship-Data-Extractor\2109da8e-31a6-4995-a9b0-5036194d1636\scratchpad\clone\`

**Interfaces:**
- Consumes: everything. This task adds no code.

The twenty-one numbered checks below are the spec's acceptance criteria, in order. Every one of them is run and its output confirmed before the work is called done. Criteria 18, 19 and 20 are answered in a browser and by nothing else: each is invisible to the compiler and to every bUnit assertion.

**The accepted differences.** A text diff of a baseline page against its post-refactor capture may show only these six, and each occurrence must be one of them:

- **(a)** the browse rail renders `class="cx-panel cx-rail"` rather than `class="cx-rail cx-panel"`; class order is read by no selector in the design system;
- **(b)** no literal space before a panel-head badge; `.cx-panel-head` is `display: flex` with `gap: 10px` and the badge carries `margin-left: auto`, so the head text and the badge are separate flex items and a trailing space collapses;
- **(c)** no literal space before a pill badge; `.cx-pills .cx-num { padding: 0 0 0 4px }` supplies the gap, which is why the reading pills already render with no space today;
- **(d)** no empty `<span class="cx-unit"></span>` for a fact with no unit; an empty inline span with no padding and no content renders nothing, and the identity rows already emit none;
- **(e)** every element rendered by a component with a stylesheet carries a valueless `b-` scope attribute. Step 16 strips these from the capture before diffing, with the same pattern `Scoped.Bare` uses, so they never reach the comparison at all;
- **(f)** the `<head>` carries one more line, `<link rel="stylesheet" href="FellowshipExtractor.Api.styles.css" />`, immediately after the `css/codex.css` link. That line appears exactly once per page and is what loads every scoped rule.

Any seventh kind of difference is a regression and is fixed before this task completes.

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

- [ ] **Step 10: Criteria 15 and 16, no `.css` is committed and `.gitignore` carries the patterns**

```powershell
git ls-files "*.css"
git ls-files "*.css.map"
git status --porcelain
Select-String -Path .gitignore -Pattern '^\*\.css$|^\*\.css\.map$'
```

Expected: the first two return nothing; `git status` shows nothing from this plan, and any file it does show belongs to the concurrent session and is left exactly as it is; the last returns two matches. A `.css` still listed by `git ls-files` means `git rm --cached` was never run in Task 3, and adding the pattern alone does not untrack a tracked file.

```powershell
git log --oneline --diff-filter=D -- src/FellowshipExtractor.Api/wwwroot/css/codex.css
```

Expected: one commit, the one Task 3 made. The hand-written stylesheet is deleted rather than left beside its replacement.

- [ ] **Step 11: Criterion 17, a fresh clone plus `dotnet build` produces every stylesheet**

Nothing beyond the build is needed and `sasscompiler.json` is what makes that true. Prove it in a directory that has never been built:

```powershell
$clone = "C:\Users\Sean\AppData\Local\Temp\claude\G--source-Fellowship-Data-Extractor\2109da8e-31a6-4995-a9b0-5036194d1636\scratchpad\clone"

Remove-Item -Recurse -Force $clone -ErrorAction Ignore
git clone --branch (git rev-parse --abbrev-ref HEAD) G:\source\Fellowship_Data_Extractor $clone

Get-ChildItem (Join-Path $clone "src/FellowshipExtractor.Api/wwwroot/css") -ErrorAction Ignore
```

Expected: the clone succeeds and the second command returns nothing. `codex.css` is the only file in `wwwroot/css`, so once it is untracked git has no content under that path and a fresh checkout carries neither the file nor the folder. Then build it:

```powershell
dotnet build (Join-Path $clone "src/FellowshipExtractor.slnx")

Get-ChildItem (Join-Path $clone "src/FellowshipExtractor.Api/wwwroot/css")

$built = Get-ChildItem (Join-Path $clone "src/FellowshipExtractor.Api/Components") -Recurse -File
($built | Where-Object Name -like '*.razor.scss').Count
($built | Where-Object Name -like '*.razor.css').Count

Get-Item (Join-Path $clone "src/FellowshipExtractor.Api/obj/Debug/net10.0/scopedcss/bundle/FellowshipExtractor.Api.styles.css")
```

Expected: `0 Warning(s)`, `0 Error(s)`; `codex.css` now exists; both counts are `27`; and the bundle exists and is non-empty. dart-sass creates its target directory, so the missing `wwwroot/css` folder is written on the first build. If the build instead reports that it cannot write there, commit an empty `src/FellowshipExtractor.Api/wwwroot/css/.gitkeep` so git carries the folder, and re-run this step from the clone.

```powershell
Select-String -Path (Join-Path $clone "src/FellowshipExtractor.Api/obj/Debug/net10.0/scopedcss/bundle/FellowshipExtractor.Api.styles.css") -Pattern '\[b-[a-z0-9]{10}\]' | Measure-Object | Select-Object -ExpandProperty Count
```

Expected: a count in the hundreds. The bundle carries the component rules with scope attributes on them; a count of zero means the `.razor.css` files were generated but never scoped.

If the build fails on a type declared in a file `git status` reported as untracked, that file belongs to the concurrent session rather than to this plan. Record which file it is, wait for that session to commit it, and re-run this step; the criterion is not answered until the clone builds.

Then remove the clone:

```powershell
Remove-Item -Recurse -Force $clone
```

- [ ] **Step 12: Criterion 21, every rule in the stylesheet this refactor started from appears in exactly one `.scss`**

**Declarations are the unit here, not selectors.** The split deliberately rewrites selectors: `.cx-facts tr` becomes a bare `tr` in `Fact.razor.scss`, `.cx-pills li a` becomes `.cx-pills a` in `Pill.razor.scss`, and `.cx-mega-sec-head, .cx-sub-head` becomes an `@include eyebrow` in two files. Searching the `.scss` sources for the old selector text reports every one of those as missing while it is correctly implemented. What survives the split unchanged is the declarations: nesting and `::deep` move selectors around, `@include plated` expands to the same three declarations, and `$gutter` to the same `clamp()`. So compare declarations, and compare them against the built output rather than against the sources.

Build first, so the two generated files are current:

```powershell
dotnet build src/FellowshipExtractor.slnx

$scratch = "C:\Users\Sean\AppData\Local\Temp\claude\G--source-Fellowship-Data-Extractor\2109da8e-31a6-4995-a9b0-5036194d1636\scratchpad"
$bundle  = "src/FellowshipExtractor.Api/obj/Debug/net10.0/scopedcss/bundle/FellowshipExtractor.Api.styles.css"

function Declarations($text) {
    [regex]::Matches(($text -replace '(?s)/\*.*?\*/', ''), '\{([^{}]*)\}') |
        ForEach-Object { $_.Groups[1].Value -split ';' } |
        ForEach-Object { ($_ -replace '\s+', ' ').Trim() } |
        Where-Object { $_ -ne '' } |
        Sort-Object -Unique
}

$before = Declarations (Get-Content (Join-Path $scratch "codex.css.before") -Raw)
$after  = Declarations ((Get-Content src/FellowshipExtractor.Api/wwwroot/css/codex.css -Raw) +
                        (Get-Content $bundle -Raw))

Compare-Object $before $after | Where-Object SideIndicator -eq '<='
```

Expected: no output. Every declaration in the stylesheet this refactor started from appears somewhere in the built output, which is the global sheet plus the scoped bundle together. A reported line is a declaration that was dropped: find it in `codex.css.before`, read which rule it belongs to, and put that rule in the `.scss` the census names.

Two kinds of reported line are legitimate and are read rather than fixed, and each is accounted for by name:

- A declaration whose value dart-sass reformats, such as a colour it lower-cases or a shorthand it re-spaces. Confirm the same declaration is in `$after` under a different spelling before dismissing it.
- A declaration Task 1 Step 9 recorded as deleted by the concurrent session, which is in `codex.css.before` because the copy was taken before that reconciliation was applied.

Then read the census table Task 3 Step 10 wrote, which is the record of *which* file owns each rule. For each row, confirm the named `.razor.scss` exists and contains the rule in its rewritten form. The script above proves nothing was dropped; the table is what says each rule has exactly one home.

```powershell
Get-ChildItem src/FellowshipExtractor.Api -Recurse -Filter *.css | Select-String -Pattern 'code\[b-'
```

Expected: no matches, in any generated stylesheet in the project rather than only in the three `IsolationTests` reads.

- [ ] **Step 13: Criterion 10, confirm the baseline exists and predates the refactor**

```powershell
$baseline = "C:\Users\Sean\AppData\Local\Temp\claude\G--source-Fellowship-Data-Extractor\2109da8e-31a6-4995-a9b0-5036194d1636\scratchpad\baseline"
Get-ChildItem $baseline | Select-Object Name, Length, LastWriteTime
```

Expected: the eight files from Task 1. If they are missing, the baseline cannot be recreated after the refactor and criterion 11 is answered by the browser comparison in Steps 17 and 20 alone.

- [ ] **Step 14: Criteria 13 and 14, run the app**

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

- [ ] **Step 15: Criterion 18, the scoped bundle is asked for and answered**

```powershell
$page = (Invoke-WebRequest -Uri http://localhost:5280/ -UseBasicParsing).Content

Select-String -InputObject $page -Pattern 'css/codex\.css'
Select-String -InputObject $page -Pattern 'FellowshipExtractor\.Api\.styles\.css'

$bundle = Invoke-WebRequest -Uri http://localhost:5280/FellowshipExtractor.Api.styles.css -UseBasicParsing
$bundle.StatusCode
$bundle.Content.Length

$global = Invoke-WebRequest -Uri http://localhost:5280/css/codex.css -UseBasicParsing
$global.StatusCode
$global.Content.Length
```

Expected: both links present in the page, both requests `200`, and both bodies non-empty. Then open `http://localhost:5280/` in the browser and confirm in the network panel that the page itself requests both, rather than inferring it from the two `Invoke-WebRequest` calls: a link the server can answer but the page never asks for leaves every component unstyled with the build still green.

- [ ] **Step 16: Criterion 12, capture the same eight pages and diff them**

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
```

The scope attribute is on almost every element now, so it is stripped from the capture before the comparison with the same pattern `Scoped.Bare` uses. That is accepted difference (e), removed rather than read eight times:

```powershell
foreach ($name in $pages.Keys) {
    "=== $name ==="

    $bare = (Get-Content (Join-Path $after $name)) -replace '\sb-[a-z0-9]{10}(?=[\s>])', ''

    Compare-Object (Get-Content (Join-Path $baseline $name)) $bare
}
```

Expected: every reported difference is one of the accepted differences (a) to (d) and (f) above. Read each one and say which of the five it is. Anything else is a regression to fix before this task completes.

- [ ] **Step 17: Criterion 11, compare the three entity pages in a browser**

Open each of `$knobs`, `$legendary` and `$ranks` at `http://localhost:5280` and confirm against the notes taken in Task 1 Step 13:

- the hero item shows the hero select, the rarity rungs, the item level and the tempered inputs; clicking a rung repaints the tooltip stage; changing a number repaints it;
- the legendary item's granted ability and each legendary roll draw their own text, facts and stats and not the item's;
- the trait or ability's Printed row shows its values separated by ` / ` on one line, with no blank lines, and with its coloured rich-text runs styled.

Confirm the frame each panel sits in as well, because these three pages are where a scoped rule that failed to match shows first: each panel has its gold-bordered plate and a coloured, uppercased head bar; the panels in the side column sit in a grid with no doubled gap; the tooltip stage keeps its 340px minimum width; a rule between detail lines draws as the textured bar rather than a hairline.

- [ ] **Step 18: Criterion 19, rich text is still coloured**

This is the regression the scope attribute predicts, it is invisible to the compiler and to every bUnit assertion, and grey text is the only symptom. On the entity whose description carries coloured runs, confirm against the notes from Task 1 Step 13:

- each `rt-*` span draws in its token colour, not in the body ink;
- a `.sub` run keeps its dotted underline and its help cursor;
- a `.tok` run keeps the mono face and the negative colour;
- an `.rt-unstyled` run takes the surrounding ink.

Then confirm the same on the rank-progression Printed row, which draws its runs inside a `<span class="cx-v cx-prose">` rather than a `<p>`. Both elements wear `cx-prose` and the four rules that style what is inside them are global, in `Styles/codex.scss`, precisely so that both are reached.

The three `::deep` rules are read the same way. Find a `<code>` in a note, in a fact cell and in a key/value row, and confirm each takes the mono face and the pale ink rather than the body font. `IsolationTests` guards their generated form; this is what confirms the browser agrees.

- [ ] **Step 19: Criterion 20, links still behave**

Hover a browse type tab, a facet value, a pill, a crumb, a results-table column header and a pager link. Confirm against the notes from Task 1 Step 13: none of them underlines, and each takes its own hover colour.

Those six rules exist only to beat the global `a:hover { text-decoration: underline }`, and they now sit in a different stylesheet from it. They win on specificity, and the scope attribute adds a class-unit to each of them, so each outranks the global element rule by one more unit than it did in the flat sheet. An underline appearing on any of the six means its rule is not matching at all.

- [ ] **Step 20: Criterion 11, compare the remaining pages in a browser**

Open and confirm:

- `/browse` ungrouped and `/browse?group=slot` grouped, with the group-head rows, the chips and the facet rail intact. The chips are the sharpest of these: `table.cx-rows .cx-chip` is the only rule that gives a chip its border, padding and size, so an unstyled chip is a chip drawn outside the results table or a `Chip.razor.scss` that failed to compile;
- `/`, with the roster, each menu group and the showcase. Both kinds of card wear the same plated frame, which is what says the global `.cx-card` is reaching `Home`'s own anchor and the `EntityLink` alike;
- `/not-found`, and `/entity/items/999999`;
- the masthead, the mega menu opening to full page width, the omnibox, the crumbs and the footer, on any page. Those are `MainLayout`, `MegaMenu`, `Omnibox` and `Crumbs`, whose stylesheets are the last written and whose markup never changed, so a fault in them is a fault in the move to Sass alone.

- [ ] **Step 21: Stop the background shell running `aspire run`**

Terminate the tracked background shell. Confirm with:

```powershell
Test-NetConnection -ComputerName localhost -Port 5280 -InformationLevel Quiet
```

Expected: `False`.

- [ ] **Step 22: Confirm the working tree carries only this plan's changes**

```powershell
git status --porcelain
git status --porcelain --ignored -- '*.css'
```

Expected: the first shows nothing from this plan; any other session's uncommitted files are still there and are left exactly as they are. The second shows every generated stylesheet with the `!!` ignored marker and none with `??`, which is what says `.gitignore` is doing its job rather than the files merely not existing yet.
