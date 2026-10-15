# Codex.FellowshipAnalyzer.SDK

The package contains every entity document, the API request and response models, and a
strongly-typed .NET client for the API of codex.fellowshipanalyzer.com.

The package is published as `Codex.FellowshipAnalyzer.SDK`. The project folder, the assembly and
every C# namespace have the name `Fellowship.SDK`. Thus `using Fellowship.SDK;` is correct.

## Install the package

The package is published to [nuget.org](https://www.nuget.org/packages/Codex.FellowshipAnalyzer.SDK).
Add it to your project:

```powershell
dotnet add package Codex.FellowshipAnalyzer.SDK
```

## Read the codex

`CodexClient` takes an `HttpClient`. If the `HttpClient` has a `BaseAddress`, `CodexClient` sends
requests to that address. Otherwise, it sends requests to codex.fellowshipanalyzer.com:

```csharp
using Fellowship.SDK;
using Fellowship.SDK.Client;
using Fellowship.SDK.Records;

using var http = new HttpClient();
var client = new CodexClient(http);

SearchResponse response = await client.SearchAsync("Helena");
SearchResult first = response.Results[0];

AbilityRecord ability = await client.GetAsync<AbilityRecord>(id: 1);
EntityRecord entity = await client.GetAsync(EntityType.Item, id: 1);
```

`TooltipAsync` returns the tooltip as a self-contained HTML element:

```csharp
string tooltipHtml = await client.TooltipAsync<AbilityTooltipRequest>(
    id: 1, new AbilityTooltipRequest { Hero = "Ardeos", ShowIcon = true });
```

## Download the export

The codex serves the export of the build it reads.

- `ExportAsync` returns an `ExportResponse`. It has the Steam build id in `Build`, and the path of
  each file in `Entities` and `Settings`.
- `ExportEntitiesAsync` returns the contents of the entities file as a `byte[]`. The file has one
  document on each line.
- `ExportSettingsAsync` returns the contents of the settings file as a `byte[]`. This file is the
  input of the SDK enums.

`CodexAddresses` has the paths as the constants `Export`, `ExportEntities` and `ExportSettings`.

```csharp
ExportResponse export = await client.ExportAsync();

byte[] entities = await client.ExportEntitiesAsync();
await File.WriteAllBytesAsync("entities.jsonl", entities);

byte[] settings = await client.ExportSettingsAsync();
await File.WriteAllBytesAsync("settings.json", settings);
```
