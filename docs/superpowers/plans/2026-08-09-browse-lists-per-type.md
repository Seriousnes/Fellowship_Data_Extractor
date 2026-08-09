# Browse lists per type Implementation Plan

**Goal:** Every entity type the codex lists is drawn by a component of its own, over a shared table
that takes a list of `T` and binds each column either to a property of `T` or to a template. A
browse page shows one type at a time, chosen by a tab strip whose order the code states. An item
row is one reading of an item, so the hero-named versions of an id stand as their own rows and
carry their own armour, set, art and item level. An item says where it comes from and what level it
arrives at, and an ability or effect says whose it is, because the extractor writes all four onto
the documents. The omnibox keeps its view-results button on screen whatever the result count.

**Architecture:** `Cx/Table.razor` is a generic `Table<T>` that renders `<table class="cx-rows">`
and cascades itself to `Cx/Column.razor`, a `Column<T>` that renders nothing and registers itself
with the table. Registration completes before the header draws because the table places its
`Columns` fragment in one `<CascadingValue>` and the table markup in a sibling `<CascadingValue>`,
so the first cascade's children are instantiated before the second cascade renders. That holds
under static server rendering with no second pass and no interactivity, which is what this app
uses. Each type's component supplies the columns; `Browse.razor` picks the component and owns the
tabs, rail, count and pager as it does today.

Row identity moves from `(EntityType, Id)` to the exported document. `CodexSearch.Fold` becomes a
per-document projection, and every count, badge and link that deduped by id follows it. A row
addresses its own reading with the `hero` query parameter that `EntityPage` already resolves.

Four facts move from inference to extraction: an item's sources, an item's level, an ability's
owner and an effect's owner. Each is read from the asset that declares it and written onto the
document, so the Api reads a field instead of guessing.

**Tech Stack:** .NET 10, C# 14, Blazor static server rendering, xunit.v3 with bunit in
`src/FellowshipExtractor.Api.Tests`, xunit 2 in `src/FellowshipExtractor.Tests`, SCSS compiled to
CSS by AspNetCore.SassCompiler, CUE4Parse in the extractor.

## Global constraints

- .NET 10, C# 14. `net10.0` everywhere except `Fellowship.SDK.Generators`, which stays
  `netstandard2.0` and takes no polyfills.
- **Add no comments to any file.** Not explanatory ones, not section markers, not doc comments.
- No em dashes or en dashes anywhere: code, XML, output, commit messages.
- Never hardcode a list, id or data fact a canonical source already states. `store.Vocabulary` is
  canonical for heroes, rarities and colours; `EntityTypes.Slug` is the only place a slug is
  spelled; the enums in `Fellowship.SDK` are generated from `vocabulary.json`.
- Every hardcoded ordering is verified against the enum it orders, so adding an `EntityType`
  fails the build until the new member is placed.
- Match the surrounding idiom: expression-bodied members, collection expressions, pattern matching,
  `StringComparer` named explicitly, `IReadOnlyList<T>` on document properties.
- Stylesheets are written as `.razor.scss`. `*.css` is generated and gitignored.
- Test naming: snake_case sentences in `FellowshipExtractor.Api.Tests` with `[Fact]` only,
  PascalCase sentences in `FellowshipExtractor.Tests`.
- The Api tests find the export by walking up from the test binary for `output/export`. This
  worktree has no `output/`, so run them with
  `FELLOWSHIP_EXPORT=G:/source/Fellowship_Data_Extractor/output/export` until this branch's own
  export is written.
- This checkout is shared with other sessions. Read a file as it stands before editing it. Never
  run `git stash`.

## What the build states

Measured on 2026-08-09 against `output/export/entities.jsonl` (12,473 documents over 10,321 ids),
`output/fellowship.usmap` (9,740 schemas), and the dumped assets under `output/dump/fellowship`.

### Rows and readings

- **Only two types have more than one document per id.** `item` is 5,744 documents over 3,789 ids,
  and `talent-slot` is 216 over 19. The other twenty types are one document per id, so their row
  counts, tab counts and facet counts are unchanged by drawing one row per document.
- **A reading is addressable by hero and by nothing else.** For all 188 multi-reading item ids and
  all 19 multi-reading talent-slot ids, `EntityStore.Wearer(document)?.Name` is distinct across the
  readings, and the round trip through `EntityPage.OnParametersSet` returns the originating
  document for all 2,143 item rows and all 216 talent-slot rows.
- **`CodexView.Href` drops the hero while `CodexView.Tip` keeps it**, so today an ambiguous row's
  tooltip previews one reading and its click opens another. One hero-aware `Href` settles both.
- **Item rows stay distinct on the hero alone.** Narrowing the drawn cells over the 188
  multi-reading ids leaves 86 ids indistinguishable by name, icon, slot, armour and stat together,
  and 0 once the hero is drawn.
- **Talent-slot rows are 196 duplicates out of 216** in the cells drawn today, because
  `TalentSlotDocument` has no icon and only one of 216 carries a name. Its `Hero` is the column
  that separates them.
- **`SearchIndex` is already per document.** It indexes `store.Entities` one entry per document and
  returns one hit per matching document, so free-text browse needs no index change.

### Item level

- **No item definition states an item level.** `CRHeroItemDataRow` has 29 serialized properties and
  `CRDynamicHeroItemDefinition` has 26, and neither carries one. Item level is a property of the
  granted instance (`CRReplicatableGenericItem.ItemLevel`, `CRItemViewModel.ItemLevel`) and is set
  by whatever grants the item.
- **A granting droptable group states the level it grants at.**
  `CRSimpleDroptableHeroItemGroup.ItemLevel` and `CRSimpleDroptableDynamicHeroItemGroup.ItemLevel`
  carry it. All twelve `*VendorStarter` droptables state 15, all twelve `*VendorLeague1` state 75,
  all twelve `*VendorLeague2` state 135. The starmap ladder is 30/45/60/60 Contender,
  105/120/135 Adept, 180/195/210 Champion, 255/270/285 Paragon, 120 LeftPathWeapon, and
  `SDT_HeroPath_LegendaryItem` states 315 with the display name "Legendary ilvl 315".
- **A dungeon drop takes the curve.** `CT_DungeonDifficultyToDropItemLevel` row `Default` runs
  15 at difficulty 0 to 315 at 21; row `Legendary` runs 75 to 360 at 21. The row is chosen by
  `DT_DroptableGroups.ItemLevelCurveNameOverride`, which is `None` on `DefaultGearCheck` and
  `Legendary` on `LegendaryDropCheck`.
- **The lowest item level the build states is 15.** The hero's 66 default-outfit rows state no
  level, and need none: they carry `PrimaryStatsWeightsTemplate` "None" and empty stat weight maps,
  so they print no stat lines at any level, and both stat pools floor at `minimumItemLevel` 15.
- **`DefaultGame.ini` bounds it**: `MaxItemLevel=420`, `ItemLevelsPerUpgrade=15`, so both 315 and
  360 reach 420 by upgrades.

### Item source

- **A dropped item names its dungeons.** `CRDynamicHeroItemDefinition.DropsInSpecificDungeons` is a
  list of `Level.ID.*` tags and `DropsInAllDungeons` is a bool on the same object as the `ItemID`
  tag whose netId is `ItemDocument.Id`. 167 of 237 definitions restrict to specific dungeons and
  the other 70 drop everywhere. Fifteen distinct dungeons are named.
- **A dungeon's name is one lookup away.** `DT_LevelInfo` rows are keyed by the `Level.ID` tag and
  carry `DisplayName` ("Cithrel's Fall", "Wraithtide Vault", "Ransack of Drakheim"). The four
  GameFeature dungeons carry the same field on their own `CRLevelInfo` asset
  (`LI_SandTomb` names "Xul, The Blood Monolith", `LI_MoorCastle` names "Ruins of Regath").
- **A vendor item names its tier on the row.** `DT_HeroItems.Tags` carries `VendorGear` on 104
  rows, `VendorGear.League1` on 104 and `VendorGear.League2` on 104, and each of the twelve slot
  droptables per tier filters on exactly that tag through
  `CRSimpleDroptableFilterAnyHeroItem.ContainsTags`. The droptable's vendor item asset carries
  `VendorID`, and `CRVendorInventory` carries `VendorName` ("Equipment Vendor").
- **Every item document that carries content is reachable.** Of 5,744 item documents, 2,528 carry
  stats, and all 2,528 are either a dynamic definition (2,204) or a `VendorGear`-tagged
  `DT_HeroItems` row (324), with none outside that union. The remaining 3,216 documents serialize
  as `{"$type":"item","id":N}` and carry nothing to source.
- **All 49 crafting recipes transform an item already owned.** `DT_CraftingRecipes` rows resolve to
  transmute, reroll, unlock, upgrade-rarity and gem operations, and none names an output `ItemID`.

### Ability and effect ownership

- **A hero's abilities are declared by a data table.** `DT_CharacterInfoTable` rows carry
  `HeroID` (`CharacterID.Hero.*`), `AbilitySet` and `TalentData`; the twelfth hero, Gunde, carries
  the same struct on `HI_Hero_Gunde` as `Info.HeroID` / `Info.AbilitySet` / `Info.TalentData`.
  `CRGameplayAbilitySet.Abilities[].GameplayAbilityClass` names each ability, and stripping the
  trailing `_C` from the class leaf matches a `GameplayAbility` tag for all 239 references with
  none unmatched. `CRFellowshipCharacter.AbilitySet` exists in the schema and is unset on every
  cooked hero, so the data table is the only route.
- **A creature's abilities are declared by its behaviour component**, not by `CRAICharacter`.
  `CRAICharacter.BehaviorComponentClass` names an `AIBC_*` blueprint whose
  `Phases[].value.Abilities[].AbilityClass` carries 858 ability references across 225 of 227
  components, with `Phases[].Effects[].Effect` carrying 24, `LifetimePassiveAbilities` 23 and
  `LifetimePassiveEffects` 15. `CRAICharacter`'s own inherited `PassiveGameplayEffects` is set on
  110 of 234 creatures and its `Abilities` on 3.
- **Shared player abilities are declared apart.** `DS_UniversalHeroAbilitySet` holds 6 shared
  abilities and is named by `CRFellowshipCharacter.UniversalHeroAbilitySet` on
  `BP_PlayerCharacter_C`. Three further sets are tutorial variants of Rime, Vigor and Warmaster.
- **Talents and item powers share one class.** `CRCharacterAugmentationAsset.GrantedAbilities`
  (284 of 554 assets) and `.GrantedGameplayEffects` (125 assets) name what an augmentation grants,
  and `CRTalentData.Talents[].CharacterAugmentationAsset` reaches 312 of them across the 12 trees.
  `DT_GrantedAbilityMappings_*` declares a `HeroID` per row for 60 augmentations, 36 of which name
  exactly one hero.
- **Mounts and dungeon modifiers own abilities too.** `CRMountData.MountDataAsset` resolves to a
  `CRCharacterAugmentationAsset` whose `GrantedAbilities` names the mount ability, pairing 54
  `CRMountData` assets with 54 `CA_Mount_*` augmentations, and `CRModifierInfoDataRow.ModifierActor`
  resolves to a blueprint naming 15 abilities and 15 effects.
- **Bytecode carries references the properties do not.** `GA_Warmaster_MeleeInterrupt`'s serialized
  properties name no effect while its ubergraph names `GE_Warmaster_MeleeInterrupt_Interrupt_C` and
  `GE_Warmaster_MeleeInterrupt_Lockout_C` as `EX_ObjectConst`. Adding bytecode operands to the
  applier graph raises hero-owned effect reach from 708 to 739 and creature-owned from 1,018 to
  1,172.
- **The reachable population has a hard ceiling.** 466 of 2,307 ability tags and 747 of 3,261
  effect tags name no cooked asset anywhere in the build, so the real denominators are 1,841 and
  2,514.
- **An effect can have many owners.** After closure 1,541 effects have exactly one owner, 258 have
  two to four, the tail reaches 217, and 145 span more than one kind of owner.

### Design system

- **`table.cx-rows` is styled inside `Browse.razor.scss`**, so every one of its selectors carries
  Browse's scope attribute and reaches no other component. The three rules that do cross are the
  child components' own: `table.cx-rows .cx-chip` (Chip), `table.cx-rows .cx-art > img` and
  `table.cx-rows .cx-variants img` (Icon), `table.cx-rows .cx-nm` (EntityLink). The table chrome
  moves with the table.
- **A hover tooltip is one attribute.** `data-tip="<url>"` on any element; `wwwroot/js/codex.js`
  delegates `pointerover`, `pointerout`, `focusin` and `focusout` on the document, fetches the URL
  as an HTML fragment and paints it into a shadow root on `#cx-tip`. There is no component
  parameter and no interop call.
- **Only `Layout/Omnibox.razor` is interactive.** Every page and every other component is static
  server rendered, which is why sorting, paging and faceting are `<a href>` navigations.
- **Colour is an inline style**, never a class: `style="color:@Ui.HeroColour(hero)"`, run through
  `CodexView.Readable`, which lightens a hex until relative luminance reaches 0.16.
- **Every block is a `<Panel>`**, empty states are `<Panel><Empty Said="...">...</Empty></Panel>`,
  counts are `<CountBadge Text="@n.ToString("N0")" />`, and `_Imports.razor` already brings in every
  namespace a new component needs.

## File structure

| File | What happens |
|---|---|
| `CLAUDE.md` | States that extraction work and an export run are standing approvals. |
| `src/Fellowship.SDK/Models/Entity.cs` | Carries sources and item level through the extractor's own entity. |
| `src/Fellowship.SDK/Documents/ItemDocuments.cs` | `ItemDocument` gains `Sources` and `ItemLevel`. |
| `src/Fellowship.SDK/Documents/ItemSource.cs` | New. `ItemSource(SourceKind Kind, string Name)`. |
| `src/Fellowship.SDK/Documents/AbilityDocuments.cs` | `AbilityDocument` gains `Heroes` and `Owner`. |
| `src/Fellowship.SDK/Documents/EffectDocument.cs` | `EffectDocument` gains `Heroes` and `Owner`. |
| `src/FellowshipExtractor/Tooltip/ItemSources.cs` | New. Dungeon, vendor and starmap sources per item id, with the level each grants at. |
| `src/FellowshipExtractor/Tooltip/DungeonNames.cs` | New. `Level.ID` tag to display name, over `DT_LevelInfo` and the `CRLevelInfo` assets. |
| `src/FellowshipExtractor/Tooltip/VendorInventories.cs` | New. Vendor id to vendor name, and droptable to vendor. |
| `src/FellowshipExtractor/Tooltip/HeroItemTable.cs` | Reads the `Tags` field it skips today. |
| `src/FellowshipExtractor/Tooltip/DropItemLevels.cs` | Answers a plateau per curve row by name. |
| `src/FellowshipExtractor/Tooltip/AbilityOwners.cs` | New. Hero and creature ownership of abilities, from the declaring assets. |
| `src/FellowshipExtractor/Tooltip/AppliedEffects.cs` | New. The applier to applied graph, over properties and bytecode operands. |
| `src/FellowshipExtractor/Tooltip/GameplayEffects.cs` | Exposes its reference walk to the graph. |
| `src/FellowshipExtractor/Tooltip/TooltipResolver.cs` | Builds the new indexes and sets the new fields. |
| `src/FellowshipExtractor/Tooltip/DocumentProjection.cs` | Projects the new fields onto the documents. |
| `src/FellowshipExtractor.Api/Models/PublicTypes.cs` | States tab order and which types the codex lists. |
| `src/FellowshipExtractor.Api/Models/Codex.cs` | `Tabs`, `Sortings`, document-count facets, hero and source facets over abilities and effects. |
| `src/FellowshipExtractor.Api/Models/CodexQuery.cs` | One row per document, a default type, sort over `Sortings`. |
| `src/FellowshipExtractor.Api/Models/CodexView.cs` | A hero-aware `Href`, and the item type word. |
| `src/FellowshipExtractor.Api/Models/EntityStore.cs` | Answers a talent's slot; drops `DropItemLevel`. |
| `src/FellowshipExtractor.Api/Models/BrowseView.cs` | Sort links over the new keys. |
| `src/FellowshipExtractor.Api/Models/SearchIndex.cs` | Indexes only the types the codex lists. |
| `src/FellowshipExtractor.Api/Components/Cx/Table.razor` | New. `Table<T>`, the shared table. |
| `src/FellowshipExtractor.Api/Components/Cx/Table.razor.scss` | New. The `cx-rows` chrome, moved off the Browse page. |
| `src/FellowshipExtractor.Api/Components/Cx/Column.razor` | New. `Column<T>`, bound or templated. |
| `src/FellowshipExtractor.Api/Components/Browse/*.razor` | New. One component per listed type, plus the dispatcher. |
| `src/FellowshipExtractor.Api/Components/Pages/Browse.razor` | Tabs without an All tab, and the dispatcher in place of the table. |
| `src/FellowshipExtractor.Api/Components/Pages/Browse.razor.scss` | Keeps the rail, tabs, head and pager; loses the table chrome. |
| `src/FellowshipExtractor.Api/Components/Pages/EntityPage.razor` | Its more-like-this badge counts rows. |
| `src/FellowshipExtractor.Api/Components/Layout/Omnibox.razor` | The view-results button is always drawn and carries no count. |
| `src/FellowshipExtractor.Api/Program.cs` | `/api/catalog` publishes tabs and sortings. |
| `src/FellowshipExtractor.Api.Tests/**` | Covers the table, the per-type columns, the unfolded counts and the new fields. |
| `src/FellowshipExtractor.Tests/**` | Covers the source, level and ownership readers. |

---

### Task 1: The view-results button stays on screen

**Files:**
- Modify: `src/FellowshipExtractor.Api/Components/Layout/Omnibox.razor:76-81,182-185`
- Modify: `src/FellowshipExtractor.Api.Tests/SuggestionsTests.cs`

**Step 1: Draw the button unconditionally.**

Replace the `@if (found.Total > found.Results.Count)` guard with the anchor alone, and drop the
count from its text so it reads `View all results`.

**Step 2: Let the keyboard reach it.**

`Key` computes `last` as `hits + (found.Total > hits ? 1 : 0) - 1`. With the row always drawn the
navigable entries are the hits plus the button, so `last` becomes `hits`. Arrow keys then reach the
button on every query, including one with no hits.

**Step 3: Verify.**

A bunit test that renders the drop with a two-hit query asserts the anchor is present and its text
holds no digits. A second test asserts `ArrowUp` from the initial state selects the button.

---

### Task 2: An item states where it comes from and what level it arrives at

**Files:**
- Create: `src/Fellowship.SDK/Documents/ItemSource.cs`
- Modify: `src/Fellowship.SDK/Documents/ItemDocuments.cs`
- Modify: `src/Fellowship.SDK/Models/Entity.cs`
- Create: `src/FellowshipExtractor/Tooltip/DungeonNames.cs`
- Create: `src/FellowshipExtractor/Tooltip/VendorInventories.cs`
- Create: `src/FellowshipExtractor/Tooltip/ItemSources.cs`
- Modify: `src/FellowshipExtractor/Tooltip/HeroItemTable.cs:99-113`
- Modify: `src/FellowshipExtractor/Tooltip/DropItemLevels.cs`
- Modify: `src/FellowshipExtractor/Tooltip/TooltipResolver.cs:215`
- Modify: `src/FellowshipExtractor/Tooltip/DocumentProjection.cs`
- Create: `src/FellowshipExtractor.Tests/ItemSourceTests.cs`

**Interfaces:**

```csharp
public enum SourceKind { Dungeon, Vendor, Starmap }

public sealed record ItemSource(SourceKind Kind, string Name);
```

`ItemDocument` gains `public IReadOnlyList<ItemSource>? Sources { get; init; }` and
`public int? ItemLevel { get; init; }`.

**Step 1: Name the dungeons.**

`DungeonNames` reads `DT_LevelInfo`, keyed by its own row names which are the `Level.ID` tags, and
takes `DisplayName` off each row. It then reads every `CRLevelInfo` asset and takes
`Info.DisplayName` against `LevelID`, so the four GameFeature dungeons resolve too. It answers
`string? For(string levelTag)` and exposes the count for the resolver's log line.

**Step 2: Name the vendors.**

`VendorInventories` reads every `CRVendorInventory` for `VendorID` to `VendorName`, then every
`CRVendorItemDataAsset` for the droptable its `PurchasedItem` reward names and the `VendorID` it
carries, giving `string? VendorOf(string droptablePath)`.

**Step 3: Read what a droptable group grants.**

`ItemSources` reads every `CRSimpleDroptableDefinition`. For each group it takes `ItemLevel` and,
when `UseTagFilter` is set, `ContainsTags` and `Categories`. A `DT_HeroItems` row joins a group
when the row's own `Tags` value equals one of the group's `ContainsTags` exactly and the row's
`HeroItemType` maps to one of the group's `Categories` through the `ItemType` to
`BackendItemCategory` pair that `DT_SlotWeights` states on the same row. Exact tag equality is what
splits the three vendor tiers, and it gives each of the 324 tagged rows one tier and one level.

The vendor droptables under `Content/Items/VendorGear` yield a `Vendor` source named by
`VendorInventories`; those under `Content/Items/Starmap_SDT` yield a `Starmap` source named by the
droptable's own `DisplayName`.

**Step 4: Read what a dynamic definition drops from.**

At `TooltipResolver.cs:215` the `CRDynamicHeroItemDefinition` CDO is already in scope for
`DropsInSpecificRarities`. Read `DropsInAllDungeons` and `DropsInSpecificDungeons` off the same
object. Every named tag becomes a `Dungeon` source through `DungeonNames`. A definition that drops
in all dungeons carries one `Dungeon` source named by the same word the UI prints for it.

**Step 5: Answer an item level.**

`DropItemLevels` gains `int? Plateau(string row)` so a curve row is addressable by name.
`ItemSources` answers the item level: a vendor or starmap item takes its granting group's
`ItemLevel`; a dropped item takes the plateau of the curve row its droptable group names, which is
`Default` unless the group states an `ItemLevelCurveNameOverride`, and `LegendaryDropCheck` states
`Legendary`. An item reached by neither carries no level.

**Step 6: Project them.**

`Entity` carries `Sources` and `ItemLevel` alongside its other item fields, `TooltipResolver`
sets them in the `entity with` block, and `DocumentProjection` copies them onto `ItemDocument`.

**Step 7: Verify.**

`ItemSourceTests` asserts: `DT_LevelInfo` and the `CRLevelInfo` assets together name all fifteen
dungeons referenced by any definition; the three vendor tiers resolve to 15, 75 and 135 and split
the 324 tagged rows into 104 each; `SDT_HeroPath_LegendaryItem` resolves to 315; a dropped
non-legendary item resolves to 315 and a legendary one to 360; and every item id that carries stats
carries at least one source.

---

### Task 3: An ability and an effect state whose they are

**Files:**
- Modify: `src/Fellowship.SDK/Documents/AbilityDocuments.cs`
- Modify: `src/Fellowship.SDK/Documents/EffectDocument.cs`
- Modify: `src/Fellowship.SDK/Models/Entity.cs`
- Create: `src/FellowshipExtractor/Tooltip/AbilityOwners.cs`
- Create: `src/FellowshipExtractor/Tooltip/AppliedEffects.cs`
- Modify: `src/FellowshipExtractor/Tooltip/GameplayEffects.cs:22-42`
- Modify: `src/FellowshipExtractor/Tooltip/TooltipResolver.cs:135-139,237-261`
- Modify: `src/FellowshipExtractor/Tooltip/DocumentProjection.cs:104-105`
- Create: `src/FellowshipExtractor.Tests/AbilityOwnerTests.cs`

**Interfaces:**

`AbilityDocument` and `EffectDocument` each gain
`public IReadOnlyList<Hero>? Heroes { get; init; }` and
`public EntityReference? Owner { get; init; }`, where `Owner` is the creature that owns it.

**Step 1: Seed hero ownership.**

`AbilityOwners` reads `DT_CharacterInfoTable` for `HeroID`, `AbilitySet` and `TalentData` on every
row with `IsLive`, and reads the `CRHeroInfo` assets for the same three fields, which is how Gunde
arrives. Each `CRGameplayAbilitySet` contributes its `Abilities[].GameplayAbilityClass` leaves with
the trailing `_C` stripped, matched against the `GameplayAbility` tag table. The members of
`DS_UniversalHeroAbilitySet`, named by `CRFellowshipCharacter.UniversalHeroAbilitySet` on
`BP_PlayerCharacter_C`, are held out, and an ability that reaches more than one hero keeps them
all.

Each hero's `TalentData` contributes `Talents[].CharacterAugmentationAsset`, and each augmentation
contributes its `GrantedAbilities` and `GrantedGameplayEffects`. `DT_GrantedAbilityMappings_*` rows
that name exactly one `HeroID` contribute their `GrantedAbilityAsset` the same way.

**Step 2: Seed creature ownership.**

For every `CRAICharacter` blueprint, `BehaviorComponentClass` names a `CRAIBehaviorComponent` whose
`Phases[].value.Abilities[].AbilityClass`, `Phases[].value.Effects[].Effect`,
`LifetimePassiveAbilities` and `LifetimePassiveEffects` name what the creature owns. The creature's
own `Abilities` and `PassiveGameplayEffects` add to the same set. The owner is the creature's own
tag, so it projects as an `EntityReference` of type `creature`.

**Step 3: Build the applier graph.**

`AppliedEffects` walks from every ability and augmentation to the effects and abilities it names.
It takes the reference walk `GameplayEffects.ReferencedIn` performs over serialized properties, and
adds the `EX_ObjectConst` operands of the asset's Blueprint bytecode, keeping only names that are
`GameplayAbility`, `GameplayEffect` or `CRCharacterAugmentationAsset` tags.

**Step 4: Attribute effects.**

Propagate ownership along the graph from the seeded abilities and augmentations. An effect reached
by exactly one hero carries that hero; an effect reached by exactly one creature carries that
creature; an effect reached by more than one owner carries none. The `DisplayName is null` and
`Icon is null` gates that `EffectNameSources` and `EffectIconSources` apply do not apply here,
because an effect that names itself still has an owner.

**Step 5: Project them, and write the export.**

`TooltipResolver` constructs `AbilityOwners` and `AppliedEffects` beside `EffectNames` and
`EffectIcons`, sets `Heroes` and `Owner` in the `entity with` block, and `DocumentProjection`
copies them onto `AbilityDocument` and `EffectDocument`. Then run
`dotnet run --project src/FellowshipExtractor -- export-all` and confirm the new export loads by
running the Api tests against it.

**Step 6: Verify.**

`AbilityOwnerTests` asserts: twelve heroes resolve to an ability set and Gunde among them; all
`GameplayAbilityClass` references resolve to a tag with none unmatched; `GA_Interact` and `GA_Dance`
carry no hero; `GA_Shared_Passive_SpiritMechanic` carries eleven; `BP_NPC_VV_BossChilgar` owns the
five abilities its behaviour component names; `GA_Warmaster_MeleeInterrupt` reaches its two
lockout effects, which the property walk alone does not find; and an effect reached by several
owners carries none.

---

### Task 4: The types the codex lists, in the order it lists them

**Files:**
- Modify: `src/FellowshipExtractor.Api/Models/PublicTypes.cs`
- Modify: `src/FellowshipExtractor.Api/Models/Codex.cs:193-211`
- Modify: `src/FellowshipExtractor.Api/Models/CodexQuery.cs:76-88`
- Modify: `src/FellowshipExtractor.Api/Models/SearchIndex.cs:19-45`
- Modify: `src/FellowshipExtractor.Api/Program.cs:163-194`
- Create: `src/FellowshipExtractor.Api.Tests/TabTests.cs`

**Interfaces:**

```csharp
public sealed record PublicType(EntityType Type, string Slug, string Label, string Noun, string Group, int Position);
```

`PublicTypes` keeps `Table` in its present order, which the mega menu and the home page read, and
states the tab order separately:

```csharp
private static readonly EntityType[] Ordered =
[
    EntityType.Item, EntityType.Ability, EntityType.Effect, EntityType.Talent,
    EntityType.Blessing, EntityType.GemTrait, EntityType.Trait, EntityType.SetBonus,
    EntityType.Gem, EntityType.GemType, EntityType.Creature, EntityType.Mount,
    EntityType.CompanionPet, EntityType.Currency, EntityType.LegendaryPower,
    EntityType.LegendaryStat, EntityType.WeaponPower, EntityType.RelicPower,
    EntityType.TraitPower, EntityType.SetBonusPower,
];

private static readonly EntityType[] Unlisted = [EntityType.TalentSlot, EntityType.MountPower];
```

`Position` is the index in `Ordered`. `PublicTypes.Listed` answers whether a type has a position,
and `PublicTypes.Tabs` answers `Ordered` mapped to `PublicType`.

**Step 1: Verify the ordering against the enum.**

Extend the existing `Verified` guard so it also throws when `Ordered` and `Unlisted` together do
not cover every `EntityType` exactly once. Adding an `EntityType` then fails the build until it is
placed or explicitly left unlisted, so no list can drift.

**Step 2: Filter what the codex offers.**

`Codex.Types` keeps `Table` order and drops the unlisted types, which removes them from the mega
menu and the home page. `Codex.Tabs` is the same set in `Position` order. `PublicTypes.Accepted`,
which spells the legal slugs in the no-such-type message, lists only the listed slugs.

**Step 3: Refuse an unlisted slug as a filter.**

`CodexQuery.Parse` rejects a `type=` slug whose type is unlisted with the existing no-such-type
error, so `/browse?type=talent-slot` answers 400 and `/api/entities?type=mount-power` does too.
`/entity/{slug}/{id}` and `/api/tooltip/{slug}/{id}` resolve by `EntityTypes.TryParse` and keep
answering, which is how a talent slot stays readable as the metadata behind a talent.

**Step 4: Keep them out of search.**

`SearchIndex` builds over the listed types only, so the omnibox, `/api/search` and free-text browse
stop returning them.

**Step 5: Publish it.**

`/api/catalog` publishes `types` in tab order with each type's `position`.

**Step 6: Verify.**

`TabTests` asserts: `Ordered` and `Unlisted` partition `EntityType`; `Codex.Tabs` opens with Items,
Abilities, Effects, Talents, Blessings, Gem Traits; `Codex.Types` holds neither talent slots nor
mount powers; the search index answers nothing for a talent slot name; and `/browse?type=talent-slot`
answers a problem naming the legal slugs.

---

### Task 5: The shared table

**Files:**
- Create: `src/FellowshipExtractor.Api/Components/Cx/Table.razor`
- Create: `src/FellowshipExtractor.Api/Components/Cx/Table.razor.scss`
- Create: `src/FellowshipExtractor.Api/Components/Cx/Column.razor`
- Modify: `src/FellowshipExtractor.Api/Components/Pages/Browse.razor.scss:79-106`
- Create: `src/FellowshipExtractor.Api.Tests/Cx/TableTests.cs`

**Interfaces:**

```razor
@typeparam T
@attribute [CascadingTypeParameter(nameof(T))]

[Parameter, EditorRequired] public IReadOnlyList<T> Items { get; set; }
[Parameter] public RenderFragment? Columns { get; set; }
[Parameter] public Func<T, string?>? Group { get; set; }
[Parameter] public Func<string, string>? SortLink { get; set; }
[Parameter] public Func<string, string?>? SortState { get; set; }
```

```razor
@typeparam T

[Parameter, EditorRequired] public string Head { get; set; }
[Parameter] public Func<T, object?>? Value { get; set; }
[Parameter] public RenderFragment<T>? Content { get; set; }
[Parameter] public string? Key { get; set; }
[Parameter] public string? Unit { get; set; }
[Parameter] public bool Numeric { get; set; }
[Parameter] public string? Class { get; set; }
```

**Step 1: Register the columns before the header draws.**

`Table.razor` opens with two sibling cascades:

```razor
<CascadingValue IsFixed="true" Value="this">@Columns</CascadingValue>

<CascadingValue IsFixed="true" Value="this">
    <div class="cx-table-scroll">
        <table class="cx-rows">
            <thead>
                <tr>
                    @foreach (var column in Drawn) { ... }
```

A `CascadingValue`'s child content runs when that component renders, so the first cascade
instantiates every `Column<T>` and each registers itself in `OnInitialized` before the second
cascade renders its header row. `Add` appends to a list and calls nothing else, so one render pass
draws a full table under static server rendering.

`Column.razor` renders nothing. It takes `[CascadingParameter] public Table<T> Table { get; set; }`,
calls `Table.Add(this)` in `OnInitialized`, and `Table.Remove(this)` in `Dispose`.
`[CascadingTypeParameter]` on the table lets a call site write `<Column Head="Slot" ... />` with no
`T=`.

**Step 2: Draw a header.**

A column with a `Key` and a table with a `SortLink` draws
`<th aria-sort="@SortState(Key)"><a href="@SortLink(Key)">@Head</a></th>`; otherwise the head is
plain text. `Numeric` adds `cx-numeric` to both the header and the body cell, and `Class` adds to
the body cell.

**Step 3: Draw a cell.**

`Content` wins when it is set, so a template column draws arbitrary markup with the row as its
context. Otherwise `Value` is read and printed, with a `double` going through `Codex.Number` and a
`Unit` following it in a `<span class="cx-unit">`. A null value draws nothing.

**Step 4: Draw a group heading.**

When `Group` is set and its value changes between rows, the table emits
`<tr class="cx-group-head"><td colspan="@Drawn.Count">@value</td></tr>` ahead of the row, with the
colspan counted rather than the literal 12 the page writes today.

**Step 5: Move the chrome.**

The whole `table.cx-rows` block moves from `Browse.razor.scss` into `Table.razor.scss` unchanged,
so the header, cell, hover, numeric, art, group-head, tags, unit and variants rules land on the
component that renders them. `.cx-table-scroll` moves with it. The rail, tabs, results head, chosen
chips, ranges and pager stay on the page.

**Step 6: Verify.**

`TableTests` renders a table over a small record list and asserts: two bound columns and one
template column draw in declaration order; the header row is present on the first render with no
second pass; a `Key` plus a `SortLink` draws an anchor and `aria-sort`; a numeric column carries
`cx-numeric` on both cells; a group function emits one heading per change with a colspan equal to
the column count; and an empty item list draws a header and no body rows.

---

### Task 6: One row per reading

**Files:**
- Modify: `src/FellowshipExtractor.Api/Models/CodexQuery.cs:352-517`
- Modify: `src/FellowshipExtractor.Api/Models/Codex.cs:86,193-248,270-309`
- Modify: `src/FellowshipExtractor.Api/Models/CodexView.cs:112-127`
- Modify: `src/FellowshipExtractor.Api/Components/Pages/EntityPage.razor:123-159`
- Modify: `src/FellowshipExtractor.Api/Models/Suggestions.cs:28-37`
- Modify: `src/FellowshipExtractor.Api.Tests/CountTests.cs`, `ListingTests.cs`, `NearbyAudit.cs`,
  `EntityLinkTests.cs`

**Step 1: Stop folding.**

`CodexSearch.Fold` becomes a projection: every matched document becomes one `Listing`.
`Listed` keeps computing `Variants` as it does today, from the other readings of the same id
grouped by display name, so a row still points at the id's other namings.

**Step 2: Choose the type when the query names none.**

`Run` computes `across` first, chooses the type when `query.Types` is empty, and filters to it:

```csharp
var across = rows.Where(row => Matches(row, query, skipTypes: true)).ToArray();
var chosen = query.Types.Count > 0 ? query.Types : [Default(across)];
var matched = across.Where(row => chosen.Contains(EntityTypes.TypeOf(row))).ToArray();
```

`Default` answers the first type in `PublicTypes.Tabs` order that appears in `across`, and Items
when none does. `CodexResults` carries `Chosen` so the page knows which tab is current and which
component to draw, and drops `AcrossTypes`, which only the All tab read.

**Step 3: Count rows everywhere a row is counted.**

`Codex.Types` counts documents rather than distinct ids, `Codex.View` accumulates a document count
rather than a `HashSet<(EntityType, int)>`, and `Codex.Count` answers `store.Count`. The tab badge,
the rail counts, the mega menu counts and the results head then all state the same number as the
list beneath them.

**Step 4: Address a reading.**

`CodexView.Href(EntityDocument)` appends `?hero=` when `Reading(document)` answers a hero, which is
exactly when the id has more than one reading. `Href(EntityType, int)` keeps its present shape for
callers that hold only an id. `EntityLink` then clicks through to the reading its own tooltip
previews.

**Step 5: Match the badge to the page it opens.**

`EntityPage.Nearby` counts rows rather than distinct ids, so a more-like-this badge states the
number the browse page it links to states.

**Step 6: Keep the omnibox on ids.**

`Suggestions.For` keeps grouping hits by `(type, id)`, so a name that reads twelve ways is offered
once. The view-results button opens the browse list, which shows all twelve rows.

**Step 7: Verify.**

Update `CountTests` to assert every per-type count equals a document count and that they sum to
`Codex.Count`. Update `ListingTests`: an item id with twelve readings draws twelve rows; a talent
slot id draws one row per hero; the `Find` helper takes a hero as well as an id; a row's link
carries the hero its tooltip carries. `Every_count_beneath_the_list_counts_ids` stays as the guard
that a facet value's count equals the total of filtering by it.

---

### Task 7: Sorting by any column a list draws

**Files:**
- Modify: `src/FellowshipExtractor.Api/Models/Codex.cs`
- Modify: `src/FellowshipExtractor.Api/Models/CodexQuery.cs:164-169,469-506`
- Modify: `src/FellowshipExtractor.Api/Models/EntityStore.cs`
- Modify: `src/FellowshipExtractor.Api/Program.cs:163-194`
- Create: `src/FellowshipExtractor.Api.Tests/SortingTests.cs`

**Interfaces:**

```csharp
public sealed record Sorting(string Key, string Label, Func<EntityDocument, IComparable?> Value);
```

**Step 1: State the sortable columns.**

`Codex.Sortings` holds `name` and `type`, which move out of the special cases in `CodexSearch.Order`,
and one entry per column the per-type lists draw: `slot`, `ilvl`, `kind`, `source`, `set`, `hero`,
`school`, `gem`, `power`, `tier`, `cost`, `pieces`, `rarity`, `category`. Each reads the document
the same way the column that draws it does, so a header and its ordering cannot disagree.

**Step 2: Answer a talent's slot.**

`EntityStore` builds a `talentId` to `TalentSlotDocument` lookup in its constructor and answers
`TalentSlotDocument? Slot(int talentId)`. The `tier`, `cost` and `hero` sortings for talents read
it, and so does the Talents list.

**Step 3: Order by it.**

`CodexQuery.Parse` accepts a `sort` that names a sorting or a measure and lists both in its error.
`CodexSearch.Order` reads a sorting when one matches, keeping nulls last in both directions as
`Measured` already does, and falls through to `Measured` otherwise.

**Step 4: Publish it.**

`/api/catalog` publishes `sortings` beside `measures`.

**Step 5: Verify.**

`SortingTests` asserts: every sorting key is distinct and none collides with a measure key; sorting
by `ilvl` descending puts 360 first and nulls last; sorting by `tier` orders talents by their
slot's row; and an unknown sort key answers a problem that names the legal keys.

---

### Task 8: A component per type

**Files:**
- Create: `src/FellowshipExtractor.Api/Components/Browse/Rows.razor`
- Create: `src/FellowshipExtractor.Api/Components/Browse/Items.razor`, `Abilities.razor`,
  `Effects.razor`, `Talents.razor`, `Blessings.razor`, `GemTraits.razor`, `Traits.razor`,
  `SetBonuses.razor`, `Gems.razor`, `GemTypes.razor`, `Creatures.razor`, `Mounts.razor`,
  `CompanionPets.razor`, `Currencies.razor`, `LegendaryPowers.razor`, `LegendaryStats.razor`,
  `WeaponPowers.razor`, `RelicPowers.razor`, `TraitPowers.razor`, `SetBonusPowers.razor`
- Create: `src/FellowshipExtractor.Api/Components/Browse/Art.razor`
- Modify: `src/FellowshipExtractor.Api/Components/_Imports.razor`
- Create: `src/FellowshipExtractor.Api.Tests/Browse/ColumnTests.cs`

**Interfaces:**

Every per-type component takes the same two parameters, so the dispatcher passes them uniformly:

```csharp
[Parameter, EditorRequired] public IReadOnlyList<Listing> Rows { get; set; }
[Parameter, EditorRequired] public BrowseView View { get; set; }
```

`Rows.razor` switches on `EntityType` and renders the component for it, with a
`DynamicComponent` over a switch expression the way `Tooltip.razor` names its cards.

**Step 1: Draw the shared first column.**

`Art.razor` draws the icon cell and the name cell every list shares: a `<BlessingCrest>` for a
blessing and an `<Icon>` otherwise, then an `<EntityLink Class="cx-nm" />`. Each list declares it
as two `<Column>` entries, the art one unsortable with `Class="cx-art"` and the name one keyed on
`name`.

**Step 2: Draw the items list.**

Columns after art and name: `ILvl` bound to `ItemDocument.ItemLevel`, numeric, keyed `ilvl`;
`Slot` bound to `Slot`, keyed `slot`; `Versions`, a template drawing `row.Variants` as icon links
each carrying `href` and `data-tip`; `Source`, a template drawing each `ItemSource` with its name,
keyed `source`; `Type`, a template drawing the armour type for the eight armour slots, the slot
word for Neck, Ring and Relic, and the hero in the hero's own colour for a weapon, keyed `kind`;
`Set`, a template drawing an `EntityLink` to the set bonus the item names, keyed `set`.

The type rule is slot-gated because `ItemDocument.ArmorType` holds the wearer's armour class on a
non-armour slot, which is why five plate weapons and eighteen legendary rings read an armour type
they do not have.

**Step 3: Draw the abilities and effects lists.**

Both draw `Hero`, a template drawing a `<Chip>` per hero in the hero's colour, and an
`<EntityLink>` to the owning creature when there is no hero, keyed `hero`; and `Type`, a template
drawing a `<Chip>` per school, keyed `school`.

**Step 4: Draw the talents list.**

`Hero` from the talent's slot, keyed `hero`; `Cost` from the slot's `Cost`, numeric, keyed `cost`;
`Tier` from the slot's `Row`, numeric, keyed `tier`.

**Step 5: Draw the gem traits list.**

`Power Required` bound to `RequiredPower`, numeric, keyed `power`; `Type` bound to `Gem`, drawn in
the gem's own colour, keyed `gem`.

**Step 6: Draw the set bonuses list.**

`Items`, a template drawing one icon link per item that names the set, each carrying `href` and
`data-tip`; `Required` bound to `Pieces`, numeric, keyed `pieces`. The item list is read per
reading, so a hero's own version of a set piece is the icon that hero's row would show.

**Step 7: Draw the remaining lists.**

Each of the other fourteen draws the columns its document actually populates, taken from the
inventory in this plan's research: Traits draw category, max rank and random chance; Gems draw
type, power and rarity; Gem Types draw type and attribute; Currencies draw rarity; Creatures draw
what scales them; Mounts, Companion Pets, Legendary Powers, Legendary Stats, Weapon Powers, Relic
Powers, Trait Powers and Set Bonus Powers draw their cooldown, cast, channel, range, duration and
schools where they carry them.

**Step 8: Verify.**

`ColumnTests` renders each component over a handful of real documents from the export and asserts
the head row reads the columns this plan names, in order, for each of the six types with a stated
column set, and that every other listed type renders a table with at least the art and name
columns.

---

### Task 9: The browse page shows one type

**Files:**
- Modify: `src/FellowshipExtractor.Api/Components/Pages/Browse.razor`
- Modify: `src/FellowshipExtractor.Api/Components/Pages/Browse.razor.scss`
- Modify: `src/FellowshipExtractor.Api.Tests/RouteTests.cs`
- Create: `src/FellowshipExtractor.Api.Tests/BrowsePageTests.cs`

**Step 1: Draw the tabs.**

The tab strip iterates `Catalog.Tabs`, keeping only types that have a count in `Results.Types` or
are the chosen type, and marks `aria-current` on `Results.Chosen`. The All tab and its
`AcrossTypes` badge go.

**Step 2: Draw the chosen list.**

The table markup in the page is replaced by `<Rows Type="Results.Chosen" Rows="Results.Rows"
View="View" />`. The results head, the empty state, the rail and the pager stay where they are.

**Step 3: Keep the heading honest.**

`Heading` reads the chosen type when the query names none, so a bare `/browse` heads Items and a
search heads its own words.

**Step 4: Verify.**

`BrowsePageTests` asserts: `/browse` draws the Items list and marks the Items tab current; a query
matching only abilities draws the Abilities list; a query naming a type draws that type's list; and
no page draws a tab labelled All.

---

### Task 10: The whole surface, checked

**Files:**
- Modify: `src/FellowshipExtractor.Api.Tests/CorpusTests.cs`
- Modify: `src/FellowshipExtractor.Api.Tests/IsolationTests.cs`

**Step 1: Assert the chrome landed on the table.**

`IsolationTests` gains an assertion that the generated bundle carries `table.cx-rows` selectors
under the `Table` component's scope attribute, so the chrome reaches every list rather than one
page.

**Step 2: Walk every list.**

`CorpusTests` gains a pass that renders every listed type's first page and asserts each answers a
table with a header row and no unhandled type.

**Step 3: Run everything.**

```powershell
dotnet build src/FellowshipExtractor.slnx
dotnet test src/FellowshipExtractor.Tests
$env:FELLOWSHIP_EXPORT = "G:/source/Fellowship_Data_Extractor.browse-lists-per-type/output/export"
dotnet test src/FellowshipExtractor.Api.Tests
```

**Step 4: Look at it.**

Run the AppHost, open `/browse`, and read the Items, Abilities, Effects, Talents, Blessings and
Gem Traits lists against the columns this plan names. Screenshots of each are the check that the
columns fit, the icons size, the hover tooltips fire and the page does not scroll sideways.
