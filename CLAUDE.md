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
- The command is `dotnet run --project src/FellowshipExtractor -- export-all`. It takes about seven
  minutes and rewrites `output/export/entities.jsonl` and `output/export/vocabulary.json`.
- Run it whenever the documents change, so the checked-out export always matches the document
  classes that read it back.
