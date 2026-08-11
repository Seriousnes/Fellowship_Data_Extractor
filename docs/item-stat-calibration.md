# Item stat calibration

Every magnitude an item tooltip prints is computed, never shipped. The pool curves, slot weights,
armour-type weights and rarity slot additions are all in the build; the arithmetic that joins them is
C++ in `DynamicLootSystemRuntime`. So the only way to check a magnitude is against a real tooltip.

This file is the register of the in-game tooltips that have been supplied, and what each one settles.

## Ground truth

| Item | Id | Slot | Armour type | Level | Rung | Tempers | Declared slots (weight, kind) | Reads |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Witchslayer's Hearth-Woven Pants | 5309 | Legs | Cloth | 315 | Heroic | 0 / 8 | Stamina 40 fixed, Intellect 17 primary, Expertise 27 fixed | +89 Armor, +66 Stamina, +6 Intellect, +11 Expertise, +12 Spirit |
| Witchslayer's Hearth-Woven Pants | 5309 | Legs | Cloth | 315 | Heroic | 8 / 8 | as above | +89 Armor, +14 Intellect, +50 Stamina, +19 Expertise, +24 Spirit |
| Darkfrost Arms | 5323 | Weapon | Plate | 315 | Heroic | 8 / 8 | Stamina 22 fixed, Strength 22 primary, Haste 40 fixed | +303 Armor, +36 Stamina, +17 Strength, +27 Haste, +28 Spirit |
| Sinbinding Stone | 5250 | Relic | none | 315 | Heroic | 8 / 8 | Agility 28 primary, Haste 28 fixed, CritChance 28 fixed | +14 Intellect, +15 Critical Strike, +31 Haste |
| Skandi's Bands of Endless Winter | 5226 | Wrists | Cloth | 360 | Legendary | none | Stamina 22 fixed, Intellect 22 primary, Haste 40 fixed | +51 Armor, +15 Stamina, +5 Intellect, +9 Haste, +7 Critical Strike, +7 Expertise |
| Eldrin Signet of Undulating Spirits | 5220 | Ring | Cloth | 360 | Legendary | none | Stamina 28 fixed, CritChance 28 fixed, Expertise 28 fixed | +23 Stamina, +8 Critical Strike, +8 Expertise, +3 Intellect, +8 Haste |
| Eldrin Signet, second drop | 5220 | Ring | Cloth | 360 | Legendary | none | as above | +23 Stamina, +8 Critical Strike, +8 Expertise, +16 Haste |
| Eldrin Signet, third drop | 5220 | Ring | Cloth | 360 | Legendary | none | as above | +23 Stamina, +8 Critical Strike, +16 Expertise, +8 Haste |
| Vaultbinder's Hoard-Guard Helm | 5213 | Head | Plate | 315 | Heroic | 8 / 8 | Stamina 28 fixed, Strength 28 primary, Spirit 28 fixed | +34 Stamina, +17 Strength, +38 Spirit |
| Drakesblood Tapestry | 5218 | Back | Cloth | 360 | Legendary | none | Stamina 22 fixed, Intellect 22 primary, Haste 40 fixed | +48 Armor, +15 Stamina, +5 Intellect, +9 Haste, +7 Expertise, +7 Spirit |
| Drakesblood Tapestry, second drop | 5218 | Back | Cloth | 315 | Legendary | none | as above | +43 Armor, +27 Stamina, +4 Intellect, +9 Haste |
| Baneful Web Shroud | 5139 | Back | Cloth | 285 | Heroic | 4 / 4 | Stamina 40 fixed, Intellect 17 primary, Haste 27 fixed | +40 Armor, +18 Stamina, +7 Intellect, +10 Haste, +6 Critical Strike, +6 Expertise |
| Betrayer's Icy Veil | 5135 | Back | Cloth | 195 | Epic | 3 / 3 | Stamina 28 fixed, Intellect 28 primary, CritChance 28 fixed | +30 Armor, +10 Stamina, +7 Intellect, +8 Critical Strike |

A Legendary reads `Non Temperable` and `CT_ItemRarityAndLevelToMaxTempers` gives Legendary 0, so the
two legendaries and the `Tempered 0 / 8` pants are the three observations with no tempering in them.

The colour tells guaranteed from rolled: a declared slot prints its name and magnitude white, a
random roll prints both in `game.stat-secondary`, and a roll landing on a stat the item already
declares aggregates into one line whose magnitude alone turns. That is how the three Eldrin drops
read as one item: Stamina, Critical Strike and Expertise are the declared 23 / 8 / 8 every time, and
the drops differ only in where two rolls of 8 land, with the one roll that landed on Intellect worth
3 rather than 8.

## Armour, solved

```
armor = ceil(DT_SlotWeights.ArmorWeight x (itemLevel x DT_ArmorTypeWeights.ArmorWeight + FlatArmor))
```

Every term is shipped and there is no fitted constant.

| Item | Computed | Prints |
| --- | --- | --- |
| Betrayer's Icy Veil, Back Cloth 195 | `0.08 x (195 x 1.41 + 90)` = 29.20 | 30 |
| Baneful Web Shroud, Back Cloth 285 | `0.08 x (285 x 1.41 + 90)` = 39.35 | 40 |
| Witchslayer's pants, Legs Cloth 315 | `0.165 x (315 x 1.41 + 90)` = 88.13 | 89 |
| Darkfrost Arms, Weapon Plate 315 | `0.28 x (315 x 2.8 + 200)` = 302.96 | 303 |
| Drakesblood Tapestry, Back Cloth 360 | `0.08 x (360 x 1.41 + 90)` = 47.81 | 48 |
| Skandi's Bands, Wrists Cloth 360 | `0.085 x (360 x 1.41 + 90)` = 50.80 | 51 |
| Eldrin Signet, Ring Cloth 360 | Ring `ArmorWeight` is 0 | no armour line |
| Sinbinding Stone, Relic 315 | Relic `ArmorWeight` is 0 | no armour line |

**Tempering does not move it.** The same pants read `+89 Armor` at 0 of 8 tempers and at 8 of 8, and
the level is 315 in both.

The rounding is a ceiling, and six readings across four item levels, three slots and two armour types
now carry it. Three of the six discriminate a ceiling from nearest: the pants at 88.13, Betrayer's
Icy Veil at 29.20 and Baneful Web Shroud at 39.35 all print the next integer up.

## The primary slot takes the shipped multiplier

`BaseItemSlotCalculationSettings` declares only `StaminaMultiplierAtThreshold`, so a primary slot
found no `PrimaryMultiplier` there and fell back to 1, which was invented rather than read.
`FixedSlotCalculationSettings` ships 0.525, and Skandi's Bands settles it: its Stamina and Intellect
slots carry the **same** weight of 22 and the game prints 15 and 5, a ratio of 3.0 whose rounding
band is 2.636 to 3.444. The shipped multipliers give 1.5 / 0.525 = 2.857, inside it. The invented 1
gives 1.5, nowhere near.

A multiplier a block leaves at its default now reads the fixed-slot block's shipped value.

## The stat magnitude, read out of the binary

Superseding everything below it in this file. The whole calculation was recovered by disassembling
`UCRDynamicHeroItem`'s virtual at **0x1455D33F0**, reached through vtable slot +0x2D0. Per stat row,
in float, in this order:

```
pool      = Stamina, Strength, Intellect, Agility or Power
              ? PrimaryAndStaminaMultiplier x PrimaryPowerBase ^ (itemLevel / PrimaryDivisible) x sqrt(itemLevel)
              : SecondaryMultiplier x sqrt(itemLevel x SecondaryBaseMultiplier)
blockTerm = Stamina        ? StaminaWeight x (itemLevel >= Threshold ? AtThreshold : 1) / WeightMultiplier x PrimaryMultiplier
          : primary group  ? PrimaryWeight / WeightMultiplier x PrimaryMultiplier
          :                  SecondaryWeight / WeightMultiplier x SecondaryMultiplier
m         = pow(StatRarityBase, -StatRarityExponents[rarity]) x pool x DT_SlotWeights.StatWeight x blockTerm
m         = m x weights[min(rowOrdinal, weights.Length - 1)]
m         = max(m, 1)
m         = m + (the row is a declared slot ? TimesUpgraded : 0)
printed   = roundHalfToEven(m)
```

`weights` is each declared weight over 100 with one trailing `max(100 - sum, 0) / 100`, so `22.22.40`
gives `[0.22, 0.22, 0.40, 0.16]` and every roll past the end repeats the 0.16. The integer division in
the primary pool is integer, then cast.

For this build: primary pool `10 x 1.03^(L/15) x sqrt(L)`, secondary `2.4 x sqrt(72L)`; declared rows
take `BaseItemSlotCalculationSettings` and rolled rows `FixedSlotCalculationSettings`; the rarity term
is exactly 1 at every tier because the asset sets `StatRarityBase` to 1.

**Tempering adds exactly the temper count, once, to a declared row.** The instructions are
`0x1455D37EB cmp byte [r14],0`, `0x1455D380A jne`, `0x1455D3813 movd xmm1,[rax+30h]`, `cvtdq2ps`,
`0x1455D381B addss xmm0,xmm1`, with +0x30 confirmed as `TimesUpgraded` against the reflection records.
`MaxAllowedUpgrades` is never read. A slot emits no row at all unless its `ContentType` is 0.

Everything the reconstruction below got wrong was three compensating errors at once: the pools came
from `HeroItemStatCalculation`'s Blueprint functions, which the magnitude path never calls, instead of
the natives `PrimaryStatPowerFromItemLevel` and `SecondaryStatPowerFromItemLevel`; weights were
normalised against the declared sum instead of 100; and the multipliers came from the fixed block
rather than the base block. For a rating those cancel to within 1 per cent, for the wearer's attribute
6 per cent and for Stamina 12 per cent, which was the entire residual.

The block fields the asset omits are C++ constructor defaults, not zero: `PrimaryWeight` 0.25,
`SecondaryWeight` 0.3, `StaminaWeight` 0.5, `PrimaryMultiplier` 0.7, `WeightMultiplier` 0.15,
`StaminaMultiplierItemLevelThreshold` 315, and `SecondaryMultiplier` 0.6 on the base block and 1.5 on
the fixed block. They live in `StatRows` because no shipped file carries them.

`GroundTruthTests` pins all fifteen in-game readings, tempered and untempered.

## The stat magnitude, as it was reconstructed before the disassembly

```
share            = a stat on the primary pool takes weight / (the item's own weight total)
                   a stat on the secondary pool takes weight / 100
magnitude        = pool(itemLevel) x DT_SlotWeights.StatWeight x share x multiplier x 1.08
```

rounded to the attribute's `RoundToDecimals` **before** it aggregates with anything else. The pools
and the multipliers are the shipped ones: the primary pool for Stamina and the wearer's own
attribute, the secondary pool for every rating, and `StaminaMultiplierAtThreshold` 1.5,
`PrimaryMultiplier` 0.525 and `SecondaryMultiplier` 1.125 from
`FixedSlotCalculationSettings`.

Only the 1.08 is not shipped. Every other term is read from the build, and the factor holds across
three item levels, three slots, two armour types and two rarities.

Rounding each contribution before aggregating is what the Eldrin Signet settles: two rolls of 8.45
print 16, not the 17 that rounding their sum would give.

**Every clean reading is reproduced exactly**, where clean means untempered or a random roll, since
a roll does not temper:

| Reading | Level | Computed | Prints |
| --- | --- | --- | --- |
| Drakesblood Tapestry, all six lines | 360 | 48 / 15 / 5 / 9 / 7 / 7 | the same |
| Drakesblood Tapestry, second drop, all four lines | 315 | 43 / 27 / 4 / 9 | the same |
| Skandi's Bands, all six lines | 360 | 51 / 15 / 5 / 9 / 7 / 7 | the same |
| Eldrin Signet, all three drops | 360 | 23 / 8 / 8 and each drop's rolls | the same |
| Witchslayer's pants untempered, all five lines | 315 | 89 / 66 / 6 / 11 / 12 | the same |
| Baneful Web Shroud, armour and both rolls | 285 | 40 / 6 / 6 | the same |
| Betrayer's Icy Veil, armour | 195 | 30 | the same |
| Darkfrost Arms, armour | 315 | 303 | the same |

The `w / (weight total)` share and a flat 1.08 cannot be told apart from `w / 100` and a 1.285 factor
on the primary pool alone, because all three shipped weight rows total 84. The share form is the one
written down, because it is derived from the item's own weights rather than from a second constant.

**Every input has been verified against its own asset, and the gap survives all of them.**

| Input | Verified by |
| --- | --- |
| The pool formulas | The Kismet bytecode reads `1.08 ^ (ItemLevel / 15) x sqrt(ItemLevel) x 6` and `sqrt(ItemLevel x 72) x 2.4`, each cast to float and returned, zero below level 15 |
| That those formulas are the live ones | `DefaultGame.ini` wires `[/Script/fellowship.CRItemSettings] StatPoolCalculation` to `HeroItemStatCalculation_C` |
| Slot weights | `DT_SlotWeights` gives Legs 0.09, Wrists 0.05, Ring 0.06, and its `ArmorWeight` column from the same rows drives an armour formula that is exact on three items |
| Item stat weights | `DT_StatWeightConfiguration` holds exactly three rows, named `22.22.40`, `40.17.27` and `28.28.28` for the values they carry |
| The multipliers | `DA_StatCalculationSettings`, with the 1.5-to-0.525 ratio independently confirmed by Skandi's Bands printing 15 and 5 on equal weights |

So the 1.08 is applied outside the Blueprint, by the native caller, and no shipped value names it.
It is a measured constant, held as `StatMagnitudes.NativeFactor`, and the readings it rests on are
the table above.

**What would still improve it:** a tooltip below item level 150 that is untempered, which no reading
yet is. The lowest clean evidence is a random roll at 285, and rolls do not carry the primary pool's
`1.08 ^ (ItemLevel / 15)` term, so nothing yet tests the primary pool below 315.

**The observation that would settle it is a low item level, anywhere from 15 to 150.** The two clean
levels on hand, 315 and 360, are 14 per cent apart, and over that span
`1.08 ^ (L / 15) x sqrt(L)` scaled by 1.28 is indistinguishable from `m x L ^ 1.5`. At item level 60
the two differ by about 30 per cent. One low tooltip decides whether the Blueprint's shape is right
and only its multiplier is stale, or whether the live C++ uses a different curve altogether.

## A random roll is a slot of a shipped weight

A roll's magnitude is fixed per item, not variable: all three Eldrin drops roll ratings worth exactly
8. It follows the same arithmetic as a declared slot, taking its weight from
`DynamicSlotCalculationSettings` by the kind of stat it landed on: `StaminaWeight` 0.5 for Stamina,
`SecondaryWeight` 0.3 for a rating, and for a primary attribute the same
`1 - StaminaWeight - SecondaryWeight` residual the unnamed placeholder already used, because
`PrimaryWeight` is declared on the struct and left at its default of 0 while `RandomStatPool` lists
Strength, Agility and Intellect as stats a slot can roll.

That is what `?randomStats=` computes. Naming the rolls of each ground-truth item reproduces the
game's card line for line:

A roll takes the same arithmetic as a declared slot, with a share decided by the kind of stat it
landed on:

| Roll lands on | Share | Where the share comes from |
| --- | --- | --- |
| A rating | 0.3 | `DynamicSlotCalculationSettings.SecondaryWeight`, shipped |
| Stamina | 0.375 | measured, from two readings that agree |
| The wearer's own attribute | 0.125 | measured, from one reading |

| Reading | Level, slot | Implies a share of | Computed at the share above | Prints |
| --- | --- | --- | --- | --- |
| Drakesblood Tapestry, Stamina | 315, Back 0.05 | 0.3685 | 16.28 | 16 |
| Witchslayer's pants, Stamina | 315, Legs 0.09 | 0.3711 | 29.31 | 29 |
| Eldrin Signet, Intellect | 360, Ring 0.06 | 0.1222 | 3.07 | 3 |

The two Stamina readings are independent, sit on different slots, and agree to within a third of a
per cent, so 0.375 is not a one-point fit. Neither share is the shipped one: `StaminaWeight` is 0.5,
and the `1 - StaminaWeight - SecondaryWeight` residual the unnamed placeholder uses is 0.2.

Betrayer's Icy Veil carries a roll on Intellect as well, and although it is tempered 3 of 3 and so
cannot calibrate anything, it does discriminate between the two tempering hypotheses: an additive
temper leaves its Intellect roll at 1, which agrees with a share of 0.125, while a multiplicative one
leaves it at 3 or 4, which does not agree with any share the Eldrin Signet allows.

`StatMagnitudes.Expected` still blends the three kinds into one number for a slot whose stat the
caller has not named, which no in-game tooltip ever shows, since a real item always has its rolls
resolved.

## Power potential is unreachable

`CRItemSettings.HeroItemPowerPotentialSettings` names all fourteen inputs: `ItemLevelScoreFactor`,
`TemperScoreFactor`, `ModSlotScoreFactor`, `GemSocketScoreFactor`, the four ability and trait-tree
factors, `SetBonusScoreFactor`, `LegendaryScoreFactor`, `AllocatedTalentPointPowerPotential`,
`ScorePerGemType`, `ScorePerAllocatedWeaponTrait` and `ScorePerAllocatedNecklaceTrait`. **None of the
140 shipped `.ini` files sets any of them**, so every value is a compiled-in C++ default, and there
is no per-slot score table anywhere. Fourteen unknowns against readings that disagree slot to slot at
identical level, rarity, ability and socket count - the Eldrin Signet reads 1,342 where the Drakesblood
Tapestry reads 1,292 - do not solve. It stays `?potentialPower=`.

One thing the readings do settle: potential is computed from **max** tempers, not current. The
Witchslayer's pants read 617 at 0 of 8 and again at 8 of 8.

## Tempering

`CT_ItemRarityAndLevelToMaxTempers` states the ceiling and nothing states the effect.
`CRDynamicItemStatSettings` has no temper field, and `GetAverageItemTemperTier` is the only
temper-named function in all 7,715 Blueprint classes, so the arithmetic is native.

Now that the untempered magnitude is exact, every tempered reading states its own surplus. Against a
flat **+1 per temper** applied to the untempered value:

| Reading | Level, tempers | Base | Base + n | Prints |
| --- | --- | --- | --- | --- |
| Betrayer's Icy Veil, Critical Strike | 195, 3 | 4.84 | 8 | 8 |
| Baneful Web Shroud, Intellect | 285, 4 | 2.51 | 7 | 7 |
| Baneful Web Shroud, Haste | 285, 4 | 5.64 | 10 | 10 |
| Witchslayer's pants, Intellect | 315, 8 | 5.54 | 14 | 14 |
| Darkfrost Arms, Strength | 315, 8 | 8.76 | 17 | 17 |
| Darkfrost Arms, Haste | 315, 8 | 19.32 | 27 | 27 |
| Sinbinding Stone, Intellect | 315, 8 | 6.08 | 14 | 14 |
| Sinbinding Stone, Critical Strike | 315, 8 | 7.38 | 15 | 15 |
| Vaultbinder's helm, Strength | 315, 8 | 8.11 | 16 | 17 |

**Eight of the nine land exactly**, across three item levels, four slots and three temper counts, and
the ninth is the one reading supplied as prose rather than a screenshot.

**Stamina does not follow it, and the stamina readings do not agree with each other.** Their surplus
per temper is 1.28 at 195, **0.29** at 285, and 1.60, 1.36 and 1.37 at 315, so the Baneful Web Shroud
would need a temper to give a stamina line barely a quarter of what every other item's gives. No
single rule covers all five, and a rule fitted to four of them contradicts the fifth.

Nothing is written into the code, because the rule that fits the primary attribute and the ratings
does not fit Stamina, and a tooltip that tempered two of its three lines would be wrong in a way that
is harder to see than one that tempers none.

### The effect is in no shipped file, and this is the search that establishes it

`CRDynamicItemSettings` names the complete input set of the dynamic item stat system, and every one
has been read:

| Input | Holds |
| --- | --- |
| `DT_StatWeightConfiguration` | three weight rows, `22.22.40`, `40.17.27`, `28.28.28` |
| `DT_DynamicHeroItemSlotDefinition` | which stat each slot is and the random stat pool |
| `DT_DynamicHeroItemLootRarityRuleSets` | how many slots each rung grants |
| `CT_DynamicHeroItemLootRarityDropChance` | drop weighting by difficulty |
| `CT_ItemRarityAndLevelToMaxTempers` | the tempering **ceiling** only |
| `DT_DynamicItemGemSocketMapping` | gem sockets |
| `DA_StatCalculationSettings` | the pools' multipliers, and no temper field |
| `HeroItemStatCalculation_C` | the two pool formulas, which take item level and nothing else |

Beyond that set: the 32 item tables under `Content/Items` hold nothing keyed by tempers except
`CT_DungeonRewardItemTimesUpgraded` and `CT_DungeonRewardMaxItemUpgrades`, which state how many
tempers a dungeon reward **drops with** by difficulty. None of the 140 shipped `.ini` files carries a
tempering magnitude; what `CRItemSettings` does carry is entirely cost-side - `TemperRarityMod`,
`TemperTierMod`, `TemperItemSlotMod`, `TemperItemLevelCostExponentialFactor` and `ItemBaseCosts` -
and `CRBackendSharedVars` mirrors exactly those four and nothing more. No Blueprint function computes
an upgraded magnitude: `UW_ItemUpgradeStatRow` is handed `StatValueText` and `StatValueTextNew` as
finished text, and `GetAverageItemTemperTier` is the only temper-named function in all 7,715
Blueprint classes. `CRHeroItemInstance` stores `TimesUpgraded` as a bare integer beside `Level` and
`ItemRarity`, and carries no magnitude, so the client computes the printed number from those three.

The effect is therefore compiled into `DynamicLootSystemRuntime`. Recovering it means disassembling
`fellowship-Win64-Shipping.exe`, which ships stripped: the reflection tables this repo already reads
give property names and types, never function bodies.

## Line order

Armour prints above every stat line on both items that have one, then the declared slots in the
order the item declares them, then the lines that exist only because a roll landed on them. A roll
landing on a declared stat aggregates in place rather than moving the line, which is how the third
Eldrin drop keeps Expertise third at 16.

That reading holds for six of the eight tooltips, including every one with no tempering: the pants
untempered, Darkfrost Arms, Skandi's Bands and all three Eldrin drops. The two that disagree are both
at 8 of 8 tempers, and each swaps exactly one adjacent pair: the tempered pants lift Intellect above
Stamina, and the Sinbinding Stone lifts Critical Strike above Haste. Nothing yet separates a
tempering-related reorder from a second rule.

The previous ordering lifted the hero's own attribute above Stamina, which was read off the tempered
pants alone and disagrees with all six untempered readings.
