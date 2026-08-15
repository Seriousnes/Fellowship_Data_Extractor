# Fellowship Data Extractor

## Extraction work is always approved

Adding a field to a document, writing a new reader in `src/FellowshipExtractor/Tooltip/`, and
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
  `output/export/data/entities.jsonl`, `output/export/data/settings.json`, the composed art in
  `output/export/ui`, `output/tags.jsonl`, `output/ability-constants.jsonl` and the enums.
- Run it whenever the documents change, so the checked-out export always matches the document
  classes that read it back.

## Seeding an empty output folder takes one command

`output/` is gitignored, so a fresh worktree has none and every Api test fails in the `Export`
fixture's constructor. Seeding it is:

```powershell
$env:FELLOWSHIP_USMAP = "<any checkout>/output/fellowship.usmap"
dotnet run --project src/FellowshipExtractor
```

**Why:** with no destination named the run writes `output/export`, and it writes both halves in the
one pass: the documents under `data/`, and every composed image under `ui/`. A connection string in
`ConnectionStrings__icons` replaces that folder with the blob container, which is what the Publisher
app host does. `RouteTests` calls `Export.Found()` directly rather than reading
`FELLOWSHIP_EXPORT`, so the tests that run through `WebApplicationFactory` read the worktree's own
`output/export` whatever that variable says. A worktree can borrow another checkout's export by
copying its `data/` folder, which is a few megabytes against the whole export's few hundred.
