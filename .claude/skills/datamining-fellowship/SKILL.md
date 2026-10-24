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
