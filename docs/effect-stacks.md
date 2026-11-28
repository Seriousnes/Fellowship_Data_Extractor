# Effect stacks

A gameplay effect with a `StackingType` has one stack on its target. The stack has a count, one spec, one duration and
one period timer. Every address is of build 26320967.

## Fields of an effect document

| Field | Game member | Value |
| --- | --- | --- |
| `stackingType` | `StackingType` | `AggregateBySource` or `AggregateByTarget`. An effect with `None` has no field. |
| `maxStacks` | `StackCountMax` | The count's cap, 1 or more. A stacking effect with no cap has no field. |
| `stackDurationRefreshPolicy` | `StackDurationRefreshPolicy` | `RefreshOnSuccessfulApplication` or `NeverRefresh` |
| `stackPeriodResetPolicy` | `StackPeriodResetPolicy` | `ResetOnSuccessfulApplication` or `NeverReset` |
| `stackExpirationPolicy` | `StackExpirationPolicy` | `ClearEntireStack`, `RemoveSingleStackAndRefreshDuration` or `RefreshDuration` |
| `denyOverflowApplication` | `bDenyOverflowApplication` | true where the effect states it |
| `clearStackOnOverflow` | `bClearStackOnOverflow` | true where the effect states it |
| `overflowEffects` | `OverflowEffects` | the effects of the list |
| `silentStack` | `bSilentStack` of `CRGameplayEffectDamage` | true where the effect states it |
| `recaptureSourceAttributesBeforeEveryExecution` | `RecaptureSourceAttributesBeforeEveryExecution` | true where the effect states it |

The three policies are on every document with a `stackingType`. A cooked effect states a member only where it differs
from its parent class, and the document then has the value the constructor of its native class stores. An effect of the
`Instant` policy has no stack, and its document has none of the first eight fields.

## The stack

`FActiveGameplayEffectsContainer::ApplyGameplayEffectSpec` (0x144A6A660) first finds the applied spec's stack
(0x144A74BD0).

- An effect with no `StackingType` (+0x9E1), or with the `Instant` policy, has no stack. Each application is an
  active effect of its own, with its own duration and its own ticks.
- `AggregateByTarget`: the target has one stack of the effect.
- `AggregateBySource`: the target has one stack for each instigator ability system component (0x144A74DC7 to
  0x144A74E0C).

An application on a stack:

1. The overflow routine (0x144A7C3F0) handles an application on a stack at its cap first. The routine applies each of
   `OverflowEffects` (+0x2B0). With `bDenyOverflowApplication` (+0x2C0) the application ends there, and with
   `bClearStackOnOverflow` (+0x2C1) the stack is removed too.
2. The count is `min(existing + applied, StackCountMax)` (0x144A6A78F to 0x144A6A7AD). A `StackCountMax` of 0 or less
   is no cap.
3. The new spec is assigned over the stack's spec (0x144A6A93B). The SetByCaller magnitudes and the captured
   attributes of the new application are those of every stack from then on.
4. `StackDurationRefreshPolicy` (+0xA11) other than `NeverRefresh` restarts the duration (0x144A6A99D).
   `effect-periods.md` has the duration of the restart.
5. `StackPeriodResetPolicy` (+0xA12) other than `NeverReset` sets the period timer again (0x144A6A9A9, 0x144A6B2F4 to
   0x144A6B518). The next tick is one full period after the application. An effect with
   `bExecutePeriodicEffectOnApplication` (+0x240) also ticks on the next frame.

The spec's `StackCountMax` (+0x58) is the effect's `StackCountMax` scalable float (+0x9E8) rounded at the spec's level
(0x144A8F520 to 0x144A8F550). No routine reads `StackLimitCount` (+0x9E4).

`StackExpirationPolicy` (+0xA13) is read at the end of the duration (0x144A6FC35).

| Policy | At the end of the duration |
| --- | --- |
| `ClearEntireStack` | The effect is removed. |
| `RemoveSingleStackAndRefreshDuration` | One stack is removed and the duration restarts. The effect is removed at a count of 1. |
| `RefreshDuration` | The duration restarts with every stack. |

### Constructor values

`UGameplayEffect`'s constructor (0x144A63AB0) stores 0 at `StackingType` (0x144A63EEE), at
`RecaptureSourceAttributesBeforeEveryExecution` (+0xA10, 0x144A63E60), and at `StackDurationRefreshPolicy` and
`StackPeriodResetPolicy` as one word (0x144A63F2B). It has no store at +0xA13, +0x2C0 or +0x2C1, and the allocation is
zero filled. Each of the four enums has its first member at 0: `None`, `RefreshOnSuccessfulApplication`,
`ResetOnSuccessfulApplication`, `ClearEntireStack`.

`CRGameplayEffectDamage`'s constructor (0x1450E84D0) stores none of those members. It sets bits 1 and 3 of the byte at
+0xA98. `bSilentStack` is bit 0 of that byte (setter 0x144FF4170).

`CRChargeEffect`'s constructor (0x1450E71E0) is the one other routine of the executable with a store at +0x9E1 or
+0xA13. A charge effect stating no stack member has its values:

| Member | Value | Where |
| --- | --- | --- |
| `StackingType` | `AggregateByTarget` | 0x1450E749D |
| `StackDurationRefreshPolicy`, `StackPeriodResetPolicy` | `NeverRefresh`, `NeverReset` | 0x1450E74D3 |
| `StackExpirationPolicy` | `RemoveSingleStackAndRefreshDuration` | 0x1450E74DC |
| `StackCountMax` | 0 | 0x1450E74CA |

## The stack count of a hit

The damage calculation's gather reads the spec's `StackCount` (+0x1C0) through `GetStackCount` (0x144A7BDD0, called
at 0x1450DB595). For a `CRGameplayEffectDamage` with `bSilentStack` it takes `min(count, 1)` (0x1450DB5BE to
0x1450DB5D1). The source formula multiplies the hit by the result (0x145107A48). `effect-periods.md` has the formula.

The healing driver (0x1450DE250) reads the same count (0x1450DE6AA) and the healing formula multiplies by it
(0x145107770, `cvtsi2ss` 0x14510785D, `mulss` 0x145107874). The healing path has no `bSilentStack` test.

A tick of a stack of count `n` is therefore `n` times the tick of one application, with the SetByCaller magnitudes and
the captured attributes of the latest application.

## The source attributes of a tick

A source capture with `bSnapshot` has the source's attribute value of the spec's creation. An application on a stack
replaces the spec, and the values with it. Every source definition of the damage and healing calculation classes has
`bSnapshot`, and every target definition is live: `attribute-captures.md` has the definitions, and `settings.json` has
them in `relevantAttributesToCapture`.

An effect with `RecaptureSourceAttributesBeforeEveryExecution` captures the source's attributes again before each
execution: a periodic tick (0x144A808C4) and the execution of an instant application (0x1449FD735) clear the spec's
source capture flag and call the capture routine (0x144A6F7D0). The effect needs the instigator's component for it.

## Log lines

An effect with `logsEffectEvents` has an `EFFECT_REFRESHED` line for an application on its stack.
`combat-log-ids.md` has the logger. Fellowship Logs has an application with a new count as `applydebuffstack` or
`applybuffstack`, with the count in `stack`, and an application on a stack at its cap as `refreshdebuff` or
`refreshbuff`.

## `FellowshipMakeLooseEffectSpec`

`CRBlueprintLibrary::FellowshipMakeLooseEffectSpec(Source, EffectClass, Context, Ability, VisualDelay,
ScaleVisualDelay, Level, StackCount)` (exec thunk 0x144F66590, native 0x14522E110) makes a spec outside the ability
calling it.

- The spec is made by `Source`'s ability system component (`MakeOutgoingSpec`, vtable +0x6C8, called at 0x14522E22E)
  with `Context` and the level `Level`.
- `SetStackCount` (0x144A8FC60, called at 0x14522E249) writes `StackCount`.
- A `VisualDelay` above 0 goes on the effect context (0x14522CFC0). With `ScaleVisualDelay` and an `Ability` it is
  divided first by the ability's time factor for `Source`'s component (0x1450E0910, flags at ability +0x3B8).
- With an `Ability`, the spec has the ability's tags and the SetByCaller magnitudes of the ability's own spec (vtable
  +0x2E0 of the ability, called at 0x14522E4A1).

## Entropic Burst

Aeona's talent 592 has effect 2819, `GE_Lisa_Talent_SingleTargetDot_DotAoe_Dot`, and ability 1957,
`GA_Lisa_Passive_Talent_SingleTargetDot_DotAoe`.

### The eruption

Entropy's Claim's effect 2603 grants ability 1957 to its target through an `AbilitiesGameplayEffectComponent` with the
removal policy `CancelAbilityImmediately`. The ability's `K2_ActivateAbility` stores the Claim's source, ability
instance and effect context for a source with the tag `Talents.Lisa.SingleTargetDot.DotAoe`, and cancels the ability
for any other source. Its `K2_OnEndAbility` erupts. The ability ends with the Claim's effect, so each removal of a Claim
erupts once: the end of its duration, the death of its target, and any other removal.

Effect 2603 has no `StackingType`. A second Claim on one enemy is a second active effect with its own ticks and its own
eruption. With Mass Entropy, legendary power 620, Entropy's Claim has 2 charges.

The eruption, in the order of the graph:

1. `AcquireTargets_CylinderAoe` for the Claim's source: origin 200 below the feet of the Claim's target, axis up,
   height 700, radius `Lisa.SingleTargetDot.Talent.DotAoe.Aoe.Radius` (800), the filters of
   `CreateAoeTargetFilterCollection(AliveCheck 1, InCombatCheck 2, Relationship 3)`. By ordinal those are
   `MustBeTrue` and `Ignore` of `ECRTargetFilter_BoolState` and `NotFriendly` of `ECRTargetFilter_FactionRelation`.
   The Claim's target is among the targets while it is alive. The query
   (0x1450FAD20, list loop 0x145534620) has one pass over a flat list of registered objects.
2. No target: the eruption ends.
3. `FellowshipMakeLooseEffectSpec(source, effect 2819, the Claim's effect context, the Claim's ability instance, 0.2,
   true, 1, 1)`: level 1, stack count 1.
4. `CalculateAndSetMultiTargetDamageFalloffScaler` (0x145218380) writes `SetByCaller.DamageScaleTargetCount` on the
   spec: `sqrt(T / N)` for `N > T`, otherwise 1. `T` is `Lisa.SingleTargetDot.Talent.DotAoe.Aoe.TargetCountDamageThreshold`
   (5) and `N` is the actor count of the target data.
5. `K2_ApplyGameplayEffectSpecToTarget` applies the spec to each target.

The ability's `Damage Effect` property names effect 2732. No statement of the graph applies it.

### The eruption at a target's death

Four conditions of the graph stop an eruption, each before step 3:

| Offset | Condition |
| --- | --- |
| 10 | `Has Talent` is false. The activation sets it from the tag `Talents.Lisa.SingleTargetDot.DotAoe` of the Claim's source. |
| 73 | `IsValid` of the ability's owning character, the Claim's target, is false. |
| 116 | `IsValid` of `Source Character`, the Claim's source, is false. |
| 630 | The target list of step 1 is empty. |

The route has no test of the source's alive state, of the ability's activation, of authority or of a tag.

At the death of the Claim's target the query of step 1 leaves the target out, and an eruption with no other target
ends at offset 630:

- The death routine of a character (0x1452ED190, authority alone) writes the dead flag (+0x92D, 0x1452ED233) and
  calls virtual +0xAB8 (0x1452ED25A). For `CRAICharacter` that is 0x145157A20, and its callee 0x1452F0BD0 writes bit
  0 of the character's flags in the targeting registry (0x145534A50) from virtual +0xA10, the dead flag equal to 0
  (0x144F3AC00).
- The routine removes the effects with `EffectLogic.RemoveOnDeath` after that (0x1452ED326, tag global
  0x14A3C9EE0). Effect 2603 has the tag, and its removal ends the ability.
- The filter context of a query (0x1450C9260) has a state of `MustBeTrue` in the first state byte as required bit 0
  (0x1450C941A). The list walk (0x145534620) leaves out an entry without the required bits (0x1455346E0).

An eruption at the target's death therefore has the alive enemies within the radius of the dead target as its
targets, and `N` is their count.

### Effect 2819

| Member | Value |
| --- | --- |
| Native class | `CRGameplayEffectDamage` |
| `EffectCalculationPreset` | `HeroMagicalOT` |
| `DurationPolicy` | `HasDuration`, `Lisa.SingleTargetDot.Talent.DotAoe.Dot.Duration` (9) |
| `Period` | `Lisa.SingleTargetDot.Talent.DotAoe.Dot.TickInterval` (1.5), divided by the instigator's Haste |
| `bExecutePeriodicEffectOnApplication` | false |
| Execution | `CRIntellectDamageCalculation` |
| Modifier 1 | source `Intellect` with `bSnapshot`, `Multiplicitive`, `Lisa.SingleTargetDot.Talent.DotAoe.Dot.IntellectCoefficient` (0.41) |
| Modifier 2 | source `DamageScale` with `bSnapshot`, `MultiplyCompound`, SetByCaller `SetByCaller.DamageScaleTargetCount` |
| `StackingType` | `AggregateBySource` |
| `StackCountMax` | 10 |
| Stack policies | none stated: `RefreshOnSuccessfulApplication`, `ResetOnSuccessfulApplication`, `ClearEntireStack` |
| Asset tags | `EffectType.Harmful`, `EffectType.Magic.Arcane`, `EffectType.GeneratesThreat`, `EffectType.Dot`, `EffectLogic.RemoveOnDeath`, `EffectVisualization.Icon`, `CustomAssetTags.Lisa.AlwaysGoToHeal` |

Neither modifier has a source or target tag requirement. The effect states neither `bSilentStack` nor
`RecaptureSourceAttributesBeforeEveryExecution`.

### The tick

`tick = Intellect x 0.41 x DamageScale x sqrt(5 / N) x outcome x spread x (Expertise + 1) x PartialTickFactor x stacks
x BalancingDamageAura`, rounded, then the target terms of `HeroMagicalOT`.

- **`stacks`**: 1 after the first eruption on a target, and 1 more for each later eruption of the same Aeona on it, up
  to 10.
- **`Intellect`, `DamageScale`, `Expertise`, `BalancingDamageAura`, `N`**: those of the latest eruption on the target.
  Each attribute is a source capture with `bSnapshot`, made at the spec's creation in step 3, and the effect has no
  `RecaptureSourceAttributesBeforeEveryExecution`.
- **`outcome`**: rolled at each tick, with the `CritChance` and the `CritMultiplier` of the latest eruption. Both are
  source captures with `bSnapshot`.
- **Target terms**: the target's attributes of the tick. Each is a live target capture.
- **Duration**: 9 seconds from the latest eruption, with no Haste term and no time carried over. The whole stack ends
  together, with the partial last tick of an `EffectType.Dot` effect.
- **Period timer**: set again by each eruption. The first tick after an eruption is one hasted period after it.
- An eruption on a stack of 10 replaces the spec and restarts the duration and the period timer, and the count stays
  at 10.

The rule has no term for the stack's age, for a resource, for a stored amount or for another effect on the target.

### The rule against combat logs

Seven Fellowship Logs fights with an Aeona have 56,302 ticks of 1002819. A target's count is 1 at
`applydebuff` and the `stack` of each later `applydebuffstack`. `N` is the count of the 1002819 application lines
within 5 ms of the target's latest one.

| Reading | Result |
| --- | --- |
| Normal ticks at a count of 2 to 10, amount over `sqrt(5 / N)`, over the median at a count of 1 (report c6ga1PKVfJNb2yHA, fight 1) | 1.99, 2.90, 3.80, 4.44, 5.39, 6.57, 7.34, 8.05, 9.67 |
| Targets of one eruption in one tick batch, amount ratio over count ratio (same fight, 172 batches) | medians 0.96 to 1.07 |
| Claim removals at the end of the duration with an eruption within 5 ms | 625 of 625 |
| Claim removals at the target's death with an eruption | 42 of 49 |
| First tick after an `applydebuffstack` line (same fight) | median 776 ms, 10th percentile 736 ms |
| Removal after the latest application line, target alive | median 9016 to 9021 ms in each fight |
| Highest count in each fight | 10, 9, 3, 5, 10, 9, 3 |

Twelve fights with an Aeona have 90 removals of 1002603 within 250 ms of the target's `death` line. An enemy's
position is the `x` and `y` of its nearest log line, and an enemy is alive from its first line to its `death` line.

| Reading | Result |
| --- | --- |
| Removals with an eruption within 60 ms | 78 of 90 |
| Removals with no eruption, and no other enemy alive within 800 of the dead target | 12 of 12 |
| Eruptions with the dead target among their targets, removal 0 to 11 ms after the `death` line | 0 of 76 |
| Eruptions with the dead target among their targets, removal 103 and 133 ms before the `death` line | 2 of 2 |
| Eruptions at a death with a target count equal to the alive enemies within 800 | 75 of 76 |
| Other removals of 1002603 with an eruption | 958 of 958 |
