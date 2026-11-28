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

Decoded against **26320967**, `CRStaggerActorComponent` (character +0xF78), behind `settings.json` `stagger`
(`StaggerComponent.cs`):

| What | Where |
| --- | --- |
| Vtable; `BeginPlay` +0x3C0, `TickComponent` +0x3D8, `OnStaggerChanged` broadcast +0x4B8 | 0x148C9A4A8; **0x1452C3990**, **0x1452CDD70**, 0x1452C97D0 |
| Fields: `StaggerPeriodRef` +0xA8, `StaggerDurationRef` +0xD0, `StaggerMaxHealthPercentage` +0xF8 (all `FScalableFloat`), `DamageEffectClass` +0x120, `DamageEffectSetByCallerTag` +0x128, `CurrentStagger` +0x140, damage spec +0x158, last tick +0x168, period countdown +0x16C, buckets `TArray<float>` +0x178, `CachedStaggerContributions` +0x188 | reflection records from 0x148C98F60; constructor 0x1452C2320 |
| Rows: `BP_PlayerCharacter` `StaggerActorComponent` binds `Stagger.Period`, `Stagger.AmountOfBuckets`, `Stagger.MaxHealthPercentage`, `GE_Shared_Stagger_Damage`, `SetByCaller.Damage`; no hero Blueprint overrides them | asset |
| Bucket count `ceil(StaggerDurationRef)`, `SetNumZeroed` | 0x1452C3DB4 to 0x1452C3E1D |
| Natives: `AuthClearStagger`, `AuthUpdateStagger(StaggerDelta, ShouldUpdateStaggerDamage)`, `AuthUpdateStaggerAndLog`, `UpdateStaggerDamage` | 0x1452C3510, **0x1452C3540**, 0x1452C3670, **0x1452CF830**; table 0x148C9A9B0 |
| Pool setter: `clamp(cur + Δ, 0, cap)`, unchanged value returns at once; flag set calls the bucket update with the Δ taken | 0x1452C3580, 0x1452C35EF to 0x1452C3618, 0x1452C364E |
| Bucket add: countdown 0 takes `Period` (first add only); `f = countdown / Period`; first bucket `Δ·f/(n−1+f)`, others `Δ/(n−1+f)` | 0x1452CF884 to 0x1452CF9AA |
| Bucket removal (Δ ≤ 0): every bucket × `max(sum + Δ, 0) / sum`, × 0 when sum ≤ 0.0001 | 0x1452CF9C2 to 0x1452CFAF2 |
| Tick: authority, `CurrentStagger > 0` else return before the countdown moves; countdown −= dt; on expiry pop bucket 0, shift, deal `min(bucket0, CurrentStagger)` when bucket0 > 0, `CurrentStagger −= bucket0` (flag 0), countdown += Period | 0x1452CDD80 to 0x1452CDE68; deal 0x1452C3380 (`AssignTagSetByCallerMagnitude` 0x1449FE350, apply 0x1449FE3F0) |
| Damage hook part, flag 1; heal hook for `EffectLogic.HealAffectStagger` (0x14A3C9F88): Δ = −min(heal × `StaggerHealReductionFactor`, CurrentStagger), heal less by it | 0x1452C87D5; 0x1452C8900, call 0x1452C8B0B |
| `CRModifyStaggerExecution`: `SetByCaller.Stagger` to `AuthUpdateStaggerAndLog` with flag 1 | 0x1451091E0, call 0x145109383 |
| Hero death override clears Stagger before the base death (+0x92D, `EffectLogic.RemoveOnDeath`) | 0x145312310 (vtable entry 0x148CFC7A8), call 0x145312378; base 0x1452ED190 |

Decoded against **26320967**, the school and armor terms of the target formula in `docs/effect-periods.md`:

| What | Where |
| --- | --- |
| Target attribute gather: one capture for each set preset flag, `Armor` +0x22, `Avoidance` +0x23, `IncomingDamageMultiplier` +0x24, `PhysicalDamageReduction` +0x25, `MagicDamageReduction` +0x26, `PhysicalIncomingDamageMultiplier` +0x27, `MagicalIncomingDamageMultiplier` +0x28, `AoeIncomingDamageMultiplier` +0x29 | **0x1450DB100**, flag tests 0x1450DB128 to 0x1450DB303 |
| School pair by asset tag, `GameplayTags` then `ParentTags`: `EffectType.Physical` first, else `EffectType.Magic` | 0x145107B1E to 0x145107BC0; tag globals 0x14A3C9E50, 0x14A3C9E48 |
| Armor term: `CRItemSettings` default object +0x38, `CalcDamageReductionFromArmorValue` of `trunc(Armor)` | 0x145107A90 to 0x145107AE9, caller of the Blueprint function 0x145020120, name global 0x14A3B1970 |

Decoded against **26320967**, `CRAbilityTask_ServerCasting` and `CRAbilityTask_ServerChanneling`, written up in
`docs/casting-and-channeling.md`, behind the `channelingData` flags of `AbilityTaskReads.cs`:

| What | Where |
| --- | --- |
| `ServerStartCasting`, exec thunk 0x144F1A680; `Duration` +0xB0 from `ModifyCastingDuration`, `CastSpeed` +0xB4 `clamp(rate, 0, 999)` | **0x145121260**; 0x14512137D, 0x14512139D to 0x1451213AD |
| `ServerStartChanneling`, exec thunk 0x144F1C070; `rate = clamp(rate, 0.0001, 999)`, `ScaledDuration` +0xB0, `TickInterval` +0xB4 `/ rate` | **0x145121540**; 0x14512167F, 0x1451216CC, 0x1451216FD |
| `ModifyCastingDuration`, `ModifyChannelingDuration`: callers by name, native bodies return the input (ability vtable +0x4C8, +0x4C0) | 0x144FEC850, 0x144FEC8F0; 0x1450E30F0; names 0x14A3ACC68, 0x14A3ACC70 |
| Ability time rate for an ability instance, and for a class default object with a component | 0x1450E06E0, 0x1450E0910 |
| End montage delay: first `AnimNotify_CastEndMarker` (class getter 0x144F19050) of the montage's notifies (+0x80, stride 0xB8), `WaitDelayCR` 0x14512E940 | cast 0x145121460 to 0x1451214C2; channel 0x1451217D0 to 0x145121830 |
| Cast task `Activate`: `CRCastingGameplayEffect` (0x144FF4380), `SetDuration(Duration / CastSpeed)`, removal handler, time change handler | **0x14511BE30**; 0x14511C06B to 0x14511C077; 0x14511EFC0; 0x14511EE30 |
| Channel task `Activate`: `CRChannelingGameplayEffect` (0x144FF4430) or `CRInfiniteChannelingGameplayEffect` (0x144FF44E0), tick count, looping timer with first delay -1 or 0.01 | **0x14511C300**; 0x14511C8CB to 0x14511C935; 0x14511CAE2 |
| Channel tick timer body; expiry handler with the remaining ticks, the partial tick and `OnFinished`; time change handler | 0x145120A20; **0x14511F270**; 0x14511F030 |
| `OnLoopTimerExpired` of the cast and of the channel; `OnDestroy` of each | 0x145120770, 0x145120820; 0x145120190, **0x1451202F0** |
| `EstimateChannelValues`, `ForceMarkChannelComplete`; native tables | 0x14511E000, 0x14511E810; 0x148ABE700, 0x148AC0880 |
| Channel task: `OnTick` +0x90, `OnFinished` +0xA0, `TickImmediately` +0xB8, flags +0x100 (bit 0 `ContinuousChannel`, 2 `Interruptible`, 5 `EnablePartialTicks`, 6 `AllowSpellPushback`), effect handle +0x104, start time +0x10C, tick count +0x110, ticks remaining +0x114, partial tick +0x118, timer +0x120, complete +0x129, delay task +0x130 | reflection records from 0x148ABF7A0 |
| Cast task: `OnFinished` +0x90, flags +0xF0 (bit 0 `ShowOverhead`, 1 `Interruptible`, 2 `AllowSpellPushback`), effect handle +0xF4, end montage played +0xFC, finished +0xFD, delay task +0x100 | reflection records from 0x148ABCC40 |
| `CRChannelingTaskData` (0x80): `ContinuousChannel` +0x00, `ChannelingDuration` +0x08, `ChannelingTickInterval` +0x30, `TickImmediately` +0x78, `ScaleDurationWithAbilityTimeRate` +0x7C, `EnablePartialTicks` +0x7E, `AllowSpellPushback` +0x7F; `CRCastingTaskData`: `CastingDuration` +0x00, `CastEndAnimMontage` +0x30, `Interruptible` +0x39 | reflection records from 0x148ABE840 and 0x148ABC1C0 |
| `EffectVisualization.OverheadStatus`, `EffectLogic.UninterruptibleCast`, `EffectLogic.NeverShowInterruptOnCastBar`, `EffectLogic.Dynamic.NoSpellPushback` | tag globals 0x14A3CA2B0, 0x14A3C9ED0, 0x14A3C9ED8, 0x14A3C9F78 |

Decoded against **26320967**, the cancel of abilities by an applied effect, written up in
`docs/casting-and-channeling.md`:

| What | Where |
| --- | --- |
| `CRCancelAbilitiesBlockedByThisEffectsParentExecution`: vtable 0x148B3C718, execute +0x2D0; granted tags (+0xA48) of the context's `RootGEDef` (+0x98) | **0x1451085F0** |
| `RootGEDef` written at the application of an effect with an `AdditionalEffectsGameplayEffectComponent` (class getter 0x1449D01E0) | 0x1452D1229 to 0x1452D128A, in 0x1452D0F50 |
| Filter: with `AbilityType.Skill` (0x14A3C9FB0) or `Ability.Skill`, without `AbilityLogic.AtomicExecution` (0x14A3CA0C8) | 0x1450CB280; without-tags form 0x1450CAF20, execution 0x145108570 |
| Component cancel loop over the activatable abilities (+0x500, stride 0xF0): `AbilityTags` (+0xB0), `ActivationBlockedTags` (+0x248, getter 0x1450E0B30) | **0x1452D21A0**; blocked test 0x1452D23E2 to 0x1452D2483 |
| Per-ability cancel: `CancelAbility` (vtable +0x330) on each active instance, then `ApplyCooldown` (+0x3B0) for a committed ability | 0x1452D1F50; calls 0x1452D20ED, 0x1452D2136 |
| Engine `CancelAbility`: `CanBeCanceled` (+0x338), `EndAbility` (+0x470) with `bWasCancelled` | 0x1449DA700; 0x1449DA94B |
| `CRGameplayAbility` flags +0x414: bit 0 ended, 1 committed, 2 cooldown applied; `CommitAbility` (+0x370), `ActivateAbility` (+0x448), `EndAbility` (+0x470) | 0x1450DC8F0, 0x1450D95A0, **0x1450DCC60** |
| `EndAbility`: global cooldown effect removed for a cancelled ability not committed; `ApplyCooldown` under `CCP_OnAbilityEnd` (`CooldownCommitmentPolicy` +0x3C8 = 2) | 0x1450DCD26 to 0x1450DCDB1; 0x1450DCDB6 to 0x1450DCDF0 |
| `CharacterStatus.IgnoreMoveInput`, `.IgnoreActionInput`, `.IgnoreRotationInput`, `.Silenced`, `.Uninterruptible` | tag globals 0x14A3CA1A8, 0x14A3CA1B0, 0x14A3CA1B8, 0x14A3CA200, 0x14A3CA210 |

Decoded against **26320967**, the wait of `WaitDelayCR`, read by `EndAbilityPaths.cs` and written up in
`docs/ability-end-delays.md`:

| What | Where |
| --- | --- |
| `CRAbilityTask_WaitDelay::WaitDelayCR(OwningAbility, Time, ScaleTimeWithAbilitySpeed)`; name literals `WaitDelayCR` 0x148AD1AA0, `ScaleTimeWithAbilitySpeed` 0x148AD1A80 | **0x14512E940** |
| `ScaleTimeWithAbilitySpeed` true: task `Time` (+0x90) is `Time / factor`, divided once in the factory | `test bpl,bpl` 0x14512EA06, factor call 0x14512EA0E, `divss` 0x14512EA19, store 0x14512EA1D |
| `ScaleTimeWithAbilitySpeed` false: `Time` stored unchanged | 0x14512EA2D |
| Ability time factor: 1.0, the owner's `Haste` under `ScaleTimeWithHaste` (+0x3B8 bit 0), then `+ AttackSpeed - 1` under `ScaleTimeWithAttackSpeed` (bit 1) | 0x1450E06E0; tests 0x1450E0710, 0x1450E07A1; `addss` 0x1450E07F0, `subss` 0x1450E07F7 |
| Task `Activate`: `Time` above 0 sets a timer; `Time` of 0 or less ends the task and broadcasts the delegate at +0x80 in the same call | 0x145125D70; `comiss` 0x145125D9C, broadcast 0x145126028 |
| `InterruptCooldownDuration` +0x4A8, `InterruptCooldownEffectBaseClass` +0x4D0: constructor default `Value` 0, no table, row or registry | `UCRGameplayAbility` constructor 0x1450D7D60, stores 0x1450D7E7E to 0x1450D7EA3 |

Decoded against **26320967**, the Stagger pool's cap, removals and log line, behind the `stagger` texts in
`StaggerComponent.cs` and the `staggerDeltas` of `StaggerDeltas.cs`, written up in `docs/stagger.md`:

| What | Where |
| --- | --- |
| Setter cap, for Δ ≥ 0 alone: owner (component +0x98) cast to `CRFellowshipCharacter`, `StaggerMaxHealthPercentage` at level 0 × virtual +0xA38 | 0x1452C3589 to 0x1452C35EF; class getter 0x14509CBB0 |
| Cap under the pool: Δ = 0 and return. New pool over the cap: Δ −= new − cap | 0x1452C35F3 to 0x1452C3602; 0x1452C3604 to 0x1452C3618 |
| `CRFellowshipCharacter` vtable (constructor 0x145308740); +0xA38 is the current `MaxHealth` (attribute set character +0x898, attribute +0x38, `GetCurrentValue` 0x1421D97D0) | 0x148CFBD20; 0x1452EC9A0 |
| Damage hook: factor getter `StaggerDamageAdditionFactor`, `H −= Δ taken`, a `DAMAGE_ABSORBED` line of Δ taken × weight (entry +0x298, stride 0x2A0) | 0x1452C76E0; 0x1452C87E9 to 0x1452C87FA; 0x1452C887C |
| Heal hook: `Role` (+0x170) authority, target = owner, `EffectType.Environmental` returns, `EffectLogic.HealAffectStagger` required, factor getter `StaggerHealReductionFactor`, factor ≤ 0 returns, `clamp(heal × factor, 0, CurrentStagger)` | 0x1452C8932, 0x1452C8971, 0x1452C897A, 0x1452C89F2, 0x1452C7790, 0x1452C8AB6, 0x1452C8ACE to 0x1452C8AEA |
| `CRHeroAttributeSet` constructor: `StaggerDamageAdditionFactor` +0x218 and `StaggerHealReductionFactor` +0x228, base and current value 0 | **0x14500A430**, stores 0x14500A503, 0x14500A50A; records 0x148C0F6C0, 0x148C0F700 |
| `CRModifyStaggerExecution`: transient magnitude of `SetByCaller.Stagger` (tag global 0x14A3CA5E8), the target avatar's component (+0xF78), no other factor | 0x145109278, 0x145109367 to 0x145109383 |
| `AuthUpdateStaggerAndLog`: setter, combat logger, meters call for a removal with an ability and an effect (`StaggerModData`: source, target, `InstigatingAbility`, `InstigatingGameplayEffect`) | 0x1452C3699, 0x1452C36E1, 0x1452C37B2 (0x14527D320) |
| Stagger change log writer: event variant 0xA, the variant of the attribute resource writer 0x145297E00 (`RESOURCE_CHANGED`); type byte 7 at record +0xF0, amount `round(new × 100)` +0xF4, change +0xF8, maximum and overcap −1 | **0x145298560**, 0x145298685 to 0x1452986DD; queue 0x1452AE960 called at 0x145298985 |
| Every caller of the setter and of the logging form | 0x1452C3531 clear, 0x1452C87E4 damage hook, 0x1452C8B0B heal hook, 0x1452CDE4D tick, 0x1452C3699; 0x145109383; exec thunks 0x14505E495, 0x14505E568, 0x14505E69B, 0x14505EE5F |
| Period routine: the Haste division has no `EffectType.Dot` or `EffectType.Hot` test before it | 0x1452D5C70, `divss` 0x1452D5E0B |
| `UGameplayEffect.ConditionalGameplayEffects` +0x2A0 (record 0x1489892B0, deprecated): constructor and destructor alone in the ability module | 0x144A63B62, 0x144A6518C |
| `HealthChangedInfo` field names: `TotalHealthChange`, `OverHealOrOverKillAmount`, `MitigatedDamage`, `AbsorbedDamage`, `BlockedDamage` | literals from 0x148AAF108 |

Decoded against **26320967**, the AI leap, the spawned actor, the Blueprint timer and the actor tick interval,
written up in `docs/leaps-timers-respawn.md`:

| What | Where |
| --- | --- |
| `CRAbilityTask_AILeapTo`: vtable, constructor (`LeapTime` +0x150 default 1.5), size 0x158 | 0x148A97650, 0x14510DF20, `StaticClass` 0x144EFD800 |
| `AILeapToLocation`, `AILeapToTarget`: exec thunks, factories, parameter records | 0x144EFE0D0, 0x144EFE340; **0x14510EB10**, 0x14510EC70; 0x148A96DF0, 0x148A970B0 |
| Task fields: delegates `OnActivate` +0x80 to `OnNoPath` +0xD0, `OwningAICharacter` +0xE0, `GoalLocation` +0xE8, `MaxHeight` +0x100, `Timeout` +0x104, `AcceptanceRadius` +0x108, curve +0x110, curve end time +0x128, start time +0x130 | records from 0x148A97360 |
| `Activate` (vtable +0x2C8): start time, arc, timeout timer | **0x14510F4A0**; arc 0x145110650, launch velocity 0x1439CCB40, path to curve 0x14524FFF0, timeout handler 0x145115080 |
| `FindGroundLocationAt`, called by both leap factories with 500; exec thunk 0x144F66AF0 | 0x14522EF60 |
| `TickTask` (vtable +0x2D8): position at `clamp(elapsed / LeapTime, 0, 1)`, `OnArrived` at `LeapTime - elapsed <= 0` | **0x145117E30**; `subss` 0x145117FAB, broadcast 0x145117FD3 |
| `UWorld::TimeSeconds` +0x6B8, read by `GameplayStatics::GetTimeSeconds` | 0x1439DC5F4 |
| `CRAbilityTask_SpawnActorFromAbility`: factory, `BeginSpawningActor`, `FinishSpawningActor` | 0x14512BF10, 0x1451267D0, **0x145127440**; exec thunks 0x144F252A0, 0x144F249F0, 0x144F24C30 |
| `FinishSpawningActor`: `FinishSpawning`, `EndTask`, `Success` (task +0x80); `DidNotSpawn` is +0x90 | 0x145127477, 0x14512748A, 0x1451274A2 |
| Spawn refusal for an owner failing `DestroyConditions` (task +0xB0) | 0x145143C50 |
| `K2_SetTimerDelegate`: exec thunk, native, variance roll, `FirstDelay = Time + InitialStartDelay` | 0x143AFFB80, **0x143ABCF70**, 0x143ABD02B to 0x143ABD060, 0x143ABD16C |
| `FTimerManager::SetTimer` with a parameter struct (loop +0, once per frame +1, `FirstDelay` +4), and the overload building it | **0x144042D80**, 0x1440430A0; `FirstDelay < 0` takes the rate 0x144042F2F |
| Script delegate broadcast, and its wrapper for a delegate with no parameters | 0x1421F7F60, 0x1432D7890 |
| `SetActorTickInterval` exec (writes actor +0x3C), `AActor::SetActorTickEnabled` (vtable +0x490) | 0x1435CD9A0, 0x1435BE680 |
| `FTickFunction::SetTickFunctionEnable`, `FTickTaskLevel::AddTickFunction` (enabled set at level +8) | 0x144055450, 0x144027480 |
| Tick queueing: enabled function with `TickInterval > 0` goes on cooldown; next cooldown `TickInterval - (DeltaSeconds - CumulativeCooldown)` | 0x144051D20; 0x14405202C, 0x144051DCF to 0x144051DF4 |

Decoded against **26320967**, the wipe and the respawn, behind `settings.json` `respawn` (`GameModeRespawn.cs`) and
written up in `docs/leaps-timers-respawn.md`:

| What | Where |
| --- | --- |
| `CRGameModeBase`: vtable, constructor, `StaticClass`; size 0x438 | 0x148BD4978, 0x14533DF60, 0x144FE6E30 |
| `CRGameModeDungeon`: vtable, constructor, `StaticClass`; size 0x4E8; `OnTeamRespawnedAfterWipe` +0x438 with no broadcast | 0x148BD68A0, 0x14533E140, 0x144FE7050; record 0x148BD2A00 |
| `CRGameModeBase` fields: `RespawnCooldownEffect` +0x3A8, `OnCharacterDeathEvent` +0x3B8, wiped flag +0x3C9, wipe count +0x3CC, actors destroyed on a dungeon reset +0x3D0 | records from 0x148BD30D0 |
| `Tick` (vtable +0x500) runs the wipe check (+0xA38) once `HasMatchStarted` (+0x7B8) | 0x145349320, **0x145349640** |
| Wipe virtual (+0xA30): empty in the base, wipe count to the backend component in the dungeon game mode | 0x1453465D0 |
| Reset after a wipe: dungeon reset actors, dead AI characters' `AbilityLogic.ActivatableWhileDead` abilities, AI characters with +0xDE8 | 0x145347CC0, 0x145154DE0 |
| `CRFellowshipPlayerController`: vtable; `ServerRequestRespawn` (+0x1028) | 0x148D05A10, **0x14532E770** |
| Respawn: restart at a player start (+0x898) or in place (+0x8A0), `RespawnCooldownEffect` application, flag clear | **0x145347EA0**; 0x14534812A to 0x14534822A, 0x14534827C |
| `Cooldown.Respawn`, `ActorTypeTags.Boss`, `AbilityLogic.ActivatableWhileDead` tag globals | 0x14A3CA740, 0x14A3CA6B8, 0x14A3CA0D0 |
| `CRGameStateBossComponent.CurrentBossFightIndex` +0xD8: `IsInAnyBossFight`, setter | 0x1453450D0, 0x145348BA0 |
| `CRGameModeBossComponent`: boss fight end (index, completed), its two callers for a completed and a reset fight | **0x145337AC0**; 0x14533A4E0, 0x14533BF00 |
| `CRAICharacter::OnAllThreatLost`: a boss ends its boss fight, any other character resets | 0x145156CD0; reset 0x145155700; `GetBossFightIndex` 0x145155890 |
| `HasMatchEnded` (game mode vtable +0x7C0), `HasMatchStarted` (+0x7B8) | 0x1439BBF40, 0x1439BBF90 |
| Class names by `StaticClass`: `CRFellowshipCharacter`, `CRAICharacter`, `CRCharacterBase`, `CRGameStateBase`, `CRGameStateBossComponent`, `CRGameModeBossComponent` | 0x14509CBB0, 0x144F3A240, 0x144F86210, 0x144FF69E0, 0x144FF90C0, 0x144FE74C0 |

Decoded against **26320967**, implemented in `DurationPolicies.cs` and written up in `docs/effect-periods.md`:

| What | Where |
| --- | --- |
| `UGameplayEffect` constructor stores `DurationPolicy` (+0x30) 0, `Instant` | 0x144A63AB0, store 0x144A63ED7 |
| `CRGameplayEffectDamage` constructor, no store at +0x30; class registration 0x144FF3E90, constructor wrapper 0x144FF5590 | **0x1450E84D0** |
| The other native effect classes' registrations, each with its name literal and its constructor wrapper | 0x144FF4380 to 0x144FF5090, wrappers 0x144FF5500 to 0x144FF5650 |
| Their constructors, each with one store at +0x30 after the `UGameplayEffect` constructor call | 0x1450E67B0 to 0x1450EA350, the table in `docs/effect-periods.md` |

Decoded against **26320967**, the school test of the damage formula and an effect's asset tags, implemented in
`EffectAssetTags.cs` and written up in `docs/effect-periods.md`:

| What | Where |
| --- | --- |
| Calculation context: evaluate parameters +0x08 (source tags pointer, target tags pointer), spec copy +0x70, `GetAllAssetTags` +0x308, preset row +0x328, aggregated source tags +0x330, aggregated target tags +0x350 | **0x145106850**; `GetAggregatedTags` 0x144AAAF50 called at 0x145106929 and 0x145106940 |
| Spec: `CapturedSourceTags` +0x60, `CapturedTargetTags` +0xE8, `DynamicAssetTags` +0x190 | 0x145106922, 0x145106939, 0x144A76EB0 |
| School test over the aggregated source tags: `EffectType.Physical` first, then `EffectType.Magic` | 0x145107B1E, 0x145107B6E; tag globals 0x14A3C9E50, 0x14A3C9E48; the container is the driver's `r9` at 0x1450DD27E |
| Gather: preset flags +0x25 to +0x28 over the four school captures | 0x1450DB1AB, 0x1450DB1D6, 0x1450DB201, 0x1450DB22C |
| Value defaults: reductions (+0x34, +0x38) 0, multipliers (+0x30, +0x3C, +0x40, +0x44) 1 | constants 0x147CC0F50 and 0x147F57540, stored at 0x1450DD187 and 0x1450DD195 |
| Preset row of an effect (`EffectCalculationPreset` at Def +0xAA0); the row for `Execution.UseEmptyCalculationPreset` (tag global 0x14A3C9F90) | 0x1450ECDB0; 0x149FD0140 |
| Hit flag byte: `AbilityType.AutoAttack` 0x01, periodic 0x04 or 0x02, `EffectType.Magic` 0x10 before `EffectType.Physical` 0x08, `EffectLogic.NotReflectable` 0x40, `EffectType.StaggerDamage` 0x80 | **0x145107C50**; tests 0x145107CBD, 0x145107D77, 0x145107DB0, 0x145107E16, 0x145107E71, 0x145107D1B |
| Attack table roll and spread roll `(rand() & 0x7FFF) / 32767 - 0.5` | 0x14510C4D0; 0x14510C5D0 to 0x14510C5EA |
| `FGameplayEffectSpec::GetAllAssetTags`: `DynamicAssetTags` and the Def's cached asset tags (+0xA28) | 0x144A76EA0 |
| `AssetTagsGameplayEffectComponent` load and `SetAndApplyAssetTagChanges`: parent component 0x144A9CD90, `UpdateInheritedTagProperties` 0x144A91D70, `ApplyTo` 0x144A6B880 | 0x144AAFE60, 0x144AB4390 |
| `ApplyTo`: an empty target container takes `CombinedTags` whole | 0x144A6B88A to 0x144A6B8AC |
| `FellowshipMakeEffectSpec` calls the ability's virtual +0x2E0 with the new spec | 0x1450E03DC |
| `CRGameplayAbility::ApplyAbilityTagsToGameplayEffectSpec`: the engine's body (`AbilityTags` +0xB0), then `AbilityInstanceTags` (+0x648) into the spec's source tags and `AbilityInstanceSetByCallerMagnitudes` (+0x668) into the spec | **0x1450D9940**; engine 0x1449D8EB0 |
| `GetAbilityInstanceTags`, `GetAbilityInstanceSetByCallerMagnitudes` exec thunks | 0x144FEDDD0, 0x144FEDD00 |
| `CRSetByCallerDamageGameplayEffect` constructor: an `AssetTagsGameplayEffectComponent` subobject with `EffectType.Magic` | 0x1450E9F70, tag at 0x1450EA0C1; heal class 0x1450EA350, 0x1450EA4A1 |

Decoded against **26320967**, implemented in `CombatLogIds.cs` and written up in `docs/combat-log-ids.md`:

| What | Where |
| --- | --- |
| `HealthChangedInfo` fill: `InstigatingAbilityClass` +0x40 from the effect context's ability (context +0x18), `InstigatingEffectClass` +0x48 from the spec's effect | **0x1450E3600**; 0x1450E3685, 0x1450E36D2 |
| Log record's `AbilityClass` from the event's ability class, replaced by its `LogAsADifferentAbility` (`CRGameplayAbility` +0x610); the form taking an ability instance | **0x145299170**, read 0x145299285; 0x145299390 |
| `CRCombatLogAbilityEffectData`: id +0x00, name +0x08, source ability id +0x20, duration flag +0x28, auto attack flag +0x29, `AbilityClass` +0x30, `EffectClass` +0x38; text `{0}\|"{1}"\|{2}` | text 0x1452851D0, literal 0x148D35098 |
| Flags and resolve of a damage record's `InstigatingAbilityEffectData` (record +0x108): `DurationPolicy` not `Instant`, `AbilityType.AutoAttack` (0x14A3C9FA8) or `Ability.AutoAttack` (0x14A3CAA10) | **0x1452AE480**, called at 0x1452AB2AF; 0x1452AE5F9, 0x1452AE694, 0x1452AE6F0 |
| Id resolver: effect id for a `CRGameplayEffectDamage` with a duration, or with `AlwaysUseVisualDisplayName` (+0xAC0) and a `VisualDisplayName` (+0xAB0) on no auto attack; else the named ability's id; else the effect's | **0x145285FC0**; 0x1452860FA, 0x1452863D3, 0x1452865D2, 0x145286769, 0x145286959 |
| `CRCombatLogEffectData` resolver (aura lines), `CRCombatLogAbilityData` resolver (cast lines) | 0x145286AB0, 0x145285DC0 |
| `CRCombatLogGuidSubsystem`: getter, class name to id lookup (map +0x30, `_C` removed), builder over `Content/guid` (first line 0 tags +0xD0, 1 classes +0x30, 2 strings +0x80), row parser | 0x1452AB840, **0x1452AB8D0**, 0x1452ABF80, 0x1452A80A0 |
| `CRAbilityDataSubsystem`: getter, `AbilityDataMap` (+0x30, element 0x1E0) name lookup, default data +0x90, populator keyed by `GameplayAbilityAsset` and `AdditionalGameplayAbilityAssets` | 0x1450CD0A0, 0x1450CD280, **0x1450C7FE0** (inserts 0x1450C8459, 0x1450C84CD) |
| Literals `Invalid Ability Data`, `Unnamed DoT` | 0x148D2A698, 0x148D350B8 |
| Damage record constructor, its text function, the event names by `Flags` (+0x90) and `DamageTypeFlags` (+0x91) | 0x14526F780, **0x145275470** |
| Record reader by schema, the dispatcher by record type, the line writer (timestamp, text, newline) | 0x1452AA370, 0x1452A8260, 0x1452A0AD0 |
| `CRGameplayEffectDamage` constructor stores `AlwaysUseVisualDisplayName` false | 0x1450E8528 |
| `GameplayEffectUIData_BuffDebuff` `Name` +0x28; the effect's UI data component getter | 0x144F09F40 (class), 0x1449EECC0 |
| Logger's registration of a character: `OnActiveGameplayEffectAddedDelegateToSelf` (+0x1A0) bound to the applied handler, each active effect replayed | 0x145293BEB; bind 0x145293F6C, replay 0x14529429E |
| Applied handler (`EFFECT_APPLIED`, record type 0xC): active effect of the handle, tag test over the effect's cached asset tags (+0xA28), handlers of the other lines bound | **0x145295100**; 0x145295147, 0x145295162 to 0x1452951C7 |
| Tags of the test: `Effect.Buff`, `Effect.Debuff` (literals 0x148D34608, 0x148D34620), `EffectVisualization.Icon` (0x14A3CA2B8), `EffectVisualization.IconOnPartyUnitFrame` (0x14A3CA2C0) | container getter **0x145292650** |
| Removal handler (`EFFECT_REMOVED`, event set +0x18), stack change handler (`EFFECT_REFRESHED`, record type 0xD, +0x30), handler of the delegate at +0x60 (`EFFECT_REMOVED` on a true flag), dispel line (record type 0xE) | 0x145296DC0, 0x145296AE0, 0x1452968E0, 0x145296634 |
| Effect event record fill, its prepare (a record whose effect data does not resolve is left out), the text function with the three event names | 0x14528E2E0, **0x1452AE130** (0x1452AE1CE), 0x145275E30 |
| `EffectLogic.System` tag global, with no reader in the logger | 0x14A3C9EC0 |

No routine of the executable has the immediate 1,000,000 in the combat log code. Fellowship Logs adds it to the id of a
line logged under an effect.

Decoded against **26320967**, the damage modification task and the actor dependency effects, implemented in
`DamageModificationTasks.cs` and `ActorDependencies.cs` and written up in `docs/damage-absorption.md`:

| What | Where |
| --- | --- |
| `DamageModificationTask` factory: `IncomingDamageCharacter` +0x90, `OutgoingDamageCharacter` +0x98, `BlockOnTaskOwnerTag` +0xA0, `CapturedSourceTagsRequirements` +0xA8, `Prio` +0xF0; exec thunk 0x144EFF890, parameter names at 0x148A99660 | **0x1451115E0** |
| Task activation: handler into the incoming character's map (+0xC90) and the outgoing character's (+0xC78), keyed by `Prio` | 0x14510F960; map entry 0x145112C00, bind 0x14510D160 |
| Task handler: block tag, source tag query against context +0x330, `OnDamage` broadcast, new value from task +0xF4 | **0x145114290**; 0x1451142D1, 0x145114309, 0x14511439C, 0x1451143A9 |
| `SetDamageValue`, exec thunk 0x144F00310 | 0x145116A60 |
| Hook dispatcher: levels ascending, listeners of a level last first, virtual +0x60 | 0x1450D4920 |
| Levels of the native hooks: damage redirect 0, stagger 1, death prevention 3 | 0x1452824D1, 0x1452CA0E7, 0x1452720B1 |
| Driver delegates of the hit's target: before the attack table roll (character +0x848), after the absorption passes (+0x860); `Miss`, `Parry` and `Dodge` skip the second | 0x1450DD072, 0x1450DD3B9; `jne` 0x1450DD1D3 |
| `CRActorDependencyComponent`: instigator's class +0x28, owner's class +0x30; vtable 0x148ADF808, effect-added override at +0x2D0 | **0x1452EF3E0** |
| Its listeners: owner's class apply 0x1452F04A0 and remove 0x1452E37F0; instigator's class apply 0x1452F0820 and remove 0x1452E38D0 | lambda bodies 0x1452E9AA0, 0x1452E9B80, 0x1452E9C50, 0x1452E9D30 |
| Instigator's class apply: the hit's source is the main effect's owner (0x1452F089F), stack count of the main effect (0x1452F099F), `ApplyGameplayEffectSpecToTarget` (0x1452F0AE8) | 0x1452F0820 |
| Main effect removal: unbinds both listeners, removes the class | 0x1452EFAE0 |
| `CRGameplayEffectActorDependency` static class | 0x144FF4FE0 |

Decoded against **26320967**, the movement, tick and combat event tasks and the NPC walk speed, written up in
`docs/movement-tick-and-event-tasks.md`:

| What | Where |
| --- | --- |
| `AbilityTask_FellowshipMoveTo` natives `FellowshipMoveTo`, `FellowshipMoveToLocation`, `FollowActor`; exec thunks 0x144EFFAB0, 0x144EFFD20, 0x144EFFFA0; parameter records 0x148A998D0, 0x148A99B90, 0x148A99E30 | **0x145111AC0**, **0x145111BA0**, **0x145112AE0** |
| Task fields: `Goal` actor +0xE8, location +0xF0, flags +0x108 (`DisableCollision` 1, actor goal 2, follow 4, `CancelIfTargetDies` 8, `AllowPartialPaths` 0x10), `ProximityTolerance` +0x10C, `Timeout` +0x110, request id +0x114, `Controller` +0x118 | static class 0x144EFF0E0, size 0x120, constructor 0x144EFF340, vtable 0x148A9A790 |
| `GetInteractionRadius` of a `CRInteractable` goal added to `ProximityTolerance` | 0x145019620, `addss` 0x145112B4C |
| `Activate`: arrival within `ProximityTolerance` in 2D, timer of `Timeout` for `OnTimeout` | **0x14510F060**; `comisd` 0x14510F21C, timer 0x14510F3CF, handler 0x145115040 |
| Move start: `FindPathSync`, `OnNoPath` for a result other than Success (3), `RequestMove` | **0x145110BE0**; 0x1443D3B40, `cmp` 0x145110E74, 0x145110F63 |
| Request finished: nothing for the follow flag, Success to `OnArrived`, InvalidPath to `OnNoPath` | 0x145114AB0 |
| `TickTask`: dead goal to `OnNoPath`, follow move restart at `ProximityTolerance + 20` | **0x145117C60**; `test` 0x145117CA8, `addss` 0x145117DA3 |
| `FTimerManager::SetTimer` wrapper over 0x144042D80 | 0x1440430A0 |
| `CRAICharacter` combined move speed (vtable 0x148AEB930 +0xA50): `MoveSpeed` (set +0x168) x `GlobalNPCBaseMoveSpeed` (globals +0x310) x `PassiveMoveSpeed` (+0x178) x character +0xEB0 | **0x145155D80** |
| `MaxWalkSpeed` (movement +0x268) written from the virtual; character +0xEB0 default 1.0 and its setter | 0x1452ED990; 0x145152975, 0x14515A7B0 |
| Engine `AbilityTask_MoveToLocation::MoveToLocation` exec thunk, factory inlined: `max(Duration, 0.001)` to +0xD0, end time +0xD8 | 0x144A3BDE0; `maxss` 0x144A3C1C0, `addss` 0x144A3C200 |
| Its `TickTask`, vtable 0x1489758E0 +0x2D8 | 0x144A26EA0, `comiss` 0x144A26F62 |
| `CRAbilityTask_TickAsync::TickAsync`, exec thunk 0x144F02F50, parameter records 0x148A9EE80: `TickInterval` +0xB0, `TotalTime` +0xB4, `TickAtStart` +0xB8 | **0x145122B00**, vtable 0x148A9F6C8 |
| Its `Activate`: looping timer of `TickInterval`, timer of `TotalTime + 0.05` | 0x14511CE40; 0x14511D023, `addss` 0x14511D0F4, 0x14511D25C |
| Its tick handler, finish handler and `ExternalCancel` | 0x145120A50, 0x14511F3D0, 0x14511E670 |
| `CRAbilityTask_WaitForCombatEvents::WaitForCombatEvents` and `WaitForCombatEventsWithParams`, exec thunk 0x144F284A0, parameter records 0x148AD0E20 | **0x14512EDD0**, 0x14512EEA0, vtable 0x148AD2680 |
| Task fields: `EventTags` +0x90, `OnlyFromCurrentLife` +0xC8, external target flag +0xC9, `OnlyTriggerOnce` +0xCA, `OnlyMatchExact` +0xCB, `AssetTagsRequirements` +0xD0, `OptionalExternalTarget` +0x158 | factory stores 0x14512EE44 to 0x14512EE5A |
| Its `Activate` and event handler | 0x1451260E0, **0x1451275B0** |
| An ability task's vtable: `Activate` +0x2C8, `TickTask` +0x2D8, `ExternalCancel` +0x2E8, `OnDestroy` +0x308 | e.g. `CRAbilityTask_AILeapTo` 0x148A97650: 0x14510F4A0, 0x145117E30 |

Decoded against **26320967**, the attributes of the ability time rate and the partial channel tick, behind `haste` and
`attackSpeed` in `CreatureStats.cs` and written up in `docs/casting-and-channeling.md`:

| What | Where |
| --- | --- |
| `CRBasicAttributeSet` static class (size 0x430), constructor | 0x144F04090, **0x1450BB750** |
| `Haste` +0xF8: base +0x100 and current +0x104 both 1.0 | stores 0x1450BB89E, 0x1450BB8A8; reflection record 0x148AA0D00 |
| `AttackSpeed` +0x2A8: base +0x2B0 and current +0x2B4 both 1.0 | stores 0x1450BBABE, 0x1450BBAC8; reflection record 0x148AA13C0 |
| Time rate reads: `Haste` property global 0x14A3C0728, `AttackSpeed` 0x14A3BFF98, `GetNumericAttribute` 0x144A10E50 | calls 0x1450E077B, 0x1450E07E6 |
| `CRAbilityTask_TargetedNPCCast::ServerTargetedNPCCast`, exec thunk 0x144F24FC0: its cast is a `ServerStartCasting` call | **0x14512B9C0**, call 0x14512BADD |
| Channel expiry: `EnablePartialTicks` test, `partial` (+0x118) against 1e-8, `OnTick(partial)`, `OnFinished(0)` | 0x14511F2CE, 0x14511F2E4, 0x14511F2F4, 0x14511F333 |
| `SetDynamicDuration` (`ChannelingDuration` +0x08), `SetDynamicTickInterval` (`ChannelingTickInterval` +0x30); exec thunks 0x144F1C520, 0x144F1C660 | 0x145121B30, 0x145121B40 |
| `FScalableFloat::SetValue`: `Value` stored, curve table, row and registry cleared | 0x144AB46C0 |

Decoded against **26320967**, the effect stack rule in `EffectStacks.cs`, written up in `docs/effect-stacks.md`:

| What | Where |
| --- | --- |
| Stack lookup: none for `StackingType` (+0x9E1) 0 or the `Instant` policy; `AggregateBySource` matches the instigator component | **0x144A74BD0**; 0x144A74BFD, 0x144A74C0F, 0x144A74DC7 |
| Stack count `min(existing + applied, StackCountMax)`: spec `StackCount` +0x1C0, spec `StackCountMax` +0x58 | 0x144A6A78F to 0x144A6A7AD, 0x144A6A977 to 0x144A6A98B |
| Spec `StackCountMax` from the effect's `StackCountMax` scalable float (+0x9E8), rounded. `StackLimitCount` (+0x9E4) has no reader | 0x144A8F520 to 0x144A8F550 |
| Overflow routine: `OverflowEffects` +0x2B0, `bDenyOverflowApplication` +0x2C0, `bClearStackOnOverflow` +0x2C1 | **0x144A7C3F0**, called at 0x144A6A782 |
| Period timer set again under `StackPeriodResetPolicy`; next-frame tick for `bExecutePeriodicEffectOnApplication` (+0x240) | 0x144A6B2F4 to 0x144A6B518, flag test 0x144A6B3BB |
| `StackExpirationPolicy` (+0xA13) at the end of the duration | 0x144A6FC35 |
| `UGameplayEffect` constructor stores at `StackingType`, `RecaptureSourceAttributesBeforeEveryExecution` (+0xA10), the duration and period policies | 0x144A63EEE, 0x144A63E60, 0x144A63F2B |
| `CRChargeEffect` constructor stack stores: `AggregateByTarget`, `NeverRefresh` and `NeverReset`, `RemoveSingleStackAndRefreshDuration`, `StackCountMax` 0. No other routine stores at +0x9E1 or +0xA13 | 0x1450E71E0; 0x1450E749D, 0x1450E74D3, 0x1450E74DC, 0x1450E74CA |
| `GetStackCount`; the damage gather's read and its `bSilentStack` clamp (bit 0 of +0xA98, setter 0x144FF4170) | 0x144A7BDD0; 0x1450DB595, 0x1450DB5BE |
| Healing driver's stack count read; healing formula's multiply | 0x1450DE6AA; 0x145107770, `mulss` 0x145107874 |
| `RecaptureSourceAttributesBeforeEveryExecution`: source capture again before a tick and before an instant execution | 0x144A808C4, 0x1449FD735; capture 0x144A6F7D0 |
| `FellowshipMakeLooseEffectSpec`, exec thunk 0x144F66590: `MakeOutgoingSpec` on the source's component, 8th argument the level, 9th to `SetStackCount` | **0x14522E110**; 0x14522E22E, 0x14522E249 |
| `CalculateAndSetMultiTargetDamageFalloffScaler` in this build, exec thunk 0x144F5AD60; actor count from `GetAllActorsFromTargetData` 0x144A0D930 | **0x145218380**, `sqrtss` 0x1452184C1 |
| `AcquireTargets_CylinderAoe`, exec thunk 0x145072420; character query, flat list walk | **0x1450FAD20**; 0x1450F3920, 0x145534620 |
| `CRGameplayEffectDamage` static class | 0x144FF3E90 |

Decoded against **26320967**, the attribute captures of the calculation classes in `CalculationCaptures.cs`, written
up in `docs/attribute-captures.md`:

| What | Where |
| --- | --- |
| Capture definition, 0x40 bytes: `AttributeSource` +0x38 (`Source` 0, `Target` 1), `bSnapshot` +0x39; a statics constructor stores both as one word, 0x0100 or 0x0001 | e.g. `DamageScale` 0x1450D58ED, `DodgeChance` 0x1450D5CFF |
| Shared statics: `Strength` +0x08, `Agility` +0x50, `Intellect` +0x98, `PrimaryAttribute` +0xE0, `CritChance` +0x128, `CritMultiplier` +0x170, each `Source` with `bSnapshot` | accessor 0x1450E5720, constructor **0x1450D6E10** |
| Damage statics: 7 `Source` definitions with `bSnapshot`, 15 `Target` definitions without it | accessor 0x1450DCBE0, constructor **0x1450D5390** |
| Heal statics: `LifeSteal` +0x08, `HealThreatMultiplier` +0x128, `HealScale` +0x170, `Expertise` +0x1B8, `BalancingHealAura` +0x290 as `Source` with `bSnapshot`; `IncomingHealMultiplier` +0x50, `IncomingHealSign` +0x98, `ReflectHeal` +0xE0, `Health` +0x200, `MaxHealth` +0x248 as `Target` | accessor 0x1450E27E0, constructor **0x1450D65C0** |
| Attribute set classes of the statics: `CRBasicAttributeSet`, `CRHeroAttributeSet` (`Expertise`, `BalancingDamageAura`, `BalancingHealAura`, `Avoidance`) | 0x144F04090, 0x1450094E0 |
| `RelevantAttributesToCapture` (+0x28) additions: `CRDamageCalculationBase` 24, `CRHealCalculationBase` 12 | **0x1450D7510**, **0x1450D80C0** |
| Main stat constructors, damage: Strength, Agility, Intellect, PrimaryAttribute, UserDefined | 0x1450D8910, 0x1450D73F0, 0x1450D84C0, 0x1450D8700, 0x1450D8A30 |
| Main stat constructors, heal: Strength, Agility, Intellect, PrimaryAttribute, UserDefined | 0x1450D89A0, 0x1450D7480, 0x1450D8550, 0x1450D8790, 0x1450D8A60 |
| `CRDamageReflectDamageCalculation`, `CRLifeStealHealCalculation`, `CRReflectHealCalculation`, `CRManaGainCalculation` constructors | 0x1450D7CC0, 0x1450D85E0, 0x1450D8820, 0x1450D86D0 |
| Static class functions with the name literal, super class and constructor wrapper: Intellect damage, ManaGain, UserDefined damage, UserDefined heal | 0x144FB2320, 0x144FB2480, 0x144FB2110, 0x144FB1CF0; wrappers 0x144FB2CB0 to 0x144FB2D70 |
| `CaptureAttributeForGameplayEffect`: component 0x144A01BE0, container; `bSnapshot` test, aggregator copy (0xB8 bytes), aggregator reference | **0x144A6F7A0**; 0x144A6F7AE, 0x144AB4A30, 0x144A65E90 |
| `FGameplayEffectSpec::CaptureDataFromSource(bSkipRecaptureSourceActorTags)`: `Source` definitions at spec +0x18, 0x50 bytes each | **0x144A6F7D0** |
| Its callers: `Initialize` 0x144A80040, the periodic tick 0x144A80830, the instant execution, `SetContext` 0x144A8F240 | 0x144A801B0 (and 0x144A800CD), 0x144A808F1, 0x1449FD764, 0x144A8F27D |
| `CRBlueprintLibrary::SetEffectContext`, exec thunk 0x144F75580, the one caller of `SetContext` | 0x1452432E0 |
| `FGameplayAbilityTargetData::ApplyGameplayEffectSpec`: spec copy and context copy for each target, no capture | 0x144A4F410; 0x144A4F5B9, 0x144A4F5D8 |
| Target capture at an application; `CaptureAttributeDataFromTarget` | 0x144A6ACEB; 0x144A6F710 |
| `AttemptCalculateCapturedAttributeMagnitude`: scoped aggregator lookup, `FindCaptureSpecByDefinition`, capture evaluation | **0x144AA2990**; 0x144A9CAC0, 0x144A73C10, 0x144A6B950 |
| Aggregator evaluation and its pass over the modifiers with the parameters; `ExecuteActiveEffectsFrom` reads the target actor's tags (spec +0xE8) | 0x144AA8310, 0x144AA87B0; 0x144A72D10 |
| Attack table gather (calculation vtable +0x2E0): `CritChance`, `DodgeChance`, `ParryChance`, `BlockChance`; `min(CritChance, 1)` without `CanGrievousCrit` (preset +0x1D) | **0x1450DB760**; 0x1450DB7BD |
| Source formula's `EHitType` factor: 0.75 for `GlancingBlow` (3), `CritMultiplier` for `CriticalStrike` (4), `(CritChance - 1) x G + CritMultiplier` for `GrievousCriticalStrike` (5), 1.5 for `CrushingBlow` (6); `G` from the row of `GlobalGrievousCritScalerKey` (globals +0x368) | 0x1451079B8 to 0x1451079F9; 0x1450CDBF0 |
| Attack table roll's thresholds in `ECRAttackResult` order: miss (values +0x0C), dodge (+0x04), parry (+0x10), 0, block (+0x08), crit (+0x00), 0 | 0x14510C51D to 0x14510C5CD |
| Healing gather's `HealScale` factor of the source and target characters | 0x145362710, called at 0x1450DB9C6 |

Decoded against **26320967**, the death of a character and the alive state of a target query, written up in
`docs/effect-stacks.md`:

| What | Where |
| --- | --- |
| Death routine of a character, authority alone: dead flag (+0x92D), virtual +0xAB8, removal of the `EffectLogic.RemoveOnDeath` effects | **0x1452ED190**; 0x1452ED233, 0x1452ED25A, 0x1452ED326 |
| `EffectLogic.RemoveOnDeath` tag global; removal of the active effects with a tag | 0x14A3C9EE0; 0x144A208D0 |
| `CRAICharacter` vtable 0x148AEB930: alive getter +0xA10, death state handler +0xAB8 | 0x144F3AC00, 0x145157A20 |
| Death state handler of the base class: bit 0 of the character's flags in the targeting registry (handle at character +0xA82) | **0x1452F0BD0**; flag write 0x145534A50 |
| Filter context of a target query: the four state bytes of the state filter (+8 to +0xB) as bits 1, 2, 4 and 8, required for 1 and forbidden for 0 | **0x1450C9260**; 0x1450C9402 to 0x1450C9474 |
| List walk's flag test: required bits (context +0), forbidden bits (context +4), entry flags (+0x30 of a 0x38 byte entry) | 0x1455346E0 to 0x1455346F2 |

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
