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

Read a record at an item level, tempering, rarity, rank, set of rank sources, glyph tier or difficulty. An item answers
`Magnitudes`, an augmentation answers `Equipped` and a creature answers `Difficulty`:

```csharp
ItemRecord grips = await client.GetAsync<ItemRecord>(
    id: 5872, new RecordReading { Hero = "Helena", ItemLevel = 315, Upgrades = 8 });

EnchantRecord enchant = await client.GetAsync<EnchantRecord>(
    id: 67, new RecordReading { Hero = "Mara", ItemLevel = 300, RankSources = ["Head", "Chest"] });
```

List one type's records a page at a time, each read at the `Reading`. `Fields` takes a facet, a flag, a
measure bound such as `cooldownMin`, or any scalar record field, as `slot` or `primaryStat`:

```csharp
RecordPage<ItemRecord> page = await client.ListAsync<ItemRecord>(new RecordFilter
{
    Sort = "itemLevel",
    Dir = "desc",
    Reading = new RecordReading { ItemLevel = 300, Rarity = "Epic" },
    Fields = new Dictionary<string, string> { ["slot"] = "Head", ["primaryStat"] = "Intellect" },
});
```

Download the data export:

```csharp
ExportResponse export = await client.ExportAsync();

byte[] entities = await client.ExportEntitiesAsync();
await File.WriteAllBytesAsync("entities.jsonl", entities);

byte[] settings = await client.ExportSettingsAsync();
await File.WriteAllBytesAsync("settings.json", settings);
```
