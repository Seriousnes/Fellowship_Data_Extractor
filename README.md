# Fellowship Data Extractor

Console app that reads Fellowship's cooked UE5 containers and exports game data as JSON/JSONL.

Fellowship (Chief Rebel AB) is built on an Unreal Engine 5 source fork, installed by default at
`C:\Program Files (x86)\Steam\steamapps\common\Fellowship`. The game install is read-only input;
everything this tool produces lands in `output/`.

## Running

```powershell
dotnet run --project src/FellowshipExtractor -- <command>
```

| Command | What it does |
| --- | --- |
| `list` | Mounts the containers and writes `output/files.txt` plus an extension histogram |
| `cat <filter> [lines]` | Prints the first N lines of every container file whose path contains `<filter>` |
| `raw <filter>` | Extracts matching container files verbatim to `output/raw/` |
| `registry` | Parses `AssetRegistry.bin` to `output/asset-registry.jsonl` and a class histogram |
| `tags` | Exports the `Content/guid` tag tables to `output/tags.jsonl` |
| `constants` | Exports the tuning-constant CSVs to `output/ability-constants.jsonl` |
| `locres [culture]` | Exports the localization table to `output/locres-<culture>.jsonl` (default `en`) |
| `index` | Joins the tag tables to the asset registry into `output/index.jsonl` |
| `dump <filter>` | Loads the first matching `.uasset` and writes its exports as JSON |
| `textures <filter> [format]` | Decodes matching textures to `output/textures/` as png (default), jpeg, tga or dds |
| `tooltip <table> <id>` | Reconstructs one entity's tooltip: name, description, icon and every constant behind it |
| `probe [sample]` | Samples packages and reports how many parse with the current mappings |

| Environment variable | Effect |
| --- | --- |
| `FELLOWSHIP_USMAP` | Path to a `.usmap` supplying property mappings |
| `FELLOWSHIP_UE_VERSION` | Engine version override, e.g. `5.6` or `GAME_UE5_6` |

## Container facts

Established by reading the shipped files directly:

- IoStore TOC version 8, container flags `Compressed | Indexed`, **no encryption** - no AES key needed.
- Pak version 11, unencrypted index, **Oodle** compression. The game links Oodle statically, so no
  `oo2core` DLL ships on disk; `ProviderFactory` downloads one next to the extractor on first run.
- 2 containers mount cleanly: 57,501 files from the `.utoc`/`.ucas`, 37,097 from the `.pak` (94,598 total).
- The provider mounts case-insensitively and loads virtual paths. Both are required: the asset
  registry says `Items/Mounts` where the container says `Items/mounts`, and GameFeature plugin
  content is referenced by its own root (`/ForestMeadow/...`) rather than its mounted location.
- Engine version is set to `EGame.GAME_UE5_5` in `ExtractorOptions`. The build string is
  `++depot+release-source-CL-112206`, an engine source fork, so there is no stock Epic version tag to read.

## Exports that work today, with no mappings

`registry`, `tags`, `constants` and `locres` all run against the shipped build:

| Output | Records | Source |
| --- | --- | --- |
| `asset-registry.jsonl` | 55,433 | `AssetRegistry.bin` - package path, asset class, cooked tags. DataTable rows carry their `RowStructure`, so this is the index for locating content. |
| `tags.jsonl` | 13,088 | `Content/guid/*.csv` - every item, ability, effect, talent and trait name with its network id, across 18 tables. |
| `ability-constants.jsonl` | 13,156 | `RawDataTables/**/*.csv` plus the GameFeature plugin copies - per-hero, per-dungeon and per-item tuning values. |
| `locres-en.jsonl` | 14,273 | `Game.locres` - display strings under namespaces including `ST_Items`, `ST_Abilities`, `ST_Gems`, `DT_HeroItems`. |
| `index.jsonl` | 13,088 | tags joined to the registry, see below. |

`tags.jsonl` breaks down as 5,787 `ItemID`, 3,261 `GameplayEffect`, 2,307 `GameplayAbility`,
772 `CRCharacterAugmentationAsset`, 338 `CRAICharacter`, 63 `ItemTrait.ID`, 40 `Talent.ID` and
the remainder across 11 smaller tables.

Record shapes:

```jsonc
{"table":"GameplayAbility","name":"GA_Interact","netId":2,"flag":0}
{"table":"AbilityConstants","key":"Global.DefaultCCCooldown","value":9,"comment":"Default CC cooldown used for all casted/channeled abilities"}
{"table":"FinesseConstants","key":"Finesse.BasicToIncreasedPower.PowerMultiplier","value":0.015,
 "values":[0.015,0.025,0.04,0.06],"columns":["1","2","3","4"]}
{"ns":"ST_Items","key":"A1B2...","text":"Thunderlord's Judgement"}
```

Constants CSVs use a `---,1,2,3,4` header to declare tier columns; rows with more than one value
carry `values` and `columns`. Two rows in the whole set hold non-numeric text (`1.5s` and a `DR`
placeholder) and keep it under `raw`.

### The joined index

`index` emits one record per gameplay tag, carrying the cooked asset that implements it:

```jsonc
{"table":"GameplayEffect","name":"GE_Shared_MaxHealthHeal","netId":2,"matchedBy":"AssetName",
 "assetPath":"/Game/Abilities/Shared/StandardEffects/GE_Shared_MaxHealthHeal.GE_Shared_MaxHealthHeal",
 "assetClass":"Blueprint","nativeClass":"/Script/fellowship.CRGameplayEffectDamage"}
```

Cooked assets advertise their tag as `PrimaryAssetName`, which is the exact join. Gameplay
ability and effect blueprints instead carry the tag as their asset name, so the index falls
back to `AssetName` and skips any name that more than one asset claims.

Of 13,088 tags: 1,022 join on `PrimaryAssetName`, 4,743 on `AssetName`, 7,323 have no standalone
cooked asset. The join produces only ten table-to-class pairings, all coherent - `GameplayAbility`
to `GameplayAbilityBlueprint`, `GameplayEffect` to `Blueprint` under `CRGameplayEffect*` parents,
`ItemID.VisualCollection.*` to `CRVisualCollection`, and so on.

The 5,200 unmatched `ItemID` tags are the DataTable-driven items - hero gear, gems, currencies -
which live as table rows rather than standalone assets. `dump` now reads table rows, so these are
reachable through the tables that hold them.

Localization is exported but not joined. `ST_Abilities` keys such as
`Abilities_HeroAbility_Gunde_SingleTargetInterrupt_Description` only match tag names for about
20% of entries under any normalization, and the authoritative link lives inside the ability
assets. Joining display text to entities needs mappings.

## Property mappings

Cooked packages use unversioned property serialization and the build ships no `.usmap`.
`probe 300` sampled 302 packages spread across the whole content set: 0 parsed, 302 raised
`MappingException`. Fellowship is not present in TheNaeem's Unreal-Mappings-Archive (checked 2026-08-01).

Reading `.uasset` exports needs a mappings file supplied via `FELLOWSHIP_USMAP`. That is what
gates the per-asset half of the database: `GameplayAbilityBlueprint` (1,602),
`DataTable` (528), `CRDynamicHeroItemDefinition` (237), `CRVendorItemDataAsset` (145),
`CRMountData` (54), `CRItemStatWeightTemplate` (20), `CRGameplayAbilitySet` (16),
`CRTalentData` (12) and `CRTraitTreeGraph` (6).

## Static usmap generator

`src/FellowshipExtractor/Usmap/` recovers UnrealHeaderTool's generated reflection tables directly
from `fellowship-Win64-Shipping.exe`, with no process involvement. Commands:

| Command | What it does |
| --- | --- |
| `usmap-scan` | Reports what the scanner recovers, for measuring progress |
| `usmap` | Writes `output/fellowship.usmap` and round-trips it through CUE4Parse |
| `usmap-diff <ref.usmap>` | Grades recovered schemas against another UE5 game's usmap |
| `usmap-debug <property>` | Traces one property record back to its owning params record |

Layouts established empirically against this binary, not assumed:

- `FPropertyParamsBase` in `.rdata`, 56 bytes: `+0x00` name, `+0x08` RepNotify, `+0x10` CPF flags,
  `+0x18` `EPropertyGenFlags`, `+0x1C` object flags (always `0x45`). Variants append a type
  function pointer at `+0x38`. **70,438** records recovered, and every one is referenced by some
  `PropPointers[]` array, so the scan has no gaps.
- `FStructParams` in `.data`, 56 bytes: `+0x00` outer, `+0x08` super, `+0x10` struct ops,
  `+0x18` name, `+0x20` property array, `+0x28` u16 count, `+0x2A` u16 sizeof, `+0x30` object flags.
- `FClassParams`: `+0x00` NoRegister func, `+0x08` config name, `+0x18` dependency singletons
  (first entry is the base class), `+0x28` property array. It has no name field; class names live
  in a separate registration table as UTF-16 holding the C++ identifier, linked by the NoRegister
  pointer. UE strips the `U`/`A` prefix at registration, so `UCRTalentData` becomes `CRTalentData`.

Additional details recovered by decoding code rather than data:

- `EPropertyGenFlags` packs the type in the low 6 bits with modifiers above it, so a bitfield bool
  reads as `0x4C` (`Bool | NativeBool`) and an object pointer as `0x92`. Masking is mandatory:
  without it every modified property is dropped, which silently truncates the class that owns it.
- `TEnumAsByte<E>` is generated as a Byte property carrying an enum function. usmap spells that as
  an `EnumProperty` with a `ByteProperty` inner, and reading it as a bare byte consumes the wrong
  width and desynchronises everything after it.
- UHT emits a `TMap`'s **value** property before its **key**, but usmap and every reader want key
  first. Folding the run in array order therefore produces every map reversed.
- A struct that declares no properties of its own still has an `FStructParams` record, and that
  record is the only place its name appears. Its property array is null, which is the check that
  admits it without letting in false positives.
- Struct and enum constructors are not in the class registration table. Their names are recovered
  by decoding the RIP-relative `lea`/`mov` in each `Z_Construct_*` body and following it back to
  the params record, which carries the name. Only functions a property actually references are
  decoded, so this is a few thousand short reads rather than a sweep of 128 MB of `.text`.
  **The scan has to stop at the function's end**, which the `.pdata` exception directory gives
  exactly. A constructor whose own params record was not recovered otherwise runs on into the
  next function and adopts its type, which is a confident wrong answer rather than a missing one.

### Where it stands

Working end to end. `usmap` produces a 1.1 MB mappings file covering **9,740 schemas** from 5,688
structs and 3,824 classes, and cooked assets deserialize with it.

Validation, in order of strength, graded against usmaps from other UE5 titles used purely as
fixtures:

- **6,277** schemas shared with Mafia: The Old Country's usmap have identical property names in
  identical order. 22 differ in order only; all are engine-version drift in Slate, Niagara, Mass
  and animation types, confirmed on the one that matters here (see `SlateBrush` below).
- **5,787** struct-typed properties name the same struct as the fixture; **3** differ, and those
  three are Wwise iOS/Mac/tvOS settings that a Windows shipping build does not link.
- **199** map properties shared with the fixture agree on key and value type, none reversed.
- A Fellowship `Texture2D` dumped with our mappings yields the same values as the same asset
  dumped with Mafia's - `ImportedSize: X: 64, Y: 64`, `LightingGuid: 0D54BFE3413D2A05...`.
- On the class side, `GameplayAbility` matches Predecessor's usmap exactly: all 30 properties in
  the same order, from `AbilityTags` through `bMarkPendingKillOnAbilityEnd`. The game's own
  `CRGameplayAbility` extends it with 47 more, including `CooldownDuration`, `AbilityCategory`
  and `TargetAcquisitionPolicy`.
- `SlateBrush` is one of the 22 whose order differs from the fixture, and ours is the one that
  holds for this build: every field of a decoded `UIIconTexture` carries a semantically right
  value, and `ResourceObject` resolves to the icon named after the ability that owns it.

Note that `probe` is only a gate, not a validation: CUE4Parse checks that a mappings container is
present, so any syntactically valid usmap passes it. The comparisons above are what actually
demonstrate correctness.

Outstanding: enums are written as an empty table, so enum-typed properties deserialize to the
correct numeric value under a wrong type name (`CompressionSettings` reads as
`KTaperedCapsuleElem::7` where the value 7 is right and the enum name is not). Extracting
`FEnumParams` and populating the enum table is the remaining work.

## Images

A cooked texture is block-compressed GPU data, so the path an asset holds is not an image. `textures`
decodes them:

```powershell
dotnet run --project src/FellowshipExtractor -- textures ui/icons/hero_abilities
```

```
198 package(s) match 'ui/icons/hero_abilities'; writing Png
wrote 198 image(s) to G:\source\Fellowship_Data_Extractor\output\textures
  packages holding no texture: 0
     184  PF_DXT1
      12  PF_DXT5
       2  PF_B8G8R8A8
```

Output mirrors the container layout, so an icon keeps the path assets refer to it by. Decoding comes
from `CUE4Parse-Conversion`, which pulls in SkiaSharp for the encode side. BC6H and BC7 need a
native `Detex.dll`; it ships embedded in that package and `ProviderFactory` unpacks it next to the
extractor on first run, the same way Oodle is handled. Every other format decodes in managed code.

Across the whole `ui/` tree - 4,091 packages, of which 1,389 are widgets, materials and fonts
holding no texture - **2,695 of 2,702 textures export**:

| Pixel format | Count | Decoder |
| --- | --- | --- |
| `PF_DXT1` | 1,121 | managed |
| `PF_B8G8R8A8` | 1,071 | uncompressed |
| `PF_DXT5` | 489 | managed |
| `PF_BC7` | 9 | native `Detex.dll` |
| `PF_BC5` | 3 | managed |
| `PF_FloatRGBA` | 2 | managed |

The 7 that do not are `PF_Unknown` on `MP_*_Video` assets, which are media streams rather than
still images. Spot-checked visually rather than by count: the Apocalypse icon is the fire eruption
it should be, and a 4096x2048 BC7 hero-select background decodes clean, which is the check that
proves the native path rather than just exercising it.

`tooltip` exports its own icon as a side effect, to
`output/textures/tooltip/<table>-<id>-<texture>.png`, and records the file, dimensions and source
pixel format under `iconImage`.

## Reconstructing a tooltip

`tooltip <table> <id|name>` assembles everything the game shows for one entity. It takes a
gameplay tag's table and network id, or its name:

```powershell
dotnet run --project src/FellowshipExtractor -- tooltip GameplayAbility 1273
```

```
presentation index: 1330 rows across 269 CRAbilityDataRow tables (0 unreadable)
tuning constants:   12928 keys
GameplayAbility coverage: 1339 of 2307 names have a presentation row

  APOCALYPSE
    castTime         3
    cooldown         45
    radius           1000
    rangeHorizontal  3000

  Conjure a major eruption of fire in the targeted area, dealing <rt.warning>{ApocDirect}</> fire
  damage and applying <rt.absorb>Searing Blaze</> to all enemies in a large radius.

  {ApocDirect} <- .../GE_Firemage_CastedPlacedAoe_Damage.GE_Firemage_CastedPlacedAoe_Damage_C
    captures  Intellect, DamageScale
    Firemage.CastedPlacedAoe.Damage.IntellectCoefficient             24.07

  icon: /Game/ui/icons/hero_abilities/Firemage/Firemage_Apocalypse.Firemage_Apocalypse
```

The full record goes to `output/tooltip-<table>-<id>.json`.

Scaling comes out as the attribute and the coefficient, checkable against the ability's own text.
Raging Stomp says "56% Strength" and resolves to exactly that:

```
dotnet run --project src/FellowshipExtractor -- tooltip GameplayAbility 1253
```

```
  RAGING STOMP
    cooldown         45

  Stomp the ground, dealing <rt.warning>56% Strength</> Physical damage to all enemies around you
  and <rt.effect>Stunning</> them for 1.5 seconds.

  3 applied effect(s):
    DamageEffectRef                  GE_Gunde_AoeDamageAndStun_Damage_C  scales off Strength, DamageScale
      Gunde.AoeDamageAndStun.DamageStrengthCoefficient                 0.56
    DebuffEffectRef                  GE_Gunde_AoeDamageAndStun_Stun_C
      Gunde.AoeDamageAndStun.StunDuration                              1.5
    GlobalCooldownGameplayEffectClass GE_GlobalCooldown_C
```

### How it resolves

The chain is the same for every table, so nothing about heroes or asset types is hardcoded:

1. The tag names an asset, joined the same way `index` joins it.
2. **Presentation** - display name, description and icon - lives in `CRAbilityDataRow` tables
   keyed by the ability's asset name, not on the ability. 269 such tables hold 1,330 rows.
   Row keys sometimes carry the generated-class `_C` suffix that tags leave off, and the
   `GameplayAbilityAsset` field gives a second route; the output says which one matched.
   Data assets such as `CRMountData` have no row and carry their own name, description and icon
   fields instead, which are found by shape.
3. **Numbers** come from the class default object. Every `FScalableFloat` on it names a curve
   table row, and that row name is a key into the shipped tuning constants, so one recursive walk
   yields every number an asset uses along with the constant's designer comment. A handle whose
   `CurveTable` is unset serializes as a bare `RowName`, so the handle is recognised by the `Curve`
   field it sits in as well as by its shape; requiring both fields dropped the cooldown of every
   ability in the build.
4. **Scaling** is on the gameplay effect the ability applies, never on the ability. The effect
   captures an attribute and holds the coefficient that multiplies it, and both are reached two
   ways: a description token such as `{ApocDirect}` names the effect the number comes from, and
   the ability's own properties reference the effect directly. Both routes are read by the same
   code, so Apocalypse reports `Intellect` and `24.07` from either.

   The direct route is the one that covers the game. An ability holds its damage effect in an
   ordinary blueprint variable, and designers named those `DamageEffect`, `DamageEffectRef`,
   `Damage Effect`, `DotEffect`, `BuffEffect` and a dozen more, so a reference is recognised by
   **what it points at** rather than by the field it sits in: the `GameplayEffect` gameplay tag
   table is the game's own enumeration of its effects, covering 2,513 of 2,515 `GE_`-named assets
   (the two misses are AoE actors). Neither the asset name nor the native parent class identifies
   one - the parent is `CRCostEffect`, `CRProcEffect`, `CRAbsorptionEffect` or five others as often
   as it is `GameplayEffect`.

Coverage is reported rather than assumed: 1,339 of 2,307 `GameplayAbility` names have a
presentation row. The rest are passives, targeting helpers and talent monitors, which are
internal and never show a tooltip.

### Heroes and per-hero variants

One item id can be several differently named things. Shared gear carries a `DisplayNameByHero` map
keyed by a hero gameplay tag, so `ItemID 5224` is five items:

```powershell
dotnet run --project src/FellowshipExtractor -- tooltip ItemID 5224
```

```
  5 hero variant(s):
    Elarion (Bowguy)
      DisplayName: Master Astronomer's Bracers
      Ability: Astronomer's Hail
    Gunde (Gunde)
      DisplayName: Feathered Wraps of the Raven God
      Ability: Carrion Onslaught
    Meiko (Meiko)
      DisplayName: Bracers of Everlasting Spirit
      Ability: Everlasting Spirit
    Sylvie (Mosse)
      DisplayName: Bands of the First Dryad
      Ability: Dryad's Haven
    Xavian (Sune)
      DisplayName: Sandworn Bands of the Fortress
      Ability: Fortress in the Sands

  3 legendary stat(s):
    Strand of Eternity       +10% Cooldown Acceleration
    Blood of the Undying     +10 Damage Reduction
    Mote of Creation         +10% Primary Stat
```

Every variant goes in the one file. An id names one item however many things the game shows for it,
so `tooltip-ItemID-5224.json` carries all five with their own names, abilities and ability
constants, and the item's own `DisplayName` stays the placeholder it is rather than one variant's
name standing in for the rest.

Any map keyed by a `CharacterID.Hero.*` tag is picked up by shape, so a new per-hero field appears
without a code change. A trailing `ByHero` is stripped from the field name. A hero the item is
allowed on counts as a variant even when no field differs, because the ability it grants still can.

The name and description are not the whole tooltip, and neither is on the item:

- **The granted ability** takes two hops. The item names only a mapping key
  (`DynamicItemGrantedAbilityMapping.Key.Legendary.Wrist`); the 12 `CRDynamicItemGrantedAbilityMapping`
  tables hold 216 rows, each carrying the key it answers *and* the hero it answers for, so the
  lookup is on the pair. That row points at a `CRCharacterAugmentationAsset`.
- **Legendary stats** are three more augmentation assets referenced directly, each with its own
  name and effect line.

Both are the same asset class, so one reader serves them. An augmentation carries its own name and
line when it is a legendary stat, and usually carries neither when it is a granted ability: it names
a gameplay ability instead, whose presentation row holds the text. So the reader follows
`GrantedAbilities` and resolves each one as a full ability, which also brings its icon, its
description tokens and its constants.

Whether an item's ability belongs to the item or to a hero is read from the mapping tables rather
than from the item. A key that answers the same asset for every hero is one ability on the item; a
key that answers a different one per hero is an ability per variant.

Assets name heroes only by **codename**; the name the game shows is elsewhere. The 12
`BP_HeroMetaDataAsset_C` assets carry `HeroName`, and their `HeroColor.NamedOverrideColor.RowName`
reads `game.game.hero-<codename>`, which is the join. That is a data field rather than a path
convention, so it holds for Gunde whose content sits in its own plugin root, and the codename's
canonical spelling is taken from the `CharacterID` tag table. **Bowguy is Elarion, Mosse is Sylvie,
Sune is Xavian, Firemage is Ardeos, Warmaster is Helena.**

The icon is shared: these items declare a single `DefaultIcon`, not one per hero.

Two limits worth stating. Enum-valued fields (`abilityCategoryOrdinal`, a token's
`calculationOrdinal`) are emitted as ordinals, because the usmap's enum table is empty. And the
displayed damage range is not emitted: the data gives the coefficient and the attribute it
captures, but the player's stat value and the spread the UI applies around it are not in the
build.

### Items with no hero dimension

A relic is the other shape: usable by every hero, one name, one ability. `ItemID 5252` is
`Ancient Poultry Fetish`:

```powershell
dotnet run --project src/FellowshipExtractor -- tooltip ItemID 5252
```

```
  ANCIENT POULTRY FETISH
  Relic

  3 stat slot(s):
    HeroRelativePrimaryStat      hero relative
    StatCrit                     ECRItemStat::CritChance
    StatSpirit                   ECRItemStat::Spirit

  Ability: Chickenize!
    Instantly turn your target into a chicken.
    <rt.absorb>Chickenize!</> can only be used a limited amount of times per dungeon. The charges
    are shared by everyone in the party.
    <rt.effect>Chickenized</> enemies <rt.warning>do not give Kill Score.</>
    <rt.meikoability2>Can be used during Global Cooldown</>
    CooldownDuration    RelicPower.PolymorphTargetWithSharedCharges.Cooldown       180

  icon: /Game/ui/icons/RelicIcons/T_Relic_Chicken.T_Relic_Chicken
        output\textures\tooltip\ItemID-5252-T_Relic_Chicken.png  (1306x1306, PF_B8G8R8A8)
```

Three fields on the item make that work, and none of them is a `DisplayName`:

| Wanted | Where it is |
| --- | --- |
| `Ancient Poultry Fetish` | `DisplayNameByArmorType`, one entry. The item's own `DisplayName` is its asset name. |
| `Relic` | `RuleSet`, a gameplay tag. `HeroItemType` is a byte enum and cooks down to `10`. |
| the three stat lines | `FixedSlots`, joined to `CRDynamicHeroItemSlotDefinition` for the stat each slot carries |

A display name in a map is picked up whatever the map is keyed by, so `DisplayNameByArmorType` and
`DisplayNameByHero` need no separate handling. The name is taken from the map when it holds one
distinct value; several values mean a dimension has to be chosen first, which is what passing a
hero does.

Stat **magnitudes** are not in the build. A dynamic item declares which slots it has, not what they
roll to: `DA_StatCalculationSettings` holds the coefficients (`PrimaryPowerBase` 1.03,
`FixedSlotCalculationSettings.PrimaryMultiplier` 0.525, `SecondaryMultiplier` 1.125) and the
calculation takes the item's rarity and level, both decided at drop time. The item lists five
rarities it can drop in.

## Everything at once

`export-all` writes one JSONL line per gameplay tag with every icon beside it in the same folder,
using the same resolution the `tooltip` command uses for one entity. Naming an icon after its own
texture means a texture shared by many entities is decoded once:

```powershell
dotnet run --project src/FellowshipExtractor -- export-all
dotnet run --project src/FellowshipExtractor -- export-all ItemID GameplayAbility
```

```
wrote output\export\entities.jsonl
  13088 entities, 2482 with a display name, 1650 with an icon
  992 images in output\export
```

Each line carries the interpreted tooltip fields *and* the asset's full property set under
`properties`, so a field this code does not yet read is still in the export.

Presentation coverage is uneven, and the cause is that only `CRAbilityDataRow` tables are indexed:

| table | tags | asset found | display name | icon |
| --- | --- | --- | --- | --- |
| ItemID | 5787 | 587 | 334 | 221 |
| GameplayEffect | 3261 | 2514 | 404 | 1 |
| GameplayAbility | 2307 | 1841 | 1328 | 1155 |
| CRCharacterAugmentationAsset | 772 | 554 | 185 | 123 |
| CRAICharacter | 338 | 233 | 231 | 211 |

`DT_HeroItems` holds 480 `CRHeroItemDataRow` rows with a `DisplayName`, `ItemIconTexture` and
`LoreText`, keyed by the last segment of an `ItemID` tag; all 480 row names match a tag exactly.
`CRItemTraitsDefinitionRow`, `CRModifierInfoDataRow` and `CRLevelInfoRow` are the equivalents for
`ItemTrait.ID`, `Modifiers` and `Level.ID`. Indexing those is what raises item coverage.

## Layout

- `src/FellowshipExtractor/ProviderFactory.cs` - the single place that knows about the install path,
  engine version, Oodle and mappings. Commands take the provider it builds.
- `src/FellowshipExtractor/UeJson.cs` - writes CUE4Parse's property model to `Utf8JsonWriter`.
  CUE4Parse only ships Newtonsoft converters, so the export shape is defined here. It counts every
  value that falls through to `ToString()` in `UeJson.StringifiedTypes` and `dump` reports the
  tally, which is how native structs still needing real handling get identified. Data and curve
  tables keep their rows in a map beside their properties, so those are written under `$rows`;
  without that a table serializes to its config flags and nothing else.
- `src/FellowshipExtractor/Tooltip/` - the `tooltip` and `export-all` commands. `TooltipResolver`
  builds the shared indexes once and turns a tag into a `TooltipRecord`, so resolving one entity
  and resolving the whole game are the same code and the same setup cost. `PropertyWalk` is the
  recursive property walk everything else is built on, `CurveBindings` turns `FScalableFloat`
  references into resolved constants, `AbilityPresentationIndex` holds the `CRAbilityDataRow` join,
  `ItemSlots` holds the item slot definition join, and `DescriptionTokens` follows a `{Token}` to
  the gameplay effect behind it.

## Known limits

Two lightmap `.ubulk` files exceed 2 GB (`SandTomb_Environment_BuiltData.ubulk`,
`ForestMeadow_Environment_BuiltData.ubulk`) and overflow CUE4Parse's single-array read. `raw`
reports them and continues.
