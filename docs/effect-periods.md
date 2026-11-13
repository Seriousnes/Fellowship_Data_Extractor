# Effect periods, refreshes and partial ticks

A periodic gameplay effect ticks on a looping timer. `CRAbilitySystemComponent` sets the timer's rate, the
duration of a refreshed effect and the last tick of an expiring one. Every address is of build 26320967.

## Period

The timer's rate is `Period / Haste x TickRateOverride`.

- **`Period`**: the effect's `Period` scalable float.
- **`Haste`**: the `Haste` attribute of the effect's instigator, 1 plus the percentage. An effect with the asset tag
  `EffectLogic.TickrateNotAffectedByHaste` or `EffectLogic.TickrateNotAffectedByHasteExclusively` has no Haste term.
- **`TickRateOverride`**: the spec's SetByCaller magnitude `SetByCaller.System.TickRateOverride`, 1 when the spec
  has none. `SetGameplayEffectCustomTickPeriodScaler` writes it and `GetGameplayEffectCustomTickPeriodScaler` reads it.

A component keeps the handle of each effect it applies to a target with the asset tag `EffectType.Dot` or
`EffectType.Hot`. A change of the applier's Haste, and a write of the override, rate each kept timer again. The new
first delay is the new period times the old timer's remaining fraction, so a tick in progress keeps its progress.

## Refresh of a stack

A second application of a stacking effect assigns the new spec over the existing effect's spec and returns the existing
effect. The SetByCaller magnitudes of the old spec, `SetByCaller.System.TickRateOverride` among them, are replaced by
the new spec's.

`StackPeriodResetPolicy` `NeverReset` keeps the running period timer. `StackDurationRefreshPolicy` other than
`NeverRefresh` restarts the duration.

An effect with the asset tag `EffectLogic.CarryOverDuration` and without `EffectLogic.Proc` restarts with
`Duration + min(Duration x GameplayEffectRefreshDurationCarryOverLimit, time remaining)`.

## Applied-effect listeners

Each application, a refresh included, broadcasts the instigator's `OnGameplayEffectAppliedDelegateToTarget` with the
active effect's own spec and handle.

- A `WaitGameplayEffectAppliedToTarget` task copies that spec in its callback, and its Blueprint event receives the
  copy. A write to the active effect's spec by one listener is in the copy of every listener after it.
- Listeners run from the one registered last to the one registered first. A task registers at `ReadyForActivation`.
- A task's event does not run again for an application its own event makes.
- A write by the applying ability after its apply call runs after every listener of that application.

## Last tick

An effect with the asset tag `EffectType.Dot` or `EffectType.Hot`, and without `EffectLogic.IgnorePartialTicks`, ticks
once more when its duration ends between two ticks. The tick's `SetByCaller.PartialTickFactor` is the elapsed part of
the period divided by the period, and the damage calculation multiplies by it. An elapsed part under
`MinimumPartialTickTime` gives no tick.

## Settings

`[/Script/fellowship.CRAbilitySystemGlobals]` in `fellowship/Config/DefaultGame.ini` has
`GameplayEffectRefreshDurationCarryOverLimit` and `MinimumPartialTickTime`.

## Damage formula

`CRDamageCalculationBase` multiplies in this order, and a clear preset flag leaves its term at the default:

1. The main stat's magnitude, `DamageScale`, the hit outcome's multiplier, `DifficultyDamageScale` and
   `SpreadRoll x Spread + 1`.
2. `Expertise + 1`, under `ApplySourceExpertiseMultiplier`.
3. `SetByCaller.PartialTickFactor`.
4. The stack count.
5. `BalancingDamageAura`, under `ApplySourceDamageHealingBalancingAura`.

The product is rounded half away from zero before the target's terms.

The target's terms multiply in this order, each under its preset flag:

1. `clamp(1 - Avoidance, 0, 1)`, under `ApplyTargetAvoidance`.
2. `IncomingDamageMultiplier`, under `ApplyTargetIncomingDamageHealingMultipliers`.
3. For an effect with the asset tag `EffectType.Physical`: `clamp(1 - PhysicalDamageReduction, 0, 1)` and
   `PhysicalIncomingDamageMultiplier`. For `EffectType.Magic`: `clamp(1 - MagicDamageReduction, 0, 1)` and
   `MagicalIncomingDamageMultiplier`.
4. `AoeIncomingDamageMultiplier`, under `ApplyTargetAoeIncomingDamageMultiplier`.

An instant effect executes inside its application call, so the attributes and the hit outcome of each hit are those
of the moment the ability's graph applies it.

`ApplyAscensionScaling` is read by the healing calculation alone. Its scale is the curve row
`GlobalAscensionReduceHealingScalerKey` names.
