# Fellowship Data Extractor

Exports the game data of Fellowship (Chief Rebel AB) and serves it as a website, HTTP API, MCP
server and [NuGet package](src/Fellowship.SDK/README.md). Unofficial fan project; see
[Licence](#licence).

## Requirements

- .NET 10 SDK
- Fellowship installed through Steam. It's found in your Steam libraries automatically. Otherwise
  set `FELLOWSHIP_INSTALL` to the install folder.
- Docker, only to run the codex locally

## Export the game data

```shell
dotnet run --project src/FellowshipExtractor
```

This writes `output/export-<steam build id>/` and `output/ui/`. For options, see
[src/FellowshipExtractor](src/FellowshipExtractor/README.md).

## Run the codex

```shell
dotnet run --project src/FellowshipExtractor.Publisher
dotnet run --project src/FellowshipExtractor.AppHost
```

## Use the MCP server

```shell
dotnet build src/Fellowship.Codex.Mcp -c Release
```

`.mcp.json` then serves the newest export in `output/` to Claude Code.

## Without the game

The checkout already has the latest export in `output/`. You can also download it from
`https://codex.fellowshipanalyzer.com/api/export/entities.jsonl` and `.../settings.json`.

## Licence

The source code is under the [MIT licence](LICENSE). Fellowship game content belongs to Chief Rebel
AB and is not covered by that licence. This includes names, artwork, textures, text and game data,
and the extracted files in `output/` and `mappings/`. For details, see [NOTICE.md](NOTICE.md).
