# Fellowship Data Extractor

## Extraction work is always approved

Adding a field to a document, writing a new reader in `src/FellowshipExtractor/Extraction/`, and
re-running the export are standing approvals. Treat them as available at any time, not as a cost to
be weighed, a blocker to be reported or a reason to reach for a cheaper route.

**Why:** the export is the API's source of truth, and the game assets are the extractor's. A fact
that lives in the assets belongs on the document that carries it, computed once at extraction time.
Deriving it in `Fellowship.Codex` instead puts a second, weaker copy of the same fact in a
place that cannot see the assets, and every consumer of the export then goes without it.

**How to apply:**

- When a UI or API need is not met by a document's current fields, extend the document and the
  projection first. Do not infer, guess or approximate the value in the Api layer.
- Say what you are extracting and run it. Do not ask whether the extraction is worth it, and do not
  offer a no-extraction alternative as the safer option.
- The command is `dotnet run --project src/FellowshipExtractor`, with no arguments. It rewrites
  `output/export-<build>/data/entities.jsonl`, `output/export-<build>/data/settings.json`, the
  composed art in `output/export-<build>/ui`, and `src/Fellowship.SDK/settings.json`, which the
  source generator turns into the enums the documents are read against.
- Run it whenever the documents change, so the checked-out export always matches the document
  classes that read it back.

## The export folder is named for the build

`<build>` is the Steam build id, read from the `appmanifest_*.acf` whose `installdir` matches the
game install. `FELLOWSHIP_BUILD` overrides it for a copy Steam did not install. Everything that
reads an export back globs `output/export-*` and takes the highest build carrying both
`data/entities.jsonl` and `data/settings.json`, so several builds can sit side by side and the
newest wins. `FELLOWSHIP_EXPORT`, and `--export` on the MCP server, name one folder outright and are
the only way to read a build other than the newest.

The container carries the same shape: `export-<build>/data/entities.jsonl` and
`export-<build>/data/settings.json`, with the images at `ui/<texture>.png` at the container root.
The codex loads the newest build as its snapshot and every build beneath it as raw JSONL lines
grouped by `$type` slug and id, so an entity page can state what changed between adjacent versions.

Only `data/` is tracked; `ui/` is gitignored. `mappings/fellowship-<build>.usmap`, which a `--usmap`
run writes, is tracked in full, so a fresh clone reads the containers with no regeneration and the
mappings stay pinned to the export filed under the same id. `ResolvedMappingsFile` picks the highest
build present the same way, and `FELLOWSHIP_USMAP` names one outright.

## Seeding an empty output folder takes one command

`output/` is gitignored except for the committed `data/`, so a fresh worktree has the documents but
no art, and any test that reads an image fails. Filling it is:

```powershell
dotnet run --project src/FellowshipExtractor
```

**Why:** with no destination named the run writes `output/export-<build>`, and it writes both halves
in the one pass: the documents under `data/`, and every composed image under `ui/`. It needs no
environment at all, because the committed `mappings/` carries the usmap. A connection
string in `ConnectionStrings__icons` replaces that folder with the blob container, which is what the
Publisher app host does. `RouteTests` calls `Export.Found()` directly rather than reading
`FELLOWSHIP_EXPORT`, so the tests that run through `WebApplicationFactory` read the worktree's own
newest `output/export-*` whatever that variable says. A worktree can borrow another checkout's
export by copying its `data/` folder into an `output/export-<build>/`, which is a few megabytes
against the whole export's few hundred.
