# A storage-backed app

The codex reads every entity, every setting and every image from blob storage. It holds no path to a
local file, does no pixel work, and starts with nothing on disk beyond its own build output. The
extractor is the only writer, and it writes a complete target in one pass, either straight into blob
storage or into a folder shaped exactly like the container.

## Topology

Two Aspire app hosts share one Azurite instance.

`FellowshipExtractor.AppHost` holds the storage emulator and the codex.

`FellowshipExtractor.Publisher` holds the same storage emulator and `FellowshipExtractor` as a
resource, with the container connection string injected by `WithReference`. Running it publishes.

Both reach the same Azurite because `Stack()` derives its identity from the checkout root, the
directory `Checkout()` already walks to for `.git`, rather than from `AppContext.BaseDirectory`. One
checkout gets one volume name and one port offset whichever host starts. The emulator carries
`WithLifetime(ContainerLifetime.Persistent)`, so the container outlives either host and the other
attaches to it.

## Output targets

`export-all` takes its destination from two arguments:

- `--connection-string <value>` writes every document, setting and image straight into blob storage.
  Nothing touches the file system. The value is also taken from the `ConnectionStrings:icons`
  configuration key when the argument is absent, which is the key `WithReference` injects, so a run
  under the Publisher app host needs no argument at all.
- `--output <path>` writes a folder laid out exactly like the container.
- Both a connection string and a path writes both, in one pass over the build.
- Neither aborts, naming both.

## Layout

One container, and one folder shape that mirrors it. `<root>` is the container or the folder, and
every blob sits under a prefix:

```
<root>/ui/<texture>.png
<root>/ui/<icon>.png
<root>/data/entities.jsonl
<root>/data/settings.json
```

Entity icons and chrome textures share the `ui/` prefix, so `ChromeAssets.Prefix` is the single place
that knows where an image lives, and a blessing's unallocated rune sits beside its allocated twin.
A name written twice with different bytes throws.

## The write seam

`IExportTarget` carries three writes:

```csharp
Task WriteDocumentAsync(EntityDocument document, CancellationToken cancellationToken);
Task WriteSettingsAsync(SettingsFile settings, CancellationToken cancellationToken);
Task WriteImageAsync(string name, byte[] png, CancellationToken cancellationToken);
```

Images always land under `ui/`, documents and settings always under `data/`. `FolderTarget` and
`BlobTarget` implement it, and `export-all` writes through whichever pair the arguments name.

`BlobTarget` creates the container with `PublicAccessType.Blob`, stamps every upload with an MD5
`ContentHash` and `CacheControl: no-cache`, and skips any blob whose hash already matches, so
republishing uploads only what changed. Publishing is additive.

`TextureExporter.Export` gains a path that returns PNG bytes, so a blob run never rounds a texture
through a temporary file.

## What export-all writes

One command produces a complete target from one build: the documents, the settings, the entity icons
and the chrome textures, including every texture `UW_GenericItemWidget` names.

Chrome textures gain the seven the slot stack needs:

| Texture | Draws |
| --- | --- |
| `T_Borders_RarityGlow` | the rarity fill inside the slot border |
| `T_Borders_SlotHoverHighlight` | the hover state |
| `T_Borders_Glows_ItemSquare` | the unseen-item glow |
| `T_Borders_ItemRomanNumerals_I` to `_VIII` | the tempering numeral |
| `T_Icons_Gems_EmptySlot` | the empty gem socket |
| `T_Borders_EmptySlot` | the plate `ChromeAssets.SlotPlate` names |

Every image transform happens here, before anything lands on disk or in storage:

- Four textures are written once per rarity, multiplied by that rarity's colour the way the game's
  `SetColorAndOpacity` multiplies them: `T_Borders_Frame_TooltipRarityFrame`,
  `T_Backgrounds_TooltipHeader_Rarity`, `T_Borders_RarityFrame` and `T_Borders_RarityGlow`.
- `T_GoldDivider` is written as rows 24 to 27 of its 56 row canvas, the gold band the header rule
  draws.

## What settings.json states

`settings.json` carries everything the vocabulary carries, plus three tables read from the build so
that nothing is derived from pixels.

**The ornament each rarity draws**, from the `K2Node_Select` in
`UW_TooltipRarityFrame.SetRarityFrame`:

| Rarity | Prints | Ornament |
| --- | --- | --- |
| Epic | Epic | `T_Backgrounds_TooltipItemHeader_TopDecor_Clean_Champion` |
| Champion | Heroic | `T_Backgrounds_TooltipItemHeader_TopDecor_Clean_Paragon` |
| Heroic | Regal | `T_Backgrounds_TooltipItemHeader_TopDecor_Clean_Eternal` |
| Legendary | Legendary | `T_Backgrounds_TooltipItemHeader_TopDecor_Clean_Legendary` |

Common, Uncommon and Rare draw none.

**The colour row each rarity names**, from `BPL_DS_ColorsFunctions.GetNamedColorFromItemRarity`:

| Rarity | Row |
| --- | --- |
| Common | `game.rarity-common` |
| Uncommon | `game.rarity-uncommon` |
| Rare | `game.rarity-rare` |
| Epic | `game.rarity-epic` |
| Champion | `game.game.rarity-champion` |
| Heroic | `game.game.rarity-heroic` |
| Legendary | `game.rarity-legendary` |

`DesignTokens.Rarity()` reads this table.

**The colour each tooltip section heading reads**, from the widget that draws it:

| Heading | Row | Value |
| --- | --- | --- |
| Ability | `game.game.hero-mosse`, the `"Ability: "` label in `UW_ItemEffectsBlock` | `#EA4F84` |
| Legendary | `game.rarity-legendary` in `UW_LegendaryEffectBlock` | `#FF8000` |
| Set bonus | `game.game.character-friendly`, `UsageTxt` in `UW_SetBonusEntry` | `#00BB1D` |

## The slot

An item icon's background carries its rarity. The shipped icon's alpha channel is zero over the
artwork and non-zero over the background, so compositing the rarity colour through that channel
colours the background and leaves the artwork alone.

`export-all` writes each icon that a rarity reaches once per rarity, as
`<icon>-<rarity>.png`, composed as `colour * alpha + rgb * (1 - alpha)` with the alpha the texture
ships and the colour that rarity's row names. Fitted against a screenshot of item 5217 for Helena,
`Linked Heavy Metal Cloak`, this reaches 18.98 RMS where the untinted icon reaches 30.97, at a fitted
alpha scale of 0.93, so the channel is used as it ships. `ItemDocument.IconByRarity` names the
variant each rarity reads.

An item slot then draws, in this order:

1. `T_Borders_EmptyWithoutFrame`, the plate.
2. The icon variant for the slot's rarity, clipped to the chamfered well.
3. `T_Borders_RarityFrame` in the slot's rarity, and `T_Borders_GlassFrame` over it.

## The read side

`IExportSource` returns one snapshot:

```csharp
Task<ExportSnapshot> LoadAsync(CancellationToken cancellationToken);
```

```csharp
public sealed record ExportSnapshot(SettingsFile Settings, IReadOnlyList<EntityDocument> Entities);
```

`BlobExportSource` reads `data/settings.json` and `data/entities.jsonl` whole, once. Every
`*Document` type is a table and every JSONL line is a row, so a source backed by a document store
runs a query per table and unions the rows into the same list, returning the same snapshot.
`FolderExportSource` reads the same two files out of a folder.

`EntityStore` takes an `ExportSnapshot` and stays immutable. `Program.cs` resolves the source after
`builder.Build()` and awaits the load before `app.Run()`, seating the snapshot in a holder the
`EntityStore` factory reads, so the store and everything built on it construct once and never change.
The load uses the `BlobContainerClient` the Aspire client integration registers, so no connection
string is parsed.

A container with no `data/settings.json` fails at startup, naming the Publisher app host and the
`export-all` command that fills it.

`ChromeAssets` composes URLs and reads the tables in the settings. The API references no imaging
package.

## Naming

`vocabulary.json` becomes `settings.json`, in the published data and in the copy
`Fellowship.SDK.csproj` passes to the generator as an `AdditionalFiles` item. `VocabularyFile`
becomes `SettingsFile`, `EntityStore.Vocabulary` becomes `EntityStore.Settings`,
`VocabularyEnumGenerator` becomes `SettingsEnumGenerator` emitting `SettingsEnums.g.cs`,
`IOutputFormat.WriteVocabularyAsync` becomes `WriteSettingsAsync`, and the extractor's
`Vocabulary.cs` and `VocabularyReader.cs` become `Settings.cs` and `SettingsReader.cs`.

This rename lands first, as its own commit, so the storage change reads on its own.

## Tests

`Export.cs` in both test projects discovers a folder holding `data/entities.jsonl` and builds its
fixture through `FolderExportSource`, so the suite runs with no Azurite and no imaging package.

`FellowshipExtractor.Tests` covers what `export-all` produces against a folder target: that the gold
rule written is rows 24 to 27 of a 56 row canvas, all four rows painted; that each of the four tinted
textures is written once per rarity; and that the seven slot textures are present.

`FellowshipExtractor.Api.Tests` covers what the app reads: that `ChromeAssets` answers the texture
each rarity's ornament, colour row and section heading name; that every slot URL resolves under
`ui/`; and that a snapshot missing its settings fails with a message naming `export-all`.

## Verification

A rendered screenshot of a tooltip and of a gear slot at each of the seven rarities, compared against
the in game character sheet. Markup assertions do not see compositing, and the layer this change adds
is a compositing layer.
