---
name: datamining-fellowship
description: Use when a Fellowship tooltip number is wrong or missing, when a value is not in any data table, when reconstructing a game formula from screenshots, when reading the shipping binary, or when deciding where a fact about items, stats, tempering or power potential actually lives.
---

# Datamining Fellowship

The general method is the `datamining-unreal-builds` skill. The asset-level data model, the tag join
and the export contract are `extracting-fellowship-data`.

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

Every address below was decoded against the **24736527** shipping exe. The exe changed in 24952830, so
re-anchor from the name-literal chain before reusing one.

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

Decoded against **26162532**, the damage pipeline every `CRDamageCalculationBase` execution runs:

| What | Where |
| --- | --- |
| `UCRDamageCalculationBase::Execute_Implementation` | **0x1450DAFB0**, vtable 0x148B73498 slot +0x2D0; `CRIntellectDamageCalculation` vtable 0x148B74058 and `CRUserDefinedDamageCalculation` vtable 0x148B73788 differ from it only at slot +0x2D8 |
| Attribute gather, slot +0x2D8: base 0x1450D9120; Intellect override 0x1450D9680 adds the Intellect capture to A[0x24]; user-defined override 0x1450D9770 puts the `Transient.Damage` aggregator (tag global 0x14A3BB450) there | capture statics 0x14A3B1E90 (built at 0x1450D33B0: LifeSteal +0x8, DamageScale +0x50, SourceDamageReflect +0x98, TargetDamageReflect +0xE0, DodgeChance +0x128, ParryChance +0x170, BlockChance +0x1B8, BlockDamageReductionFlat +0x200, BlockDamageReductionMultiplier +0x248, IncomingDamageMultiplier +0x290, Armor +0x2D8, DifficultyDamageScale +0x320, DamageThreatMultiplier +0x368, Avoidance +0x3B0, PhysicalDamageReduction +0x3F8, MagicDamageReduction +0x440, PhysicalIncomingDamageMultiplier +0x488, MagicalIncomingDamageMultiplier +0x4D0, AoeIncomingDamageMultiplier +0x518, Expertise +0x560, DamageMitigationScale +0x5A8, BalancingDamageAura +0x5F0) and 0x14A3B19B0 (built at 0x1450D4E30: Strength +0x8, Agility +0x50, Intellect +0x98, PrimaryAttribute +0xE0, CritChance +0x128, CritMultiplier +0x170, MaxHealth +0x1B8) |
| Raw roll `round(A[0x24] × DamageScale × hitMultiplier × DifficultyDamageScale × (1 + roll × Spread) × (1 + Expertise) × PartialTickFactor × StackCount × BalancingDamageAura)` | **0x145105A10**; hit multiplier 0x145105A21-0x145105A71 (crit = CritMultiplier A[4], grievous = (CritChance − 1) × grievous + CritMultiplier, glancing 0.75, crushing 1.5); roll uniform in [−0.5, 0.5) at 0x14510A640-0x14510A662; Spread = `SetByCallerTags.Spread` (0x14A3BB388) else preset `DefaultSpread`; `SetByCaller.PartialTickFactor` (0x14A3BB3B0) for EffectType.Dot/Hot specs at 0x1450D9522-0x1450D960B; stack count at 0x1450D9610-0x1450D9661 |
| Target side: armor table, `clamp(1 − Avoidance)`, IncomingDamageMultiplier, EffectType.Physical (0x14A3BAC10) or EffectType.Magic (0x14A3BAC08) reduction pair, AoeIncomingDamageMultiplier, block | **0x145105AE0** |
| Final: `round(T × DamageMitigationScale)`, threat, lifesteal, reflect (skipped under EffectLogic.IgnoreDamageReflect 0x14A3BACE8) | **0x145105930**; round half away from zero **0x1412A6B40** |
| `HealthChangedInfo` (struct params 0x149F7D900) built in Execute: `TotalHealthChange` = −(health damage + absorbed) at 0x1450DB752-0x1450DB769, `OverHealOrOverKillAmount` 0 at 0x1450DB7BD | handed to 0x1450E1670 (instigating classes, then character virtuals +0xB18 target / +0xB20 source of `ACRCharacterBase` vtable 0x148B41418 = 0x1452E0540 / 0x1452E0940, which copy it into the `Event.Incoming.*` / `Event.Damage.*` gameplay-event payloads) and to the recorder 0x145278E20 (skipped under EffectLogic.NoLogging 0x14A3BACF0) |
| Preset row (`CREffectCalculationPreset`, size 0x38, `FText` 0x10 here): bools CanMiss 0x18 … ApplyTargetAoeIncomingDamageMultiplier 0x29, `DefaultSpread` 0x2C, `ApplyAscensionScaling` 0x30 | read through ctx+0x328 in 0x1450D9120 and 0x1450D97C0 |
| `FAggregatorModChannel::EvaluateWithBase`: Override wins, else `((Base + ΣAdditive) × (1 + Σ(Multiplicitive − 1)) / (1 + Σ(Division − 1))) × ΠMultiplyCompound + ΣAddFinal` | **0x144AA79C0**, channel loop 0x144AA7B50, capture entry 0x144AA71C0 / 0x144AA1840; `EGameplayModOp` params 0x149F62B38 (0 AddBase, 1 MultiplyAdditive, 2 DivideAdditive, 3 Override, 4 MultiplyCompound, 5 AddFinal) |
| `UCRBasicAttributeSet` vtable 0x148A999E8: PostGameplayEffectExecute **0x1450C2B10**, health apply (clamp to MinHealth/MaxHealth, `CRHealthModifiedData` to character delegates +0x17A8/+0x17C0) slot +0x318 = **0x1450C3300** | `DamageScale` base 1.0 at 0x1450BA04B, `DamageMitigationScale` base 1.0 at 0x1450BA0D3 in the constructor 0x1450B9C70 |
| `AddFilter_CharacterContext` params: FilterBuilder, Alive, Targetable, Hittable, InCombat, Falling, Moving, Mounted, each `ECRTargetFilter_BoolState` (0 MustBeFalse, 1 MustBeTrue, 2 Ignore); `AddFilter_Relationship`: `ECRTargetFilter_FactionRelation` (0 Friendly, 1 Neutral, 2 Hostile, 3 NotFriendly, 4 NotNeutral, 5 NotHostile, 6 None) | UFunction params 0x149FB0DA0 / 0x149FB1260, enum params 0x149F81828 / 0x149F81858 |

Also decoded against **26162532**:

| What | Where |
| --- | --- |
| `CRLinkedEffectComponent` activation override: registers parent callbacks, applies every `AdditionalEffectsToApply` entry on each activation (no already-applied test), links child handles in the context's list at +0xA0 | vtable 0x148C3BE20 slot +0x2D0 = **0x1450BD430**; constructor 0x14502E7C0 defaults the two RemoveLinkedOnDestroy flags true |
| Partial tick on removal of a Dot/Hot spec: factor = (period − remaining)/period into `SetByCaller.PartialTickFactor`, one extra tick; a boundary expiry executes a full tick instead | ASC vtable 0x148AAE478 slot +0x948 = **0x1452D0C70**, called from 0x144A6EC1A (full tick at 0x144A6EBE2); `UCRAbilitySystemGlobals.MinimumPartialTickTime` +0x32C, 0; skip tag `EffectLogic.IgnorePartialTicks` 0x14A3BACD0 |
| Stat-enchant node ranks: `GetStatEnchantPowerFromItemLevel` (curve at subsystem +0xE8, truncated) summed per `ItemTrait.TraitCategory.StatEnchant` node, 1 per other node, into `CREquippedTraitInfo.EquippedRanks` | body **0x1453ACAD0** (exec 0x145025560, native table 0x148C2B300); the sum at **0x145370B40**; tag global 0x14A3BB6B8 |
| `GE_PrimaryAttributeIntellect` (effect 4): `Intellect MultiplyCompound = AttributeBased(PrimaryAttributeMultiplier)`, `Intellect Additive = AttributeBased(PrimaryAttributeFlatBonus)`; no native reader of the multiplier | effects dump |
| `ACRAoeActorBase` constructor **0x1450C61F0** (thunk 0x144F3CA70): `ApplicationDelay` +0x330 defaults to **−1.0** (0x1450C629C), `ApplicationPeriod` +0x300 to 0 (0x1450C6294); a delay below 0 applies at once in the periodic callback, a delay ≥ 0 goes through the one-shot timer 0x1450CD020 (SetTimer 0x144042060 at 0x1450CD108) | `NetServerMaxTickRate` 30 in the ini; 0x144F3FEA0 is a different class's constructor |
| `UCRGemAttributeSet` vtable 0x148BF2DB0 overrides no attribute-change virtual | ctor 0x144FFD230 |
| Empty-preset branch 0x1450D9170-0x1450D91B7: the source's `HeroAttributeSet` (`ACRFellowshipCharacter` +0x1010) at +0x238 = `CasualDamageScaling` (base and current 1.0 at 0x145008DD9 / 0x145008DE3), not Tenacity (`UCRBasicAttributeSet` +0x238, default 0 at 0x1450B9F3B) | gather 0x1450D9120 |
| `ACRAoeActorBase::SetActive` (exec 0x144F3DB80, body **0x1450D1130**, jump table 0x1450D154C on `ECREffectApplicationRule`): PeriodicApplication sets a looping timer of ApplicationPeriod with first delay 0 (0x1450D13FE-0x1450D1421); the callback 0x1450CF830 → **0x1450D20F0** calls the Blueprint event `OnTryApplyEffectPreTargetValidation` (FName 0x14A3909F8) per target, then applies at once only for ApplicationDelay < 0, else a one-shot timer of ApplicationDelay (0x1450CD020); `FTimerManager::InternalSetTimer` 0x144041D40 sets no timer for a rate ≤ 0 (0x144041DE0) | `UpdateApplicationDelay` exec 0x144F3DCA0, body 0x1450D20D0 writes +0x330 |
| `UCRHeroAttributeSet` vtable 0x148C040E0: slot +0x2D8 **0x1450EEC10** clamps SpiritPoints to [0, MaxSpiritPoints] (0x1450EEE04-0x1450EEE4F), slot +0x2D0 **0x1450EF890** clamps the incoming change (0x1450EF9F4); `GE_Shared_SpiritPointGain` magnitude is `GEC_Shared_SpiritPointGainCalculation` = SetByCaller × dungeon-timer bonus; `SpiritGenerationDamage` has no native reader | effects dump |
| Not found: the passive Spirit regen timer (`Hero.Spirit.PassiveGainRate/Period` have no literal; `SpiritRegenRate` +0x188 / `SpiritRegenPeriod` +0x198 are read only by a UI object at 0x14526F390 and copy constructors) and the combatantinfo writer (the `COMBATANT_INFO` FNames at e.g. 0x14A38CFC8 have no code reader) | open |
| Not found: the stack-count factoring (`ComputeStackedModifierMagnitude`); no {0,1,1,0,1,0} bias table in the image, no int-to-float plus ModOp compare in 0x1449C0000-0x144B00000 or 0x144F00000-0x145500000, `GetStackCount` 0x144A7AC80 (spec +0x1C0) has no such caller | open |

Decoded against **26162532**, the rating conversion, the tempering term and the max health formula:

| What | Where |
| --- | --- |
| `UCRAttributeRatingToPercentageModifier::CalculateBaseMagnitude_Implementation` (`GEC_CritRating`, `GEC_HasteRating`, `GEC_PlayerExpertiseCalculation`, `GEC_PlayerSpiritCalculation` on `GE_HeroAttributesBase`): captured rating (0x145230BE0) rounded half to even (0x1412A6C40) and truncated to an int, then the subsystem's percentage | vtable **0x148AEBF58** slot +0x2D0 = **0x1450D8AE0**; name literal 0x148AE9DD2, registration 0x144F3E070, constructor thunk 0x144F3E870 |
| `UCRItemStaticDataSubsystem` percentage for an attribute and an integer rating: a per-attribute `TArray<float>` cache at +0x210 indexed by the rating, filled from the `AttributeProperties` row (map at +0x40, found by `FGameplayAttribute ==` at 0x145387D00); no row gives 0 | **0x14538E7A0**, getter 0x145386DF0 |
| The brackets, on the result with compounded penalties: `remaining = rating × BaseStatMultiplier`; per bracket in order `remaining ×= PenaltyPercentage` (0x1453A5200, before the band is filled), `inside = min(MaxBracketValue − previousCeiling, remaining)`, `remaining −= inside`, `total += inside`, stop at `remaining ≤ 0`; result `(remaining × FinalPenaltyPercentage + total) × 0.01`. 199 rating at 0.16 with 10/15/20/25 at 1/0.98/0.96/0.94 then 0.92 = 29.694%. | **0x1453A51D0**; row fields `BaseStatMultiplier` +0x58, `Brackets` +0x60 (`CRTwaxelStatReductionBracket` stride 8: `MaxBracketValue` +0, `PenaltyPercentage` +4), `FinalPenaltyPercentage` +0x70 |
| `UCRDynamicHeroItem` item stat magnitude (re-anchored; `this`, `FCRHeroItemInstance*`, item level, out array) | vtable **0x148DAAFF0** slot +0x2D0 = **0x14565A0A0**; registration 0x14564F3B0, constructor thunk 0x145651B60 |
| Tempering: BaseItemFixed slots only (`cmp byte ptr [r15],0` at 0x14565A4B6); `+ TimesUpgraded × 1` for every stat (0x14565A4DB), and for Stamina (`cmp sil,1` at 0x14565A4DF) `× UCRItemSettings.TemperStaminaMultiplierAtItemLevel` (+0x548, `CRItemTemperMultiplierAtItemLevel` {ItemLevel +0, TemperValue +4}, the entry with the greatest `ItemLevel` ≤ the item level, 0x14565A4EC-0x14565A524) else `TemperDefaultStaminaMultiplier` (+0x540, 0x14565A53E); `DefaultGame.ini` has 1.0 and (315, 2.0) | `movd xmm1,[rax+0x30]` **0x14565A54E**, `cvtdq2ps` 0x14565A553, `mulss` 0x14565A556, `addss` **0x14565A55A**, round half to even at 0x14565A561 |
| `UCRBasicAttributeSet` attribute-change hook: a `BaseHealth`, `Stamina`, `StaminaToMaxHealthMultiplier` or `DifficultyHealthScale` change (statics 0x14A3B0CB0, 0x14A3B0CE0, 0x14A3B0DD0, 0x14A3B0DB0) recomputes `MaxHealth` **base** = `(ceil(scale × BaseHealth) + Stamina × StaminaToMaxHealthMultiplier) × InstanceMaxHealthMultiplier (+0x3D8, 1.0) × DifficultyHealthScale`; scale is `UCRAbilitySystemGlobals.HealthScale4p` (+0x31C, 1.4) or `HealthScale5p` (+0x320, 1.8) for an `ACRAICharacter` owner (StaticClass 0x144F39030) when the `ACRGameStateBase` int at +0x468 is 4 or 5, else 1.0; health is rescaled by newMax / oldMax | vtable 0x148A999E8 slot +0x2E8 = **0x1450C2500**, body 0x1450C2558; recompute **0x1450C2280**, `roundps …,2` (ceil) at 0x1450C23CE, `SetNumericAttributeBase` 0x144A22620 at 0x1450C2405 |

Decoded against **26162532**, the gem node granting (`UCRGemPowerComponent`, vtable 0x148BF31E0, constructor thunk
0x144FFD2B0; `AppliedGemPower` map +0xA8, element stride 0x58, key `GemRankKey` {Rank +0, GemTypeID +4}; the per-colour
active rank set map at +0xF8):

| What | Where |
| --- | --- |
| BeginPlay (slot +0x3C0) binds the callback to each gem power attribute's value-change delegate (0x144A0D790) and evaluates each colour once with the current value truncated to an int | **0x145383260**; callback pointer at 0x14538334D |
| Callback: `if (NewValue != OldValue) Update(this, &Attribute, (int)NewValue)` | **0x145390CE0** |
| `Update`: authority only (`GetOwnerRole` 0x1437E4340 == 3); attribute → `GemTypeID` by the row's `GameplayAttribute` (0x14538A7B0); desired = `ActiveRanks`; toRemove = current − desired, toAdd = desired − current (`TSet::Difference` 0x145385930); removals (ClearAbility 0x144A01520, RemoveActiveGameplayEffect 0x144A1F870, loose tags 0x144AB1DB0, map entry 0x145392130) before grants (augmentation resolved 0x14153C5C0, `IsA(UCRCharacterAugmentationAsset)` 0x144F80FF0, applied by 0x145382BB0: abilities given, effects applied to self 0x144A143A0, loose tags added) | **0x145393E00**; removal loop 0x145394124-0x145394415, grant loop 0x14539441A-0x1453946E7 |
| `ActiveRanks(subsystem, gemType, int power)`: from the row's last `GemDefinitionsData` node downwards, each node with `RequiredGemPower` ≤ power is taken until **five** are taken (`cmp r8d,[0x149FC33D8]`, a `.data` word of 5 nothing writes); the replacement of a lower node is this cap, not a node field | **0x145388B10**; loop 0x145388BF0-0x14538914C, threshold compare 0x145388C0B |

Decoded against **26162532**, procs, the applied and refresh delegates and the trait grant:

| What | Where |
| --- | --- |
| `UCRRandomStreamComponent::RollProcPerMinute(ProcTag, PPM, ScaleWithHaste)`: per-tag `{LastRollTime, LastProcTime, HasProcced}` map at +0xF8 (stride 0x1C, new entry at `DungeonStartTimestamp`); `PPM ×= Haste` attribute (0x1452C8137); `chance = PPM × sinceRoll / 60 × max(1, 0.05 × PPM × min(sinceProc, 1000) − 3.5)`; before the first proc `chance = max(chance, PPM / FirstProcChanceFloorPPMDivisor)` (+0x14D8, ini 9.0); `rand() & 0x7FFF / 32767`; `LastRollTime = now` on every roll | exec 0x1450547A0, body **0x1452C7ED0**, roll 0x1452C8159-0x1452C8243 |
| `UCRPrimaryAttributeDamageCalculation` gather (vtable 0x148B74348 slot +0x2D8): base gather, then the `PrimaryAttribute` capture (0x14A3B19B0 +0xE0) into A[0x24]; every capture of that static is Source + **snapshot** (`mov word ptr [rbp+0x27],0x100` at 0x1450D51D2) | **0x1450D96D0**, definitions built at 0x1450D4E30 |
| `UCRAbilityTask_WaitGameplayEffectAppliedToTarget` Activate binds ASC +0x188 and, under `InTriggerPeriodicExecutions` (+0x2B9), +0x1D0; `UCRAbilityTask_WaitGameplayEffectDurationRefresh` Activate binds ASC **+0x12C0** | vtables 0x148AC9050 / 0x148ACAB58, Activate **0x14512FEE0** / **0x145130020**, callbacks 0x145136650 / 0x145136890 |
| `ApplyGameplayEffectSpecToSelf` broadcasts +0x170 then the instigator's +0x188 unconditionally after a successful application, a stack add included | **0x1449FC32B**, broadcasts 0x1449FC72A / 0x1449FC73B; container apply 0x144A69540 (existing stack flag 0x144A69625, `StackDurationRefreshPolicy` +0xA11 at 0x144A6987D) |
| Stack refresh: `RestartActiveGameplayEffectDuration` (0x144A8D1E0, from 0x144A69E24) → `OnDurationChange` 0x144A84840 → owner virtual +0x718 = fork override **0x1452D5500**, which broadcasts the instigator's +0x12C0 only when the effect's progress (0x144A7A8D0) is 0 | `jmp [rax+0x718]` 0x144A84944, `add r14,0x12C0` 0x1452D5623 |
| Trait grant: rank = `min(EquippedRanks, MaxTraitRank)` (0x14536B8F3-0x14536B902) into the record's +0x2C; `FGameplayAbilitySpec(class, Level = record+0x2C, InputID = record+0x30 or −1 under ability CDO byte +0x3BA)` then `GiveAbility` | apply **0x145369190** (0x145369373-0x1453693BA), caller chunk 0x14536A777, gather 0x1453A8D40 |

Decoded against **26162532**, the periodic timer, the fork's time rate and the channel task:

| What | Where |
| --- | --- |
| A periodic effect's real period: owner virtual +0x950 = **0x1452D1D70** gives `Spec.Period / Haste(instigator at application)` unless the asset-tag component holds 0x14A3BAC60 / 0x14A3BAC68 (`TickrateNotAffectedByHaste` / `…Exclusively`), then `× SetByCaller 0x14A3BB418`; armed once as a looping timer on `PeriodHandle` (+0x318) at 0x144A6A3DA; re-armed only on un-inhibition (0x144A67570, `PeriodicInhibitionPolicy` GE +0x241, authority virtual +0x688) | apply 0x144A69540; period callback `ExecutePeriodicGameplayEffect` 0x144A72A20 → body 0x144A7F6E0 |
| Time rate (duration clock): `GE_HeroAttributesBase`'s `CREffectAttributeGEComponent` (virtual +0x7D0 = 0x1450BD170, vtable 0x148B9C458) registers `Haste − 1` under `EffectLogic.TimeRateInfluencedByHaste` and `CooldownRecovery − 1` under `…ByCooldownRecovery` in the owner's `UCREffectAttributeCollection` (`AddMod` 0x145261110, `SetModValue` 0x1452691C0, sum 0x145267960); a change (0x1450C3660) broadcasts delegate 0, bound in ASC BeginPlay to **0x1452D50F0**: own-container effects matching the tag get `rate = 1 + Σ mods` (0x1452D2160) through `SetTimeRate` **0x144A6E800** (progress +0x2E0, times +0x2E4 / +0x2EC, `TimeRate` +0x2DC) → `CheckDuration` → `OnDurationChange` | no `TickRate` binding exists; effects on other actors are untouched |
| `CheckDuration` **0x144A6E990** (duration timer callback 0x144A00B70, and `SetTimeRate`): `remaining = (Duration − progress × Duration) / TimeRate`; > 0.0001 re-arms the duration timer, else at expiry runs the pending period in full when `GetTimerRemaining(PeriodHandle)` (0x144041D00: 0 while executing) ≤ 0.0001 (0x144A7F6E0 at 0x144A6EBE2), else the partial tick +0x948 with factor `(period − remaining) / period` (0x144A6EC1A; `MinimumPartialTickTime` 0, so factor 0 still executes), then clears the period timer | the repeated Bursting Ice pulse: the expiry's extra execution signals the ability |
| `UCRAbilityTask_ServerChanneling` (vtable 0x148AB50D8, thunk 0x144F1A410): `Activate` +0x2C8 = **0x14511A370** arms one looping timer at `TickInterval` (+0xB4) with `ScaledDuration` (+0xB0) ticks (0x14511A929-0x14511A9E0); callback 0x14511EA90 counts +0x114 down and broadcasts `OnTick` (+0x90) with 1.0; no Haste read after activation | |

Decoded against **26162532**, the hit roll and the grievous crit:

| What | Where |
| --- | --- |
| Chance table (slot +0x2E0 = **0x1450D97C0**): `chance[0]` = captured source CritChance (scoped modifiers included, 0x144AA1840 → 0x144AA71C0), clamped to 1 at 0x1450D9821 when `CanGrievousCrit` (preset +0x1D) is false; `chance[1]` DodgeChance, `chance[4]` ParryChance, `chance[2]` BlockChance under their preset bools; **0x1450E1170** zeroes miss under `!CanMiss`, zeroes dodge and parry for an attacker not in front, and adds 1 to `chance[0]` for a player's `AbilityType.AutoAttack` (0x14A3BAD68) from behind (dot ≤ −0.6428) | Execute 0x1450DAFB0 at 0x1450DB115 / 0x1450DB126 |
| Roll **0x14510A540**: one uniform from a hash of four `rand()`, cumulative pick over `{miss, dodge, parry, 0, block, crit, 0}` → index 0-6 else 7; spread roll `u − 0.5` | 0x1450DB13F |
| Index → `EHitType` **0x145105CC0** (jump table 0x145105FC8): 0 Miss, 1 Dodge, 2 Parry, 3 GlancingBlow, 4 Block, **5 → CriticalStrike or GrievousCriticalStrike by `CritChance > 1`** (0x145105F8B), 6 CrushingBlow, 7 Hit; adds spec tag `HitType.CriticalStrike` (0x14A3BB300) or `HitType.CriticalStrike.Grievous` (0x14A3BB308) | 0x1450DB16D |
| Gather: `A[0]` CritChance (clamped to 1 under `!CanGrievousCrit`, 0x1450D93EE), `A[1]` CritMultiplier, **`A[2]` = `Global.GrievousCritScalar`** (`CT_AbilityConstants`, 1.0; 0x1450CC4A0 reads `UCRAbilitySystemGlobals.AbilityConstantsTable` +0x388 by the ini key `GlobalGrievousCritScalerKey`) | 0x1450D939C-0x1450D9410 |
| Hit multiplier 0x145105A10: Grievous = `(A[0] − 1) × A[2] + A[1]` (0x145105A4A-0x145105A59) | `DT_EffectCalculationPresets` `HeroMagicalDirect` (effect 1391): `CanCrit` and `CanGrievousCrit` true |

Decoded against **26162532**, the spec's source tags and the attribute reads:

| What | Where |
| --- | --- |
| `FellowshipMakeEffectSpec` (exec 0x144FEC040) body **0x1450DE2A0**: ASC virtual +0x6C8 `MakeOutgoingSpec`, `FindAbilitySpecFromHandle` 0x144A0B1C0, `SetStackCount` 0x144A8EB10, the ability spec's SetByCaller map appended (0x14169BD70), then the ability virtual **+0x2E0** | `UCRGameplayAbility` vtable 0x148BD4810 (constructor 0x1450D5D80, thunk 0x144FEA5D0) |
| +0x2E0 = **0x1450D7960**: `ApplyAbilityTagsToGameplayEffectSpec` **0x1449D7DC0** (`AbilityTags` +0xB0 and the ability spec's `DynamicAbilityTags` +0x58 appended to `CapturedSourceTags.CapturedSpecTags`, `GetSpecTags` 0x144AAA400 on spec +0x60), then the ability's unreflected container +0x648 appended too, and +0x668 to 0x144A81D80 | `TagContainerAggregator` {ActorTags +0, SpecTags +0x20, ScopedTags +0x40} |
| Damage execution record **0x1451048C0** (from the engine execution params, at 0x1450DB03E): spec copy +0x70, aggregated source tags +0x330, aggregated target tags +0x350, evaluate parameters at +0x8 = {&+0x330, &+0x350} (0x145104A17); every capture (0x144AA1840 → 0x144AA71C0) evaluates with them | `FAggregatorMod::UpdateQualifies` **0x144AB4D40**: null tags → empty container 0x14A3813B8, requirements through 0x144AB1F10 |
| `UAbilitySystemBlueprintLibrary::GetFloatAttribute` exec **0x144A35BD0** (native table 0x148972110) → 0x144A0D5F0 → `GetNumericAttribute` **0x144A0FD30** → `GetNumericValue` (0x144A0C9B0 / 0x144A0CAA0): the stored current value, no tags | a UFunction's exec is the second pointer of its `FNameNativePtrPair`; the first `str` hit is the singleton table |

Decoded against **26162532**, the effect constructor defaults and the inhibition re-arm's reach:

| What | Where |
| --- | --- |
| `UGameplayEffect` constructor **0x144A62990**: `mov word ptr [rsi+0x240],1` (`bExecutePeriodicEffectOnApplication` true, `PeriodicInhibitionPolicy` +0x241 NeverReset), `StackingType` +0x9E1 None, `StackDurationRefreshPolicy` +0xA11 / `StackPeriodResetPolicy` +0xA12 both 0 (Refresh / Reset on successful application); an asset that omits a field has these | 0x144A62DBB, 0x144A62DCE, 0x144A62E0B; `UCRGameplayEffectDamage` ctor 0x1450E6540 sets none of them |
| `InhibitActiveGameplayEffect` = ASC vtable **+0x710** = **0x144A21B50**: no-op when `bIsInhibited` (+0x30C) already equals the request; un-inhibit → 0x144A67570 (re-arms the period only under `PeriodicInhibitionPolicy != 0`, 0x144A67734), inhibit → 0x144A8B390 | game-code callers: component check 0x144A7F8C0 (application 0x144A6A5C2, replication 0x144A880B9), tag handler 0x144AAF450, 0x144A120E0 |
| Period timer arming in the container apply is gated by `bSetPeriod` (`[rsp+0x41]`: 1 for a new effect, `StackPeriodResetPolicy != NeverReset` for an existing stack) | 0x144A6A1D4 → SetTimer 0x144A6A3DA |
| Every `SetTimer` on `PeriodHandle` (+0x318): application 0x144A6A3DA, the inhibition re-arm 0x144A67AEB, and the fork re-arms 0x1452D814C (in 0x1452D7F40) and 0x1452D8B98 (in 0x1452D88D0, the table below); every call of +0x950: 0x144A6A2F3 and 0x144A679B8; the partial-tick virtual +0x948 has one caller besides the duration check, 0x14533CE50 in `ACRGameStateDungeon` | the instigator's Haste change re-times every Dot/Hot period it applied through the table below |

Decoded against **26162532**, the Haste re-time of every `EffectType.Dot` / `EffectType.Hot` period a character applied
(`binary-answers-pulse-haste.md` of the rime-accuracy-v1 run):

| What | Where |
| --- | --- |
| `UCRAbilitySystemComponent::BeginPlay` 0x1452CDB90 binds **0x1452D4E60** to `OnGameplayEffectAppliedDelegateToTarget` (+0x188): a spec whose Def asset tags (GE +0xA28 / +0xA38) hold `EffectType.Dot` (0x14A3BABE0) or `EffectType.Hot` (0x14A3BABE8) has its handle added to the ASC's `TSet` at **+0x13B0** (0x144A56380 at 0x1452D4F70) and the removal callback 0x1452D5050 bound (0x14512D100 at 0x1452D4FAC) | bind at 0x1452CDC19; static tag pair 0x14A3B9648 built at 0x1452D4FC3 |
| `ACRCharacterBase` (`AbilitySystemComponent` +0x8E0) binds **0x1452EBCA0** to the `Haste` value-change delegate (0x144A0D790, static 0x145277510) at 0x1452EDB68; the callback runs **0x1452EE870** under `Role (+0x170) == 3` and `NewValue != OldValue`, which calls **0x1452EEAB0** per handle in the set | loop 0x1452EE9D0-0x1452EEA0E |
| 0x1452EEAB0: live `PeriodHandle` and `GetPeriod() > 0`; `EffectLogic.TickrateNotAffectedByHaste` (0x14A3BAC60) → no re-arm; `…Exclusively` (0x14A3BAC68) → ratio 1; else ratio = `1 / max(Haste of the character's own ASC, 1e-8)` (0x1452EEC57-0x1452EEC8D) → **0x1452D88D0** | |
| 0x1452D88D0 `(targetASC, &handle, ratio)`: `fraction = GetTimerRemaining(PeriodHandle) / GetTimerRate(PeriodHandle)` (0x144041D00 / 0x144041CE0), `newRate = Spec.Period (+0x6C, the raw asset period: the application divides a copy) × ratio × SetByCaller 0x14A3BB418`, `firstDelay = newRate × fraction`; `ClearTimer` 0x144041C00 then `SetTimer(ExecutePeriodicEffect 0x144A0A3E0, newRate, loop, firstDelay)` | `GetTimerRemaining` returns **0 for an `Executing` timer** (0x144041D20), so a Haste change inside the tick's own execution puts the next tick on the next server frame; `InternalSetTimer` 0x144041D40 takes `FirstDelay ≥ 0` as given (0x144041EEF) |
| Blueprint library: `SetGameplayEffectCustomTickPeriodScaler(handle, scaler)` exec 0x144F74720 → 0x145240900 (writes the SetByCaller through ASC virtual +0x6E8, then 0x1452EEAB0); `SetActiveEffectNextTickDelay(handle, delay)` exec 0x144F742B0 → 0x145240560 → **0x1452D7F40** (re-arms at the raw `Spec.Period` with the given first delay) | names in the `FNameNativePtrPair` table at 0x148B26B40 / 0x148B26B70 |
| The periodic body 0x144A7F6E0 calls `UAbilitySystemGlobals` (`Get()` 0x1449DC690) virtuals +0x2E8 / +0x2F8, `PushCurrentAppliedGE` / `PopCurrentAppliedGE`, around `ExecuteActiveEffectsFrom` 0x144A71BC0 (0x144A71BC0-0x144A72411, no timer access), then `OnPeriodicGameplayEffectExecuteOnSelf` (+0x1B8 broadcast, 0x144A1A460) and `…OnTarget` (+0x1D0, 0x144A1A470) | 0x144A7F745, 0x144A7F898 |

Decoded against **26162532**, the server frame, the timer manager and the ability queue
(`binary-answers-pulse-order.md` of the rime-accuracy-v1 run):

| What | Where |
| --- | --- |
| `UWorld::Tick` **0x143B785E0**: the tick-dispatch delegate broadcast (client RPCs) at 0x143B78849, then `FTickTaskManagerInterface::Get()` (0x144034DE0) `StartFrame` (+0x18) at 0x143B78EC7 and `RunTickGroup` (+0x28) PrePhysics..PostPhysics at 0x143B78EDE-0x143B78F74, latent actions 0x143B78FDA, then `GetTimerManager()` (0x1440B75E0) `.Tick` at 0x143B78FF7, then TG_PostUpdateWork from 0x143B790C8 | `World.NetDriver` +0x38, `World.TickGroup` +0x139 |
| `FTimerManager::Tick` **0x144056E90**: `InternalTime` (+0x140) += Delta at 0x144056F03; a popped timer is `Executing` (3) at 0x144057117 through its delegate, then `ExpireTime += CallCount × Rate` (0x14405736B) and `Active` (0x144057358); `LastTickedFrame` (+0x150) at 0x144057465; pending timers activated at 0x144057664 | `FTimerData` {status +1, Rate +4, ExpireTime +8} |
| `GetTimerRemaining` 0x144041D00: `Active` → `ExpireTime − InternalTime` (0x144041D24, negative for a due timer not yet popped), **`Executing` → 0** (0x144041D20), else the stored value; `GetTimerRate` 0x144041CE0 = +4; `InternalSetTimer` 0x144041D40 replaces a negative first delay by the rate (0x144041EEF) and makes the timer `Active` at `InternalTime + FirstDelay` when ticked this frame (0x144041F17-0x144041F24), else `Pending` | |
| Ability queue: `UCRGameplayAbility.ActivationQueue` +0x531 (`ECRAbilityActivationQueue` 0 Sequential, 1 Global, 3 NoQueue); `ServerTryQueueAbility` exec 0x144F17240 → **0x1452D7B80** writes the ASC slot `+0x15D8 + queue × 0xD0` {handle +0x15D8, target data +0x15E0, prediction key +0x1690, world time +0x16A0, flag +0x16A4}; `ServerTryQueueAbilityOnEvent` exec 0x144F172C0 → 0x1452D7EE0; slot clear 0x1452D7300 | ASC natives table 0x148AAEFA0 |
| The flush **0x1452D7370**: each held request is activated when activatable (0x144F14F80, `InternalServerTryActivateAbility` 0x144A20080 at 0x1452D7991), else kept **0.5 s** (0x1452D79E4, literal 0x147C11080) then dropped; runs from `UCRAbilitySystemComponent::TickComponent` (vtable 0x148AAE478 +0x3D8 = **0x1452D8220**, before `UAbilitySystemComponent::TickComponent` 0x144A25740) and from **`UCRGameplayAbility::EndAbility`** (vtable 0x148BD4810 +0x470 = **0x1450DACD0**; base 0x1449DAAB0 at 0x1450DAEDA; flush at 0x1450DAF3F) | so an ability ending inside a periodic callback (Rime's auto Anima Spike from `DoAoeExplosion`) activates a queued instant inside that callback |
| `UCRAbilityTask_ServerCasting` (class params 0x148AB1A98: `Duration` +0xB0, `CastSpeed` +0xB4): `ServerStartCasting` exec 0x144F194A0 → 0x14511F2D0; the completion is its own looping timer, `OnLoopTimerExpired` exec 0x144F19480 → 0x14511E7E0 | task-family `SetTimer` sites 0x14511AB52-0x14511B2CC |

`vscan <vtable> <disp>..` in the harness lists the slots whose function body touches a displacement.

`regfn <va>` in the harness names the class a `StaticClass` getter belongs to, from the registration table.

The harness for this build is a console project (PeImage, ReflectionScanner, OwnerScanner, EnumScanner,
ClassScanner, TypeFuncResolver copied beside a `Program.cs`, with a `PackageReference` to Iced 1.21.0): `dis`,
`fn`, `cfg` (control-flow sweep, for functions split across `.pdata` ranges), `xref` (RIP-relative and
rel32 index cached beside the exe size), `str`, `ctor` (InternalConstructor thunk → constructor chain →
vtable stores), `vtable`, `class`/`struct`/`prop`/`enum`/`recs` (reflection records with the `+0x32`
offset word), `disp`. A property record's struct offset is the `uint16` at record +0x32, except for a
bool, whose record has a different layout. The native gameplay tag behind a `.data` global is the ASCII
literal in the static initializer that `lea rcx,[global]` points at, found with `xref`.

All ten terms of the per-item body are confirmed against printed cards, and both `ItemType` gates are
confirmed by a card that carries a gated slot and scores nothing for it. When a card's arithmetic could
split two ways because two factors share a value, read the rung's `rarityAdditions` row rather than
counting the card's coloured stat lines.

**Two enums differ only in word order and only one is the instance's.** `CRHeroItemDynamicSlot.SlotType`
is `ECRHeroItemDynamicSlotType`; `CRDynamicHeroItemSlotDefinition.SlotType` is
`ECRDynamicHeroItemSlotType`, whose members are entirely different. Reading an instance byte against the
definition enum turns Rerollable into RandomStat and silently misprices every item. Resolve an ordinal
through the struct that holds it, never through the enum whose name reads closest.

## Traps this build sets

**An omitted asset field is a live C++ constructor default, not zero.** `DA_StatCalculationSettings`
has three of eight fields per block; the rest are real values: `PrimaryWeight` 0.25,
`SecondaryWeight` 0.3, `StaminaWeight` 0.5, `PrimaryMultiplier` 0.7, `WeightMultiplier` 0.15,
`StaminaMultiplierItemLevelThreshold` 315, and `SecondaryMultiplier` 0.6 on the base block, 1.5 on the
fixed block. They are in `StatRows` because no shipped file states them. `SecondaryBaseMultiplier` is
72 and `PrimaryDivisible` is 15 the same way.

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
