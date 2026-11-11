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

Read a record at an item level, tempering, rarity, set of rank sources, glyph tier or difficulty. An item answers
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

## Read an item's stats from the export

`FellowshipExport` reads `settings.json` and `entities.jsonl` from one folder. A folder of
`export-<build>` folders reads its highest build. The stats it returns are the codex API's
`Magnitudes` for the same reading:

```csharp
using Fellowship.SDK.Calculations;
using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;
using Fellowship.SDK.Records;

FellowshipExport export = FellowshipExport.Load("output");

var orb = new EntityReference(EntityType.Item, 5919, "Orb of Sanguine Restoration");
var scaling = new ItemScaling { Hero = "Aeona", ItemLevel = 315, Upgrades = 8 };

ItemMagnitudesRecord magnitudes = export.Magnitudes(orb, scaling);
// magnitudes.MaxUpgrades is 8, and magnitudes.Stats has Expertise 27.

IReadOnlyList<StatLine> lines = export.Stats(orb, scaling);
decimal? potential = export.PotentialPower(export.Find<ItemDocument>(orb, "Aeona")!, scaling);
```

`ItemScaling` has the item level, tempers, tempering ceiling, rarity, armor, stat values, and the
modifiers and attunements in the item's crafted rows. An enchant among the modifiers or attunements adds
its stats at the item level. A field left unset reads as the codex API reads it: the drop item level,
the item's highest rarity, and that rarity's tempering ceiling.

An augmentation's rank and the gear slots it ranks from:

```csharp
var enchant = export.Augmentation(id: 67, wearer: "Mara")!;
var reading = new EquippedReading { ItemLevel = 300, RankSources = ["Head", "Chest"] };

int rank = AugmentationRanks.Rank(export, reading, enchant);
```

The static classes in `Fellowship.SDK.Calculations` take the settings and documents directly.

## Find the rows a document's field is set from

`EntityDocument.FieldSources` is keyed by a scalar field's name in the document, such as `castTime` or
`range`. Each `FieldSource` has the full names of the ability constants rows the field is set from, and a
`Kind` from the vocabulary of `ResourceGain.SourceKind`. A `ConstantsRow` field has the value of its one
row. `Tuning` has the keys of the document's `Tuning` for those rows:

```csharp
var ability = export.Find<AbilityDocument>(new EntityReference(EntityType.Ability, 1273, "Apocalypse"))!;

FieldSource castTime = ability.FieldSources!["castTime"];
string key = castTime.Tuning![0];
// key is "CastTime", and ability.Tuning![key] is the row castTime has the value of.
```

A field set from no row has no entry, and a document with no such field has no `FieldSources`.

A reading the codex API refuses throws `RefusedReadingException`. Its `Refusal` has the API's problem
title and detail. `FellowshipExport.Refusal` returns the same refusal without the throw.
