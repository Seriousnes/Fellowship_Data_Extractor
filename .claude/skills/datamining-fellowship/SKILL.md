---
name: datamining-fellowship
description: Use when a Fellowship tooltip number is wrong or missing, when a value is not in any data table, when reconstructing a game formula from screenshots, when reading the shipping binary, or when deciding where a fact about items, stats, tempering or power potential actually lives.
---

# Datamining Fellowship

The asset-level data model, the tag join and the export contract are `extracting-fellowship-data`.

**The failure this prevents:** fitting a formula to screenshots. It cost a full session here. Three
independent mistakes cancelled to within 1 per cent on ratings and 12 per cent on Stamina, so only one
was visible, and the visible one looked like a missing mechanic rather than a wrong formula. The answer
was four instructions in the binary.

## The ladder, resolved for this build

| Rung | Where | What it gives |
| --- | --- | --- |
| 0. Already done? | `output/export-<build>/entities.jsonl` | Whether the value is already projected onto a document. This is the **output** of extraction, not a view of the build: it answers "does the export have this entity's cast time", never "what fields does `DA_StatCalculationSettings` declare". A miss here means keep going, not that the asset lacks it. |
| 1. Schema | `UsmapParser` on `mappings/fellowship-<build>.usmap` | The **complete** input set of a system. `CRDynamicItemSettings` enumerates eight things and there is no ninth. Do this before searching the asset tree; it is what tells you when to stop. |
| 2. Config and assets | a reader under `src/FellowshipExtractor/Extraction/` | `ProviderFactory.CreateAsync` mounts the containers; `AssetRegistry.ReadAssets` is the index and `provider.LoadPackageObject<T>` reads a package. `.ini` files are container files like any other. Read rows, never file names. |
| 3. Binary | see below | Everything the first three do not state. |

What the config already states, so nobody reads it twice:
`[/Script/fellowship.CRItemSettings]` in `DefaultGame.ini` holds `StatPoolCalculation`,
`ItemLevelsPerUpgrade`, `MaxItemLevel`, the tempering **cost** mods, `GemSocketPowerMultipliers`,
`ItemGemPowers` and, as of build 24952830, all fourteen fields of `HeroItemPowerPotentialSettings`.
`ItemSettings.cs` reads that section; `ConfigIni.cs` is the paren-depth-aware literal parser it needs,
because `ScorePerGemType=((Tier1, 20),…)` is inside the `HeroItemPowerPotentialSettings=(…)` struct
and a split on commas breaks it. Map keys are enum **member names**, checked against the usmap enum
tables (`ECRGemTier`, `ECRGemSocketTier`, `ECRHeroItemType`) rather than trusted as text.
`[/Script/fellowship.CRItemStaticDataSubsystem]` names `DT_SlotWeights`, `DT_AttributeProperties`,
`DT_HeroItems` and the drop curves. `DynamicLootSystem.ini` names the loot plugin's seven assets.

A bare run writes the export. Three commands inspect the build:

- `usmap` recovers the mappings without exporting.
- `dump <filter> [count]` writes every export of the matching cooked assets as JSON under
  `output/dump/`.
- `dump-all <folder>` writes the evidence base for every ability and gameplay effect the export
  carries: property trees, expanded modifier magnitudes, Blueprint bytecode, the constants and tag
  tables, every curve table, the locres and the config.

A fact outside those dumps needs a reader of its own. That is a standing approval under
`CLAUDE.md`. `CsvTables.ReadTags` and `CsvTables.ReadConstants` parse the CSV tables in memory.

## Reading the shipping binary

`<install>/fellowship/Binaries/Win64/fellowship-Win64-Shipping.exe` (`ExtractorOptions.ShippingExe`),
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

Every address below was decoded against the **24736527** shipping exe. The exe changed in 24952830, so
re-anchor from the name-literal chain before reusing one.

| What | Where |
| --- | --- |
| Item stat magnitude, whole calculation | `UCRDynamicHeroItem` virtual at **0x1455D33F0**, vtable 0x148C8B230 slot +0x2D0 |
| The pools it really calls | natives `PrimaryStatPowerFromItemLevel` 0x1455D4050, `SecondaryStatPowerFromItemLevel` 0x1455D4190 |
| Tempering | `0x1455D3813 movd xmm1,[rax+30h]`, `cvtdq2ps`, `0x1455D381B addss`, gated by `0x1455D37EB cmp byte [r14],0` |
| Round half to even, 1e-8 snap | **0x1412922F0** |
| Armor, one ceil after the slot multiply | **0x14531AC8B** |
| `FCRHeroItemInstance` | Level 0x20, TimesUpgraded 0x30, MaxAllowedUpgrades 0x34, ItemRarity 0x38, DynamicSlots 0x40 |
| Dynamic slot, stride 0x18 | SlotType 0x00, ContentType 0x01, RerollableSlotIndex 0x04, ContentVariantData 0x08 |
| `ECRItemStat` ordinals | table at 0x148B176D0; Stamina 1, Strength 2, Intellect 3, Agility 4, Power 18 |

`StatRows` and `StatMagnitudes` in `Fellowship.Codex/Models/` implement the formula.
`GroundTruthTests` in `FellowshipExtractor.Tests` pins each in-game reading it reproduces.

Decoded against **24952830**, implemented in `Fellowship.Codex/Models/ItemPower.cs` and pinned
against printed cards by `ItemPowerTests` in `Fellowship.Codex.Tests`:

| What | Where |
| --- | --- |
| Item power potential, whole calculation | `UCRItemsBlueprintLibrary::CalculateItemPowerPotential` **0x145317B00** |
| Loadout Power Score, whole calculation | unnamed native **0x145349B70**, `powf(1.04, talentPoints)` at 0x145349F3B |
| Slot weight map build, keyed on row+0x08 | `0x145314782` / `0x145314795` |
| `ECRHeroItemDynamicSlotType`, the instance's slot | BaseItemFixed 0, DynamicFixed 1, **Rerollable 2** |
| `ECRHeroItemDynamicSlotContentType` | GemSocket **1**, SetBonus **2**, GrantedAbility **4**, TraitTree **5** |

The two numbers are different scoring rules and the loadout total is not the sum of the per-item
function, so never call the per-item one to mean an item's contribution to a Power Score.

Decoded against **25647984**, implemented in `TargetCountFalloffs.cs` and the
`MultiTargetDamageFalloff` text in `Settings.cs`:

| What | Where |
| --- | --- |
| `CRBlueprintLibrary::CalculateMultiTargetDamageFalloffScaler` | **0x145200A70**, exec thunk 0x144F4E560 named by the ASCII literal at 0x148ADC388 |
| `CalculateAndSetMultiTargetDamageFalloffScaler`, writes the scaler as a SetByCaller magnitude | **0x145200540**, exec thunk 0x144F4E030 |
| `T = clamp(trunc(threshold), 0, INT_MAX)` | `cvttss2si` at 0x145200A99, clamps at 0x145200AA4 and 0x145200AAB |
| `sqrt(T / N)` when `N > T` | `cmp` at 0x145200AAE, `divss` at 0x145200AC0, `sqrtss` at 0x145200AC7 |
| 1.0 otherwise, and for an invalid threshold or `N ≤ 0` | 0x145200AD6 loads 1.0 from 0x147BC0010 |
| Target selection `MinNumberOfTargets` +0x28, `MaxNumberOfTargets` +0x50, both `FScalableFloat` | records 0x148C59220 and 0x148C59260 |
| Both default to literal 1.0 on every ActorSet-family constructor | e.g. RandomActorSet 0x14505DA60, Max store at 0x14505DA9D |
| The cap applied to the candidate list | 0x1450EFF30, vtable slot +0x2C8, subset picker at 0x1450F0158 |
| `CalculateAndSetMultiTargetDamageFalloffScalerInt`, the target count passed as an int | **0x145200740**, exec thunk 0x144F4E300 named by the ASCII literal at 0x148ADBD30 |
| Int form: the same `T` clamp and `sqrt(T / N)` | `cvttss2si` at 0x1452007C5, `cmp ebp,eax` at 0x1452007DB, `sqrtss` at 0x1452007F4 |
| Int form: `N ≤ 0`, a null tag or an invalid spec writes no magnitude at all | `test ebp,ebp` / `jle` at 0x145200787 jumps to 0x145200815, past the SetByCaller write at 0x145200808 |

The threshold argument is parameter 1 of all three natives and is an `FScalableFloat` instance
variable. The call is not always on the ability's own graph. `TargetCountFalloffs` reaches it three
ways:

| Route | Example | Where the threshold is |
| --- | --- | --- |
| The ability's own package | `GA_Gunde_AoeDamageAndStun` | the ability's property |
| An actor the ability spawns through `BeginSpawningActor` | `BP_Sune_DroppedPulseAoe_AoeActor` from `GA_Sune_DroppedPulseAoe` | the ability's property when it calls `SetStructurePropertyByName` on the spawned actor with that property name before `FinishSpawningActor`, otherwise the actor's own default |
| A component the ability looks up through `Actor:GetComponentByClass`, and the actor that component spawns through `CRSpawnActorLibrary:BeginDeferredActorSpawn` | `GA_Mosse_Passive_ExplosiveMinions_Activation` → `AC_Mosse_ExplosiveMinionHandler` → `BP_Mosse_ExplosiveMinion` | the actor's own default, `Mosse.ExplosiveMinion.TargetCountDamageScalingThreshold` = 8 |
| An ability granted by an effect the ability applies, through `AbilitiesGameplayEffectComponent.GrantAbilityConfigs` | Coalescing Frost: `CAA_Talent_Rime_Chillblain` grants `GA_Rime_Passive_ChanneledBeamSingleDamageMonitor`, which applies `GE_Rime_Talent_CleaveBuildUpPerTick_VisualBuff`, which grants `GA_Rime_Talent_Passive_ChanneledBeamSingleDamage_DelayedAoe_BuffMonitor`; its call at ubergraph offset 1565 reads `Rime.ChanneledBeamSingleDamage.Talent.DelayedAoe.TargetCountThreshold` = 5 | the granted ability's property |

Each hop follows only the statements a character holding no tags reaches. `BareStateWalk.Boolean`
evaluates each branch condition with every tag test false, and a branch that evaluates false gates
the statements only it reaches. A boolean member set to true only inside such a branch, and false
everywhere else, reads as false. In a spawned actor or a granted ability the same gate applies to the
falloff calls themselves. An effect counts as applied only as an argument of a spec maker or an
`ApplyGameplayEffect` call; `RemoveActiveGameplayEffectBySourceEffect` does not apply one.

| Not followed | Why |
| --- | --- |
| Cold Snap → Glacial Assault stacker, threshold 12 | `GA_Rime_InstantSingleDamage` applies the stacker under `HasMatchingGameplayTag(TalentInstantAndBonusDamageTag)` at offset 1433 |
| Entropy's Claim → `GA_Lisa_Passive_Talent_SingleTargetDot_DotAoe`, threshold 5 | the base dot grants it, but its graph runs only while `Has Talent`, set true only under a tag test at offset 1387 |
| Serenity → `GA_Meiko_Talent_Passive_InstantSelfDefensive_DamageReductionLessHeal_AoeDamage`, threshold 5 | the ungated reference at offset 2010 is a removal; the application at 2173 is under `Talents.Meiko.InstantSelfDefensive.DamageReductionLessHeal` |
| `BP_Firemage_FireToad` | `AC_Firemage_FrogTracker` spawns it under `HasMatchingGameplayTag(Talent_BonusFireToad_Tag)`, and abilities reach the tracker only through the function library `BPFL_FiremageUtils` |

The effect a scaler is tied to is the class argument of the spec maker feeding the call's spec
parameter. Its position differs by maker: parameter 0 of `FellowshipMakeEffectSpec`,
`MakeOutgoingGameplayEffectSpec` and `FellowshipMakeEffectSpecBasedOnOtherEffectSpecContext`,
parameter 1 of `FellowshipMakeLooseEffectSpec` and `FellowshipMakeEffectSpecAsIfByOriginalAbility`.
The extractor takes the first parameter naming a `GameplayEffect` tag.

`DT_AbilityData_AdvancedLogs_AbilityData` names the Coalescing Frost buff monitor "Soulfrost Torrent"
under that ability's own row key, while its description is Coalescing Frost's. The ability export has
that name as the game has it.

Decoded against **25647984**, implemented in `BareStateWalk.cs` for `GMMC_Lisa_ResourceManager`, the
Chrona cost:

| What | Where |
| --- | --- |
| `GetAllAssetTagsForThisEffect`: spec `DynamicAssetTags` (+0x190) plus the Def's AssetTags-component `CombinedTags` at GE+0xA28 | **0x14521A620**, exec thunk 0x144F5B990; append at 0x144A6BBEA/0x144A6BBF7; GE+0xA28 writer 0x144AA4B90 at vtable +0x2F0 |
| `GetGameplayEffectDef`: the Def's **class**, not its CDO | **0x14521E060**, exec thunk 0x144F5F7C0; `ClassPrivate` read 0x141229340 |
| `GetCapturedAttribute`: the captured attribute's evaluated current value | **0x14521B530**, exec thunk 0x144F5C740; capture lookup 0x144A68940, aggregator evaluate 0x144A9D040 |
| `GetScalableFloat64Value`: `Value × row` at level 0 | **0x1452214C0**, exec thunk 0x144F61EE0; `mulss` at 0x144A9D37A |

All ten terms of the per-item body are confirmed against printed cards, and both `ItemType` gates are
confirmed by a card that carries a gated slot and scores nothing for it. When a card's arithmetic could
split two ways because two factors share a value, read the rung's `rarityAdditions` row rather than
counting the card's coloured stat lines.

**Two enums differ only in word order and only one is the instance's.** `CRHeroItemDynamicSlot.SlotType`
is `ECRHeroItemDynamicSlotType`; `CRDynamicHeroItemSlotDefinition.SlotType` is
`ECRDynamicHeroItemSlotType`, whose members are entirely different. Reading an instance byte against the
definition enum turns Rerollable into RandomStat and silently misprices every item. Resolve an ordinal
through the struct that holds it, never through the enum whose name reads closest.

Decoded against **26320967**, implemented in `ProcRolls.cs` and the `ProcPerMinuteRoll` text in
`Settings.cs`, written up in `docs/proc-rolls.md`:

| What | Where |
| --- | --- |
| `execRollProcPerMinute`, in `CRRandomStreamComponent`'s native table beside `ControlledRandomBool` | **0x145055FF0**, table entry 0x148C86F08, name literal 0x148C856E0 |
| `CRRandomStreamComponent::RollProcPerMinute(FGameplayTag ProcTag, float RealProcsPerMinute, bool ScaleWithHaste)` | **0x1452CBD20** |
| Hasted rate: `rate × Haste`, the owner's `Haste` attribute found by name | `mulss xmm0,xmm8` at 0x1452CBF87, FName at 0x1452CC0DA |
| Base chance `rate × max(0, t) ÷ 60`, drought factor `max(1, 0.05 × rate × min(d, 1000) − 3.5)` | 0x1452CBFD1 to 0x1452CC00B |
| First-proc floor `rate ÷ max(FirstProcChanceFloorPPMDivisor, 0.1)` | 0x1452CC049 to 0x1452CC069; divisor at component +0x14D8, default 8.0 stored by the constructor at 0x1452C220F |
| The roll: `(rand() & 0x7FFF) ÷ 32767`, proc when the chance exceeds it | 0x1452CC011 to 0x1452CC083 |
| Per-stream state keyed by `ProcTag`: last roll time +0, last proc time +4, procced flag +8 | map at component +0xF8, element stride 0x1C |

The Blueprint side is the rate expression: Dragonbone Dice passes `PPM × (CritChance + 1)` with
`ScaleWithHaste` false; 21 of the build's 25 calls pass true and the row alone.

Decoded against **26320967**, behind the Cooldown Recovery values in `CommitTriggers.cs`,
`EffectModifiers.DurationScalingOf` and `MagnitudeCalculations.Formula`:

| What | Where |
| --- | --- |
| `WeaponAbilityCooldown` base writer, sets 0 and then the weapon ability's cooldown | **0x14537A060** |
| Scalable float at ability +0x448, evaluated at ability level | **0x1450E1D40** |
| `WeaponAbilityCooldown` attribute getter, field +0x208 | **0x145375400** |
| `AbilityType.Weapon` tag global, literal 0x148D443C0 | **0x14A3CA048** |
| `GetGameplayEffectStackCount`, exec thunk 0x144F6CFD0 | **0x1452369C0** |
| `AuthRegisterAbilityTimeWithEffectForOwningAbility`, exec thunk 0x144F2DFB0 | **0x1452593A0** |

Decoded against **26320967**, implemented in `AbilityCooldowns.cs` and written up in
`docs/cooldown-haste.md`:

| What | Where |
| --- | --- |
| `UCRGameplayAbility` constructor; `CooldownDuration` +0x448 `Value` 0, no table, row or registry | **0x1450D7D60**, stores 0x1450D7E1E to 0x1450D7E46 |
| `FScalableFloat` evaluator, shared by `IsValid` 0x144AACDD0 and `GetValueAtLevel` 0x144AAB590 | **0x144AA85B0**; `mulss` by `Value` at 0x144AA864A; no table and no registry returns `Value`, invalid, at 0x144AA8714 |
| `ApplyCooldown`, vtable slot +0x3B0 | **0x1450D9990**; `IsValid` 0x1450D9A4B; `operator!=` against the default 0x1450D9A7C; `CooldownTags` count 0x1450D9A89; charges above 1 0x1450D9AC4; `CooldownEffectBaseClass` 0x1450D9BE8 |
| Charge class has `HasDuration` | 0x1450E23F0, `cmp byte [rax+0x30],2` at 0x1450E24AB |
| `ChargeGameplayEffectClass` +0x618, `FreeChargeOwnedAnyTags` +0x620 | getter 0x144FEC290 at vtable +0x508 |
| Native `GetMaximumCharges` returns 0 | 0x1450E18E0 |

Decoded against **26320967**, behind the relic cooldown factor in `docs/cooldown-haste.md`:

| What | Where |
| --- | --- |
| Cooldown spec builder, called by `ApplyCooldown` at 0x1450D9DC1 | **0x1450E2B40** |
| Duration `spec.Duration × ((CooldownDuration + pre) × multipliers + post)`, spec `Duration` +0x50 | `addss` 0x1450E2F8A, `mulss` 0x1450E2F8F, `addss` 0x1450E2F94, `mulss` 0x1450E2FA0, `SetDuration` 0x144A8F3F0 called at 0x1450E3006 |
| `CooldownPreAdditives` +0x478, `CooldownMultipliers` +0x488, `CooldownPostAdditives` +0x498: stride 0x30, `RequiredGrantedTag` +0x28 | loops at 0x1450E2E31, 0x1450E2EB0 and 0x1450E2F30; tag count test 0x1452EDE40 |
| `CrGetAllActiveEffectsForQuery(Query, out Handles)`, exec thunk 0x144F5FA30 | **0x14521FDC0** |
| Process-wide map of active effect handle to owning component: stride 0x18, weak component pointer +8 | global **0x149F6FFB0**, getter 0x144A7A460, owner lookup 0x144A7AF50 |
| Query filter over every element of that map: `GetActiveGameplayEffect` 0x144A0D7B0, `FGameplayEffectQuery::Matches` 0x144A82910 | **0x14520FD10**, loop at 0x145210140 |
| `GetFirstValidActiveEffect`, exec thunk 0x144F6C2E0 | **0x145236180** |
| `GetActiveEffectClass`: spec `Def` +0x18, `ClassPrivate` +0x10; exec thunk 0x144F67DB0 | **0x145231E20** |
| `CheckIfCharmIsReady`, exec thunk 0x144F5BCD0: the owner's tag `CharacterStatus.Charm.Charm0Ready` or `Charm1Ready` | **0x145219E90**; tag globals 0x14A3CA260 and 0x14A3CA268 |
| Charm ready setter, true when both equipment slots of the charm's pair hold an item | setter **0x14536DB30**, caller 0x14536F1C0, slot pairs at 0x148D4D14C |
| `AuthRegisterAbilityTimeWithEffectForOwningAbility` keeps each applied effect handle under the ability's spec handle, in the component's map at +0xD8 | 0x1452593A0, append at 0x145259644 |

Decoded against **26320967**, written up in `docs/effect-periods.md`:

| What | Where |
| --- | --- |
| `FActiveGameplayEffectsContainer::ApplyGameplayEffectSpec` | **0x144A6A660** |
| Stack refresh: `ExistingStackableGE->Spec = Spec`, existing effect returned | assignment 0x144A66DD0 called at 0x144A6A93B; `mov rax,r12` 0x144A6B6F4 |
| `bSetDuration` from Def +0xA11, `bSetPeriod` from Def +0xA12 | 0x144A6A99D, 0x144A6A9A9 |
| `CRAbilitySystemComponent` vtable 0x148AB98B8: partial tick +0x948, period +0x950, refresh duration +0x958, time rate +0x960 | 0x1452D4B70, 0x1452D5C70, 0x1452D7CD0, 0x1452D5EF0 |
| Period: `/ Haste` of the instigator, `x SetByCaller.System.TickRateOverride` | `divss` 0x1452D5E0B, `mulss` 0x1452D5E3A; tag global 0x14A3CA658 |
| `EffectLogic.TickrateNotAffectedByHaste`, `EffectLogic.TickrateNotAffectedByHasteExclusively` | tag globals 0x14A3C9EA0, 0x14A3C9EA8 |
| `GetGameplayEffectCustomTickPeriodScaler`, `SetGameplayEffectCustomTickPeriodScaler` | 0x1452365A0, 0x145243620; exec thunks 0x144F6CA60, 0x144F75930 |
| Applied `EffectType.Dot` and `EffectType.Hot` effects kept at component +0x13B0 | 0x1452D8D70, tag globals 0x14A3C9E20, 0x14A3C9E28 |
| Haste change delegate, loop over the kept handles, timer refresh, timer rated again | 0x1452EFC60, 0x1452F2830, 0x1452F2A70, 0x1452DC7E0 |
| Refresh duration: `+ clamp(D x CarryOverLimit, 0, remaining)`, globals +0x334 | `mulss` 0x1452D7E11 to `addss` 0x1452D7E22; tags 0x14A3C9F18 `EffectLogic.CarryOverDuration`, 0x14A3C9F20 `EffectLogic.Proc` |
| `FActiveGameplayEffectsContainer::CheckDuration`, partial tick call | **0x144A6FAB0**, call 0x144A6FD3A |
| Partial tick: `SetByCaller.PartialTickFactor` = elapsed / rate, `MinimumPartialTickTime` at globals +0x32C | `divss` 0x1452D4D26, `comiss` 0x1452D4D13; tags 0x14A3CA5F0, 0x14A3C9F10 `EffectLogic.IgnorePartialTicks` |
| Ability system globals through the `GameplayAbilities` module, vtable +0x40 | 0x1450BD610 |
| `CRDamageCalculationBase` driver, attribute gather, source formula, target formula | 0x1450DCF40, 0x1450DB100, **0x1451079A0**, 0x145107A70 |
| Source formula: `x (Expertise + 1)`, `x PartialTickFactor`, `x StackCount`, `x BalancingDamageAura`, round | 0x145107A30, 0x145107A3D, 0x145107A48, 0x145107A53, 0x1412A7850 |
| Damage capture statics: `DamageScale` +0x50, `DifficultyDamageScale` +0x320, `Expertise` +0x560, `BalancingDamageAura` +0x5F0 | accessor 0x1450DCBE0, constructor 0x1450D5390 |
| Calculation preset row flags: `CanBeBlocked` +0x1B to `ApplyAscensionScaling` +0x30 | bit setters of the reflection records at 0x148ABADE0 onward |
| Healing gather; ascension scale from `GlobalAscensionReduceHealingScalerKey` (globals +0x358) | 0x1450DB8C0, flag test 0x1450DBB73, getter 0x1450CDB20 |

Decoded against **26320967**, `CrAddToAndRecalculatePeriodicEffect` in `docs/effect-periods.md`:

| What | Where |
| --- | --- |
| Name literal, native table entry, exec thunk | 0x148B13660, 0x148B30CC0, 0x144F5CA10 |
| Body; `TargetingFilters` check; return value false on every path | **0x14521C1D0**; 0x14524B830 called at 0x14521C2B1; 0x14521CB2F |
| Query: `EffectSource` +0x178 = `Source`, `EffectDefinition` +0x180 = `EffectClass`, on `Target`'s component; first live handle | stores 0x14521C349, 0x14521C365; `GetActiveEffects` 0x144A0D680; loop 0x14521C410 |
| `FGameplayEffectQuery::Matches(Spec)`: context vtable +0x58 (`GetSourceObject`, as the `EffectContextGetSourceObject` thunk 0x144A32A60 calls it) against `EffectSource`; `Def` against the class default object | 0x144A82E23 to 0x144A82E4C; 0x144A82E57 to 0x144A82EBC |
| Fresh: `AddedValue / (floor(D/P) + Fmod(D,P)/P + bExecutePeriodicEffectOnApplication)`, spec `Duration` +0x50, `Period` +0x54, CDO +0x240 | 0x14521C992 to 0x14521C9DF; getters 0x144A78C20, 0x144A7B020; bit setter 0x1434A2410 |
| Existing: time remaining `D - D x ((t - start +0x2EC) x rate +0x2DC / D + offset +0x2E0)` | 0x144A7BF10 |
| Existing: period timer (+0x318) remaining and elapsed | 0x1452D5AC0 (`GetTimerRemaining` 0x144042D40), 0x1452D5A10 (`GetTimerElapsed` 0x144042CD0) |
| Existing: ticks left `ceil(clamp(R-Pr,0,10000)/P) + f + [f==0]`, `f` from `Fmod(R-Pr,P)` or `Pe+R` | 0x14521C6B4 to 0x14521C732; 10000 at 0x147C084C8; `Fmod` 0x1411EA290 |
| Existing: new tick `(left x V + AddedValue) / (floor(D/P) + Fmod(D-Pr,P)/P)` | 0x14521C737 to 0x14521C782; `V` 0x145232130; `D` 0x144A0EF40 |
| Duration restart: vtable +0x740 `ModifyActiveEffectStartTime(D - R)`, start `+= diff / max(rate, 0.0001)`, then `CheckDuration` | 0x14521C83F; 0x144A83160, `CheckDuration` call 0x144A83236 |
| Magnitude edit: vtable +0x6E8 `UpdateActiveGameplayEffectSetByCallerMagnitude` | 0x14521C874; 0x144A911F0, `SetSetByCallerMagnitude` 0x144A8FB20 |

Decoded against **26320967**, the activation checks of `UCRGameplayAbility`, written up in `docs/cooldown-haste.md`:

| What | Where |
| --- | --- |
| `CanActivateAbility`: tag requirements, `CheckCooldown` (vtable +0x3A8), global cooldown check, `CheckCost` (+0x3B8) | **0x1450DAB50**; calls 0x1450DACE5, 0x1450DAD7D, 0x1450DAD91, 0x1450DADC3 |
| Global cooldown check: passes for `AbilityLogic.IgnoreGlobalCooldown` (tag global 0x14A3CA0F0) and for an ability with no `GlobalCooldownGameplayEffectClass`; otherwise the owner's tags against the class's granted tags | **0x1450DC780**; null return of the getter at 0x1450DC801 |
| `GlobalCooldownGameplayEffectClass` +0x3C0, getter of its granted tags (class default object +0xA48) | 0x1450E16D0 |
| Global cooldown application, nothing with no class | 0x1450E2870 |
| `CheckCooldown`: effects with a `CooldownType` asset tag granting a `CooldownTags` tag or `Cooldown.Padding` | **0x1450DBF50**; tag globals 0x14A3CA4D8 `CooldownType`, 0x14A3CA4E0 `.Regular`, 0x14A3CA4F0 `.Interrupt`, 0x14A3CA4F8 `.SpamProtection`, 0x14A3CA500 `.Padding`, 0x14A3CA508 `.Charge`, 0x14A3CA738 `Cooldown.Padding` |
| `BlockedByPaddingCooldown` +0x530, constructor default false | test 0x1450DC32E, store 0x1450D7F10 |
| Padding cooldown application from `ApplyCooldown`: `PaddingCooldownEffectBaseClass` +0x4D8, `PaddingCooldownDuration` +0x4E0 | 0x1450DA5D0, called at 0x1450D9E8D |

Decoded against **26320967**, the applied-effect listeners in `docs/effect-periods.md`:

| What | Where |
| --- | --- |
| `UAbilitySystemComponent::ApplyGameplayEffectSpecToSelf`: handle and `&AppliedEffect->Spec` after the apply | call 0x1449FD5AC, `lea r12,[rax+18h]` 0x1449FD5CA |
| Broadcasts with that live spec: `OnGameplayEffectAppliedDelegateToSelf` (component +0x170), then the instigator's `OnGameplayEffectAppliedDelegateToTarget` (+0x188) | 0x1449FD821, 0x1449FD83B |
| Delegate broadcast, last listener first | **0x144A00010**, `sub rsi,1` / `jns` 0x144A0008D |
| `AddUObject`, new listener at the end of the invocation list | 0x1449EDE80 |
| `UAbilityTask_WaitGameplayEffectApplied_Target::RegisterDelegate` | 0x144A1FAD0 |
| `UAbilityTask_WaitGameplayEffectApplied::OnApplyGameplayEffectCallback`: tag requirements, `Locked` (task +0x240), `new FGameplayEffectSpec(SpecApplied)` | **0x144A176A0**, copy at 0x144A177CE |
| `FGameplayEffectSpec` copy constructor, size 0x298 | 0x144A62750 |
| `FGameplayAbilityTargetData::ApplyGameplayEffectSpec`, a spec copy for each target | 0x144A4F410 |

Decoded against **26320967**, the apply helpers and the target terms of the damage formula:

| What | Where |
| --- | --- |
| `CalculateMultiTargetDamageFalloffScaler` in this build, exec thunk 0x144F5B290 | **0x1452188B0**, `cvttss2si` 0x1452188D9, `sqrtss` 0x145218907 |
| `UGameplayAbility::ApplyGameplayEffectSpecToTarget` | 0x1449D9250 |
| `CRAbilitySystemComponent::ApplyGameplayEffectSpecToSelf` override (vtable +0x6B0), base called in the same call | 0x1452D0F50, base 0x1449FD330 |
| An instant effect executes inside `ApplyGameplayEffectSpecToSelf` | call 0x1449FD77C |
| `FellowshipMakeEffectSpec`, exec thunk 0x144FED8A0; the visual delay goes on the effect context | 0x1450E0230, 0x145244120, 0x14522CFC0 |
| `BP_FellowshipApplyGameplayEffect`, exec thunk 0x144FED1F0, worker; the last argument is the stack count | 0x1450DA840, 0x1450DF9C0, `SetStackCount` 0x144A8FC60 |
| Target formula terms: `Avoidance` (statics +0x3B0, values +0x5C, preset +0x23), `IncomingDamageMultiplier` (+0x290, +0x30), `PhysicalDamageReduction` (+0x3F8, +0x34), `MagicDamageReduction` (+0x440, +0x38), `PhysicalIncomingDamageMultiplier` (+0x488, +0x3C), `MagicalIncomingDamageMultiplier` (+0x4D0, +0x40), `AoeIncomingDamageMultiplier` (+0x518, +0x44, preset +0x29), `Armor` (+0x2D8, +0x18) | 0x145107A70, statics constructor 0x1450D5390 |

Decoded against **26320967**, the damage result, the dot average, the delay task, the area actor and the hero setup:

| What | Where |
| --- | --- |
| Damage result struct: `TotalDamage` +0x00, `MitigatedDamage` +0x04, `RawDamage` +0x08, `PreBlockDamage` +0x0C, `BlockedDamage` +0x10, `AbsorbedDamage` +0x14 | reflection records from 0x148CF15B0 |
| Blocked outcome: `max(PreBlockDamage x values +0x4C - values +0x48, 1)` | 0x145107C1D to 0x145107C37 |
| `TotalDamage x DamageMitigationScale` (values +0x64, statics +0x5A8), `MitigatedDamage` the remainder, rounded | **0x1451078C0**, `mulss` 0x145107964, `subss` 0x14510796A |
| Health change event: `MitigatedDamage` +0x70, `AbsorbedDamage` +0x74, `RawDamage` +0x78, `BlockedDamage` +0x7C | reflection records from 0x148AAE8C0; filled by the driver at 0x1450DD77F |
| Combat logger literal `ABILITY_DAMAGE` | 0x148AA7838 |
| `CrApproximateCurrentDotAverageDamage`, exec thunk 0x144F5DA50: per effect `T x (D / timer rate) / D` | **0x14521D420**, per-effect tick 0x145214E40 |
| Damage calculation virtual +0x2E8: a tick with outcome 9 and spread 0, through both formulas | 0x1450E3F40 |
| `CRAbilityTask_WaitDelay::WaitDelayCR`, exec thunk 0x144F28280; `Activate` sets a timer of `Time` (+0x90) | 0x14512E940, 0x145125D70 |
| Ability time factor: `Haste` with `ScaleTimeWithHaste` (+0x3B8 bit 0), + `AttackSpeed` - 1 with `ScaleTimeWithAttackSpeed` (bit 1) | 0x1450E06E0 |
| `CRAoeActorBase` constructor: rule `Continuous`, `ActiveFromStart` true, `ApplicationPeriod` 0, `ApplicationDelay` -1 | **0x1450C7730** |
| `ECREffectApplicationRule` | `Continuous` 0, `ContinuousWithDuration` 1, `OneOff` 2, `OverlapOneOff` 3, `RepeatingOverlapOneOff` 4, `PeriodicApplication` 5, `ContinuousInstant` 6 |
| Area actor: begin play, begin overlap, end overlap, tick, sync of applied effects, activation, end play | 0x1450CA5E0, 0x1450D0770, 0x1450D0940, 0x1450D3270, **0x1450D40D0**, 0x1450D2B00, 0x1450CBFB0 |
| `LimitToSingleApplication` removal: a count per effect class on the target's component (+0x1270) | 0x1452DAE20 |
| Hero setup grants `Character.PrimaryAttribute.Strength`, `.Agility`, `.Intellect` from the metadata's `PrimaryAttribute` (+0x31) | **0x14530A340**; tag globals 0x14A3CA280, 0x14A3CA288, 0x14A3CA290 |
| `UGameplayEffect` constructor and destructor, the only engine routines touching the deprecated `ApplicationTagRequirements` (+0x4B0) | 0x144A63AB0, 0x144A65000 |
| Engine `GiveAbility`, `ClearAbility`, `TryActivateAbility` | 0x144A11CD0, 0x144A02610, 0x144A276D0 |
| `CRGameplayAbility::OnGiveAbility`: `TryActivateAbility` under `ActivateAbilityOnGranted` (+0x3B9); `PassiveAbility` is +0x3BA | **0x1450E3970**, test 0x1450E399D |
| `CRGameplayAbilityPassive` constructor sets both flags | 0x1450E82D0, store 0x1450E82E5 |
| Component give wrapper over `GiveAbility` | 0x1452D6200 |
| Hero `Restart` override (engine `ACharacter::Restart` 0x143751E50), under authority: `EquipmentComponent` (+0xF40) then `TalentComponent` (+0xF60) | 0x145317420; calls 0x14531759E, 0x1453175AD |
| `CRGameplayAbilityPassive` vtable 0x148BE4688 differs from `CRGameplayAbility`'s 0x148BE1FB0 in one slot (+0x450); `OnGiveAbility` is +0x418 and the empty `OnAvatarSet` +0x428 | 0x1450F0F70 over 0x1450E3D70 |
| `CRAssetManager::Get`, `LoadAssetList` (vtable 0x148AF6BD0 +0x580), `RequestAsyncLoad`, `StartHandleRequests`, `CheckCompletedRequests`, `FStreamableHandle::CompleteLoad` (completed byte +0x10) | 0x1452EAA60, 0x1436E4370, 0x143FE9AE0, 0x143FEC7C0, 0x143FD5840, 0x143FD6E20 |
| `CancelAbilitiesOnCharacterDeath`, `ResetAbilitiesOnFellowshipReset` over the dungeon's ability lists (+0xD40, 0x110 bytes each) | 0x1453B7F90, 0x1453BF860; body 0x1453B7FA0 |
| Hero's own loadout change handler (flags 6) | 0x145314450 |
| `EquipmentComponent` loadout interface setter, loadout application, loadout change handler (flags 0x1A) | 0x1453779D0, 0x145376740, 0x145376A80 |
| `EquipmentComponent` asynchronous load of its augmentation assets: completed test, completion delegate | **0x14537AA80**; 0x14537AC77, 0x145376670 |
| `EquipmentComponent` augmentation application (wanted +0x528, applied +0x538, `PreloadedAugmentationAssets` +0x620), one asset's `GrantedAbilities` (+0x40) | **0x14536B990**, 0x14536A480 |
| `TalentComponent` loadout interface setter, talent application, change handler (flag 4) | 0x145446510, **0x1454456A0**, 0x1454459E0 |
| `TalentComponent` removal of one talent's grants, application of one talent (synchronous load 0x14153D2D0) | 0x1454469C0, 0x145440BC0 |
| Hero ability input by input ID: the spec with bit 16 first, else the plain spec, both with null event data | **0x145313640**; lookups 0x14531368D, 0x1453136DB |
| Hero activation of a spec with event data: a `CRGameplayAbilityTargeting` spec by `TryActivateAbility`, any other through the component | 0x145319D50 |
| Component activation with event data: null event data goes to `TryActivateAbility` | 0x1452D7440; test 0x1452D7585, call 0x1452D7730 |
| `OnGiveAbility` gives `TargetingAbility` (+0x400); the give wrapper sets bit 16 of a targeting spec's input ID | 0x1450E39E2 to 0x1450E3AC3; 0x1452D626F |
| `CRGameplayAbility::PreActivate` (vtable +0x450) writes `PreActivateTargetData` (+0x550); the engine's writes `CurrentEventData` (+0xF0) for non-null event data alone | 0x1450E3D70; 0x1449E04B0, test 0x1449E05C3 |
| `CRGameplayAbilityTargeting` (vtable 0x148BE4EE0, constructor 0x1450E8370): event data built after target acquisition, then the ability with the same input ID activated | 0x1450EB943; calls 0x1450EBAAE, 0x1450EBB38 |

Decoded against **26320967**, the target selection defaults in `CreatureAbilityReads.cs`:

| What | Where |
| --- | --- |
| ActorSet `MinNumberOfTargets` +0x28, `MaxNumberOfTargets` +0x50: `Value` 1.0, no curve row, in every subclass | ActorSet 0x14506EE80 (stores 0x14506EEA9, 0x14506EEC7); RandomActorSet **0x14506F0F0** (0x14506F10B, 0x14506F12D) |
| Threat `SelectionType` +0x50: 0, `ThreatSorted`, the allocation's zero fill; `NumberOfTargets` +0x28 1.0 | constructor 0x14506F350 stores nothing past +0x48; 1.0 at 0x14506F379 |
| RemainingHealthActorSet `SelectionType` +0x78: 0, `LeastHealthLeft` | constructor 0x14506F2E0, store 0x14506F341 |
| RandomPreferRoleActorSet `PreferredRole` +0x78: 0, `ROLE_TANK` | constructor 0x14506F160, store 0x14506F1C1 |
| A class's constructor: name literal, registration record [-8] StaticClass, `GetPrivateStaticClassBody` argument [r11-30h] | 0x14144CE70; UObject base constructor 0x14156D290 |

Decoded against **26320967**, implemented in `DamageAbsorptions.cs` and written up in `docs/damage-absorption.md`:

| What | Where |
| --- | --- |
| Absorption pass: effects with `EffectType.Absorption` (tag global 0x14A3C9E88), share `rest / count`, an effect at its end removed and the pass ended | **0x1450E4BE0**; share 0x1450E4D99, compare 0x1450E4E6A, removal 0x1450E4F7C, `rest -= absorbed` 0x1450E4FCB |
| Sort of the effects by `pool / (ConsumptionScaler x crit factor)`, least first | 0x1450D4FE0, predicate 0x1450D9480 |
| Deduction crit factor: `DefaultAbsorbDeductionCritScaler` (character +0xCF8) with chance `2c / (1 + c)` of the instigator's `CritChance`, kept for each effect and hit | **0x1450D90F0**; chance 0x1450D934E to 0x1450D937C |
| `SetByCaller.System.EffectAbsorptionConsumptionScaler` (0x14A3CA670, default 1.0), `SetByCaller.System.EffectAbsorptionPool` (0x14A3CA668) | read 0x1450E4E3F; written by the pool setter |
| Character `CachedAbsorptionValuesMap` +0xA88, `CurrentAbsorption` +0xD38: getter by handle, total, setter | 0x1452EAC00, 0x1452EABF0, **0x1452E4DC0** |
| Driver loop over the absorption pass: at most 30 passes, `Raw = Raw / PreBlock x rest` before each pass after the first | 0x1450DD270 to 0x1450DD373 |
| Health damage `max(round(T x DamageMitigationScale) - A, 0)`; a hook's reduction added to `AbsorbedDamage`; event amount `-(H + A)` | 0x1450DD3E9, 0x1450DD48D, 0x1450DD4C8 and 0x1450DD6E2 |
| Hook listeners (virtual +0x60) in the character's maps +0xC78 and +0xC90: Stagger, damage redirect, death prevention | caller 0x1450D4920; 0x1452C85F0, 0x14527DD90, 0x14527E150 |
| Stagger pool add, cap `StaggerMaxHealthPercentage` (+0xF8) x MaxHealth, `CurrentStagger` +0x140 | 0x1452C3540 |
| `EffectType.Environmental`, `EffectLogic.DamageIgnoreStagger`, `EffectLogic.NoLogging` | tag globals 0x14A3C9E80, 0x14A3C9F80, 0x14A3C9F30 |
| Attribute set `PostGameplayEffectExecute`; Health `clamp(Health - Damage, MinHealth, MaxHealth)` (Health +0x58, MaxHealth +0x38, MinHealth +0x48) | 0x1450C45C0; **0x1450C4DB0**, vtable slot +0x318 |
| Health change event to the meters, and from there to the combat log writer | 0x14527C080, call 0x14527C422 |
| Combat log damage line: `Amount = clamp(abs(event amount) - CurrentAbsorption, 0, Health - MinHealth)`, hit points `Health - Amount` | **0x145296FB0**; 0x1452976FD to 0x145297736 |
| Combat log `DAMAGE_ABSORBED` line, `AmountAbsorbed` the damage absorbed | 0x1452959F0, store 0x145295CE2 |
| Damage line record: `HitType` +0x92, `Amount` +0x94, `OverAmount` +0x98, `Absorbed` +0x9C, `Blocked` +0xA0, `RawDamage` +0xA4 | reflection records 0x148AAA9B0 to 0x148AAAAD0 |
| `FGameplayEffectContextHandle` vtable: `GetInstigator`, `GetInstigatorAbilitySystemComponent`, `GetEffectCauser` (context slots +0x28, +0x30, +0x38) | 0x148947BB8; 0x1449DE200, 0x1449DE220, 0x1449DDFB0 |
| `CrAddOnAbsorbValue`: `x IncomingAbsorptionMultiplier` (target), `x OutgoingAbsorptionMultiplier` (instigator), `x (1 + Expertise)` | 0x14521BE40; 0x14521BFEA, 0x14521BFFA, 0x14521C02F |
| A second execution over the absorption pass, with the captured `DamageScale` as its damage | 0x1450DDDE0, vtable entry 0x148B806C8 |

## Traps this build sets

**An omitted asset field is a live C++ constructor default, not zero.** `DA_StatCalculationSettings`
has three of eight fields per block; the rest are real values: `PrimaryWeight` 0.25,
`SecondaryWeight` 0.3, `StaminaWeight` 0.5, `PrimaryMultiplier` 0.7, `WeightMultiplier` 0.15,
`StaminaMultiplierItemLevelThreshold` 315, and `SecondaryMultiplier` 0.6 on the base block, 1.5 on the
fixed block. They are in `StatRows` because no shipped file states them. `SecondaryBaseMultiplier` is
72 and `PrimaryDivisible` is 15 the same way. The exception is `StaminaMultiplierItemLevelThreshold`:
`StatRows.StaminaThreshold` reads it from the lowest `TemperStaminaMultiplierAtItemLevel` step in
`[/Script/fellowship.CRItemSettings]`, which states the same 315.

**A Blueprint function can be named right, wired through config, and never called.**
`HeroItemStatCalculation`'s `CalcPrimaryStatPoolFromItemLevel` is wired via
`CRItemSettings.StatPoolCalculation` and the magnitude path calls the two natives instead. Index the
Blueprint functions yourself before believing one: if no Blueprint function computes your number, it
is native.

**A parser that raises no exception has told you nothing.** Cooked packages degrade silently. Verify
values, never the absence of errors.

**Screenshot annotations are reconstructions.** Only the printed total is on the screenshot. Two
readings recorded as "declared plus one roll" were declared plus two, and one recorded as having a
roll had none. Let the recovered formula tell you the split.

## Known unreachable

Native C++ bodies called from Blueprint bytecode are visible as a call with its arguments, never as
what the callee computes.

**Re-test a "no `.ini` sets it" claim against the build in front of you.** For 24736527 the claim here
was that `HeroItemPowerPotentialSettings` was fourteen compiled-in defaults; 24952830's `DefaultGame.ini`
writes all fourteen. The schema was byte-identical across the two builds, so nothing in the mappings
signalled the change — only re-reading the config did.

## Stop fitting when

- Every input the schema enumerates is read and a residual remains. The residual is code.
- The runtime struct carries the inputs but not the output. `CRHeroItemInstance` stores level, rarity,
  `TimesUpgraded` and slots and no magnitudes, so the client computes the printed number.

## Delegating it

Anchor strategies are independent, so this parallelises. One agent builds the harness while others
follow anchors; one lead each, told plainly that a dead end is an acceptable answer and that guessing a
formula from the printed numbers would corrupt the evidence; then adversarial verification on separate
lenses, with a fidelity lens that re-decodes every cited address rather than trusting the summary. Here
the refutation lens found a gate the synthesis missed and the fidelity lens found a double rounding in
the port. Neither came from a single pass.

## Red flags

"One more screenshot would settle it." "It fits every reading I have." "That field is absent, so it is
zero." "The function is named for exactly this." "It is native, so it is unreachable."

Every one means: go back to rung 2, and if the schema is exhausted, disassemble.
