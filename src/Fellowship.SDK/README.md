# FellowshipAnalyzer.Codex.SDK

A .NET client and models for the codex.fellowshipanalyzer.com API.

## Install

```powershell
dotnet add package FellowshipAnalyzer.Codex.SDK
```

## Use

The namespaces are under `Fellowship.SDK`. `CodexClient` sends requests to the `BaseAddress` of the
`HttpClient` it is given, or to codex.fellowshipanalyzer.com if none is set.

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

string tooltipHtml = await client.TooltipAsync<AbilityTooltipRequest>(
    id: 1, new AbilityTooltipRequest { Hero = "Ardeos", ShowIcon = true });
```

Download the data export:

```csharp
ExportResponse export = await client.ExportAsync();

byte[] entities = await client.ExportEntitiesAsync();
await File.WriteAllBytesAsync("entities.jsonl", entities);

byte[] settings = await client.ExportSettingsAsync();
await File.WriteAllBytesAsync("settings.json", settings);
```
