# Fellowship Data Extractor

Exports the game data of Fellowship, by Chief Rebel AB, and serves that export as a website, an HTTP API, an MCP server and a NuGet package.

## Projects

- **`src/FellowshipExtractor`**: the console app that reads the game install and writes the export.
  For the commands and options it takes, see
  [Fellowship extractor](src/FellowshipExtractor/README.md).
- **`src/Fellowship.Codex`**: the website and HTTP API that serve an export as pages, search and
  tooltips, deployed at codex.fellowshipanalyzer.com.
- **`src/Fellowship.Codex.Mcp`**: an MCP server that answers an agent from the same export over
  standard input and output.
- **`src/Fellowship.SDK`**: the published package with data models and a HTTP API client with structured request and response models for the website. For installation, see
  [Codex.FellowshipAnalyzer.SDK](src/Fellowship.SDK/README.md).

## Prerequisites

- The .NET 10 SDK.
- Fellowship installed through Steam, by default at
  `C:\Program Files (x86)\Steam\steamapps\common\Fellowship`. The extractor accesses the data as read-only.
- Docker.

## Write the export

To write the export to `output/`, run the extractor:

```powershell
dotnet run --project src/FellowshipExtractor
```

## Run the codex

To fill the local storage emulator with the export, run the publisher:

```powershell
dotnet run --project src/FellowshipExtractor.Publisher
```

To serve the codex against what the publisher wrote, run the app host:

```powershell
dotnet run --project src/FellowshipExtractor.AppHost
```