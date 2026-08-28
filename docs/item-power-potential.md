# Item power potential

Every address here was decoded against the **24952830** shipping binary. The exe changes between
builds, so re-anchor from the name-literal chain before reusing one.

Fellowship carries two power numbers, as two separate native floats, computed by two different
routines. They are not the same scoring rule, and the loadout total is not the sum of the per-item
function.

| Number | Written to | Printed as | Computed by |
| --- | --- | --- | --- |
| Per item | `CRItemViewModel.ItemPowerPotential` (+0xEC), at `0x14544D4D2` | "Power Potential", on the item card | `UCRItemsBlueprintLibrary::CalculateItemPowerPotential`, `0x145317B00` |
| Per loadout | `CRHeroInfoViewModel.PowerPotential` (+0x1B8) and `CRPlayerTooltipViewModel.PowerPotential` (+0x190) | "Power Score", on the hero panel | an unnamed native at `0x145349B70` |

Both are native. `CalculateItemPowerPotential` has zero importers across all 46,651 cooked packages,
against sibling functions in the same library that resolve, so the zero is real and not a broken
detector. No Blueprint reads any of the fourteen factors; `UW_PowerPotentialBlock` receives the float
already computed and only rounds it half to even to no fractional digits for display.

## The factors, and where they live

`[/Script/fellowship.CRItemSettings]` in `fellowship/Config/DefaultGame.ini`, inside the pak. The class
is a native `UDeveloperSettings` and ships no asset, so the ini is the only source. `ItemSettings.cs`
reads it and the export carries it as `items.powerPotential`.

The per-item body reads eleven of the fourteen. `ScorePerGemType`, `ScorePerAllocatedWeaponTrait` and
`ScorePerAllocatedNecklaceTrait` are read only by the loadout body, and
`AllocatedTalentPointPowerPotential` only by the loadout body.

## The slot weight

Both bodies scale the item level term, and only that term, by a per-slot float that is in neither the
ini nor the exe: `FCRSlotWeightRow::StatWeight` keyed by `ECRHeroItemType`, from
`/Game/Items/DT_SlotWeights.DT_SlotWeights`. The export already carries it as
`statCalculation.slotWeights[<slot>].statWeight`.

**Key on the row's `ItemType` field, never on the row name.** The row named `Legs` carries
`ECRHeroItemType::Waist` and `Gloves` carries `Hands`. The game builds its map from row+0x08, decoded
at `0x145314782` and `0x145314795`. `StatCalculation.SlotWeightRows` reads the ordinal, so the export
is keyed correctly; a reader that took the row name would silently get two of the twelve wrong.

All twelve types are present, so the map's `0.0f` default can never fire on shipped data. The twelve
sum to 0.88, not 1.0 — they are per-slot stat budget shares, and nothing divides by their sum.

## The two byte tests

A `DynamicSlots` element is `CRHeroItemDynamicSlot`, stride 0x18: `SlotType` at +0, `ContentType` at
+1, `RerollableSlotIndex` at +4, `ContentVariantData` at +8.

- `ECRHeroItemDynamicSlotType`: BaseItemFixed 0, DynamicFixed 1, **Rerollable 2**
- `ECRHeroItemDynamicSlotContentType`: Stat 0, **GemSocket 1**, **SetBonus 2**, AbilityRank 3,
  **GrantedAbility 4**, **TraitTree 5**, ItemTrait 6, GemPower 7

So `byte[e+0] == 2` counts **rerollable** slots, and the four `byte[e+1]` tests are GemSocket,
SetBonus, GrantedAbility and TraitTree. `ModSlotScoreFactor` multiplies the rerollable count, which in
the export is `statCalculation.rarityAdditions[<ruleSet>][<tier>].randomRolls`.

**Two enums have near-identical names and only one belongs to the instance.**
`ECRDynamicHeroItemSlotType` (HeroRelativePrimaryStat 0, Stat 1, RandomStat 2, GemSocket 3, …) is the
`SlotType` of `CRDynamicHeroItemSlotDefinition`, the table row. It is **not** what the instance's byte
holds, and reading `byte[e+0] == 2` against it says RandomStat, which is wrong. Match the struct, not
the name.

## Per item, `0x145317B00`

`float CalculateItemPowerPotential(const FCRHeroItemInstance&)`, signature read from `FFunctionParams`
`0x149E5E900`: two properties, one const-ref struct at frame 0x00 and a float return at frame 0x70.

```
S = GetDefault<UCRItemSettings>()->HeroItemPowerPotentialSettings     // CDO+0x4E8
staticData = ItemStaticDataSubsystem->Find(Item.ItemID)
if (!staticData) return 0.0f                                         // 0x145317B43
type = staticData[+0x60]                                             // ECRHeroItemType
W    = SlotStatWeight[type]

score  = Item.Level(+0x20) * S.ItemLevelScoreFactor * W              // 0x145317C9C, CA8, CB8
score += Item.MaxAllowedUpgrades(+0x34) * S.TemperScoreFactor        // 0x145317CA2, CB0, CC1
score += count(slots where SlotType == Rerollable) * S.ModSlotScoreFactor
score += any(ContentType == GemSocket)                     ? S.GemSocketScoreFactor         : 0
score += type==Weapon   && any(ContentType == GrantedAbility) ? S.WeaponAbilityScoreFactor     : 0
score += type==Trinket  && any(ContentType == GrantedAbility) ? S.RelicAbilityScoreFactor      : 0
score += type==Weapon   && any(ContentType == TraitTree)      ? S.WeaponTraitTreeScoreFactor   : 0
score += type==Necklace && any(ContentType == TraitTree)      ? S.NecklaceTraitTreeScoreFactor : 0
score += any(ContentType == SetBonus)                      ? S.SetBonusScoreFactor          : 0
score += Item.ItemRarity(+0x38) == Legendary               ? S.LegendaryScoreFactor         : 0
return score                                                         // 0x145317D34
```

The associativity is load bearing: the weight multiplies the level term alone, grouped
`(Level x Factor) x W`, because `mulss xmm1,xmm7` at `0x145317CB8` precedes `addss xmm1,xmm0` at
`0x145317CC1`. All arithmetic is float32 in source order. There is no round, floor, ceil, clamp, min,
max or division anywhere in `0x145317B00..0x145317D4B`.

Seven of the ten terms are booleans, and the six slot-derived ones are idempotent: a second matching
slot adds nothing. In the machine code they are one if/else-if chain keyed on `ItemType`, evaluated per
element, and a slot matching an ability or trait-tree branch jumps past the set-bonus test. Writing
them as six independent `any()` predicates is equivalent only because a ContentType cannot be both 4
or 5 and 2 at once.

`ItemRarity` (+0x38) is read, not `OriginalRarity` (+0x39), so an upgraded rarity counts.

## Per loadout, `0x145349B70`

Recorded so it is not decoded twice. Nothing here is built.

```
for each equipped item with a non-zero ItemID
    s  = Level * ItemLevelScoreFactor * SlotStatWeight[type]
    s += TimesUpgraded(+0x30) * TemperScoreFactor
    for each dynamic slot                                    // per element, not idempotent
        SlotType == RandomStat            -> += ModSlotScoreFactor
        type==Weapon  && Content==Ability  -> += WeaponAbilityScoreFactor
        type==Trinket && Content==Ability  -> += RelicAbilityScoreFactor
    n = count(TreeTraits(+0x58) where allocated)
    type==Weapon   -> s += ScorePerAllocatedWeaponTrait   * n
    type==Necklace -> s += ScorePerAllocatedNecklaceTrait * n
    socketed gem   -> s += ScorePerGemType[tier]           // skipped when tier == MAX
    virtual [staticData+0x300]  -> s += SetBonusScoreFactor
    Legendary      -> s += LegendaryScoreFactor
    total += s
return powf(AllocatedTalentPointPowerPotential, totalTalentPointsSpent) * total   // 0x145349F3B
```

`GemSocketScoreFactor`, `WeaponTraitTreeScoreFactor` and `NecklaceTraitTreeScoreFactor` are copied to
the frame here and never reloaded — dead in this body, live only in the per-item one.

Four decoded divergences separate the two: the per-item body reads the tempering **ceiling** where the
loadout reads **tempers taken**; a flat socket bonus where the loadout reads the **socketed gem's**
tier; a flat tree bonus where the loadout counts **allocated** traits; and a slot scan where the
loadout calls a virtual. Reading those as "per item scores capacity, per loadout scores realised state"
is an inference from the four divergences, not from any name in the binary.

Because of that, **never call the per-item function to mean an item's contribution to a Power Score.**
The game exposes the per-item potential and the whole-loadout total, never the loadout body's per-item
term.

## What does not enter

`GemSocketPowerMultipliers` and `ItemGemPowers` are read by neither body; they belong to the separate
integer Gem Power system (`CRGemPowerComponent`, `CRGemTierDefinition.GemPower`). `MaxItemLevel` and
`ItemLevelsPerUpgrade` normalise neither result. Both bodies are decoded end to end, so this is
exhaustive for them.

## Confirmed in game

Seven readings, all reproduced to the printed digit. `ItemPowerTests` pins every one. Between them
**all ten terms of the per-item body have now been observed firing in game**, and both `ItemType` gates
have been observed refusing to.

| Term | Value | Confirmed by |
| --- | --- | --- |
| `ItemLevelScoreFactor` | 14 | every reading |
| `TemperScoreFactor` | 20 | the gloves pair, at the ceiling |
| `ModSlotScoreFactor` | 20 | the gloves pair |
| `GemSocketScoreFactor` | 40 | Masochistic Razor Loop |
| `WeaponAbilityScoreFactor` | 80 | Asha's Chronoshift Spire |
| `RelicAbilityScoreFactor` | 80 | Grimoire of Resurrection, Ancient Poultry Fetish |
| `WeaponTraitTreeScoreFactor` | 120 | Asha's Chronoshift Spire |
| `NecklaceTraitTreeScoreFactor` | 120 | Pendant of the Deep, at 0 allocated |
| `SetBonusScoreFactor` | 20 | Witchslayer's Legs of Determination |
| `LegendaryScoreFactor` | 1000 | Masochistic Razor Loop |

The game's own description of the field, printed under it on some cards, is "The item's potential
power; derived from its Item Level, Rarity, and the Modifiers it has." Rarity enters twice and only
twice: the legendary term, and the slots the rung's `rarityAdditions` row adds.

**Asha's Chronoshift Spire**, item 5326, Weapon, Epic, item level 315, Tempered 0 / 6, printed **825**.

```
315 x 14 x 0.11  = 485.1     level x factor x the Weapon stat weight
6   x 20         = 120       the tempering CEILING, not the nought taken
1   x 20         =  20       one rerollable slot at Epic on the Weapon rule set
                     80      a granted ability on a weapon
                    120      a trait tree on a weapon
                 -------
                  825.1  ->  825
```

Tempered 0 / 6 is the discriminator the decode turned on: reading `TimesUpgraded` gives 705.

**Webbed Gloves and Soaked Gloves**, Hands, Regal, item level 315, **Tempered 4 / 8 and 6 / 8**, both
printed **485**. Two items differing only in tempers taken, scoring identically, is direct proof that
the term is the ceiling.

```
315 x 14 x 0.06  = 264.6
8   x 20         = 160       the same ceiling for both cards
3   x 20         =  60       three rerollable slots at Regal on Default_NoGemSlot
                 -------
                  484.6  ->  485
```

That pair also settles the rerollable-versus-RandomStat question: counting guaranteed RandomStat slots
gives 465, and the game prints 485.

**Masochistic Razor Loop**, item 5221, Ring, Legendary, item level 360, **Non Temperable**, carrying a
granted ability (Razor Crash), three legendary stats and a gem socket, printed **1,342**.

```
360 x 14 x 0.06  = 302.4     level x factor x the Ring stat weight
                  1000       LegendaryScoreFactor
                    40       GemSocketScoreFactor
                 -------
                 1342.4  ->  1342
```

This card confirms four things at once. `LegendaryScoreFactor` is 1000 and `GemSocketScoreFactor` is
40, both previously read from the ini alone. Non Temperable means `MaxAllowedUpgrades` is 0, not a
constructor default. And the two `ItemType` gates are confirmed **negatively**: the ring carries a
granted ability and three legendary stats and scores nothing for either, because a Ring is neither
Weapon nor Trinket and neither Weapon nor Necklace. Without the ability gate the card would print
1,422.

The `Legendary` rule set's tier 6 row lists its two stat slots as `guaranteedSlots`, with
`randomRolls` 0, so the rerollable term contributes nothing here either — a second reading of the same
distinction the gloves pair drew.

**Grimoire of Resurrection**, item 5254, Relic, Epic, item level 315, Tempered 0 / 6, printed **485**,
and **Ancient Poultry Fetish**, item 5252, Relic, Regal, item level 315, Tempered 0 / 8, printed
**545**. Both carry a relic ability, and the `Relic` rule set makes one a guaranteed slot from tier 2
up.

```
Grimoire  315 x 14 x 0.06 = 264.6   Epic  is tier 3: randomRolls 1
          6 x 20          = 120
          1 x 20          =  20
                             80     RelicAbilityScoreFactor
                          -------
                           484.6 -> 485

Fetish    315 x 14 x 0.06 = 264.6   Regal is tier 5: randomRolls 2
          8 x 20          = 160
          2 x 20          =  40
                             80     RelicAbilityScoreFactor
                          -------
                           544.6 -> 545
```

`RelicAbilityScoreFactor` is 80, twice over. The Fetish also settles what its `Imbued Essence:
Sapphire` slot is: the `Relic` rule set carries no `GemSocket` special slot at any tier, so the essence
is a `GemPower` from the randomized pool and scores nothing. Adding a socket term would print 585.

**Pendant of the Deep**, item 5235, Necklace, Heroic, item level 255, Tempered 0 / 4, showing
**Imbued Traits (0 / 2)**, printed **470**.

```
255 x 14 x 0.07  = 249.9     the Neck stat weight
4   x 20         =  80
1   x 20         =  20       Champion, tier 4 of Default_NoGemSlot_Necklace: randomRolls 1
                   120       NecklaceTraitTreeScoreFactor
                 -------
                  469.9  ->  470
```

`NecklaceTraitTreeScoreFactor` is 120, and the card is printed at **0 / 2 traits allocated**. The term
is therefore a flat boolean on the slot's presence, exactly as `0x145317B00` reads it, and not the
allocated count `0x145349B70` uses. That is the first in-game confirmation of one of the four
divergences between the two bodies: an unallocated tree scores the same 120 as a full one.

The card prints the rung as **Heroic**, which is the tier named `Champion`. The ladder's printed names
sit one rung below the internal ones from tier 4 up, so resolve a card's rung by printed name.

**Witchslayer's Legs of Determination**, item 5309, Legs, Regal, item level 315, Tempered 8 / 8,
carrying **Set: (2/2) Drakheim's Absolution**, printed **617**.

```
315 x 14 x 0.09  = 396.9     the Legs stat weight
8   x 20         = 160
2   x 20         =  40       Heroic, tier 5 of SetBonus_NoGemSlot: randomRolls 2
                    20       SetBonusScoreFactor
                 -------
                  616.9  ->  617
```

`SetBonusScoreFactor` and `ModSlotScoreFactor` are both 20, so the 60 above the level and temper terms
could split either way on the arithmetic alone. The rule set decides it: `SetBonus_NoGemSlot` tier 5
states `randomRolls` 2 and carries `SetBonus` as a guaranteed slot, so the split is 40 + 20 and not
60 + 0. Reading the rung's row rather than counting the card's coloured stat lines is what makes this
reading load bearing.

## Still open

All of these sit on the loadout body or the backend wire path, and none of them affect the per-item
number.

- The virtual at `[staticData+0x300]` gating the loadout's set bonus term: no concrete body resolved.
- Whether the loadout body's return is what reaches the backend as the `powerPotential` wire key. The
  serializer is table-dispatched with no direct callers, and `FCRPlayerProfile::PowerPotential` is a
  **received** field.
- The hero number a player sees is dual sourced: two of the three writers of
  `CRHeroInfoViewModel.PowerPotential` are server supplied, and the player tooltip takes the received
  value first and is overwritten by the local roll-up only when a matching local loadout is found.
- Whether the client reports its own computed number to the backend.
- The name of the unreflected C++ struct carrying the wire record.
