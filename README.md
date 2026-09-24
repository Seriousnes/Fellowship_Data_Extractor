# Fellowship Data Extractor

Fellowship Data Extractor exports the game data of Fellowship, a game by Chief Rebel AB. It serves
the export as a website, an HTTP API, an MCP server and a NuGet package.

## Projects

- **`src/FellowshipExtractor`**: the console app. It reads the game install and writes the export.
  For its commands and options, see
  [Fellowship extractor](src/FellowshipExtractor/README.md).
- **`src/Fellowship.Codex`**: the website and HTTP API. It serves an export as pages, search and
  tooltips. It is deployed at codex.fellowshipanalyzer.com.
- **`src/Fellowship.Codex.Mcp`**: an MCP server. It answers an agent from the same export over
  standard input and output.
- **`src/Fellowship.SDK`**: the published package. It contains the data models and an HTTP API
  client for the website, with structured request and response models. For installation, see
  [Codex.FellowshipAnalyzer.SDK](src/Fellowship.SDK/README.md).

## Prerequisites

- The .NET 10 SDK.
- Fellowship installed through Steam, by default at
  `C:\Program Files (x86)\Steam\steamapps\common\Fellowship`. The extractor opens the game data as
  read-only.
- Docker.

## Write the export

Run the extractor. It writes the export to `output/`:

```powershell
dotnet run --project src/FellowshipExtractor
```

## Run the codex

Run the publisher. It writes the export to the local storage emulator:

```powershell
dotnet run --project src/FellowshipExtractor.Publisher
```

Run the app host. It serves the codex from the export that the publisher wrote:

```powershell
dotnet run --project src/FellowshipExtractor.AppHost
```

## Download the export

The codex serves the export of the build it reads. A machine without a clone of this private
repository can download the export from the codex.

`GET https://codex.fellowshipanalyzer.com/api/export` returns the Steam build id and the path of
each file:

```json
{ "build": "<steam build id>", "entities": "/api/export/entities.jsonl", "settings": "/api/export/settings.json" }
```

- `GET /api/export/entities.jsonl` returns the entities file. The file has one document on each
  line, with the media type `application/x-ndjson`.
- `GET /api/export/settings.json` returns the settings file. This file is the input of the SDK
  enums.

To download each file, run `curl -O`:

```shell
curl -O https://codex.fellowshipanalyzer.com/api/export/entities.jsonl
curl -O https://codex.fellowshipanalyzer.com/api/export/settings.json
```
