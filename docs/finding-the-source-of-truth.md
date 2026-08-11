# Finding the source of truth for a computed number

The method is the `datamining-unreal-builds` skill. This file is what that method resolves to in this
build: the exact commands, the exact anchors, and the findings that are worth not rediscovering.

## The ladder, in this repository

**1. Shipped `.ini` files.** `dotnet run --project src/FellowshipExtractor -- raw ".ini"` writes all
140 under `output/raw`. `[/Script/fellowship.CRItemSettings]` in `DefaultGame.ini` is where
`StatPoolCalculation`, `ItemLevelsPerUpgrade`, `MaxItemLevel` and the tempering cost mods live.
`[/Script/fellowship.CRItemStaticDataSubsystem]` names `DT_SlotWeights`, `DT_AttributeProperties`,
`DT_HeroItems` and the drop curves. `DynamicLootSystem.ini` names the loot plugin's seven assets.

**2. The reflection schema.** Parse `output/fellowship.usmap` with CUE4Parse's `UsmapParser` and read
the settings class. `CRDynamicItemSettings` enumerates the complete input set of the dynamic item stat
system: eight entries, no ninth.

**3. The assets.** `dump <name>` writes one as JSON under `output/dump`.

**4. The binary.** `C:\Program Files (x86)\Steam\steamapps\common\Fellowship\fellowship\Binaries\Win64\fellowship-Win64-Shipping.exe`,
177 MB and stripped. `src/FellowshipExtractor/Usmap/PeImage.cs` maps sections and addresses and
`ReflectionScanner.cs` walks property registration records, so a disassembler harness is
`#:package Iced@1.21.0` plus those two files.

## Anchors already recovered

| What | Where |
| --- | --- |
| Item stat magnitude, the whole calculation | `UCRDynamicHeroItem` virtual at **0x1455D33F0**, vtable 0x148C8B230 slot +0x2D0 |
| The pools it actually calls | natives `PrimaryStatPowerFromItemLevel` and `SecondaryStatPowerFromItemLevel`, bodies at 0x1455D4050 and 0x1455D4190 |
| Tempering | `0x1455D3813 movd xmm1,[rax+30h]`, `cvtdq2ps`, `0x1455D381B addss`, gated by `0x1455D37EB cmp byte [r14],0` |
| Round half to even, 1e-8 near-tie snap | helper at **0x1412922F0** |
| Armour | one ceil after the slot multiply, at **0x14531AC8B** |
| `FCRHeroItemInstance` offsets | Level 0x20, TimesUpgraded 0x30, MaxAllowedUpgrades 0x34, ItemRarity 0x38, DynamicSlots 0x40 |
| Dynamic slot offsets, stride 0x18 | SlotType 0x00, ContentType 0x01, RerollableSlotIndex 0x04, ContentVariantData 0x08 |
| `ECRItemStat` ordinals | enumerator table at 0x148B176D0, Stamina 1, Strength 2, Intellect 3, Agility 4, Power 18 |

The resulting formula and every reading it reproduces are in `item-stat-calibration.md`.

## What this build taught about its own data

`DA_StatCalculationSettings` omits most of each calculation block, and every omitted field is a live
C++ constructor default: `PrimaryWeight` 0.25, `SecondaryWeight` 0.3, `StaminaWeight` 0.5,
`PrimaryMultiplier` 0.7, `WeightMultiplier` 0.15, `StaminaMultiplierItemLevelThreshold` 315, and
`SecondaryMultiplier` 0.6 on the base block and 1.5 on the fixed block. They are carried in `StatRows`
because no shipped file has them.

`HeroItemStatCalculation`'s Blueprint functions are wired through `CRItemSettings.StatPoolCalculation`
and the magnitude path never calls them. `output/blueprint-functions.jsonl` is how you check that: if
no Blueprint function computes the number, it is native.

Power potential remains unreachable. `CRItemSettings.HeroItemPowerPotentialSettings` names fourteen
factors and no `.ini` sets any of them, so all fourteen are compiled-in defaults.
