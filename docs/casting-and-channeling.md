# Casting and channeling tasks

`CRAbilityTask_ServerCasting` is an ability's cast on the server and `CRAbilityTask_ServerChanneling` is its channel.
Both are native. Every address is of the build 26320967 shipping executable.

## Export fields

An ability document has:

- **`castTime`**, **`channelTime`**, **`channelTick`**: `CastingDuration`, `ChannelingDuration` and
  `ChannelingTickInterval`.
- **`castTimeScalesWithHaste`**, **`scaleTimeWithAttackSpeed`**: `ScaleTimeWithHaste` and `ScaleTimeWithAttackSpeed`,
  the two flags of the time rate.
- **`channelingData`**: `continuousChannel`, `tickImmediately`, `scaleDurationWithAbilityTimeRate`,
  `enablePartialTicks` and `allowSpellPushback` of the ability's `ChannelingData`. Each flag is from the nearest class
  of the ability's Blueprint chain with it.
- **`abilityTags`**, **`activationBlockedTags`**: `AbilityTags` and `ActivationBlockedTags`.

An effect document has:

- **`grantedTags`**: the combined granted tags of the effect's `TargetTagsGameplayEffectComponent`.
- **`executions`**: the calculation class of each of the effect's `Executions`.

A stun cancels an ability when an effect of the stun's `applies` has
`CRCancelAbilitiesBlockedByThisEffectsParentExecution` in its `executions`, and the ability's `abilityTags` and
`activationBlockedTags` pass the tests of [Cancel by a stun](#cancel-by-a-stun) against the stun's `grantedTags`.

## Ability time rate

Both tasks read the ability's time rate once, in their factory call. The rate (0x1450E06E0) is:

- the owner's `Haste` attribute under the ability's `ScaleTimeWithHaste`,
- plus the owner's `AttackSpeed` minus 1 under `ScaleTimeWithAttackSpeed`,
- 1 with neither flag.

A change of either attribute after the factory call changes no time of a running cast or channel.

## Cast

`ServerStartCasting(OwningAbility, CastingData, FocusTargetDataHandle)` is 0x145121260.

- **`Duration`** = `ModifyCastingDuration(CastingData.CastingDuration)`, with no rate term. `ModifyCastingDuration` is a
  Blueprint native event of `CRGameplayAbility`, and its native body returns its input (0x1450E30F0). Nine hero
  abilities have a Blueprint body: Firemage `CastedDotBoostProjectile`, `CastedPlacedAoe` and `CastedSingleHeavyDot`,
  Lisa `CastedHeal` and `CastedSingleTargetProjectile`, Rime `CastedProjectileDamageOrbSpender`, Sune
  `TargetedCastedHealOrDamage`, Vigor `CastedSingleTargetDamage` and `CastedSingleTargetHeal`.
- **`CastSpeed`** = `clamp(rate, 0, 999)`.

`Activate` (0x14511BE30) runs these steps:

1. It broadcasts `OnActivate`.
2. With a `Duration` of 0 or less it plays `CastEndAnimMontage`, ends the task and broadcasts `OnFinished`.
3. Otherwise it applies a `CRCastingGameplayEffect` of duration `Duration / CastSpeed` to the caster (`SetDuration`
   at 0x14511C077). The effect is the cast's timer.
4. It binds the effect's removal to 0x14511EFC0 and plays `CastingAnimMontage` at the rate `CastSpeed`.
5. It starts `PostLoopMontagePlayDelayTask`, a `WaitDelayCR` the factory made, and binds its end to
   `OnLoopTimerExpired` (0x145120770).

The factory gives the delay task the time `clamp(Duration - T, 0, Duration) / CastSpeed` (0x1451214A4 to 0x1451214B1).
`T` is the trigger time of the first `AnimNotify_CastEndMarker` among the notifies of `CastEndAnimMontage`. A montage
with no marker has the time `Duration / CastSpeed`. `OnLoopTimerExpired` plays `CastEndAnimMontage` at the rate
`CastSpeed`, so the marker is at the cast's end.

The effect's expiry calls 0x14511EFC0. A removal before the expiry returns at once. An expiry runs, in one call:

1. `OnLoopTimerExpired`, for a montage the delay task has not yet played.
2. `EndTask`.
3. The `OnFinished` broadcast.

`OnFinished` is therefore at `Duration / CastSpeed` after `Activate`, with no wait on the montage. The montage's start
is `T / CastSpeed` before `OnFinished`.

## Channel

`ServerStartChanneling(OwningAbility, ChannelingData, FocusTargetDataHandle)` is 0x145121540. With
`rate = clamp(rate, 0.0001, 999)`:

- **`ScaledDuration`** = `ModifyChannelingDuration(ChannelingData.ChannelingDuration)`, divided by `rate` under
  `ScaleDurationWithAbilityTimeRate`. `ContinuousChannel` has 0. No Blueprint has a body for
  `ModifyChannelingDuration`.
- **`TickInterval`** = `ChannelingData.ChannelingTickInterval / rate`, under every state of the flags.

`Activate` (0x14511C300) applies a `CRChannelingGameplayEffect` of duration `ScaledDuration` to the caster, or a
`CRInfiniteChannelingGameplayEffect` under `ContinuousChannel`. It then counts the ticks (0x14511C8CB to 0x14511C935):

```
q = floor(ScaledDuration / TickInterval * 100000 + 0.5) / 100000
N = trunc(q), plus 1 under TickImmediately
partial = q - floor(q), under EnablePartialTicks
```

It sets a looping timer of the period `TickInterval` (0x14511CAE2). The first tick is one period after `Activate`, and
0.01 seconds after it under `TickImmediately`. Each tick of the timer (0x145120A20) lowers the count of ticks remaining
and broadcasts `OnTick` with a `PartialTickFactor` of 1 while that count is 0 or more. A `ContinuousChannel` has no
count.

The effect's expiry calls 0x14511F270. A removal before the expiry returns at once. An expiry runs, in one call:

1. `OnTick` with the factor 1, once for each tick remaining.
2. `OnTick` with the factor `partial`, under `EnablePartialTicks` and for a `partial` above 0.00000001.
3. `OnLoopTimerExpired` (0x145120820), for a `ChannelEndAnimMontage` not yet played.
4. `EndTask`.
5. The `OnFinished` broadcast.

A channel therefore has exactly `N` ticks of the factor 1. With a `ScaledDuration` of a whole number of intervals, the
tick of the last period is at the channel's end, from its own timer or from the expiry, and before `OnFinished`. With
such a duration under `TickImmediately`, the timer broadcasts `N - 1` ticks and the expiry broadcasts tick `N`.

| `ScaleDurationWithAbilityTimeRate` | Duration | Ticks |
| --- | --- | --- |
| true | `ChannelingDuration / rate` | `ChannelingDuration / ChannelingTickInterval`, the same at every rate |
| false | `ChannelingDuration` | `ChannelingDuration * rate / ChannelingTickInterval` |

`EstimateChannelValues` (0x14511E000) returns the same duration, interval and tick count for a character, an ability
class and a `ChannelingData`, and adds `partial` to the count under `EnablePartialTicks`.

The factory gives the channel's `PostLoopMontagePlayDelayTask` the time
`clamp(ScaledDuration - T / rate, 0, ScaledDuration)` (0x145121809 to 0x145121823).

### Constructor defaults of `CRChannelingTaskData`

| Field | Default |
| --- | --- |
| `ContinuousChannel` | false |
| `ShowOverheadBar` | true |
| `TickImmediately` | false |
| `Interruptible` | true |
| `NeverShowInterruptOnCastBar` | false |
| `PredictPlayedOnClient` | false |
| `ScaleDurationWithAbilityTimeRate` | true |
| `ScaleAnimationSpeedWithAbilityTimeRate` | true |
| `EnablePartialTicks` | false |
| `AllowSpellPushback` | false |

`CRCastingTaskData` has `ShowOverhead` true, `Interruptible` true and `AllowSpellPushback` false. The exec thunks
0x144F1C070 and 0x144F1A680 store each default before they read the argument.

### Tags on the cast and channel effect

| Flag state | Tag on the effect's spec |
| --- | --- |
| `ShowOverheadBar`, `ShowOverhead` | `EffectVisualization.OverheadStatus` |
| `Interruptible` false | `EffectLogic.UninterruptibleCast` |
| `NeverShowInterruptOnCastBar` | `EffectLogic.NeverShowInterruptOnCastBar` |
| `AllowSpellPushback` false | `EffectLogic.Dynamic.NoSpellPushback` |

### Spell pushback

Under `AllowSpellPushback` the task binds the effect's time change to 0x14511F030, and to 0x14511EE30 for a cast. The
channel's handler counts the ticks again from `(new start time - start time + ScaledDuration) / TickInterval` and adds
the difference to the ticks remaining. Both handlers end the running delay task and start one with the changed time.

## End of a task before its expiry

`OnDestroy` of the channel task (0x1451202F0) unbinds the removal handler, removes the channeling effect, clears the
tick timer and ends the delay task. The cast task's `OnDestroy` (0x145120190) does the same for its effect. A task
destroyed before the expiry therefore broadcasts no remaining tick, no partial tick and no `OnFinished`.

## Cancel by a stun

A stun cancels a channel through an effect the stun applies, and the rule is each effect's own.

1. A stun effect such as `GE_Gunde_AoeDamageAndStun_Stun` grants `CharacterStatus.BlockAllSkills`,
   `CharacterStatus.IgnoreActionInput`, `CharacterStatus.IgnoreMoveInput` and `CharacterStatus.IgnoreRotationInput`.
   Its `AdditionalEffectsGameplayEffectComponent.OnApplicationGameplayEffects` has
   `GE_Default_CancelAllAbilitiesBlockedByParentEffect`.
2. `CRAbilitySystemComponent`'s `ApplyGameplayEffectSpecToSelf` writes the applied effect's definition to the
   context's `RootGEDef` (+0x98) when the definition has that component and the context has no `RootGEDef`
   (0x1452D128A).
3. `GE_Default_CancelAllAbilitiesBlockedByParentEffect` is instant and has the execution
   `CRCancelAbilitiesBlockedByThisEffectsParentExecution` (0x1451085F0). It runs inside the stun's application call
   and reads the granted tags of `RootGEDef` (definition +0xA48).
4. The component (0x1452D21A0) cancels each active `CRGameplayAbility` of the target with all of:
   - `AbilityTags` with `AbilityType.Skill`, `Ability.Skill` or a child of either,
   - `AbilityTags` without `AbilityLogic.AtomicExecution`,
   - `ActivationBlockedTags` (+0x248) with one of the granted tags, or with a child of one.

   A `RootGEDef` with no granted tag leaves the third test out. The ability instance of the stun's own context is
   never cancelled.
5. The cancel (0x1452D1F50) is the engine's `CancelAbility` (0x1449DA700), and so `EndAbility` with `bWasCancelled`.
   For an ability already committed in this activation it then calls `ApplyCooldown`. `ApplyCooldown` returns at once
   for a cooldown already applied in the activation.
6. `EndAbility` (0x1450DCC60) destroys the ability's tasks. The channel task's `OnDestroy` is the section above, so
   a tick due at the stun's own instant is broadcast only when its timer ran before the stun's application.

`EndAbility` also removes the global cooldown effect of a cancelled ability not yet committed, and calls
`ApplyCooldown` for a committed ability with the `CooldownCommitmentPolicy` `CCP_OnAbilityEnd`.

Self-stuns of some creatures, `GE_CM_TrollBoss_Charge_Stun` among them, grant the four tags and apply no cancel effect.
`ChannelingData.Interruptible` is no part of this path.
