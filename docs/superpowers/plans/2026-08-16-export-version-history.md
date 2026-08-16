# Export Version History Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `Fellowship.Codex` loads every export version it can see, keyed by build, builds the site from the newest, and renders what changed between adjacent versions for any JSON-shaped value.

**Architecture:** `IExportSource.LoadAsync` returns an `ExportHistory` instead of an `ExportSnapshot`: the newest build deserialized as today, plus every older build held as raw JSONL lines grouped by `$type` slug and id. A singleton `IExportHistoryService` answers `Of(document)` with one `JsonNode?` per version, and `Cx/Changes.razor` renders adjacent pairs of those nodes as leaf-level differences. `BlobTarget` moves its two data blobs under an `export-<build>/` prefix so a container can hold several builds the way `output/` already does.

**Tech Stack:** .NET 10, Blazor Server with scoped CSS compiled from `.razor.scss` by AspNetCore.SassCompiler, `System.Text.Json` including `JsonNode.DeepEquals`, Azure.Storage.Blobs, xUnit v3 with bunit.

**Spec:** `docs/superpowers/specs/2026-08-16-export-version-history-design.md`

## Global Constraints

- Never add a comment of any syntax to any file. Never use an em dash or an en dash in any output.
- Never run `git stash` in any form.
- Derive names and paths from their canonical source: `ExportFolders.Prefix` / `ExportFolders.Name` for the folder and blob prefix, `ExportReader.Data` / `ExportReader.EntitiesName` / `ExportReader.SettingsName` for the two data files, `EntityDocuments.Discriminator` for `$type`, and `EntityDocuments.Options.PropertyNamingPolicy` for a JSON property name.
- `ExportReader.Blob(string name)` and `ExportReader.Missing` keep their exact current text. `SnapshotTests` asserts on that message. Add an overload, do not edit the existing one.
- The version colours are `semantic.negative` and `semantic.positive`, which `DesignTokenService.CssVariables()` writes as `--semantic-negative` and `--semantic-positive`. `semantic.change` exists in the same table and is not what the spec names.
- A `.razor.scss` selector that reaches a `code` element without `::deep` fails `IsolationTests`. The Changes markup uses no `code` element.
- No re-export is needed. The on-disk folder layout does not change and no document changes, so `dotnet run --project src/FellowshipExtractor` is not a prerequisite for any task here.
- Every scoped rule for the Changes markup lives in `Changes.razor.scss`. The `.razor.css` beside it is generated and gitignored.

---

## Task 1: History shapes and the folder source

**Files:**
- Modify: `src/Fellowship.SDK/Models/ExportFolders.cs`
- Modify: `src/Fellowship.Codex/Services/ExportSource.cs`
- Modify: `src/Fellowship.Codex/Services/FolderExportSource.cs`
- Modify: `src/Fellowship.Codex/Services/BlobExportSource.cs`
- Modify: `src/Fellowship.Codex/Services/CodexServices.cs`
- Modify: `src/Fellowship.Codex.Tests/Export.cs`, `SnapshotTests.cs`, `RouteTests.cs`
- Test: `src/Fellowship.Codex.Tests/HistoryTests.cs`

**Interfaces:**
- Produces: `ExportFolders.Id(string folder)`; `DocumentKey(string Slug, int Id)`; `ExportVersion(string Build, IReadOnlyDictionary<DocumentKey, IReadOnlyList<string>> Documents, string Settings)`; `ExportHistory(string Latest, ExportSnapshot Snapshot, IReadOnlyDictionary<string, ExportVersion> Older)`; `ExportHistoryHolder.History`; `ExportReader.Blob(string build, string name)`; `ExportReader.Group(IEnumerable<string> lines)`; `Task<ExportHistory> IExportSource.LoadAsync(CancellationToken)`.

The signature change is atomic: `IExportSource`, both sources, the holder rename, `CodexServices`, and the three test call sites move in one commit or the tree does not compile. The gate is the existing suite still passing, plus the new history tests.

- [ ] **Step 1: Write the failing tests** in `HistoryTests.cs`: two synthetic `export-<build>` folders under one temp `output/`, loaded through `new FolderExportSource(higher)`. Assert `Latest` is the higher build, `Snapshot.Entities` is the higher build's documents, `Older` holds exactly the lower build, and the lower build's `Documents` carry the raw line under `DocumentKey("item", 5224)`. Add a case where the folder name does not parse as `export-<build>`: `Older` is empty.
- [ ] **Step 2: Run and watch them fail to compile** (`LoadAsync` still returns `ExportSnapshot`).
- [ ] **Step 3: Implement.** `ExportFolders.Id` strips `Prefix` when present and otherwise returns the name. `ExportReader.Group` parses each line as a `JsonDocument` far enough to read `$type` and `id`, skipping a line that carries neither. `FolderExportSource` reads its own folder as `Latest` and every `export-*` sibling in the parent whose `ExportFolders.Build` is `>= 0` and lower than its own, and which `Carries` both files. A folder whose own name does not parse has build `-1`, so no sibling is lower and `Older` is empty; do not invent a fallback that pulls higher builds in.
- [ ] **Step 4: Run the new tests and the full Codex suite.**
- [ ] **Step 5: Commit.**

---

## Task 2: The blob source and the blob target

**Files:**
- Modify: `src/Fellowship.Codex/Services/BlobExportSource.cs`
- Modify: `src/FellowshipExtractor/Output/ExportLayout.cs`, `BlobTarget.cs`, `src/FellowshipExtractor/Program.cs`
- Modify: `src/FellowshipExtractor.Tests/OutputFormatTests.cs`
- Test: `src/Fellowship.Codex.Tests/BlobHistoryTests.cs`

**Interfaces:**
- Consumes: `ExportReader.Blob(build, name)` from Task 1.
- Produces: `ExportLayout.Data(string build, string file)`; `BlobTarget(string connectionString, string build)`.

- [ ] **Step 1: Write the failing tests.** `ExportLayout.Data("19240315", ExportLayout.EntitiesFile)` is `export-19240315/data/entities.jsonl`. For the source, stand a `BlobContainerClient` on `new BlobClientOptions { Transport = new HttpClientTransport(new HttpClient(stub)) }` whose stub answers the ListBlobs GET with two `export-<build>/data/...` pairs and each blob GET with its body; assert both builds load and `Latest` is the higher.
- [ ] **Step 2: Run and watch them fail.**
- [ ] **Step 3: Implement.** `BlobExportSource` lists under `ExportFolders.Prefix`, groups by first path segment, keeps groups carrying both `ExportReader.Blob(name)` paths, orders by `ExportFolders.Build` descending then name descending ordinal, and reads the newest as the snapshot and the rest as raw versions. When no group carries both but one carries entities, throw `ExportReader.Missing(SettingsName)`; otherwise `Missing(EntitiesName)`. `BlobTarget` takes the build and publishes through `ExportLayout.Data`; images stay at `ui/<texture>.png`. `Program.cs` resolves `GameBuild.Read(options)` once, lazily, for whichever destinations need it.
- [ ] **Step 4: Run both suites.** If the ListBlobs XML fights the SDK deserializer after two attempts, fall back to a pure `BlobExportSource.Versions(IEnumerable<string> names)` seam tested with plain strings, and say plainly that the end-to-end container read is not covered.
- [ ] **Step 5: Commit.**

---

## Task 3: The history service

**Files:**
- Create: `src/Fellowship.Codex/Services/ExportHistoryService.cs`
- Modify: `src/Fellowship.Codex/Services/CodexServices.cs`
- Test: `src/Fellowship.Codex.Tests/HistoryServiceTests.cs`

**Interfaces:**
- Produces: `VersionValue(string Build, JsonNode? Value)`; `IExportHistoryService` with `IReadOnlyList<string> Builds` and `IReadOnlyList<VersionValue> Of(EntityDocument document)`.

- [ ] **Step 1: Write the failing tests.** `Builds` is newest first. `Of` returns the live document serialized with `EntityDocuments.Options` as its newest entry. A version holding several lines under one key returns the line whose `armorType`, `primaryStat`, `hero` and `heroes` equal the viewed document's; a version holding none returns `null`; a version holding one returns that one.
- [ ] **Step 2: Run and watch them fail.**
- [ ] **Step 3: Implement.** Compare the four variant fields with `JsonNode.DeepEquals`, naming them through `EntityDocuments.Options.PropertyNamingPolicy`. Register as a singleton built from `ExportHistoryHolder` in `AddCodexServices`.
- [ ] **Step 4: Run the suite.**
- [ ] **Step 5: Commit.**

---

## Task 4: The leaf walk and the component

**Files:**
- Create: `src/Fellowship.Codex/Models/Changed.cs`
- Create: `src/Fellowship.Codex/Components/Cx/Changes.razor`, `Changes.razor.scss`
- Test: `src/Fellowship.Codex.Tests/Cx/ChangesTests.cs`

**Interfaces:**
- Consumes: `VersionValue` from Task 3.
- Produces: `ChangedLeaf(string Path, string? Older, string? Newer)`; `Changed.Leaves(JsonNode?)`; `Changed.Between(JsonNode? older, JsonNode? newer)`; `Changes` with `[Parameter, EditorRequired] IReadOnlyList<VersionValue> ByVersion`.

- [ ] **Step 1: Write the failing tests** over hand-built `JsonNode` values: a changed scalar, a changed string, a changed array element (`stats[1].max`), an added property, a removed property, a nested object, two equal values. Then the component: a single version heads its build and marks it `current`; an adjacent pair heads the older build; newer present and older absent reads `added in <newer build>`; the reverse reads `removed in <newer build>`; equal values read `no change`.
- [ ] **Step 2: Run and watch them fail.**
- [ ] **Step 3: Implement.** Paths join object keys with `.` and write array positions as `[n]`. A string leaf renders without quotes, everything else as its JSON literal. The older value takes `var(--semantic-negative)` and the newer `var(--semantic-positive)`, both set in `Changes.razor.scss`.
- [ ] **Step 4: Run the suite.**
- [ ] **Step 5: Commit.**

---

## Task 5: Where it appears

**Files:**
- Modify: `src/Fellowship.Codex/Components/Views/EntityView.razor`
- Modify: `src/Fellowship.Codex.Tests/Export.cs`, `DetailTests.cs`, `Views/CreatureViewTests.cs`
- Modify: `README.md`, `CLAUDE.md`

**Interfaces:**
- Consumes: `IExportHistoryService` from Task 3 and `Changes` from Task 4.

- [ ] **Step 1: Write the failing test.** Rendering `Detail` puts a `Changes`-headed `cx-panel` last among the roots.
- [ ] **Step 2: Run and watch it fail.**
- [ ] **Step 3: Implement.** `EntityView` injects `IExportHistoryService`, computes `History.Of(Entity)` in `OnParametersSet`, and renders `<Panel Head="Changes">` after `@ChildContent`. Every one of the seven views wraps `EntityView`, so this is the single insertion point. The `Export` test fixture gains a `History` the two `Detail`-rendering test classes register. `DetailTests.The_panels_are_siblings_with_no_wrapper_around_them` now expects three roots.
- [ ] **Step 4: Run both suites.**
- [ ] **Step 5: Commit.** Update the README container layout block and the CLAUDE.md paragraph on the export folder to state the `export-<build>/` blob prefix.
