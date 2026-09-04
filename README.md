# Fellowship Data Extractor

Console app that reads Fellowship's cooked UE5 containers and exports game data as JSON/JSONL.

Fellowship (Chief Rebel AB) is built on an Unreal Engine 5 source fork, installed by default at
`C:\Program Files (x86)\Steam\steamapps\common\Fellowship`. The game install is read-only input;
everything this tool produces lands in `output/`.

## Running

```powershell
dotnet run --project src/FellowshipExtractor
```

With no command that writes the whole export, described under [Everything at once](#everything-at-once):
`output/` unless a remote store is named. It holds one `export-<build>` folder per build, where
`<build>` is the Steam build id, and one `ui/` folder of art shared by all of them.

| Argument | What it does |
| --- | --- |
| `--usmap` | Recovers the mappings first, and stops before exporting if they come back short |
| `--output <path>` | Writes a folder laid out like a store, `output/` by default |
| `--connection-string <value>` | Writes into an Azure Blob container, also read from `ConnectionStrings__icons` |
| `--bucket <name>` | Writes into an S3 bucket, also read from `S3__Bucket` |

| Command | What it does |
| --- | --- |
| `usmap` | Recovers the mappings without exporting |

| Environment variable | Effect |
| --- | --- |
| `FELLOWSHIP_USMAP` | Path to a `.usmap` supplying property mappings. Unset, the extractor reads `mappings/fellowship-<build>.usmap`, the file the `usmap` command writes |
| `FELLOWSHIP_BUILD` | Names the build the export folder is filed under. Unset, the Steam `appmanifest_*.acf` whose `installdir` matches the game install supplies it |
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

## The tables that read with no mappings

Four sources parse straight out of the containers without a `.usmap`, and the readers that hold
them are what every later stage joins against. They stay in memory rather than reaching disk:

| Reader | Records | Source |
| --- | --- | --- |
| `AssetRegistry.ReadAssets` | 55,433 | `AssetRegistry.bin` - package path, asset class, cooked tags. DataTable rows carry their `RowStructure`, so this is the index for locating content. |
| `CsvTables.ReadTags` | 13,088 | `Content/guid/*.csv` - every item, ability, effect, talent and trait name with its network id, across 18 tables. |
| `CsvTables.ReadConstants` | 13,156 | `RawDataTables/**/*.csv` plus the GameFeature plugin copies - per-hero, per-dungeon and per-item tuning values. |
| `LocalizedText` | 14,273 | `Game.locres` - display strings under namespaces including `ST_Items`, `ST_Abilities`, `ST_Gems`, `DT_HeroItems`. |

The tag table breaks down as 5,787 `ItemID`, 3,261 `GameplayEffect`, 2,307 `GameplayAbility`,
772 `CRCharacterAugmentationAsset`, 338 `CRAICharacter`, 63 `ItemTrait.ID`, 40 `Talent.ID` and
the remainder across 11 smaller tables.

Constants CSVs use a `---,1,2,3,4` header to declare tier columns; rows with more than one value
carry every column, keyed by its label. Two rows in the whole set hold non-numeric text (`1.5s` and
a `DR` placeholder) and keep the raw string.

### Joining a tag to its asset

Cooked assets advertise their tag as `PrimaryAssetName`, which is the exact join. Gameplay
ability and effect blueprints instead carry the tag as their asset name, so the resolver falls
back to `AssetName` and skips any name that more than one asset claims.

Of 13,088 tags: 1,022 join on `PrimaryAssetName`, 4,743 on `AssetName`, 7,323 have no standalone
cooked asset. The join produces only ten table-to-class pairings, all coherent - `GameplayAbility`
to `GameplayAbilityBlueprint`, `GameplayEffect` to `Blueprint` under `CRGameplayEffect*` parents,
`ItemID.VisualCollection.*` to `CRVisualCollection`, and so on.

The 5,200 unmatched `ItemID` tags are the DataTable-driven items - hero gear, gems, currencies -
which live as table rows rather than standalone assets. The readers walk table rows, so these are
reachable through the tables that hold them.

Localization is exported but not joined. `ST_Abilities` keys such as
`Abilities_HeroAbility_Gunde_SingleTargetInterrupt_Description` only match tag names for about
20% of entries under any normalization, and the authoritative link lives inside the ability
assets. Joining display text to entities needs mappings.

## Property mappings

Cooked packages use unversioned property serialization and the build ships no `.usmap`. A sample of
302 packages spread across the whole content set parsed 0 and raised `MappingException` 302 times.
Fellowship is not present in TheNaeem's Unreal-Mappings-Archive (checked 2026-08-01).

Reading `.uasset` exports needs a mappings file. `FELLOWSHIP_USMAP` names one, and with it unset the
extractor reads `mappings/fellowship-<build>.usmap`, which is where the `usmap` command writes the mappings it
recovers, so a checkout that has generated them needs no environment at all. A run that finds
neither stops before it opens a package and names the command that writes them, rather than reading
every table back empty. That mappings file is what gates the per-asset half of the database: `GameplayAbilityBlueprint` (1,602),
`DataTable` (528), `CRDynamicHeroItemDefinition` (237), `CRVendorItemDataAsset` (145),
`CRMountData` (54), `CRItemStatWeightTemplate` (20), `CRGameplayAbilitySet` (16),
`CRTalentData` (12) and `CRTraitTreeGraph` (6).

## Static usmap generator

`src/FellowshipExtractor/Usmap/` recovers UnrealHeaderTool's generated reflection tables directly
from `fellowship-Win64-Shipping.exe`, with no process involvement. The `usmap` command, and the
`--usmap` flag on an export run, writes `mappings/fellowship-<build>.usmap`, round-trips it through
CUE4Parse, and fails if the round-trip resolves no schema for a type the export reads.

Layouts established empirically against this binary, not assumed:

- `FPropertyParamsBase` in `.rdata`, 56 bytes: `+0x00` name, `+0x08` RepNotify, `+0x10` CPF flags,
  `+0x18` `EPropertyGenFlags`, `+0x1C` object flags (always `0x45`). Variants append a type
  function pointer at `+0x38`. **70,438** records recovered, and every one is referenced by some
  `PropPointers[]` array, so the scan has no gaps.
- `FStructParams` in `.data`, 56 bytes: `+0x00` outer, `+0x08` super, `+0x10` struct ops,
  `+0x18` name, `+0x20` property array, `+0x28` u16 count, `+0x2A` u16 sizeof, `+0x30` object flags.
- `FEnumParams` in `.rdata`, 48 bytes: `+0x00` outer, `+0x08` display-name func, `+0x10` name,
  `+0x18` C++ type, `+0x20` enumerator array, `+0x28` object flags, `+0x2C` **u16** count,
  `+0x2E` enum flags, `+0x2F` cpp form. The count is two bytes where `FStructParams` puts four,
  so reading it as a u32 finds nothing at all. Each enumerator is 16 bytes: a UTF-8 name and an
  `int64` value, and the name is scoped (`ECRAbilityCategory::Major`), which is what confirms a
  candidate record belongs to the enum whose name sits at `+0x10`.
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

Note that a package parsing is only a gate, not a validation: CUE4Parse checks that a mappings
container is present, so any syntactically valid usmap passes it. The comparisons above are what
actually demonstrate correctness.

The enum table carries **2,276 enums and 12,333 members**, read from the `FEnumParams` records the
same way the structs are, so an enum-typed property deserializes to `ECRAbilityCategory::Major`
rather than to a bare `4` under whatever type name a mis-resolved constructor happened to land on.
2,789 properties name an enum, 751 of them `TEnumAsByte` bytes that now spell as an `EnumProperty`
with a `ByteProperty` inner. `EnumOrdinals` reads the same table back the other way, so a reader
that wants the ordinal still gets it.

## Images

A cooked texture is block-compressed GPU data, so the path an asset holds is not an image.
`TextureExporter` decodes them, and the export run draws every one it needs. Across the 198 packages
under `ui/icons/hero_abilities` the formats break down as:

```
198 package(s) match 'ui/icons/hero_abilities'; writing Png
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

Every icon is written to `ui/` under its own texture name and recorded under `icon`. Naming an icon
after its own texture rather than after the entity that asked for it means a texture several
entities share is decoded once and they all agree on the file.

## Reconstructing a tooltip

`EntityResolver.Resolve` assembles everything the game shows for one entity from its gameplay tag.
`GameplayAbility` 1273 comes back as:

```json
{
  "table": "GameplayAbility",
  "netId": 1273,
  "name": "GA_Firemage_CastedPlacedAoe",
  "displayName": "Apocalypse",
  "tags": ["Aoe", "Casted", "Magic.Fire", "Major", "Ranged", "Skill.Offensive"],
  "description": "Conjure a major eruption of fire in the targeted area, dealing <rt.warning>{ApocDirect}</> fire damage and applying <rt.absorb>Searing Blaze</> to all enemies in a large radius.",
  "icon": "Firemage_Apocalypse.png",
  "category": "Major",
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

That is the resolver's own record. `DocumentProjection` turns it into the document the export
writes, and the two differ where it matters most: the record's `name` is the build's spelling of the
row, while the exported document's `name` is the display name and nothing else.

The rule is everything the tooltip needs and nothing else. **A reference to another asset is never
emitted as a path**, because a class name reconstructs nothing: either the value behind it is read
and folded in, or it is left out. So the five gameplay effect classes Apocalypse names are gone and
the one number among them is `Damage.IntellectCoefficient`; the `{ CurveTable, RowName }` handles
are gone and the rows they point at are the constants; an enum-valued field is the member's name,
which is what the usmap's enum table is for.

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

1. The tag names an asset, joined on `PrimaryAssetName` and then on `AssetName`.
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
keyed by a hero gameplay tag, so `ItemID` 5224 resolves to five items:

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

Every variant is resolved together. An id names one item however many things the game shows for it,
so `ItemID` 5224 comes back as all five with their own names, abilities and ability constants, and
the item's own `DisplayName` stays the placeholder it is rather than one variant's name standing in
for the rest.

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

The codename is extraction vocabulary and stops at the export boundary. Every document, every key
and every settings row is written under the shipped name, and `HeroNames` is the one place the two
spellings meet.

The icon is shared: these items declare a single `DefaultIcon`, not one per hero.

One limit worth stating: the displayed damage range needs a stat value the caller supplies. The
export gives the coefficient, the attribute it captures and the `spread` the roll uses, so
everything but the player's own Intellect or Strength is there.

### Items with no hero dimension

A relic is the other shape: usable by every hero, one name, one ability. `ItemID 5252` is
`Ancient Poultry Fetish`, which `ItemID` 5252 resolves to:

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

The default run writes one JSONL line per gameplay tag with every icon beside it, through the same
`EntityResolver.Resolve` the section above walks through for one entity. Naming an icon after its
own texture means a texture shared by many entities is decoded once:

```powershell
dotnet run --project src/FellowshipExtractor
dotnet run --project src/FellowshipExtractor -- --usmap
```

Every run covers the whole build, with or without the mappings step.

Three arguments name the destination. `--output <path>` writes a store-shaped folder to that path,
and a run that names no remote store writes `output/` even without it, filing its documents under
`export-<build>`, where `<build>` is the Steam build id of the install the assets came from.
`--connection-string <value>` writes every document,
setting and image straight into an Azure Blob container instead, leaving nothing on disk; the value
is taken from the `ConnectionStrings:icons` configuration key when the argument is absent, which is
the key the Publisher app host injects, so a run under it needs no argument at all and writes no
export folder. `--bucket <name>` writes the same into an S3 bucket, read from the `S3__Bucket`
environment variable when the argument is absent; the bucket's region and the account's credentials
come from the AWS SDK's own configuration chain. Naming more than one destination writes each in one
pass over the build.

Both remote destinations, and the read the codex does, go through one interface. `IStorage` in
`src/Fellowship.Storage` names a store of objects: it lists them, reads one, prepares the store for
writing, writes one, and addresses one for a browser. `AzureStorage` implements it over an Azure Blob
container, and `S3Storage` over an S3 bucket. The extractor picks the implementation from the
destination argument, and the codex picks it from configuration: `ConnectionStrings:icons` names the
blob container, `S3:Bucket` names the bucket, and the connection string wins when both are set.

`--usmap` recovers the mappings before the pass and stops the run if the round-trip resolves no
schema for a type the export reads, so an export is never written from mappings that would read
their tables back empty.

A folder run finishes by copying `export-<build>/settings.json` to `src/Fellowship.SDK/`, which is
the copy the source generator turns into the enumerated types the documents are read against.

A run that writes a store uploads every image as it draws it, and publishes `entities.jsonl` and
`settings.json` together once the pass has both. A pass that throws part way publishes neither,
so a throw during the export leaves the store on the pair the last finished pass left.

Either destination is the same layout:

```
<root>/export-<build>/entities.jsonl
<root>/export-<build>/settings.json
<root>/ui/<texture>.png
```

On disk `<root>` is `output/`, and in a store it is the store root. Each build files its two data
objects under its own `export-<build>` folder, and every build draws on the one `ui/` beside them, so
a root holds every build's documents side by side against one set of art. A later build overwrites
the art it redraws under the name it redraws it as. The codex loads all of them, builds the site from
the highest build, and renders what changed between adjacent versions on every entity page.

The run reports what it read: 12,649 documents from 12,648 entities across 13,094 tags, 7,200 with a
display name and 6,676 with an icon; 2,064 effects named as part of one of 2,306 tagged abilities,
1,580 of them carrying a role; 965 names and 952 icons inherited from the entity that applies them,
51 and 8 withheld because two entities that apply them disagree; 12,803 tuning rows supplied by
namespace that no asset names; 26 coefficient rows banded by the `Spread` row their effect overrides
with and 111 banded by their effect's calculation preset.

It reports what it drew too: 146 chrome images, which is the chrome textures plus the rarity
variants, four textures multiplied by each of the seven rarity colours, plus the modifier row
textures, each multiplied by the colour row the row draws it in; and 4,582 entity icon images, 3,150
of them an icon composed against a rarity colour. `WriteImageAsync` never clears `ui/`, so a folder
several runs old holds more than one run wrote; this one ends at 4,736 files and 335 MB, the whole
of the transform work done once so that no consumer composes anything at render time.

Every line is the document the projection writes for one entity, one shape per type. The resolver's
record carries the build's own spelling of the row and the projection writes none of it: what the
row is for reaches the reader as `role`, and the ability it belongs to as `partOf`. 4.7 MB for the
whole game.

### settings.json

The one file beside `entities.jsonl` that is not an entity. 62,466 bytes of lookup tables that no
entity line can carry, written by the same run so the two always describe the same
build. Three of them are below; the rest are the design system's named colours, the rich text style
set and the stat calculation settings, which is what lets a renderer draw a tooltip in the game's
own colours without reading the build itself:

```json
{
  "heroes": [
    { "name": "Ardeos", "armorType": "Cloth",
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
through a table saying Ardeos is Cloth and Intellect. `name` is what every tooltip prints and the
only spelling the export uses. `color` is six hex digits from the design system row the
hero's metadata asset points at, followed rather than the `ClassColor` literal beside it because the
two disagree: Aeona's asset says `BC00FF` while the row it names is `FC9FEC`. The `colors` table
keys each hero row `game.game.hero-<name>`, and `sectionHeadingRows.Ability` names one of those
rows, so the two move together. `difficultyScaling.namespaces` is keyed the same way.

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
entity's own `schools` says which school. All three are needed because the flag is shipped on two
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

`grep -c '/Game/\|/Script/' entities.jsonl settings.json` returns 0 for both, which is the check
that no asset path survived anywhere in the export.

The second check is that no hero codename did. `grep -c '"tag":' entities.jsonl`,
`grep -c '"codename":' entities.jsonl` and `grep -c '"codename":' settings.json` all
return 0, and
`grep -o -i -E 'bowguy|firemage|ink|lisa|mosse|sune|vigor|warmaster' settings.json | wc -l`
returns 0 because settings.json holds no exempt field. `entities.jsonl` cannot be checked that way:
1,498 `icon` and 7,238 `iconByRarity` values are cooked texture filenames that carry a codename by
design, and `Venomdrinker`, `Invigorate` and `Blood Link` carry a codename as a substring.
A `placeholders[].key` is renamed together with the `{token}` its description splices it into, so
the pair stays byte-matched and neither carries a codename. What is left after
those exemptions is four shipped sentences the game itself wrote, which name a hero by the spelling
the player does not read: two descriptions naming Sune and Mosse, the Rune of Renewal flavour line
that writes both `Vigour` and `Vigor`, and the player title `The Warmaster`.

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

`src/Fellowship.Codex` serves the export as a website and as an API from one process. It reads
everything it draws out of an object store, so the store has to be filled and named to the codex
before the app will start. The store is an Azure Blob container or an S3 bucket, and the codex reads
whichever configuration names:

- `ConnectionStrings:icons`: an Azure Blob container, `icons` unless the string names another
  through `ContainerName=`. This is the key the app host injects from Azurite, so a run under
  `FellowshipExtractor.AppHost` needs no configuration of its own.
- `S3:Bucket`: the bucket the codex reads, which is how the deployed container names its store.
- `AWS:Region`: the bucket's region, read from `AWS_REGION` in the environment when the key is absent.
- `S3:PublicUrl`: an absolute URL that art links use in place of the bucket's own S3 URL, for a
  content delivery network or a custom domain. Optional.
- `S3:ServiceUrl`: an S3-compatible endpoint such as MinIO, which turns on path-style addressing.
  Optional.

The connection string wins when both name a store, so a machine holding AWS credentials still reads
its own Azurite. The codex refuses to start when neither is set, naming both keys.

Fill the store by running the extractor against it:

```powershell
dotnet run --project src/FellowshipExtractor -- usmap

dotnet run --project src/FellowshipExtractor -- --bucket BUCKET_NAME
```

Replace `BUCKET_NAME` with the S3 bucket the codex reads. The bucket's region and the account's
credentials come from the AWS SDK's own configuration chain: environment variables, a shared
credentials file, or an instance role. The run reads the same `mappings/fellowship-<build>.usmap` the
`usmap` command wrote. `FellowshipExtractor.Publisher` runs the same pass against the app host's
Azurite instead, which is what fills the container the codex reads locally.

A run with no remote store named writes the same export as a folder instead, laid out exactly like a
store, which is what the test suite reads. Either way the export is a build output and is not in the
repository, so a fresh clone or a fresh worktree has to produce it first.

| Address | What it answers |
| --- | --- |
| `/` | The front page: the wordmark and the search box, with the menus in the masthead |
| `/spells`, `/items`, `/dungeons` | One category: its type tabs, the filter rail, the results table. The whole page beyond the category is the query string |
| `/search?q=` | The same page across every category, for a search that crosses them |
| `/entity/{slug}/{id}` | One entity in full, with its live tooltip at the top |
| `/not-found` | What answers an address that names nothing. A 404 set while a component is rendering discards the markup that set it, so the message is worked back out of the address here |
| `/api/search?q=` | Ranked name matches across every type, grouped by type with counts |
| `/api/entities?...` | Every entity a filter leaves, paged |
| `/api/entity/{slug}/{id}` | One entity's exported record, every per-hero reading of it |
| `/api/tooltip/{slug}/{id}` | Any entity's tooltip as a self-contained HTML fragment. `?mobile` wraps it in the chrome a phone draws it in |
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

The behaviours that are genuinely the browser's - opening a menu, the drawer a phone opens the menus
in, drawing a hovered tooltip, the card a phone opens on tap, adding a filter to the rail and folding
the rail on a phone, and the knobs on an entity page - are one script. Every listener in it is delegated from
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

A phone has nothing to hover, so a tap on a link that carries a tooltip address opens the card in
place of following the link. The script fetches the same address with `mobile` added, and the
endpoint answers the card wrapped in the chrome a phone draws it in: a close button pinned to the
top right, the card centred in the screen, and a bar at the foot that links to the entity's own
page. A card taller than the screen scrolls, and the bar follows it below the fold. The chrome's
rules ride inside the fragment's own style element, so it is still one self-contained fragment
painted into the same shadow root, with the layer switched to fill the viewport and the foot link
pointed at the tapped link's own address. Dismissal is one attribute: the close button and the
space around the card carry `data-dismiss`, and whoever painted the fragment removes it when a tap
lands on an element that carries it. Any other caller gets the same fragment by adding `mobile` to
the tooltip address, given bare or as `mobile=true`, and needs only that one listener.

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

The rail keeps only a type's primary facets in view, which is what `Primary` on a facet declares:
slot, hero and rarity for an item, hero for an ability, gem type for a gem. Every other facet,
every flag and every measure the results carry is a row behind the `Additional filters…` select,
drawn with its values but hidden until it is picked, and a `remove` button hides it again. A row the
query already states draws open, with a `remove` link that clears it from the query. A flag no row
of the results meets is absent, as is a measure no row carries. On a phone the rail sits
above the results, folded under its head, and the head's badge counts the filters in force. A count
of rows is stated once, beside the pager that sits above the table and again below it; a search
counts each type on its tabs. Nothing else counts anything.

### Three categories are what a reader navigates by

A reader meets Spells, Items and Dungeons, and every entity type names one of the three. That is what
`Category` on a `PublicType` declares, and it is the only thing that decides a route: `/spells`,
`/items` and `/dungeons` are one page taking the category out of its own address, so adding a fourth
category adds a route without touching the page. A type is a tab within its category rather than a
route of its own - `/spells?type=relic-power` lists the relic powers, and a relic power's own page is
`/relic-power/{id}`. Landing on a category with no tab taken draws every type it holds in one table
of art, name, type and source.

The menu drills the same way: category, then type, then the facets that type declares in `Drill`.
Items drill their kind and then their slot, the shape a reader knows from an armoury; a level whose
facet offers fewer than two values is skipped rather than drawn empty.

The extractor's own drop rules already keep a row a reader cannot meet out of `entities.jsonl`. What
`PublicTypes` curates is how a type is spelled, which category files it, what it drills through, and
whether a reader meets it at all: a talent slot, a mount power, a modifier, a hero, a legendary stat
and a gem type each carry facts a page renders and none is an entity a reader browses to. A gem type
is a value of the gem facet, and a legendary stat is read inline off the item that rolls it.

The details table on an entity page is read off the row's own typed document class by reflection,
matching on the concrete type - `Item`, `Ability`, `SetBonus` and the rest - for the fields only one
of them carries, and a field the page has no section for is drawn as a fact under the words its name
spells out. A field added
upstream therefore shows up rather than disappearing. What is never drawn is a value that is a
shipped spelling - the row's loot rule set, its talent slot - because those
say nothing a printed form says better; the internal *numbers* beside them are drawn in full, which
is what the tuning and substitution panels are. The row's own gameplay tag is not among them because
the export no longer carries it. What the row is for reaches the page as `role` and `partOf`.

### The name index

14,478 rows, and one half of what a reader types has to work: the display name. A row's gameplay tag
is the build's internal spelling of it and the export does not carry it, so the index has nothing to
leave out. `SearchIndex` reads display names and nothing else, and `ga_mara` reaches nothing.
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
`WithReference` as `ConnectionStrings__icons`, so running it publishes the build into the Azure
container through the extractor's connection-string target and writes no folder of its own. This is
the publisher path, and the codex reads the container it fills: `FellowshipExtractor.AppHost` injects
the same `ConnectionStrings__icons` into the codex, which reads the blob container in place of the
S3 bucket the deployed container reads.

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

### Deploying the codex

The codex runs in production as one Docker Compose service behind Caddy, reading the export from
the `fellowship-codex` bucket and serving art through CloudFront at
`cdn.codex.fellowshipanalyzer.com`. Merging to `master` deploys it:
`.github/workflows/deploy.yml` builds `src/Fellowship.Codex/Dockerfile`, pushes the image to the
GitHub container registry and starts it on the instance. `deploy/` holds the compose file, the
CloudFormation stack, the provisioning script and the scripts the workflow runs on the server, and
[docs/deploying-the-codex.md](docs/deploying-the-codex.md) is the runbook.

## The MCP server

`src/Fellowship.Codex.Mcp` gives an AI agent the same export over MCP, with three tools:
`list_types` says what the build carries, `find_entity` turns a name into a type and an id, and
`get_entity` returns that entity's record. It speaks stdio, reads `output/export-<build>` off disk through
`FolderExportSource`, and shares every reading service with the codex through
`CodexServices.AddCodexServices`. Neither the AppHost, Azurite nor the codex needs to be running.

`.mcp.json` at the repository root registers the built executable itself,
`src/Fellowship.Codex.Mcp/bin/Release/net10.0/Fellowship.Codex.Mcp.exe`, with no arguments. It needs
two things in place:

```powershell
dotnet build src/FellowshipExtractor.slnx

dotnet run --project src/FellowshipExtractor
```

The solution is what to build, not the project: `FellowshipExtractor.slnx` carries
`<BuildType Project="Release" />` on this one project, so a Debug solution build still writes the
`bin/Release/net10.0` the entry names. `dotnet build src/Fellowship.Codex.Mcp` on its own writes
Debug and leaves the registered executable at whatever it last was.

Naming the executable rather than `dotnet run` keeps the wire clean. `dotnet run` builds first and
MSBuild writes its diagnostics to stdout, which is the JSON-RPC wire, so a build that cannot write
its output - what happens while the codex holds this checkout's `bin` - puts MSB3026 warnings where
the client expects protocol. An executable builds nothing: stdout carries protocol from the first
byte, every log line goes to stderr, and a server that was never built fails to launch instead of
answering with a parse error. The cost is that a change reaches the client only after a rebuild.

The registered path is relative, so a client launched from a worktree runs that worktree's own build
against that worktree's own export rather than another checkout's.

The server finds its export in this order: an `--export <folder>` argument, then
`FELLOWSHIP_EXPORT`, then the nearest `output/` above the working directory that holds an
`export-*`, then the nearest one above its own binary. Within an `output/` it takes the highest
build id carrying both `entities.jsonl` and `settings.json`, so several builds can sit
side by side and the newest wins. The two ancestor walks make the registered entry portable across
checkouts: the working directory covers a client started at the repository root, and the binary's
own location covers one started anywhere else. `--export` and `FELLOWSHIP_EXPORT` name a folder
outside both, and are the only way to read a build other than the newest.

Two things read differently over MCP than over HTTP. A reference's `resource` is the codex API's
own path with no host, `/api/{slug}/{id}`, because a stdio server has no request to take a scheme
and an authority from. And every art field names an image file, `Icon_Warmaster_FkPlate_Chest.png`,
rather than addressing one in a store, because there is no store to address. Both
fall out of what the host registers: `NamedArtSource` in place of `StorageArtSource`, and an empty
origin in place of `Origins.Of(http)`.

Errors are results rather than protocol faults. Every tool answers a bad slug, an unknown hero, a
missing id or an id that reads differently per hero with `isError` and the same sentence the HTTP
route puts in its problem detail, which `Lookups.Trouble` and `Lookups.NoHero` now state once for
both. `McpTests` holds the two wordings equal.

## Layout

- `src/FellowshipExtractor/ProviderFactory.cs` - the single place that knows about the install path,
  engine version, Oodle and mappings. Every reader takes the provider it builds.
- `src/FellowshipExtractor/GameBuild.cs` - reads the Steam build id from the `appmanifest_*.acf`
  whose `installdir` matches the game install, which is what names the export folder.
- `src/FellowshipExtractor/Extraction/` - the readers and the export. `EntityResolver`
  builds the shared indexes once and turns a tag into an `Entity`, so resolving one entity and
  resolving the whole game are the same code, the same setup cost and the same record shape.
  `Entity` is that shape, and `DocumentProjection` turns it into the typed document the export
  writes. `PropertyWalk` is the recursive
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
- `src/Fellowship.Codex.Mcp/` - the MCP server. `Entry` builds a plain host with no web pipeline,
  `CodexMcp` registers the codex's reading services over a folder export and names the server, and
  `CodexTools` is the three tools an agent calls.
- `src/Fellowship.Storage/` - the object store the extractor writes and the codex reads. `IStorage`
  names a store of objects; `AzureStorage` implements it over an Azure Blob container and `S3Storage`
  over an S3 bucket; `AddStorage` registers the blob one from `ConnectionStrings:icons`, or the S3
  one from the `S3` and `AWS` configuration keys.
- `src/FellowshipExtractor.AppHost/` and `src/FellowshipExtractor.Publisher/` - the two app hosts.
  `StackIdentity.cs` sits in the first and is compiled into the second as a linked item, so the
  stack name, the port offset, the data volume, the compose project and the container name have one
  implementation and both hosts reach the same Azurite.

## Known limits

Two lightmap `.ubulk` files exceed 2 GB (`SandTomb_Environment_BuiltData.ubulk`,
`ForestMeadow_Environment_BuiltData.ubulk`) and overflow CUE4Parse's single-array read. Nothing the
export reads touches them.
