---
name: datamining-fellowship
description: Use when a Fellowship tooltip number is wrong or missing, when a value is not in any data table, when reconstructing a game formula from screenshots, when reading the shipping binary, or when deciding where a fact about items, stats, tempering or power potential actually lives.
---

# Datamining Fellowship

The general method is the `datamining-unreal-builds` skill. The asset-level data model, the tag join
and the export contract are `extracting-fellowship-data`. This skill is what the method resolves to in
this build, and what has already been recovered so nobody pays for it twice.

**The failure this prevents:** fitting a formula to screenshots. It cost a full session here. Three
independent mistakes cancelled to within 1 per cent on ratings and 12 per cent on Stamina, so only one
was visible, and the visible one looked like a missing mechanic rather than a wrong formula. The answer
was four instructions in the binary.

## The ladder, resolved for this build

| Rung | Command | What it gives |
| --- | --- | --- |
| 1. Config | `dotnet run --project src/FellowshipExtractor -- raw ".ini"` | All 140 under `output/raw`. `[/Script/fellowship.CRItemSettings]` in `DefaultGame.ini` holds `StatPoolCalculation`, `ItemLevelsPerUpgrade`, `MaxItemLevel` and the tempering **cost** mods. `[/Script/fellowship.CRItemStaticDataSubsystem]` names `DT_SlotWeights`, `DT_AttributeProperties`, `DT_HeroItems` and the drop curves. `DynamicLootSystem.ini` names the loot plugin's seven assets. |
| 2. Schema | `UsmapParser` on `output/fellowship.usmap` | The **complete** input set of a system. `CRDynamicItemSettings` enumerates eight things and there is no ninth. Do this before searching the asset tree; it is what tells you when to stop. |
| 3. Assets | `dump <name>` | JSON under `output/dump`. Read rows, never file names. |
| 4. Binary | see below | Everything the first three do not carry. |

Supporting exports worth having: `ability-constants.jsonl` (13,156 tuning rows) and `tags.jsonl`,
both written by the default run, `functions` (`blueprint-functions.jsonl`, every Blueprint function
and its calls), `locres`, `list` (`output/files.txt`, every path in the build).

## Reading the shipping binary

`C:\Program Files (x86)\Steam\steamapps\common\Fellowship\fellowship\Binaries\Win64\fellowship-Win64-Shipping.exe`,
177 MB, stripped, one monolithic image. **Static parsing only. Never launch the game, attach a
debugger, or go near EasyAntiCheat.**

`src/FellowshipExtractor/Usmap/PeImage.cs` already maps sections and virtual addresses and
`ReflectionScanner.cs` walks property registration records, so a harness is those two plus
`#:package Iced@1.21.0` in a file-based app: disassemble at a VA, and find cross references to a VA.

The anchor chain: UTF-16 literal in `.rdata`, the `lea` referencing it inside an `FName` constructor
stub, the sole reader of that `FName` global, then that thunk's callers. You are now inside the routine
that resolved the function by name. For a field, take the offset from the reflection record and search
for loads at that displacement.

## Already recovered, do not redo

| What | Where |
| --- | --- |
| Item stat magnitude, whole calculation | `UCRDynamicHeroItem` virtual at **0x1455D33F0**, vtable 0x148C8B230 slot +0x2D0 |
| The pools it really calls | natives `PrimaryStatPowerFromItemLevel` 0x1455D4050, `SecondaryStatPowerFromItemLevel` 0x1455D4190 |
| Tempering | `0x1455D3813 movd xmm1,[rax+30h]`, `cvtdq2ps`, `0x1455D381B addss`, gated by `0x1455D37EB cmp byte [r14],0` |
| Round half to even, 1e-8 snap | **0x1412922F0** |
| Armour, one ceil after the slot multiply | **0x14531AC8B** |
| `FCRHeroItemInstance` | Level 0x20, TimesUpgraded 0x30, MaxAllowedUpgrades 0x34, ItemRarity 0x38, DynamicSlots 0x40 |
| Dynamic slot, stride 0x18 | SlotType 0x00, ContentType 0x01, RerollableSlotIndex 0x04, ContentVariantData 0x08 |
| `ECRItemStat` ordinals | table at 0x148B176D0; Stamina 1, Strength 2, Intellect 3, Agility 4, Power 18 |

The formula and every in-game reading it reproduces are in `docs/item-stat-calibration.md`, implemented
in `StatRows`/`StatMagnitudes` and pinned by `GroundTruthTests`.

## Traps this build sets

**An omitted asset field is a live C++ constructor default, not zero.** `DA_StatCalculationSettings`
ships three of eight fields per block; the rest are real values: `PrimaryWeight` 0.25,
`SecondaryWeight` 0.3, `StaminaWeight` 0.5, `PrimaryMultiplier` 0.7, `WeightMultiplier` 0.15,
`StaminaMultiplierItemLevelThreshold` 315, and `SecondaryMultiplier` 0.6 on the base block, 1.5 on the
fixed block. They live in `StatRows` because no shipped file carries them. `SecondaryBaseMultiplier` is
72 and `PrimaryDivisible` is 15 the same way.

**A Blueprint function can be named right, wired through config, and never called.**
`HeroItemStatCalculation`'s `CalcPrimaryStatPoolFromItemLevel` is wired via
`CRItemSettings.StatPoolCalculation` and the magnitude path calls the two natives instead. Check
`blueprint-functions.jsonl`: if no Blueprint function computes your number, it is native.

**A parser that raises no exception has told you nothing.** Cooked packages degrade silently. Verify
values, never the absence of errors.

**Screenshot annotations are reconstructions.** Only the printed total is observed. Two readings
recorded as "declared plus one roll" were declared plus two, and one recorded as carrying a roll
carried none. Let the recovered formula tell you the split.

## Known unreachable

`CRItemSettings.HeroItemPowerPotentialSettings` names fourteen factors and no `.ini` sets any of them,
so all fourteen are compiled-in defaults and power potential stays caller-supplied. Native C++ bodies
called from Blueprint bytecode are visible as a call with its arguments, never as what the callee
computes.

## Stop fitting when

- Every input the schema enumerates is read and a residual remains. The residual is code.
- The runtime struct carries the inputs but not the output. `CRHeroItemInstance` stores level, rarity,
  `TimesUpgraded` and slots and no magnitudes, so the client computes the printed number.

## Delegating it

Anchor strategies are independent, so this parallelises. One agent builds the harness while others
chase anchors; one lead each, told plainly that a dead end is an acceptable answer and that guessing a
formula from the observed numbers would corrupt the evidence; then adversarial verification on separate
lenses, with a fidelity lens that re-decodes every cited address rather than trusting the summary. Here
the refutation lens found a gate the synthesis missed and the fidelity lens found a double rounding in
the port. Neither surfaced from a single pass.

## Red flags

"One more screenshot would settle it." "It fits every reading I have." "That field is absent, so it is
zero." "The function is named for exactly this." "It is native, so it is unreachable."

Every one means: go back to rung 2, and if the schema is exhausted, disassemble.
