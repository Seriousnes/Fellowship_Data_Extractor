# Storage-backed app implementation plan

Implements `docs/superpowers/specs/2026-08-09-storage-backed-app-design.md`: the codex reads every
entity, setting and image from blob storage; the extractor is the only writer and writes a complete
target in one pass, into blob storage or into a folder shaped exactly like the container.

## Global constraints

These bind every task and every reviewer.

1. **Zero code comments.** Do not add a single comment to any file, in any language. Existing
   comments stay only where the task does not touch them.
2. **No em dashes or en dashes** in any output: code, strings, prose, commit messages, test names.
   Use hyphens, commas, or restructure.
3. **No polyfills** for missing C#/.NET runtime functionality anywhere, including the
   netstandard2.0 source generator project. Use the modern runtime feature directly.
4. **Exact spec vocabulary.** Type and member names come from the spec verbatim: `IExportTarget`,
   `FolderTarget`, `BlobTarget`, `WriteDocumentAsync`, `WriteSettingsAsync`, `WriteImageAsync`,
   `IExportSource`, `ExportSnapshot`, `BlobExportSource`, `FolderExportSource`, `SettingsFile`,
   `SettingsReader`, `SettingsEnumGenerator`, `SettingsEnums.g.cs`, `ChromeAssets.Prefix`,
   `ChromeAssets.SlotPlate`, `ItemDocument.IconByRarity`, `EntityStore.Settings`.
5. **Derive from the source of truth.** The rarity ladder comes from the settings, never a literal
   list. The ornament, colour-row and section-heading tables are read out of the cooked assets by
   the extractor. The spec's tables are the expected values tests assert, not data to embed in
   extractor code.
6. **Document JSON uses `$type`** as the discriminator (`EntityDocuments.Options`); nothing may
   collide with a document's own `Type` member.
7. **Skin render icons carry their own background.** Their alpha channel is not a transparency
   mask; it is the inverted mask the compose formula consumes. Never treat it as transparency.
8. **The icon compose runs in linear colour space**, exactly:
   `out = srgbEncode( lerp( srgbDecode(icon.rgb), srgbDecode(rarityColour), icon.a ) )`.
9. **Test frameworks differ**: `FellowshipExtractor.Tests` is xunit v2, `FellowshipExtractor.Api.Tests`
   is xunit v3 with bunit. Fixture code cannot be shared verbatim between them.
10. **Never run `git stash`** in any form. Work only inside your assigned worktree and only on the
    files your task owns.
11. **Builds stay clean**: `dotnet build src/FellowshipExtractor.slnx` succeeds with no new
    warnings after every task. Match the existing code style: file-scoped namespaces, records,
    collection expressions, primary constructors where the neighbouring code uses them.
12. Commit messages: short imperative subject in the repo's existing voice, then a blank line, then
    exactly these two trailer lines:
    `Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>`
    `Claude-Session: https://claude.ai/code/session_01FbGjsQpbKzhGhzc1mpUu9n`
13. Tests that need a freshly generated export are written and must compile in their task; they run
    in the integration task after the export is regenerated.

## Layout contract (fixed, all tasks build against this)

```
<root>/ui/<texture>.png        chrome textures, tinted variants, icon variants, entity icons
<root>/data/entities.jsonl
<root>/data/settings.json
```

`<root>` is the `icons` blob container or the folder a `FolderTarget` was given. Blob names use
forward slashes. Per-rarity names are `<name>-<rarity>.png` with the rarity lowercased, matching
the existing `ChromeAssetService.Name(texture, rarity)` convention.

## SettingsFile contract (fixed in Task 2, consumed by Tasks A2, B2)

`SettingsFile` gains three tables, serialized camelCase like every other member:

```csharp
public IReadOnlyDictionary<string, string> RarityOrnaments { get; init; }   // rarity name -> ornament texture name, only rarities that draw one
public IReadOnlyDictionary<string, string> RarityColorRows { get; init; }   // rarity name -> colour row key, all rarities
public IReadOnlyDictionary<string, string> SectionHeadingRows { get; init; } // "Ability" | "Legendary" | "SetBonus" -> colour row key
```

All three use `StringComparer.OrdinalIgnoreCase`. Values resolve to hex through the existing
`Colors` table. Expected contents (what tests assert, extraction must produce these from assets):

- RarityOrnaments: Epic -> `T_Backgrounds_TooltipItemHeader_TopDecor_Clean_Champion`,
  Champion -> `..._Paragon`, Heroic -> `..._Eternal`, Legendary -> `..._Legendary`.
  Common, Uncommon, Rare absent.
- RarityColorRows: Common `game.rarity-common`, Uncommon `game.rarity-uncommon`,
  Rare `game.rarity-rare`, Epic `game.rarity-epic`, Champion `game.game.rarity-champion`,
  Heroic `game.game.rarity-heroic`, Legendary `game.rarity-legendary`.
- SectionHeadingRows: Ability `game.game.hero-mosse` (#EA4F84),
  Legendary `game.rarity-legendary` (#FF8000), SetBonus `game.game.character-friendly` (#00BB1D).

## Task 1: Rename vocabulary to settings

**One commit, nothing but the rename.** The spec: "`vocabulary.json` becomes `settings.json`, in
the published data and in the copy `Fellowship.SDK.csproj` passes to the generator as an
`AdditionalFiles` item. `VocabularyFile` becomes `SettingsFile`, `EntityStore.Vocabulary` becomes
`EntityStore.Settings`, `VocabularyEnumGenerator` becomes `SettingsEnumGenerator` emitting
`SettingsEnums.g.cs`, `IOutputFormat.WriteVocabularyAsync` becomes `WriteSettingsAsync`, and the
extractor's `Vocabulary.cs` and `VocabularyReader.cs` become `Settings.cs` and `SettingsReader.cs`.
This rename lands first, as its own commit, so the storage change reads on its own."

Complete rename surface (enumerated from the current tree):

- **Fellowship.SDK**: `Models/VocabularyFile.cs` -> `Models/SettingsFile.cs`, record `VocabularyFile`
  -> `SettingsFile` (`StatCalculationFile` in the same file keeps its name); data file
  `vocabulary.json` -> `settings.json`; csproj `<AdditionalFiles Include="vocabulary.json" />` ->
  `settings.json`.
- **Fellowship.SDK.Generators**: `VocabularyEnumGenerator.cs` -> `SettingsEnumGenerator.cs`, class
  renamed; const `VocabularyName = "vocabulary.json"` -> `SettingsName = "settings.json"`;
  `HintName = "VocabularyEnums.g.cs"` -> `"SettingsEnums.g.cs"`; descriptors `NoVocabulary` /
  `UnreadableVocabulary` and their titles and message text move to settings wording;
  `AnalyzerReleases.Unshipped.md` titles must match the descriptors verbatim or release tracking
  fails the build; emitted header text names settings.json; method `IsVocabulary`, locals and
  parameters follow.
- **FellowshipExtractor**: `Tooltip/Vocabulary.cs` -> `Tooltip/Settings.cs` (class `Settings`,
  `FileName = "settings.json"`); `Tooltip/VocabularyReader.cs` -> `Tooltip/SettingsReader.cs`
  (class `SettingsReader`, returns `SettingsFile`); `TooltipResolver.Vocabulary` ->
  `TooltipResolver.Settings`; `IOutputFormat.WriteVocabularyAsync` -> `WriteSettingsAsync`
  (parameter type `Settings`); `JsonlOutputFormat` implementation; `ExportAllCommand` log text and
  calls; `TooltipCommand` log text; `EnumsCommand` parameters and `Settings.FileName` use;
  `Program.cs` help text. `Tooltip/ItemSlotNames.cs` uses `_vocabulary` for an unrelated local
  meaning and is NOT part of this rename.
- **FellowshipExtractor.Api**: `IEntityStoreService.Vocabulary` -> `.Settings` (and the property,
  parsing, error text in `EntityStoreService`); `ExportPathService.Vocabulary` -> `.Settings` with
  the `"settings.json"` literal; `DesignTokenService` ctor parameter; every `store.Vocabulary` /
  `Vocabulary` identifier in `TooltipViewService`, `TooltipService`, `PlaceholderService`,
  `CodexViewService`, `CodexService`, `ChromeAssetService`, `Models/PublicEntity.cs`,
  `Endpoints/Records.cs`, `Endpoints/Browsing/CatalogEndpoint.cs`, `Program.cs`,
  `Components/Pages/Home.razor`, `Components/Sections/Knobs.razor`.
- **FellowshipExtractor.Tests**: `Export.cs` (property, type, two `"vocabulary.json"` literals,
  error text), `StaleExportTests.cs` (two literals), `EnumDriftTests.cs` (including the test method
  name), `StatPoolTests.cs`, `StatParameterTests.cs`, `RollLineTests.cs`, `RarityTests.cs`,
  `AttributeTableTests.cs`.
- **FellowshipExtractor.Api.Tests**: `Export.cs`, `ColourTests.cs`, `TooltipTests.cs`,
  `BlessingCardTests.cs`, `AbilityScalingTests.cs`, `TooltipDumpTests.cs`, `Cx/IconTests.cs`,
  `TemperingTests.cs`, `StatParameterTests.cs`, `StateChromeTests.cs`,
  `PlaceholderReadingTests.cs`, `Sections/KnobsTests.cs`.
- **README.md**: the four references to `vocabulary.json`.

Done when: `dotnet build src/FellowshipExtractor.slnx` is clean; a case-insensitive grep for
`vocabul` over `src/` returns only `ItemSlotNames.cs`; committed as one commit.

## Task 2: SDK settings contract

Small serial commit. Add the three `SettingsFile` tables from the contract section above, with
empty-dictionary defaults so existing settings.json files still deserialize.
`ItemDocument.IconByRarity` already exists and is untouched here. Update the checked-in
`src/Fellowship.SDK/settings.json` by hand only if the generator requires the new keys (it reads
`heroes`, `gemTypes`, `rarities`, `statCalculation`; it should not). Done when the solution builds.

## Stream A (worktree `sba-extractor`): the extractor becomes the only writer

Owns: `src/FellowshipExtractor/**`, `src/FellowshipExtractor.Tests/**` except
`StaleExportTests.cs`. Does not touch README, SDK, Api, AppHost, slnx.

### Task A1: The write seam

Spec: "`IExportTarget` carries three writes:

```csharp
Task WriteDocumentAsync(EntityDocument document, CancellationToken cancellationToken);
Task WriteSettingsAsync(SettingsFile settings, CancellationToken cancellationToken);
Task WriteImageAsync(string name, byte[] png, CancellationToken cancellationToken);
```

Images always land under `ui/`, documents and settings always under `data/`. `FolderTarget` and
`BlobTarget` implement it, and `export-all` writes through whichever pair the arguments name.
`BlobTarget` creates the container with `PublicAccessType.Blob`, stamps every upload with an MD5
`ContentHash` and `CacheControl: no-cache`, and skips any blob whose hash already matches, so
republishing uploads only what changed. Publishing is additive. `TextureExporter.Export` gains a
path that returns PNG bytes, so a blob run never rounds a texture through a temporary file."

And the arguments: "`export-all` takes its destination from two arguments: `--connection-string
<value>` writes every document, setting and image straight into blob storage. Nothing touches the
file system. The value is also taken from the `ConnectionStrings:icons` configuration key when the
argument is absent, which is the key `WithReference` injects, so a run under the Publisher app host
needs no argument at all. `--output <path>` writes a folder laid out exactly like the container.
Both a connection string and a path writes both, in one pass over the build. Neither aborts, naming
both." And: "A name written twice with different bytes throws."

Build:
- `IExportTarget` (plus `IAsyncDisposable`) in `FellowshipExtractor/Output/`, replacing
  `IOutputFormat`. `FolderTarget` replaces `JsonlOutputFormat`: `data/entities.jsonl` written line
  by line exactly as today, `data/settings.json`, images under `ui/`. `BlobTarget` on
  `Azure.Storage.Blobs` (new package reference for the extractor): container `icons` from the
  connection string, create with `PublicAccessType.Blob`, list existing hashes once, MD5 +
  `no-cache` on upload, skip matching hashes. A composite target writes both when both destinations
  are named. Every target throws when one name is written twice with different bytes and stays
  silent when the bytes match.
- `WriteSettingsAsync` takes the SDK `SettingsFile`. The extractor's `Settings` class gains the
  conversion that produces a `SettingsFile` (one shape, no anonymous-object drift; the current
  anonymous camelCase write in `Settings.WriteAsync` becomes serializing the `SettingsFile`).
- `TextureExporter` gains the bytes-returning path (split the encode step from the file write); the
  existing file-writing overload remains for commands that still want files.
- `export-all` in `Program.cs`: parse `--connection-string` and `--output` (generalize the
  positional-table filter for value-taking flags), fall back to the `ConnectionStrings:icons`
  configuration key (environment variable `ConnectionStrings__icons`) for the connection string,
  abort naming both flags when neither destination is available. Help text updated.
- The icon writer routes through `WriteImageAsync` (bare names like `Firemage_Bolt.png`; the target
  adds `ui/`). Documents keep bare filenames in their `icon` fields.
- Chrome textures fold into `export-all`: move the `UiCommand` chrome list, family expansion and
  slot-glyph derivation into a component `export-all` invokes, writing through the target. The
  standalone `ui` command is removed from `Program.cs` and the help text.
- `FellowshipExtractor.Tests/OutputFormatTests.cs` retargets `FolderTarget` (writes land in
  `data/`), and `FellowshipExtractor.Tests/Export.cs` discovery updates mechanically: the walk
  gates on `data/entities.jsonl`, reads `data/entities.jsonl` and `data/settings.json`. Also update
  `EntityDocumentJsonTests.cs` paths and `IconArtTests.cs` paths (icons under `ui/`).

### Task A2: The settings tables read from the build

Spec: "`settings.json` carries everything the vocabulary carries, plus three tables read from the
build so that nothing is derived from pixels." The three tables and their expected contents are in
the SettingsFile contract section. Sources:

- **Ornaments**: "from the `K2Node_Select` in `UW_TooltipRarityFrame.SetRarityFrame`". Read the
  cooked widget blueprint's `SetRarityFrame` function bytecode (Kismet reading precedent:
  `Tooltip/StatCalculation.cs`, `Tooltip/DynamicTextFloats.cs`, `Tooltip/FunctionsCommand.cs`) and
  recover the rarity-to-ornament-texture mapping the select encodes.
- **Colour rows**: "from `BPL_DS_ColorsFunctions.GetNamedColorFromItemRarity`". Same bytecode
  approach against the function library.
- **Section headings**: "from the widget that draws it": the `"Ability: "` label in
  `UW_ItemEffectsBlock`, the heading in `UW_LegendaryEffectBlock`, `UsageTxt` in `UW_SetBonusEntry`.
  Widget-tree property reading precedent: `Tooltip/SlotIcons.cs`.

The tables land on the extractor `Settings` class and flow into the `SettingsFile` it produces.
Nothing is hardcoded: if an asset cannot be read the extraction throws, naming the asset. Add
focused xunit tests in `FellowshipExtractor.Tests` (new file, e.g. `SettingsTableTests.cs`)
asserting the three tables read from the fixture export match the expected contents in this plan.

### Task A3: Every image transform happens at export

Spec: "Every image transform happens here, before anything lands on disk or in storage: Four
textures are written once per rarity, multiplied by that rarity's colour the way the game's
`SetColorAndOpacity` multiplies them: `T_Borders_Frame_TooltipRarityFrame`,
`T_Backgrounds_TooltipHeader_Rarity`, `T_Borders_RarityFrame` and `T_Borders_RarityGlow`. Every
skin render icon is written once per rarity as `<icon>-<rarity>.png`, composed by the formula the
`armory_item_icon` material computes. `T_GoldDivider` is written as rows 24 to 27 of its 56 row
canvas, the gold band the header rule draws." And: "Chrome textures gain the seven the slot stack
needs: `T_Borders_RarityGlow`, `T_Borders_SlotHoverHighlight`, `T_Borders_Glows_ItemSquare`,
`T_Borders_ItemRomanNumerals_I` to `_VIII`, `T_Icons_Gems_EmptySlot`, `T_Borders_EmptySlot` (the
plate `ChromeAssets.SlotPlate` names)." And: "`export-all` performs this once per rarity per icon
and writes the results, so no consumer composes anything at render time. `ItemDocument.IconByRarity`
names the variant each rarity reads, and an icon outside the skin render population is written
once, unchanged."

Build, on top of A1 and A2:
- Add the slot-stack textures to the chrome list (`T_Borders_ItemRomanNumerals_I` through `_VIII`
  may be a family prefix if the assets name them that way; resolve from the provider, never guess).
- Per-rarity tints at export: for each of the four textures, for each rarity in
  `Settings.Rarities`, multiply the texture by the rarity colour exactly the way
  `ChromeAssetService.Tint` multiplies today (per-channel byte multiply, alpha untouched), written
  as `<texture>-<rarity>.png`. The untinted originals are still written. The rarity colour comes
  through the new colour-row table into the `Colors` table, the single source.
- The icon compose: for every icon in the skin render population (`resolver.Collections.Renders`),
  decode once, then per rarity apply the linear-space formula from the global constraints with the
  icon's own alpha as the lerp factor, output fully opaque, written as `<icon>-<rarity>.png`. The
  plain flattened copy is no longer written for renders; icons outside the render population are
  written once, unchanged, alpha preserved. The existing `discardAlpha` flattening for renders is
  replaced by the compose.
- `ItemDocument.IconByRarity` now names the composed variant for each rarity: where
  `DynamicVisuals` yields per-rarity source renders, each rarity maps to
  `<thatRaritySource>-<rarity>.png`; where a single render serves all rarities, each rarity maps to
  `<render>-<rarity>.png`. `ItemVariants.Spread` drops its two-distinct-images guard: any item
  whose art is in the render population carries the full mapping. `Icon` keeps naming the
  highest-rarity variant so single-image consumers still resolve.
- `T_GoldDivider` written as the 4-row band (rows 24 to 27 of the 56-row canvas; throw if the
  canvas is not 56 rows), under its plain name.
- Tests in `FellowshipExtractor.Tests` (new file, e.g. `ExportedChromeTests.cs`), per the spec:
  "that the gold rule written is rows 24 to 27 of a 56 row canvas, all four rows painted; that each
  of the four tinted textures is written once per rarity; and that the seven slot textures are
  present." Also update `IconArtTests.cs`: renders now assert per-rarity variants exist and are
  opaque under `ui/`.

## Stream B (worktree `sba-api`): the codex reads a snapshot

Owns: `src/FellowshipExtractor.Api/**`, `src/FellowshipExtractor.Api.Tests/**`,
`src/FellowshipExtractor.Tests/StaleExportTests.cs`, and may add new files (not existing ones) to
`FellowshipExtractor.Tests` for migrated pixel assertions. Does not touch
`src/FellowshipExtractor/**`, README, SDK, AppHost, slnx, or any other existing
`FellowshipExtractor.Tests` file.

### Task B1: The read side

Spec: "`IExportSource` returns one snapshot:

```csharp
Task<ExportSnapshot> LoadAsync(CancellationToken cancellationToken);
public sealed record ExportSnapshot(SettingsFile Settings, IReadOnlyList<EntityDocument> Entities);
```

`BlobExportSource` reads `data/settings.json` and `data/entities.jsonl` whole, once. Every
`*Document` type is a table and every JSONL line is a row, so a source backed by a document store
runs a query per table and unions the rows into the same list, returning the same snapshot.
`FolderExportSource` reads the same two files out of a folder. `EntityStore` takes an
`ExportSnapshot` and stays immutable. `Program.cs` resolves the source after `builder.Build()` and
awaits the load before `app.Run()`, seating the snapshot in a holder the `EntityStore` factory
reads, so the store and everything built on it construct once and never change. The load uses the
`BlobContainerClient` the Aspire client integration registers, so no connection string is parsed.
A container with no `data/settings.json` fails at startup, naming the Publisher app host and the
`export-all` command that fills it."

Build:
- `IExportSource`, `ExportSnapshot`, `BlobExportSource`, `FolderExportSource` in the Api project.
  The JSONL line parsing (with `$type` discrimination and error messages naming `entities.jsonl`,
  the line number, and `export-all`) moves out of `EntityStoreService` into a reader both sources
  share. The missing-settings failure message names `FellowshipExtractor.Publisher` and
  `export-all`.
- `EntityStoreService` constructs from an `ExportSnapshot` (indexing and settings wrapping stay).
  A snapshot holder is registered in DI; the `IEntityStoreService` factory reads it; `Program.cs`
  after `builder.Build()` resolves `IExportSource`, awaits `LoadAsync`, seats the snapshot, then
  `app.Run()`.
- `ExportPathService`, the `FELLOWSHIP_EXPORT` configuration read, and every local file path leave
  the Api project.
- `IconSeederService` is deleted; the app writes nothing to storage.
- `FellowshipExtractor.Tests/StaleExportTests.cs` retargets the shared reader through
  `FolderExportSource` (temp folder with `data/` files) and keeps asserting the message names
  `entities.jsonl`, the line, and `export-all`.
- `RouteTests` boots the host replacing `IExportSource` with a `FolderExportSource` over the
  discovered export.

### Task B2: ChromeAssets composes URLs and reads tables

Spec: "`ChromeAssets` composes URLs and reads the tables in the settings. The API references no
imaging package." And from the layout: "Entity icons and chrome textures share the `ui/` prefix, so
`ChromeAssets.Prefix` is the single place that knows where an image lives." And the slot:
"`T_Borders_EmptySlot`, the plate `ChromeAssets.SlotPlate` names." And the settings tables:
"`DesignTokens.Rarity()` reads this table" (the colour-row table).

Build:
- `ChromeAssetService` stops doing imaging entirely: `Payloads()`, `Tint`, `Band`,
  `MatchOrnaments`, `ReadBadgeColours` and the `PngEncoder` go. Ornaments come from
  `settings.RarityOrnaments`; badge and section heading colours come from
  `settings.SectionHeadingRows` resolved through `DesignTokens`; `SlotPlate` is a new member naming
  `T_Borders_EmptySlot`. Every URL composes through `Prefix`, including entity icon URLs
  (`TooltipViewService.Art` and any other `icons.Uri` composition route through ChromeAssets so
  `ui/` is applied in one place).
- `DesignTokenService.Rarity()` reads `settings.RarityColorRows` instead of guessing two key
  patterns.
- `SixLabors.ImageSharp` leaves `FellowshipExtractor.Api.csproj`.
- `FellowshipExtractor.Api.Tests/Export.cs` builds its fixture through `FolderExportSource` over
  the discovered folder (walk gates on `data/entities.jsonl`), constructs the snapshot store, and
  wires `ChromeAssetService` without paths. Per the spec tests: "that `ChromeAssets` answers the
  texture each rarity's ornament, colour row and section heading name; that every slot URL resolves
  under `ui/`; and that a snapshot missing its settings fails with a message naming `export-all`."
  New Api tests cover those three.
- `StateChromeTests` and `WindowChromeTests`: assertions about which texture a state names stay,
  rewritten against ChromeAssets URLs. Pixel-geometry assertions (window well sizes, opacity
  checks) move to a new file in `FellowshipExtractor.Tests` (which has ImageSharp transitively)
  reading the discovered export's `ui/` folder; name it `ChromePixelTests.cs`. The Api test project
  finishes with no imaging package reference.

## Stream C (worktree `sba-topology`): two app hosts, one Azurite

Owns: `src/FellowshipExtractor.AppHost/**`, new `src/FellowshipExtractor.Publisher/**`,
`src/FellowshipExtractor.slnx`, `README.md`, `aspire.config.json` if needed.

### Task C1: Topology

Spec: "Two Aspire app hosts share one Azurite instance. `FellowshipExtractor.AppHost` holds the
storage emulator and the codex. `FellowshipExtractor.Publisher` holds the same storage emulator and
`FellowshipExtractor` as a resource, with the container connection string injected by
`WithReference`. Running it publishes. Both reach the same Azurite because `Stack()` derives its
identity from the checkout root, the directory `Checkout()` already walks to for `.git`, rather
than from `AppContext.BaseDirectory`. One checkout gets one volume name and one port offset
whichever host starts. The emulator carries `WithLifetime(ContainerLifetime.Persistent)`, so the
container outlives either host and the other attaches to it."

Build:
- Extract the shared identity helpers (`Stack`, `Checkout`, `Branch`, `Slug`, `Rebind`, `Port`,
  `InComposeStack`) into a file in the AppHost project that the Publisher links as a compile item,
  so both hosts compute identical names, ports and volume from one implementation.
- `Stack()` hashes the checkout root directory (the directory whose `.git` the walk finds, falling
  back to the current behaviour only when no checkout exists) instead of `AppContext.BaseDirectory`.
  Worktrees remain distinct stacks because each worktree is its own checkout root.
- The Azurite emulator resource gains `WithLifetime(ContainerLifetime.Persistent)` in both hosts,
  same volume name, same blob port, same container naming through `InComposeStack`, so whichever
  host starts first creates the container and the other attaches.
- New `FellowshipExtractor.Publisher` project (Aspire AppHost sdk, same versions as the existing
  host): the same storage emulator + blob container resources, plus
  `AddProject<Projects.FellowshipExtractor>` with `WithReference(icons)` (injecting
  `ConnectionStrings__icons`) and args `export-all`, so running the Publisher publishes the build
  into the shared Azurite. The extractor project needs no code change for this; it already reads
  the configuration key.
- The codex resource in `FellowshipExtractor.AppHost` drops the `FELLOWSHIP_EXPORT` environment
  variable.
- `src/FellowshipExtractor.slnx` gains the Publisher project line.
- README: the export and run flow becomes usmap, then either `export-all --output output/export`
  for a folder or running the Publisher app host to publish into Azurite, then `aspire run` for the
  codex; the `ui` command and `FELLOWSHIP_EXPORT` disappear from the flow; the topology section
  describes the two hosts and the shared persistent Azurite.

## Task INT: integration (main worktree, after streams merge)

- `FellowshipExtractor.Tests/Export.cs` finishes as the spec states: "discovers a folder holding
  `data/entities.jsonl` and builds its fixture through `FolderExportSource`, so the suite runs with
  no Azurite and no imaging package" (the Api project reference makes `FolderExportSource`
  available; the fixture keeps exposing `Settings`, `Documents`, `Folder`).
- Regenerate the export in the worktree: `dotnet run --project src/FellowshipExtractor -- usmap`
  if needed, then `dotnet run --project src/FellowshipExtractor -- export-all --output
  output/export`.
- `dotnet test src/FellowshipExtractor.slnx` end to end; fix every failure. Integration failures
  between streams surface here.

## Task FINAL: whole-branch adversarial review

Full-branch diff review against the spec, most capable reviewer, one fix wave, one scoped
re-review.

## Task VERIFY: rendered screenshots

Spec: "A rendered screenshot of a tooltip and of a gear slot at each of the seven rarities,
compared against the in game character sheet. Markup assertions do not see compositing, and the
layer this change adds is a compositing layer." Run the Publisher against the shared Azurite, run
the codex app host, screenshot the tooltip and gear slot at each rarity in a real browser, compare
against the in-game screenshots.
