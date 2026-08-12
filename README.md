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
| `FELLOWSHIP_USMAP` | Path to a `.usmap` supplying property mappings. Unset, the extractor reads `output/fellowship.usmap`, the file the `usmap` command writes |
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

Reading `.uasset` exports needs a mappings file. `FELLOWSHIP_USMAP` names one, and with it unset the
extractor reads `output/fellowship.usmap`, which is where the `usmap` command writes the mappings it
recovers, so a checkout that has generated them needs no environment at all. A run that finds
neither stops before it opens a package and names the command that writes them, rather than reading
every table back empty. That mappings file is what gates the per-asset half of the database: `GameplayAbilityBlueprint` (1,602),
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

`tooltip` exports its own icon as a side effect, to `output/textures/tooltip/<texture>.png`, and
records the file name under `icon`. Naming an icon after its own texture rather than after the
entity that asked for it means a texture several entities share is decoded once and they all agree
on the file.

## Reconstructing a tooltip

`tooltip <table> <id|name>` assembles everything the game shows for one entity. It takes a
gameplay tag's table and network id, or its name:

```powershell
dotnet run --project src/FellowshipExtractor -- tooltip GameplayAbility 1273
```

```json
{
  "table": "GameplayAbility",
  "netId": 1273,
  "name": "GA_Firemage_CastedPlacedAoe",
  "displayName": "Apocalypse",
  "tags": ["Aoe", "Casted", "Magic.Fire", "Major", "Ranged", "Skill.Offensive"],
  "description": "Conjure a major eruption of fire in the targeted area, dealing <rt.warning>{ApocDirect}</> fire damage and applying <rt.absorb>Searing Blaze</> to all enemies in a large radius.",
  "icon": "Firemage_Apocalypse.png",
  "castTime": 3,
  "cooldown": 45,
  "range": 3000,
  "radius": 1000,
  "scalesOff": ["DamageScale", "Intellect"],
  "placeholders": {
    "ApocDirect": {
      "scalesOff": ["DamageScale", "Intellect"],
      "constants": { "Damage.IntellectCoefficient": 24.07 }
    }
  },
  "constants": {
    "MaxRangeUp": 2000,
    "MaxRangeDown": 3000,
    "Talent.StunAndCooldownReduction.StunDuration": 3,
    "Talent.AddedCharges.MaxCharges": 2
  }
}
```

The record goes to `output/tooltip-<table>-<id>.json`; the console prints the same thing as text.

The rule is everything the tooltip needs and nothing else. **A reference to another asset is never
emitted as a path**, because a class name reconstructs nothing: either the value behind it is read
and folded in, or it is left out. So the five gameplay effect classes Apocalypse names are gone and
the one number among them is `Damage.IntellectCoefficient`; the `{ CurveTable, RowName }` handles
are gone and the rows they point at are the constants; the byte-enum ordinals are gone because the
usmap carries no enum table and an index names nothing. `dump <filter>` still writes the raw asset
when that is what is wanted.

Scaling comes out as the attribute and the coefficient, checkable against the ability's own text.
Raging Stomp says "56% Strength" and "1.5 seconds", and resolves to exactly that:

```json
{
  "table": "GameplayAbility",
  "netId": 1253,
  "name": "GA_Gunde_AoeDamageAndStun",
  "displayName": "Raging Stomp",
  "tags": ["Aoe", "Physical.Bleed", "Skill.Offensive", "Targeted"],
  "description": "Stomp the ground, dealing <rt.warning>56% Strength</> Physical damage to all enemies around you and <rt.effect>Stunning</> them for 1.5 seconds.",
  "icon": "T_Gunde_Shockwave.png",
  "cooldown": 45,
  "charges": 2,
  "radius": 1000,
  "scalesOff": ["DamageScale", "Strength"],
  "constants": {
    "VisualHitDelay": 0.21,
    "DamageScalingTargetCountThreshold": 8,
    "DamageStrengthCoefficient": 0.56,
    "StunDuration": 1.5
  }
}
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
4. **A `{Token}` resolves to a number you can compute.** The game rolls
   `coefficient x attribute` inside a band, so a placeholder carries the mean and the band:

   ```json
   "placeholders": {
     "FireBallDirect": { "attribute": "Intellect", "coefficient": 5.8482, "spread": 0.2 }
   }
   ```

   `spread` is the **total** width of the band as a share of the mean, not a plus or minus:
   `min = mean x (1 - spread / 2)` and `max = mean x (1 + spread / 2)`, so 0.2 is 90 to 110 percent
   and 0.4 is 80 to 120. At 100 Intellect Fire Ball is `5.8482 x 100` = 585 at spread 0.2, shown in
   game as 526 to 643, and Apocalypse reproduces a screenshot reading 18,835 - 23,021 from
   `20.9285 x 1000` = 20,928 taken x0.9 and x1.1, with the coefficient's own comment
   (`was 22.03 then 20.9285`) dating the screenshot. Total width is what
   `DR_SlaughtervineArchephyte.AutoAttackMelee.Spread = 1` settles: read as a plus or minus, a basic
   melee swing would roll anywhere between nothing and double.

   A spread of 0 is a band of nothing, meaning the number is exact. That is a different fact from
   the field being absent, which means nothing in the build says.

   The band comes from two layers. A tuning row named `Spread` sitting beside the coefficient row is
   the ability's own override, and 456 of the 12,928 shipped rows are one. Everything else falls
   back to the `DefaultSpread` of the calculation preset the gameplay effect names, 27 rows of
   `DT_EffectCalculationPresets` at 0.2 or 0. Both are resolved at export time so nothing downstream
   needs either table: the whole Firemage namespace has no `Spread` row at all, and Fire Ball's
   direct hit still exports 0.2 because `GE_Firemage_PlacedProjectileAoe_Damage` names the
   `HeroMagicalArea` preset.

   The coefficient is identified by the tuning row naming itself: `AoeDamageIntellectCoefficient`,
   `DamageStrengthCoefficient`, `AgilityCoefficient`. That is the designer's own label, and it
   names the attribute as well as marking the row. The row's last segment has to *end* there, which
   is what separates Fire Ball's `AoeDamageIntellectCoefficient` (the direct hit `{FireBallDirect}`
   prints) from its `DotIntellectCoefficientPerTick`. 278 of 404 placeholders resolve this way; a
   token whose rows name two candidates - Temporal Barrage prints a heal and a damage number from
   rows differing only in that word - reports neither rather than the wrong one.

5. **Scaling** is on the gameplay effect the ability applies, never on the ability. The effect
   captures an attribute and holds the coefficient that multiplies it, and both are reached two
   ways: a description token such as `{ApocDirect}` names the effect the number comes from, and
   the ability's own properties reference the effect directly. Both routes are read by the same
   code, so Apocalypse reports `Intellect` and `24.07` from either.

   A token does not always name an effect. 44 of the 229 that name an object name **the ability
   that owns them**, and native code works the number out from the ability's own tuning. Reading
   those as effects claims every constant on the ability as the token's own: Fire Ball's
   `{FireBallDirect}` swallowed all 14 and emptied the ability's own list to do it. A self-naming
   token is left bare and flagged `fromOwnConstants`, which separates "the data does not say which
   rows make this number" from "this token failed to resolve".

   The direct route is the one that covers the game. An ability holds its damage effect in an
   ordinary blueprint variable, and designers named those `DamageEffect`, `DamageEffectRef`,
   `Damage Effect`, `DotEffect`, `BuffEffect` and a dozen more, so a reference is recognised by
   **what it points at** rather than by the field it sits in: the `GameplayEffect` gameplay tag
   table is the game's own enumeration of its effects, covering 2,513 of 2,515 `GE_`-named assets
   (the two misses are AoE actors). Neither the asset name nor the native parent class identifies
   one - the parent is `CRCostEffect`, `CRProcEffect`, `CRAbsorptionEffect` or five others as often
   as it is `GameplayEffect`.

6. **Naming** the numbers. A tuning row belongs to the entity whose name it starts with, so
   `Firemage.CastedPlacedAoe.Radius` is keyed as `Radius` while a row reached from elsewhere keeps
   its full name. The namespace comes from the asset name but counts only when the shipped tables
   confirm rows actually sit under it, so a name that confirms nothing simply has no namespace.

   The ones a tooltip puts in a fixed place - cast time, cooldown, charges, cost, range, radius -
   are lifted out into named fields. Designers spell the same field several ways: range is
   `MaxRange` on 154 abilities, `Max Range` on 401, `MaxRangeRef` on 117 and `Max Range Ref` on 25.
   Matching those one at a time would leave most of the corpus behind, so a name is reduced to
   spaces removed, a trailing `Ref` dropped and case folded before it is looked up.

   A row whose name contains a `Talent` segment goes under `talents`, keyed by the talent the name
   mentions - 1,457 rows across 647 entities. **That is a fact about the name and nothing more.** A
   row named for a talent is very often unconditional, because a talent folded back into its ability
   leaves its name behind in the row.

   A named field is read from the entity's own top-level rows. **Charges are the exception**, and
   worth spelling out because the obvious rules are all wrong. Fire Ball has two charges in play;
   its only charge count is `Talent.PlacedProjectileAoeHasCharges.NumCharges` and it *declares a
   gate* for it. Apocalypse has a single cast; it carries `Talent.AddedCharges.MaxCharges` = 2 and
   declares *no* gate. So the row's name does not say whether it applies, the presence of a gate
   does not, and neither does its absence.

   What does is `ChargeGameplayEffectClass`. The native `/Script/fellowship.CRChargeEffect` is the
   charge machinery; `GE_Shared_ChargeCooldownNoHaste` is a cooldown-behaviour effect that only
   reads charge-shaped. A charge count promotes to `charges` for abilities on the native effect and
   for no others, and for those it promotes from a talent-named row as readily as any other. That
   moves 19 abilities - 6 gain a count they were hiding, 13 lose one they should not have shown -
   and leaves 26 reporting charges.

   The **condition**, where there is one, is declared by the asset and reported separately in
   `talentGates`. An ability that gates a value on a talent puts a tag property beside it -
   `TalentPlacedProjectileAoeHasChargesTag` next to `...NumChargesRef` - and the tag's last segment
   is the talent's name, which is the join. Only 37 entities declare a gate against the 691 with
   talent-named rows, so the name alone proves nothing.

   Declaring a gate is not the same as having one: 10 of the 61 declared gate tags are granted by
   nothing in the build. So `talentGates` says what the asset requires and `grantedTags` on a
   character augmentation says what actually opens it, and the two are left as separate facts.
   `Talents.Firemage.PlacedProjectileAoe.PlacedProjectileAoeHasCharges` is granted by
   `CAA_Talent_Firemage_ReignOfFire`; `Talents.Firemage.CastedPlacedAoe.StunAndCooldownReduction` is
   granted by nothing.

   And a declared gate is still not runtime behaviour: Fire Ball has two charges in play without
   Reign of Fire selected. Every field here describes what an asset declares, which is the only
   thing files can say.

7. **A talent's numbers are not under its own name.** `CAA_Talent_Rime_GlacialAssault` matches no
   tuning prefix at all - a talent's rows sit under the ability it modifies, as
   `Rime.CastedProjectileDamageOrbSpender.Talent.InstantAndBonusDamage.*`. The tag the talent grants
   carries both halves, so stripping `Talents.` and inserting a `Talent` segment before the last
   part reconstructs the prefix; it is checked against the tables before use, and 149 of the 231
   granted talent tags resolve. Every depth below a talent's namespace is taken, unlike an
   ability's where only the immediate rows are its own, because nothing nests inside a talent.

   That is what makes a talent exportable. `CAA_Talent_Rime_GlacialAssault` has three properties -
   two granted abilities and one tag - so its name, text and icon come from following
   `GrantedAbilities` to a presentation row, and its numbers come from the tag. Without the tag
   route it exports without `DamageMultiplier` = 1.4, the "40% more damage" its own text quotes.
   277 of the build's 373 talent assets now carry real content.

8. **Costs** are read from the tuning tables directly, because nothing in the asset points at them.
   A cost is a `CRCostEffect`, a native class whose blueprint carries no properties at all, so the
   cost row is reachable only as the row sitting under the ability's namespace. That recovers
   `Vigor.InstantFrontalConeDamageHeal.ManaCost` = 37 for Radiant Blast and 1,322 rows across the
   build, which the export reports rather than folding in silently.

   What the cost is paid in is a third place again: `DetailedDescription3` on the presentation row,
   which designers use for the resource name rather than for a description. So Radiant Blast is
   `"cost": 37, "resource": "Mana"`, Starfall Volley is `30` `Focus` and Shield Slam is `3`
   `Combo Points`. Reading it as a description line put a bare "Mana" in 73 entities' `detail`
   arrays; read as what it is, `detail` drops to the three real lines in the build.

Coverage is reported rather than assumed: 1,339 of 2,307 `GameplayAbility` names have a
presentation row. The rest are passives, targeting helpers and talent monitors, which are
internal and never show a tooltip.

### Heroes and per-hero variants

One item id can be several differently named things. Shared gear carries a `DisplayNameByHero` map
keyed by a hero gameplay tag, so `ItemID 5224` is five items:

```powershell
dotnet run --project src/FellowshipExtractor -- tooltip ItemID 5224
```

```json
{
  "table": "ItemID", "netId": 5224, "displayName": "Legendary_Wrists_A_CriticalStrike",
  "type": "Legendary",
  "stats": ["Stamina", "HeroRelativePrimaryStat", "CritChance"],
  "icon": "Tex_bracers_6_b.png",
  "legendaryStats": [
    { "displayName": "Strand of Eternity", "description": "+10% Cooldown Acceleration",
      "scalesOff": ["CooldownRecovery"], "constants": { "CooldownReductionMultiplier": 1.1 } },
    { "displayName": "Blood of the Undying", "description": "+10 Damage Reduction",
      "scalesOff": ["IncomingDamageMultiplier"], "constants": { "IncomingDamageMultiplier": 0.9 } },
    { "displayName": "Mote of Creation", "description": "+10% Primary Stat",
      "scalesOff": ["PrimaryAttributeMultiplier"], "constants": { "AdditionalPrimaryStatMultiplier": 0.1 } }
  ],
  "variants": [
    { "hero": "Bowguy", "heroName": "Elarion", "displayName": "Master Astronomer's Bracers",
      "grants": { "displayName": "Astronomer's Hail", "description": "..." } },
    { "hero": "Gunde", "heroName": "Gunde", "displayName": "Feathered Wraps of the Raven God",
      "grants": { "displayName": "Carrion Onslaught", "description": "..." } },
    { "hero": "Meiko", "heroName": "Meiko", "displayName": "Bracers of Everlasting Spirit",
      "grants": { "displayName": "Everlasting Spirit", "description": "..." } },
    { "hero": "Mosse", "heroName": "Sylvie", "displayName": "Bands of the First Dryad",
      "grants": { "displayName": "Dryad's Haven", "description": "..." } },
    { "hero": "Sune", "heroName": "Xavian", "displayName": "Sandworn Bands of the Fortress",
      "grants": { "displayName": "Fortress in the Sands", "description": "..." } }
  ]
}
```

Every variant goes in the one file. An id names one item however many things the game shows for it,
so `tooltip-ItemID-5224.json` carries all five with their own names, abilities and ability
constants, and the item's own `DisplayName` stays the placeholder it is rather than one variant's
name standing in for the rest.

A hero the item is allowed on counts as a variant even when no field differs, because the ability
it grants still can.

The name and description are not the whole tooltip, and neither is on the item:

- **The granted ability** takes two hops. The item names only a mapping key
  (`DynamicItemGrantedAbilityMapping.Key.Legendary.Wrist`); the 12 `CRDynamicItemGrantedAbilityMapping`
  tables hold 216 rows, each carrying the key it answers *and* the hero it answers for, so the
  lookup is on the pair. That row points at a `CRCharacterAugmentationAsset`.
- **Legendary stats** are three more augmentation assets referenced directly, each with its own
  name and effect line.

Both are the same asset class, so one reader serves them. An augmentation carries its own name and
line when it is a legendary stat, and usually carries neither when it is a granted ability: it names
gameplay abilities instead, whose presentation rows hold the text. So the reader follows
`GrantedAbilities` and resolves **every** one, taking the text from the first that has any and the
numbers from all of them. A talent grants several abilities that each carry their own tuning, so
folding only the named one drops the rest.

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

One limit worth stating: the displayed damage range needs a stat value the caller supplies. The
export gives the coefficient, the attribute it captures and the `spread` the roll uses, so
everything but the player's own Intellect or Strength is there.

### Items with no hero dimension

A relic is the other shape: usable by every hero, one name, one ability. `ItemID 5252` is
`Ancient Poultry Fetish`:

```powershell
dotnet run --project src/FellowshipExtractor -- tooltip ItemID 5252
```

```json
{
  "table": "ItemID", "netId": 5252,
  "name": "ItemID.HeroItem.Dynamic.Relic.C_Crit_Spirit_Polymorph",
  "displayName": "Ancient Poultry Fetish",
  "type": "Relic",
  "stats": ["HeroRelativePrimaryStat", "CritChance", "Spirit"],
  "icon": "T_Relic_Chicken.png",
  "grants": {
    "displayName": "Chickenize!",
    "tags": ["Relic", "Skill.Secondary"],
    "description": "Instantly turn your target into a chicken.\r\n\r\n<rt.absorb>Chickenize!</> can only be used a limited amount of times per dungeon. The charges are shared by everyone in the party.\r\n\r\n<rt.effect>Chickenized</> enemies <rt.warning>do not give Kill Score.</>\r\n\r\n<rt.meikoability2>Can be used during Global Cooldown</>",
    "icon": "T_Relic_Chicken.png",
    "cooldown": 180,
    "range": 3000,
    "constants": {
      "MaxSharedCharges.Dungeon": 4,
      "MaxSharedCharges.Adventure": 2,
      "BaseHealthThreshold.Large": 3.5,
      "BaseHealthThreshold.Medium": 2,
      "ThreatAdded": 100
    }
  }
}
```

Every line of the in-game tooltip is there: the icon, the name, the `Relic` subtitle, the three
stat lines, the ability with all four of its paragraphs, and `3 MINUTE COOLDOWN` as `cooldown`.
Three fields on the item make that work, and none of them is a `DisplayName`:

| Wanted | Where it is |
| --- | --- |
| `Ancient Poultry Fetish` | `DisplayNameByArmorType`, one entry. The item's own `DisplayName` is its asset name. |
| `Relic` | `RuleSet`, a gameplay tag. `HeroItemType` is a byte enum and cooks down to `10`. |
| the three stat lines | `FixedSlots`, joined to `CRDynamicHeroItemSlotDefinition` for the stat each slot carries |

A display name in a map is picked up whatever the map is keyed by, so `DisplayNameByArmorType` and
`DisplayNameByHero` need no separate handling. The name is taken from the map when it holds one
distinct value; several values mean the item reads differently per hero, and each variant carries
its own name while the item keeps the internal placeholder.

Stat **magnitudes** are not in the build. A dynamic item declares which slots it has, not what they
roll to: `DA_StatCalculationSettings` holds the coefficients (`PrimaryPowerBase` 1.03,
`FixedSlotCalculationSettings.PrimaryMultiplier` 0.525, `SecondaryMultiplier` 1.125) and the
calculation takes the item's rarity and level, both decided at drop time. The item lists five
rarities it can drop in.

## Everything at once

`export-all` writes one JSONL line per gameplay tag with every icon beside it, using the same
resolution the `tooltip` command uses for one entity. Naming an icon after its own texture means a
texture shared by many entities is decoded once:

```powershell
dotnet run --project src/FellowshipExtractor -- export-all --output output/export
dotnet run --project src/FellowshipExtractor -- export-all --output output/export ItemID GameplayAbility
```

Two arguments name the destination. `--output <path>` writes a folder. `--connection-string <value>`
writes straight into the `icons` blob container and nothing touches the file system; the value is
taken from the `ConnectionStrings:icons` configuration key when the argument is absent, which is the
key the Publisher app host injects, so a run under it needs no argument at all. Naming both writes
both in one pass over the build.

A blob run uploads every image as it draws it, and publishes `data/entities.jsonl` and
`data/settings.json` together once the pass has both. A pass that throws part way publishes neither,
so a throw during the export leaves the container on the pair the last finished pass left.

Either destination is the same layout:

```
<root>/data/entities.jsonl
<root>/data/settings.json
<root>/ui/<texture>.png
```

The run reports what it read: 12,473 documents from 12,472 entities across 13,088 tags, 6,522 with a
display name and 5,871 with an icon; 1,288 names and 1,282 icons inherited from the entity that
applies them, 70 and 10 withheld because two entities that apply them disagree; 1,713 tuning rows
supplied by namespace that no asset names; 1 coefficient row banded by its own `Spread` row and 61
banded by their effect's calculation preset.

It reports what it drew too: 127 chrome images, which is 95 chrome textures plus 28 rarity variants,
four textures multiplied by each of the seven rarity colours, plus 4 modifier row textures, each
multiplied by the colour row the row draws it in; and 4,367 entity icon images, 3,150 of them an
icon composed against a rarity colour. `ui/` ends at 4,494 files and 212 MB, the whole of the
transform work done once so that no consumer composes anything at render time.

Every line is the same record the `tooltip` command writes for one entity - one shape, 2.9 MB for
the whole game, down from 22.5 MB.

### settings.json

The one file beside `entities.jsonl` that is not an entity. 47,646 bytes of lookup tables that no
entity line can carry, written by the same `export-all` run so the two always describe the same
build. Three of them are below; the rest are the design system's named colours, the rich text style
set and the stat calculation settings, which is what lets a renderer draw a tooltip in the game's
own colours without reading the build itself:

```json
{
  "heroes": [
    { "codename": "Firemage", "name": "Ardeos", "armorType": "Cloth",
      "primaryStat": "Intellect", "color": "EB6328" }
  ],
  "rarities": [
    { "tier": 4, "name": "Champion", "displayName": "Heroic" }
  ],
  "printFormats": { "DamageRangeMagic": "<rt.warning>{0} - {1}</> magic damage" }
}
```

**heroes**, 12 rows, is the reverse index an item needs. An item expands into rows keyed by
`armorType` and `primaryStat` and carries no hero at all, so `hero=Ardeos` selects one of them only
through a table saying Ardeos is Cloth and Intellect. `codename` is the spelling every asset uses
and `name` the one every tooltip prints. `color` is six hex digits from the design system row the
hero's metadata asset points at, followed rather than the `ClassColor` literal beside it because the
two disagree: Aeona's asset says `BC00FF` while the row it names is `FC9FEC`.

**rarities**, 7 rows, is the ladder under both of its names. `name` is what the build files a tier
under and what every `rarity` field in `entities.jsonl` says; `displayName` is what a player sees.
They differ on two tiers and the difference is a shift, so a join by name is wrong in both
directions: internal `Champion` prints as "Heroic" and internal `Heroic` prints as "Regal".

**printFormats**, 20 rows of `DT_DynamicTooltipTexts`, is how the game writes a substituted number.
`DamageRange` is the band `spread` produces, `UnitSeconds` and `UnitPercent` the two units, and the
`ModDesc*` family the way a modifier states itself.

The damage-school variants take three things to select, not one. The `showDamageType` flag a
placeholder carries says the school is wanted in the number rather than in the sentence around it;
a `coefficient` says the number is damage at all; and the `Magic.*` or `Physical` entry in the
entity's own `tags` says which school. All three are needed because the flag is shipped on two
tokens that are not damage: Reaver's Edge and Blood Arc both set it on their `{Radius}`, and a
renderer going by the flag alone would print "500 physical damage" for a distance. 10 tokens of 225
carry the flag and 8 of those carry a coefficient. Without the flag the same renderer would write
"fire damage magic damage" on the sentences that already name the school.

Changing that shape is easy to get wrong in one direction only: minimising fails by quietly
dropping something a tooltip needed. So a shape change is verified by diffing the old export
against the new one rather than by eye, on three invariants that a silent loss would break:

| Invariant | Result |
| --- | --- |
| Every tuning row the old export reached is still reachable, as a named field or a constant | 12,218 rows, 0 lost |
| Every attribute the old export captured still appears in a `scalesOff` | 4,715 attributes, 0 lost |
| Every `{Token}` written in a description has a matching `placeholders` entry | 396 tokens, 0 unresolved |
| Every `icon` names a file that exists in the folder | 1,728 references, 0 missing |

Two of the four earned their keep immediately. The row check caught a talent's second and third
granted abilities being dropped, 421 rows across the augmentation table. The attribute check caught
gameplay effects losing their own scaling: an ability's comes from the effect it applies, but an
effect asked for in its own right captures on itself, and only the referenced ones were being read.

`grep -c '/Game/\|/Script/' data/entities.jsonl data/settings.json` returns 0 for both, which is the check
that no asset path survived anywhere in the export.

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

## The codex

`src/Fellowship.Codex` serves the export as a website and as an API from one process. It
reads everything it draws out of the `icons` blob container, so the container has to be filled
before the app will start:

```powershell
dotnet run --project src/FellowshipExtractor -- usmap

dotnet run --project src/FellowshipExtractor.Publisher

aspire run
```

`FellowshipExtractor.Publisher` is an app host of its own: it starts the same Azurite the codex uses
and runs `export-all` against it, so publishing is one command and needs no connection string on the
line. The extractor resource reads the same `output/fellowship.usmap` the `usmap` command wrote, and
a `FELLOWSHIP_USMAP` set in the shell that starts the host is passed to it as an absolute path, so a
usmap named there is the one the run reads. The extractor resource runs once and finishes, and the host keeps running until it is stopped,
so stop it once the dashboard shows the run has exited. `aspire run` then starts the codex against
the container that run filled.

`export-all --output output/export` writes the same target as a folder instead, laid out exactly
like the container, which is what the test suite reads. Either way the export is a build output and
is not in the repository, so a fresh clone or a fresh worktree has to produce it first.

| Address | What it answers |
| --- | --- |
| `/` | The front page: the roster, every entity type counted, a sample of what is in there |
| `/browse` | Type tabs, the filter rail, the results table. The whole page is the query string |
| `/entity/{slug}/{id}` | One entity in full, with its live tooltip at the top |
| `/not-found` | What answers an address that names nothing. A 404 set while a component is rendering discards the markup that set it, so the message is worked back out of the address here |
| `/api/search?q=` | Ranked name matches across every type, grouped by type with counts |
| `/api/entities?...` | Every entity a filter leaves, paged |
| `/api/entity/{slug}/{id}` | One entity's exported record, every per-hero reading of it |
| `/api/tooltip/{slug}/{id}` | Any entity's tooltip as a self-contained HTML fragment |
| `/api/catalog` | The menus, types and facets the browser is built from |
| `/openapi/v1.json` | The OpenAPI 3.1 description of every route above |
| `/swagger` | Swagger UI, reading that description |

### The description is written from the parsers

`AddOpenApi` is handed two transformers. `ApiInfo` titles the document and counts the entities it
answers. `ApiParameters` fills in the query string every route reads, which the handlers take off
`HttpContext.Request.Query` rather than as bound arguments, so nothing about it would otherwise
reach the document.

What it fills in is read rather than restated. `TooltipQuery.Parameters`, `CodexQuery.Parameters`
and `Suggestions.Parameters` are the one declaration of each route's query string, and both the
parser and the document read it. `TooltipQuery` builds its reserved set from that list, and
`CodexQuery` looks a key up in it before dispatching, so in each the set of names a caller can give
is the set the document publishes and neither can gain a name the other has not.

The filters on `/api/entities` are read off `Codex` as the app is running, so a facet or flag added
there appears in the document with no second edit, and the stat parameters a tooltip accepts are
named in the operation's description from the shipped attribute table.

A test asserts that giving any parameter the document offers changes the reading it produces, so a
parameter that is declared and then ignored fails rather than sitting in the published document.

`{slug}` is a type's name hyphenated and singular - `item`, `trait`, `creature`, `set-bonus-power` -
never the plural a menu label reads, so `/api/tooltip/item/5146` and not `/api/tooltip/items/5146`.

### Static HTML, one island

The site is a Blazor Web App rendered statically. Every page - the menus with their counts, the
filter rail, the results table, the whole of an entity page - is rendered once on the server and
sent as HTML, so it needs no circuit, works with scripting off, and is what a crawler and a reader
both get. `Routes.razor` names no render mode.

The exception is the search box, which cannot be static because it answers between keystrokes.
`Omnibox.razor` declares `@rendermode InteractiveServer` on itself, so one component on the page is
interactive and nothing else is. It reads `SearchIndex` in process rather than calling `/api/search`,
which is the same answer one hop shorter; both go through `Suggestions`, so the dropdown and the
endpoint cannot disagree about ranking, grouping or the cap. Anything the island is handed has to
survive serialization across that boundary, so it takes a string and injects the rest.

The three remaining behaviours that are genuinely the browser's - opening a menu, drawing a hovered
tooltip, and the knobs on an entity page - are one script. Every listener in it is delegated from
`document`, because enhanced navigation replaces the body in place: a handler bound to an element
goes with the element, and the script itself is loaded once and never runs again.

### The browser is a consumer of its own API

Nothing here draws a tooltip. `EntityLink` puts the address of one on every link it renders, and
the script fetches that fragment on hover and paints it into a floating layer - the same endpoint,
the same bytes, that any other caller gets. The entity page's own tooltip is the same path with
knobs on the URL, which is what makes the rarity buttons real: choosing Legendary on `ItemID 5382`
returns the tooltip with the legendary frame, the legendary ornament and the stat slots the rung
grants, because the API worked them out and not the page.

The shape is FellowshipAnalyzer's, which renders fellows.gg tooltips the same way: a data attribute
naming where the fragment comes from, one container, a lazy fetch, an answer cache, and a shadow
root. What differs is the bootstrap - that app is WebAssembly, so a provider component imports the
module through `IJSRuntime`; here the pages are static and the module is loaded by a script tag and
arms itself.

### The page's styles and the tooltip's cannot touch

Both sides are the same shipped design system laid out by the same kind of markup, so they land on
the same class names: `card`, `rule`, `sub`, `tok`, `k`, `v`, `n` all exist in both. A page rule
reaching into a fragment is not a small mistake either, since `.card` is 274px with a nine-sliced
border on one side and a grid tile on the other.

Every fragment is therefore painted into a **shadow root**, through one function both callers share.
Nothing in a page style sheet crosses that boundary in either direction, whatever either side is
called, and that holds for rules nobody has written yet. Checked with four `!important` page rules
written directly at the fragment's own selectors: `.card{width:60px}`, `h3{color:red}`,
`.fx-tip *{display:none}` and `.title{letter-spacing:12px}` all failed to cross, and the card stayed
274px.

Two things sit on top of it, neither load-bearing. Every class the pages use is prefixed `cx-`,
reserved for the pages, which makes the separation legible in the source rather than resting on a
fact about a script; and `codex.css` sits inside `@scope (body) to (.fx-tip)`, which does nothing
today but keeps a fragment rendered straight into the page out of reach of every rule in the file.

What no boundary stops is inheritance - colour, font, letter-spacing and text-transform all cross a
shadow root. That is closed on the other side, in `TooltipRenderer`'s root rule, because it is the
fragment's business to be legible wherever it is embedded rather than every host's business to be
careful with it.

### Nothing in the browser is a list of the game's content

The menus, the filter rail, the type tabs and every count in them are read off `entities.jsonl` at
startup. A facet is declared once, as a name and a selector:

```csharp
new Facet("slot", "Slot", e => One(e.Slot))
```

and that one selector both counts what the export holds under each value and decides whether a row
passes the filter, so a facet can never offer a value that then matches nothing. The rail counts
each facet over the rows *every other* filter leaves, which is what keeps a second slot clickable
after the first one is chosen.

The extractor's own drop rules already keep a row a reader cannot meet out of `entities.jsonl`, so
every one of the 21 types the corpus holds reaches a tab, a menu, a facet and search: there is no
second, narrower list of which types a reader may meet. What `PublicTypes` still curates is only how
a type is spelled and filed - its label, its noun and which column of the menu it sits under, because
nothing in the build groups a trait with an item and nothing in it says which column a reader would
look under.

The details table on an entity page is read off the row's own typed document class by reflection,
matching on the concrete type - `Item`, `Ability`, `SetBonus` and the rest - for the fields only one
of them carries, and a field the page has no section for is drawn as a fact under the words its name
spells out. A field added
upstream therefore shows up rather than disappearing. What is never drawn is a value that is a
shipped spelling - the row's own gameplay tag, its loot rule set, its talent slot - because those
say nothing a printed form says better; the internal *numbers* beside them are drawn in full, which
is what the tuning and substitution panels are.

### The name index

14,478 rows, and one half of what a reader types has to work: the display name. A row's gameplay tag
is the build's internal spelling of it and is shown nowhere, so it is searched for nowhere either,
which takes the term list from 6,313 terms to 2,336 and makes `ga_mara` reach nothing.
`SearchIndex` splits every name into words at build time - including at every capital, so
`PlacedProjectileAoe` contributes `projectile` and `aoe` - and looks one up by binary search over
the sorted term list. Lookups are by prefix, so the box answers while the reader is still typing,
and several typed words all have to match, so a second word narrows.

7,957 of those rows carry no name the game would print and are reachable by no spelling of anything.
1,288 more are named by the entity that applies them, an effect taking its ability's word, and those
score below the entity that lent the name so that typing it reaches the ability first.

### Colour

Every colour on the page is a CSS custom property written out of the shipped design system, and the
panel edge, the header band and the rules between sections are the client's own textures served
from the same container as the icons. One accommodation is made and is the only one: a hero colour
used as text is mixed toward white until it clears a 4.5:1 contrast ratio against the page ground.
The client draws those on lit 3D and on bright panels, where Gunde's `#5A1F20` is legible; on
`#0C1722` it is not. The hue is kept, and a swatch beside the name is still filled with the shipped
value.

### Two app hosts, one Azurite

Two Aspire app hosts share one Azurite instance. `FellowshipExtractor.AppHost` holds the storage
emulator and the codex. `FellowshipExtractor.Publisher` holds the same storage emulator and
`FellowshipExtractor` as a resource, with the container connection string injected by
`WithReference` as `ConnectionStrings__icons` and `export-all` as its arguments, so running it
publishes the build into the container the codex reads.

The emulator carries `WithLifetime(ContainerLifetime.Persistent)` in both, so the container outlives
whichever host started it and the other attaches to the one already running rather than standing up
a second. That only works if both name the same thing, so both compute the volume name, the blob
port, the Docker Compose project and the container name from `StackIdentity`, one file in the
AppHost project that the Publisher compiles as a linked item.

The identity is the checkout root: each host walks up from its own build output directory, the one
`AppContext.BaseDirectory` names, until `Checkout()` finds a `.git` entry, and hashes the directory
holding it. Two hosts whose binaries sit in different `bin` folders therefore agree, and one
checkout gets one volume name and one port offset whichever host starts. A build output directory
with no `.git` above it has no checkout root, and is hashed itself. A worktree's `.git` is a file
pointing at the main checkout's `.git/worktrees/<name>`, and the identity is the directory holding
that file rather than the directory it points at, so every worktree is its own checkout root and its
own stack. The branch the pointer names is what the compose project is called: `fellowship-master`,
`fellowship-sba-topology`.

### One stack per worktree

Several worktrees of this repo are open at once and each runs its own hosts. Every address an app
host claims is global to the machine or to the Docker daemon, and the ports in `launchSettings.json`
are literals every worktree shares, so a second app host fails on
`Failed to bind to address https://127.0.0.1:22186` before any resource starts.

The stack's offset moves every port along by it - the dashboard, its OTLP and resource-service
endpoints, the blob port and the web app's own port - and the containers are filed under one Docker
Compose project named after the checkout, with that project name in the container name as well: a
compose label groups containers, it does not namespace their names, so two worktrees both naming an
emulator `azurite` still collide. The offset is stable across restarts, because a blob URL is baked
into every tooltip fragment already handed out; `FELLOWSHIP_PORT_OFFSET` pins it by hand.

The two hosts read different base ports out of their own `launchSettings.json`, and the ranges an
offset can move them into do not overlap, so neither takes an address the other needs.

Each checkout also gets its own `fellowship-azurite-<hash>` data volume, which nothing removes when
the worktree goes: `docker volume rm` it, or leave it and it will be reused if the worktree comes
back at the same path.

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
  builds the shared indexes once and turns a tag into an `Entity`, so resolving one entity and
  resolving the whole game are the same code, the same setup cost and the same record shape.
  `Entity` is that shape and the only thing either command writes. `PropertyWalk` is the recursive
  property walk everything else is built on, `CurveBindings` turns `FScalableFloat` references into
  resolved constants, `Numbers` sorts those into the named tooltip fields and the rest,
  `TuningNamespace` decides which prefix a row belongs to, `AbilityPresentationIndex` holds the
  `CRAbilityDataRow` join, `ItemSlots` holds the item slot definition join, `GameplayEffects` reads
  an effect for its scaling, `DescriptionTokens` follows a `{Token}` to the effect behind it,
  `Spread` works out the band a coefficient rolls in from the ability's own tuning or the effect's
  calculation preset, and `Settings` writes the shared lookup tables beside the entity stream.
- `src/Fellowship.Codex/` - the codex. `EntityStore` reads the export once; `Codex` declares
  the facets, flags and measures the whole browser is driven by and counts them off that; `SearchIndex`
  is the name index behind the omnibox and `Suggestions` is the one answer both it and `/api/search`
  give; `CodexQuery` parses a query string against the facets, `CodexSearch` runs it and `BrowseView`
  turns the result into the links the results page draws; `TooltipRenderer` draws one entity the way
  the game draws it, and `CodexView` is what the pages ask for a URL, a colour, a tooltip address or
  a piece of the game's own text so no page spells any of those out twice.
- `src/Fellowship.Codex/Components/` - the pages. `App.razor` is the document and writes the
  design system out as custom properties; `Layout/` holds the masthead, the mega menu and the one
  interactive component; `Pages/` holds the four routes; `Shared/Detail.razor` is every field of an
  entity, recursive so a granted ability and a legendary roll are drawn by the same component, and
  `Shared/EntityLink.razor` is the only place a link to an entity is built.
- `src/FellowshipExtractor.AppHost/` and `src/FellowshipExtractor.Publisher/` - the two app hosts.
  `StackIdentity.cs` sits in the first and is compiled into the second as a linked item, so the
  stack name, the port offset, the data volume, the compose project and the container name have one
  implementation and both hosts reach the same Azurite.

## Known limits

Two lightmap `.ubulk` files exceed 2 GB (`SandTomb_Environment_BuiltData.ubulk`,
`ForestMeadow_Environment_BuiltData.ubulk`) and overflow CUE4Parse's single-array read. `raw`
reports them and continues.
