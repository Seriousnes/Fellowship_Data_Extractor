# Item level, rarity and tempering: design

An item's level and its rarity are two independent axes. The build states both, and states how far
tempering can carry an item once it is held. The item model carries the rarity an item is fixed at,
a tooltip asked for no item level is answered at the level that item is found at, and the tempering
an item can take is read from the shipped ladder rather than left out.

---

## 1. Goal

**An item is answered at the level it is found at.** A request naming no item level is answered at
315, and at 360 for an item fixed at Legendary. Those are the two plateaus of
`CT_DungeonDifficultyToDropItemLevel`, and a legendary takes no tempering, so 360 is where a
legendary sits rather than a ceiling it climbs to.

**An item states the rarity it is fixed at.** The `Item` document carries `Rarity` for the items
whose rarity the build decides, and carries none for the items whose rarity the drop decides.

**An item is answered at the top of its own ladder.** The `Item` document carries every rung the
item comes in, and a tooltip asked for no rarity answers at the highest of them, so a header is
styled without the caller naming a rung.

**A currency states the rung it is.** The `Currency` document carries the rarity its row states, the
way `GemTier` already does.

**The tempering an item can take is read.** `CT_ItemRarityAndLevelToMaxTempers` states a temper
ceiling for every rarity at every item level. A tooltip answers that ceiling for the rarity and item
level the caller names.

---

## 2. The item level of a drop

`fellowship/Content/Items/CT_DungeonDifficultyToDropItemLevel` maps dungeon difficulty to item level.
Its rows are drop contexts, and the row a drop reads is the one its droptable group names.
`DT_DroptableGroups` states two groups: `Droptable.Group.Default`, which names no override and reads
the `Default` row, and `Droptable.Group.LegendaryDrop`, whose `ItemLevelCurveNameOverride` is
`Legendary`.

The curve interpolates linearly, so a level is read with a lerp between the keys either side of the
difficulty rather than by looking the difficulty up. Read that way the two rows give:

| Difficulty | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Default | 15 | 30 | 45 | 60 | 75 | 75 | 105 | 120 | 135 | 150 | 150 |
| Legendary | 75 | 75 | 75 | 75 | 105 | 105 | 150 | 150 | 150 | 195 | 195 |

| Difficulty | 11 | 12 | 13 | 14 | 15 | 16 | 17 | 18 | 19 | 20 | 21 and above |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Default | 180 | 195 | 210 | 225 | 225 | 255 | 270 | 285 | 300 | 300 | **315** |
| Legendary | 225 | 225 | 225 | 270 | 270 | 315 | 315 | 315 | 315 | 315 | **360** |

Every value is a multiple of 15, at every difficulty, on both rows.

`AdventureViewOnly` and `DungeonViewOnly` are the values a selection screen previews. `DungeonHigh`
is a reward that pays one rung above the `Default` row at each difficulty where a tier boundary
repeats a value, giving 90, 165, 240 and 315.

---

## 3. The rarity an item is fixed at

A dynamic hero item states `RuleSet` and `DropsInSpecificRarities`. Eleven of them state
`DynamicHeroItemRuleSet.Legendary` and `[Legendary]`, which is the rarity every copy of those items
is. Every other item states the same five rungs, which is the drop deciding the rarity rather than
the item.

`Item` carries that:

```csharp
/// <summary>The rung every copy of this item is, and none where the drop decides it.</summary>
public Rarity? Rarity { get; init; }
```

It is `Rarity.Legendary` on the 37 item rows whose rule set is `Legendary`, and null on the rest.
`GemTier` already carries a required `Rarity`, and `Item` already carries `IconByRarity` keyed by the
word the game prints for a rung, so the rung is a word a reader meets rather than an internal
spelling.

---

## 4. The rungs an item comes in

`DropsInSpecificRarities` is the item's own list of the rungs it is found at, and `DropsInAllRarities`
stands for the whole ladder. `Item` carries that list:

```csharp
/// <summary>Every rung this item comes in, lowest first.</summary>
public IReadOnlyList<Rarity>? Rarities { get; init; }
```

It reads `[Uncommon, Rare, Epic, Champion, Heroic]` on the 2,167 rows of the 226 dynamic items the
drop decides, `[Legendary]` on the 37 rows of the 11 legendary items, and `[Common]` on the 480 hero
item rows, whose `CRHeroItemDataRow` states `OverrideRarity` and `ItemRarityOverride` and never
drops. 2,684 of 5,744 item rows state a list; the rest are `ItemID` tags no asset answers.

A tooltip asked for no rarity answers at the highest rung the item comes in, so a dynamic item reads
*Regal*, a legendary reads *Legendary*, and a hero item reads *Common*. A rarity the caller names is
answered instead.

The rung carries the stat block and the tempering with it, because both are read for the rung the
tooltip answers at. *Witchslayer's Hearth-Woven Pants* asked for nothing reads Regal at item level
315 with 3 guaranteed stat lines, the 2 rolled slots the `SetBonus_NoGemSlot` rule set states at that
rung, and a ceiling of 8 tempers. The screenshot of the held item shows 3 declared stats and 1
distinct rolled one, which is those 2 rolls with one landing on a stat the item already carries and
folding into it.

`CRCurrencyItemDataRow` states a `Rarity` of its own, so `Currency` carries it the way `GemTier`
does and a currency is answered at the rung its row states without being asked:

```csharp
/// <summary>The rung this currency is.</summary>
public Rarity? Rarity { get; init; }
```

`DT_Currencies` states all 31 rows across all 7 rungs, and its row keys name the ladder back:
`UncommonWeaponReroll` is rung 1, `RareWeaponReroll` rung 2, `EpicWeaponReroll` rung 3 and
`LegendaryWeaponReroll` rung 6. That agreement is what says an `ECRItemRarity` ordinal is a rung of
the same ladder `RarityTier` counts, which is otherwise unreadable because the usmap carries an
empty enum table.

A row with no rarity of its own - every ability, effect, talent, creature and trait - carries no rung
and draws an unstyled header.

---

## 5. Answering a request that names no item level

A tooltip answers at the item's own plateau:

| The item states | Answered at |
|---|---:|
| `Rarity.Legendary` | 360 |
| no rarity | 315 |

Both numbers are read from the build rather than written down. The vocabulary carries the plateau of
every curve row a drop can read, each carrying the difficulty it is reached at:

```json
"dropItemLevels": [
  { "row": "Default", "difficulty": 21, "itemLevel": 315 },
  { "row": "Legendary", "difficulty": 21, "itemLevel": 360 }
]
```

`Default` is the row a droptable group naming no override reads, and `Legendary` is the row
`DT_DroptableGroups` states as `LegendaryDropCheck`'s `ItemLevelCurveNameOverride`. A plateau is the
value of a row's last key, and the difficulty is the first key that reaches it.

Which of those rows an item reads is the one thing the build does not state - no item names a
droptable group - so `EntityStore.DropItemLevel` pairs an item fixed at Legendary with the
`Legendary` row and every other item with `Default`, as a single named lookup rather than a number
written into the stat calculation. The knob a reader sees offers the same plateau the tooltip
answers at.

---

## 6. Tempering

`CT_ItemRarityAndLevelToMaxTempers` states how many times an item of a given rarity at a given item
level can be tempered. Its rows carry `InterpMode` 1, `RCIM_Constant`, where the drop ladder's rows
carry 0, `RCIM_Linear`. So this table is read as a step: a level between two keys holds the lower
key's ceiling, and an Uncommon item at level 60 tempers once, not one and a half times.

Keyed by item level, by rarity row:

| Item level | 0 | 15 | 105 | 180 | 255 | 315 | 420 |
|---|---:|---:|---:|---:|---:|---:|---:|
| Uncommon | 0 | 1 | 2 | 3 | 4 | 4 | 4 |
| Rare | 0 | 1 | 2 | 3 | 4 | 5 | 5 |
| Epic | 0 | 1 | 2 | 3 | 4 | 6 | 6 |
| Champion, printed *Heroic* | 0 | 1 | 2 | 3 | 4 | 7 | 7 |
| Heroic, printed *Regal* | 0 | 1 | 2 | 3 | 4 | 8 | 8 |
| Common | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| Legendary | 0 | 0 | 0 | 0 | 0 | 0 | 0 |

The ceiling depends on the rarity and item level the caller names, so it is answered in the API
beside `StatMagnitudes` rather than stored per item. A tooltip states it as the denominator of the
tempering line it already renders as `Tempered {upgrades} / {max}`.

The stat block a tooltip computes is reconciled against *Witchslayer's Hearth-Woven Pants*
(ItemID 5309), which the build now accounts for on every axis except magnitude: Regal at item level
315 is the `Heroic` row at 315, which is 8, and the tooltip reads `Tempered 8/8`. Tempering is what
stands between the computed stat block and that tooltip's
`+89 Armor / +14 Intellect / +50 Stamina / +19 Expertise / +24 Spirit`.

---

## 7. What the build states

Every number is read from the cooked build with `FELLOWSHIP_USMAP` set, and every curve table below
needs the struct supers the usmap generator writes.

**The drop ladder.** `fellowship/Content/Items/CT_DungeonDifficultyToDropItemLevel` carries 7 rows:
`Default`, `DefaultOvertime1`, `DungeonHigh`, `AdventureViewOnly`, `DungeonViewOnly`, `WeeklyChest`
and `Legendary`. `fellowship/Content/Items/DT_DroptableGroups` carries 2 rows, whose chances are 1
and 0.03 and whose `StartAtDifficulty` are 0 and 17.

**The 15 quantum**, stated four ways that do not share a source: every value of every row of the drop
ladder at every difficulty; every `ItemLevel` declared by the 58 `SDT_*` static drop tables, from 15
for starter vendor gear to 315 for `SDT_HeroPath_LegendaryItem`; every key time of all 29 rows of
`fellowship/Content/Crafting/CT_CraftingItemLevelDynamicGoldCostCurveTable`, spanning 0 to 420; and
the shipped string *"increased by 2.75 for every 15 item levels your item has"*.

**The two axes move after the drop.** The shipped strings state *"Upgrading will increase the item
level of your equipment"*, *"Upgrade the rarity of the item by One level"*, and *"Reforging an item
resets its Item Level to what it was obtained at"*. A held item's level and rarity are each what it
dropped at plus what crafting has done, which is why a set of equipped gear shows no correspondence
between the rarity colour and the item level.

**Rarity on items.** 237 dynamic hero items carry 9 rule sets and 2 distinct rarity declarations:
`[Legendary]` on 11 assets, and `[Uncommon, Rare, Epic, Champion, Heroic]` on the other 226. In
`entities.jsonl` those 11 assets are 37 rows, and they are exactly the 37 rows carrying
`legendaryStats`. `DT_DynamicHeroItemLootRarityRuleSets` admits every rule set other than `Legendary`
at every tier from 1 to 5.

**Weights.** `CT_DynamicHeroItemLootRarityDropChance` states, per rarity row, Common 0, Uncommon 24,
Rare 38, Epic 20 from difficulty 6, Champion 10 from difficulty 11, and Heroic 5 and Legendary 3 from
difficulty 16. They total 100 at difficulty 16.

**Printed rarity.** The tiers are 0 Common, 1 Uncommon, 2 Rare, 3 Epic, 4 Champion, 5 Heroic and
6 Legendary; tier 4 prints as *Heroic* and tier 5 prints as *Regal*.

---

## 8. Tests

**The ladder is the shipped one.** `Default` reads 315 and `Legendary` reads 360 at difficulty 21,
and each reads its stated value at every difficulty from 0 to 21.

**Every level on the ladder is a multiple of 15**, over both rows at every difficulty from 0 to 21.

**An item fixed at Legendary is answered at 360**, and an item stating no rarity at 315, when the
request names no item level. The `Default` plateau reads 315 at difficulty 21 and the `Legendary`
plateau 360 at difficulty 21, both read from the shipped curve, and every plateau is a multiple of
15.

**The 37 rows carrying `legendaryStats` are the 37 rows whose `Rarity` is `Legendary`**, and no other
row states a rarity.

**A Regal item at item level 315 tempers 8 times**, which is the tooltip the screenshot shows.

**The temper ceiling of Common and of Legendary is 0 at every item level.**

**The temper ceiling steps and the drop ladder interpolates.** An Uncommon item at item level 60
tempers once, holding the ceiling keyed at 15, and the `Default` row at difficulty 7 reads 120,
between the 105 keyed at 6 and the 150 keyed at 9.

**Every item carrying a rule set states the rungs it comes in.** An item on any rule set other than
`Legendary` tops out at the rung printed *Regal*, an item on `Legendary` comes only in Legendary, an
item fixed at a rung comes in that rung alone, and a hero item comes in the rung its row states.

**No row outside items states rungs**, so an ability, effect, talent or creature draws an unstyled
header.

**Every currency the table names states the rung it is**, and the 31 rows span the whole ladder.

**Every item in the export that states rungs answers at the highest of them** when the request names
no rarity, and *Witchslayer's Hearth-Woven Pants* (ItemID 5309) reads *Regal* at item level 315
unasked, with 3 guaranteed stat lines, 2 rolled ones and `Tempered 8 / 8`, which is the tooltip the
screenshot shows.
