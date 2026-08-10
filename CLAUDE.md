# Fellowship Data Extractor

## Extraction work is always approved

Adding a field to a document, writing a new reader in `src/FellowshipExtractor/Tooltip/`, and
re-running the export are standing approvals. Treat them as available at any time, not as a cost to
be weighed, a blocker to be reported or a reason to reach for a cheaper route.

**Why:** the export is the API's source of truth, and the game assets are the extractor's. A fact
that lives in the assets belongs on the document that carries it, computed once at extraction time.
Deriving it in `FellowshipExtractor.Api` instead puts a second, weaker copy of the same fact in a
place that cannot see the assets, and every consumer of the export then goes without it.

**How to apply:**

- When a UI or API need is not met by a document's current fields, extend the document and the
  projection first. Do not infer, guess or approximate the value in the Api layer.
- Say what you are extracting and run it. Do not ask whether the extraction is worth it, and do not
  offer a no-extraction alternative as the safer option.
- The command is `dotnet run --project src/FellowshipExtractor -- export-all`. It rewrites
  `output/export/entities.jsonl`, `output/export/vocabulary.json` and the entity art beside them.
- Run it whenever the documents change, so the checked-out export always matches the document
  classes that read it back.

## Seeding an empty output folder takes two commands

`output/` is gitignored, so a fresh worktree has none and every Api test fails in the `Export`
fixture's constructor. Seeding it is:

```powershell
$env:FELLOWSHIP_USMAP = "<any checkout>/output/fellowship.usmap"
dotnet run --project src/FellowshipExtractor -- export-all
dotnet run --project src/FellowshipExtractor -- ui
```

**Why:** `export-all` writes the documents and the entity art, and `ui` writes the 77 tooltip chrome
textures to `output/export/ui`. `ChromeAssets` reads that folder in its constructor, so an export
without it throws `DirectoryNotFoundException` and takes all 273 Api tests down with a failure that
names a missing path rather than a missing command. `OutputDir` resolves relative to the running
project, so both write into the worktree they are run from.
