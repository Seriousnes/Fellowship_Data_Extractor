# Fellowship Data Extractor

## Extraction work is always approved

Adding a field to a document, writing a new reader in `src/FellowshipExtractor/Extraction/`, and
re-running the export are standing approvals. They are available at any time, not a cost to be
weighed, a blocker to be reported or a reason to reach for a cheaper route.

**Why:** the export is the API's source of truth, and the game assets are the extractor's. A fact in
the assets belongs on the document for it, computed once at extraction time. Deriving it in
`Fellowship.Codex` instead puts a second, weaker copy in a place with no access to the assets, and
every consumer of the export then has neither.

**How to apply:**

- When a UI or API need is not met by a document's current fields, extend the document and the
  projection first. Do not infer, guess or approximate the value in the Api layer.
- Say what you are extracting and run it. Do not ask whether to extract, and do not offer a
  no-extraction alternative as the safer option.
- Run it whenever the documents change, so the checked-out export matches the document classes that
  read it back.

## One command refreshes the output folder

```powershell
dotnet run --project src/FellowshipExtractor
```

With no destination named the run writes `output/` in one pass: the documents under
`export-<build>/`, every composed texture under `ui/`, and `src/Fellowship.SDK/settings.json`, the
source generator's input for the enums the documents are read against. It needs no variables set,
because the committed `mappings/` has the usmap. It does need the Steam install, for the paks and the
appmanifest naming the build. The first run against a clean `bin/` downloads Oodle.

## The export folder is named for the build

`<build>` is the Steam build id from the `appmanifest_*.acf` whose `installdir` matches the game
install. `FELLOWSHIP_BUILD` overrides it for a copy Steam did not install. Everything reading an
export out of `output/` globs `output/export-*` and takes the highest build with both
`entities.jsonl` and `settings.json`. Several builds are present at once and the highest is read.
`FELLOWSHIP_EXPORT`, and `--export` on the MCP server, name one export folder outright, or an
`output/` folder whose highest build is read.

## The user-scope MCP server reads the main checkout

The user-scope `fellowship-codex` server runs `G:/mcp/Fellowship/Fellowship.Codex.Mcp.exe --export
G:/source/Fellowship_Data_Extractor/output`. It reads the highest build in the main checkout's
`output/`, and every 15 seconds checks that folder again. A new highest build, or a rewritten
`entities.jsonl` or `settings.json`, loads once two checks in a row agree, and a load that fails
leaves the previous export in place. An `--export` or `FELLOWSHIP_EXPORT` that names one export
folder outright is never swapped. `deploy/mcp/publish.ps1` republishes the binary to `G:/mcp/Fellowship`, and
renames each file a running server holds open to `*.replaced`, so a publish succeeds while sessions
run. The next publish deletes the `*.replaced` files no server still holds.

Textures are at `ui/<texture>_<size>.webp`, one set for every build. A size is `small`, `medium`,
`large` or `full`. `full` is the native resolution and exists for every texture. Each square size
exists where the native image is at least that many pixels on its shorter edge. A document's texture
field has the name alone, so `TextureSizes.Key` composes the file from a name and a size.

The whole of `output/` is tracked, so a fresh clone has the documents and the textures, and every
test reads them from the checkout. `mappings/fellowship-<build>.usmap` from a `--usmap` run is
tracked too, so a fresh clone reads the containers with no regeneration.
`ResolvedMappingsFile` picks the highest build present, and `FELLOWSHIP_USMAP` names one outright.
