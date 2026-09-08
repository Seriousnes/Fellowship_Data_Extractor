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
  `output/export-<build>/entities.jsonl`, `output/export-<build>/settings.json`, the composed art in
  `output/ui`, and `src/Fellowship.SDK/settings.json`, which the source generator turns into the
  enums the documents are read against.
- Run it whenever the documents change, so the checked-out export always matches the document
  classes that read it back.

## The export folder is named for the build

`<build>` is the Steam build id, read from the `appmanifest_*.acf` whose `installdir` matches the
game install. `FELLOWSHIP_BUILD` overrides it for a copy Steam did not install. Everything that
reads an export back out of `output/` globs `output/export-*` and takes the highest build carrying
both `entities.jsonl` and `settings.json`, so several builds can sit side by side and the newest wins.
`FELLOWSHIP_EXPORT`, and `--export` on the MCP server, name one folder outright and are the only way
to read a build other than the newest.

`output/` is the folder store, and it files its pair flat: `export-<build>/entities.jsonl` and
`export-<build>/settings.json` per build. An object store — the deployed S3 bucket, the app host's
Azurite — files the same pair one segment deeper, under `export-<build>/data/`, and keeps that shape.
The art is at `ui/<texture>.png` at the root of either, one set shared by every build, and a run
overwrites the art it redraws under the name it redraws it as. `FolderExportSource` reads the folder
and `StorageExportSource` reads an object store; the codex picks by configuration, taking the folder
when neither `ConnectionStrings:icons` nor `S3:Bucket` names a store. Either way it loads the newest
build as its snapshot and every build beneath it as raw JSONL lines grouped by `$type` slug and id,
so an entity page can state what changed between adjacent versions.

The whole of `output/` is tracked, so a fresh clone has the documents and the art and every test
reads them straight from the checkout. `mappings/fellowship-<build>.usmap`, which a `--usmap` run
writes, is tracked in full too, so a fresh clone reads the containers with no regeneration and the
mappings stay pinned to the export filed under the same id. `ResolvedMappingsFile` picks the highest
build present the same way, and `FELLOWSHIP_USMAP` names one outright.

## Refreshing the output folder takes one command

```powershell
dotnet run --project src/FellowshipExtractor
```

**Why:** with no destination named the run writes `output/`, and it writes both halves in the one
pass: the documents under `export-<build>/`, and every composed image under `ui/`. It needs no
variables set, because the committed `mappings/` carries the usmap. It does need the Steam install,
to read the paks and the appmanifest that names the build, and the first run against a clean `bin/`
downloads Oodle. A connection string in `ConnectionStrings__icons` replaces that folder with the blob
container, which is what the Publisher app host does.
