# Export version history

`Fellowship.Codex` loads every export version it can see, keyed by build. The site is built from the
newest one. A generic component renders, for any JSON-shaped value, what changed between one version
and the next.

## Version identity

A version is a Steam build id, the same string that names `output/export-<build>` on disk and the
`export-<build>/` prefix in the blob container. `ExportFolders` in `Fellowship.SDK` supplies the
prefix, the glob pattern and the numeric parse. Versions order by that number, descending, so the
first is the newest.

## Loading

`IExportSource.LoadAsync` returns an `ExportHistory`:

```csharp
public readonly record struct DocumentKey(string Slug, int Id);

public sealed record ExportVersion(
    string Build,
    IReadOnlyDictionary<DocumentKey, IReadOnlyList<string>> Documents,
    string Settings);

public sealed record ExportHistory(
    string Latest,
    ExportSnapshot Snapshot,
    IReadOnlyDictionary<string, ExportVersion> Older);
```

`Snapshot` is the `Latest` build, deserialized into `SettingsFile` and `EntityDocument`s exactly as it
is today. `Older` holds every build below it, each as the raw JSONL lines grouped by the `$type` slug
and `id` the line carries, plus the raw `settings.json` text. Lines are parsed when a page asks for
them, so a version costs about the size of its `entities.jsonl`.

`ExportSnapshot`, `EntityStoreService` and every service built from it keep their current shapes.
`ExportSnapshotHolder` becomes `ExportHistoryHolder`, carrying an `ExportHistory`, and
`CodexServices.AddCodexServices` passes `holder.History.Snapshot` where it passes `holder.Snapshot`.
`LoadExportAsync` assigns the history.

### Folder

`FolderExportSource` keeps taking one folder, which is the build the site shows and therefore
`Latest`. Its history is every `export-*` sibling in that folder's parent whose build is lower, so
`FELLOWSHIP_EXPORT` and the MCP server's `--export` keep pinning one build and gain the builds
beneath it. Given no override, discovery names the newest build and history is everything under it.

### Blob

`BlobTarget` writes `export-<build>/data/entities.jsonl` and `export-<build>/data/settings.json`.
Images stay at `ui/<texture>.png` at the container root, and `ChromeAssetService.Prefix` and
`BlobArtSource` are unchanged.

`BlobTarget` takes the build as a constructor argument, so `Program.cs` resolves
`GameBuild.Read(options)` for both destinations. The Publisher app host runs against the game install
and `FELLOWSHIP_BUILD` names the build where it does not.

`BlobExportSource` lists the container under the `export-` prefix, groups by first path segment, and
reads each group's two data blobs.

## History service

```csharp
public interface IExportHistoryService
{
    IReadOnlyList<string> Builds { get; }

    IReadOnlyList<VersionValue> Of(EntityDocument document);
}

public sealed record VersionValue(string Build, JsonNode? Value);
```

`Builds` is every version, newest first. `Of` returns one entry per version from `Latest` downward:

- The newest entry is the live document, serialized with `EntityDocuments.Options`.
- Each older entry is the matching line under `DocumentKey(slug, id)`, parsed to a `JsonNode`.
- A version carrying no line for that key returns `null`.

One id can carry several documents that differ only in armour type, primary stat and hero, which is
the key `ExportRun` dedups on. Where a version holds several lines under the key, the match is the
line whose `armorType`, `primaryStat`, `hero` and `heroes` equal the viewed document's. Where it
holds one, that line is the match.

The service is a singleton built from `ExportHistoryHolder`, registered in `AddCodexServices`.

## The component

`Components/Cx/Changes.razor`:

```razor
[Parameter, EditorRequired] public IReadOnlyList<VersionValue> ByVersion { get; set; } = [];
```

It walks the list in order and renders one block per adjacent pair. For the pair `(newer, older)` the
block is headed by `older.Build` and states what `newer` changed relative to it:

- Both present: every leaf whose value differs, as `path`, the older value, the newer value.
- Newer present and older absent: `added in <newer.Build>`.
- Newer absent and older present: `removed in <newer.Build>`.
- Both present and equal: `no change`.

The newest build is headed on its own and marked `current`.

Leaves come from a recursive walk of objects and arrays. A path joins object keys with `.` and writes
array positions as `[n]`, so a changed roll reads `stats[1].max`. Values render as text: strings
without their quotes, everything else as its JSON literal. An icon change therefore reads
`T_Wrists_A_Crit.png -> T_Wrists_A_Crit_02.png`, the same as any other string.

The older value takes the game's own `semantic.negative` and the newer value its `semantic.positive`,
which `DesignTokenService.CssVariables()` already writes into the page as `--semantic-negative` and
`--semantic-positive`. The colours are read from the shipped table rather than chosen.

Every rule styling this component's markup lives in `Changes.razor.scss`, because scoped CSS applies
only to the markup of the component that emits it.

The component reads `JsonNode` and nothing else, so settings or any other JSON-shaped value can feed
it.

## Where it appears

The entity detail views render it inside a `Cx.Panel` headed `Changes`, fed by
`History.Of(Entity)`, following the way `Detail.razor` dispatches a document to its view.

## Testing

- The leaf walk and the pair comparison, over hand-built `JsonNode` values: a changed scalar, a
  changed string, a changed array element, an added property, a removed property, a nested object,
  and two equal values.
- `Of` picking the right variant when a version holds several lines under one key, and returning
  `null` when it holds none.
- Two synthetic builds written into a temp `output/`, asserting both load, that `Latest` is the
  higher build, and that `Snapshot` is the higher build's documents.
- The blob source reading two `export-<build>/data` prefixes from one container.
- `RouteTests` and `McpTests` continue to run against a single build, which covers a history of one.
