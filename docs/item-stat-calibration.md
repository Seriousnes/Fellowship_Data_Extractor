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
| Witchslayer's pants, Legs Cloth 315 | `0.165 x (315 x 1.41 + 90)` = 88.13 | 89 |
| Darkfrost Arms, Weapon Plate 315 | `0.28 x (315 x 2.8 + 200)` = 302.96 | 303 |
| Skandi's Bands, Wrists Cloth 360 | `0.085 x (360 x 1.41 + 90)` = 50.80 | 51 |
| Eldrin Signet, Ring Cloth 360 | Ring `ArmorWeight` is 0 | no armour line |
| Sinbinding Stone, Relic 315 | Relic `ArmorWeight` is 0 | no armour line |

**Tempering does not move it.** The same pants read `+89 Armor` at 0 of 8 tempers and at 8 of 8, and
the level is 315 in both.

The rounding is a ceiling. Darkfrost Arms and Skandi's Bands round the same either way, so the pants
are the only observation that separates ceiling from nearest, and they are also the one item where
solving for `FlatArmor` wants 92 to 98 rather than the shipped 90. The ceiling may therefore be
standing in for a one per cent error somewhere in the product. It fits all three; give it up rather
than defend it if a fourth item disagrees.

## The primary slot takes the shipped multiplier

`BaseItemSlotCalculationSettings` declares only `StaminaMultiplierAtThreshold`, so a primary slot
found no `PrimaryMultiplier` there and fell back to 1, which was invented rather than read.
`FixedSlotCalculationSettings` ships 0.525, and Skandi's Bands settles it: its Stamina and Intellect
slots carry the **same** weight of 22 and the game prints 15 and 5, a ratio of 3.0 whose rounding
band is 2.636 to 3.444. The shipped multipliers give 1.5 / 0.525 = 2.857, inside it. The invented 1
gives 1.5, nowhere near.

A multiplier a block leaves at its default now reads the fixed-slot block's shipped value.

## The stat pools are the remaining gap

With the shipped multipliers in place, each observation states what the pool must have been. Every
band below is the printed integer widened by half a point in each direction.

| Pool | Level | Must be | `HeroItemStatCalculation` gives | Factor |
| --- | --- | --- | --- | --- |
| Primary | 360 | 893 to 933 | 721.9 | 1.24 to 1.29 |
| Primary | 315 | 685 to 809 | 536.0 | 1.28 to 1.51 |
| Secondary | 360 | 397 to 422 | 386.4 | 1.03 to 1.09 |
| Secondary | 315 | 384 to 420 | 361.4 | 1.06 to 1.16 |

The two primary bands overlap at a single scale near 1.28 and the two secondary bands near 1.07, so
one constant per pool fits all six observations. The bytecode was re-read to rule out a
mis-extraction, and it says exactly what the export carries:
`CalcPrimaryStatPoolFromItemLevel` returns `1.08 ^ (ItemLevel / 15) x sqrt(ItemLevel) x 6` and
`CalcSecondaryStatPoolFromItemLevel` returns `sqrt(ItemLevel x 72) x 2.4`, both zero below level 15.

**Nothing in the build calls either function.** The live path is native, so these two are a design
statement the C++ has moved on from, and the factors above are the measurement of how far. They are
not written into the code, because a fitted scalar is exactly the hardcoded data fact this repo
forbids.

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

| Item | Named rolls | Game | Computed |
| --- | --- | --- | --- |
| Witchslayer's pants 315 | Stamina, Spirit | 89 / 66 / 6 / 11 / 12 | 89 / **65** / 4 / 10 / **11** |
| Skandi's Bands 360 | CritChance, Expertise | 51 / 15 / 5 / 9 / 7 / 7 | 51 / 12 / 4 / 9 / **7** / **7** |
| Eldrin Signet, first drop | Intellect, Haste | 23 / 8 / 8 / 3 / 8 | 18 / 7 / 7 / 5 / **8** |
| Eldrin Signet, second drop | Haste, Haste | 23 / 8 / 8 / 16 | 18 / 7 / 7 / **16** |
| Eldrin Signet, third drop | Expertise, Haste | 23 / 8 / 16 / 8 | 18 / 7 / **15** / **8** |

Every rolled magnitude in bold is the game's own or one off it, on the uncorrected pools. The
declared lines carry the pool gap above, and the primary-attribute roll is the one kind that misses
by more than one: 5 against the 3 the Eldrin Signet prints.

`StatMagnitudes.Expected` still blends the three kinds into one number for a slot whose stat the
caller has not named, which no in-game tooltip ever shows, since a real item always has its rolls
resolved.

## Tempering

`CT_ItemRarityAndLevelToMaxTempers` states the ceiling and nothing states the effect.
`CRDynamicItemStatSettings` has no temper field, and `GetAverageItemTemperTier` is the only
temper-named function in all 7,715 Blueprint classes, so the arithmetic is native.

The pants pair does not settle it and no factor is fitted from it. Read as a uniform multiplier its
Intellect asks for 2.33 while its Stamina asks for about 1.24, and one factor cannot be both. Three
confounds sit on that pair: which lines carry rolls, what the modifier each instance holds
contributes, and a white-name against turned-magnitude distinction that is a pixel or two wide at
that resolution.

One hypothesis remains open. Taking each printed line of the two 8 of 8 items minus what is computed,
the surplus splits across stamina, the hero attribute and the secondary stat as 0.557 : 0.135 : 0.307
on Darkfrost Arms and 0.585 : 0.161 : 0.254 on the pants, near the shipped 0.5 stamina and 0.3
secondary. That is the shape of a temper adding a budget rather than scaling one, and it is
falsifiable: **an additive temper predicts that two items differing only in declared weight converge
in their tempered stamina-to-primary ratio, while a multiplicative one preserves the untempered ratio
exactly.**

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
