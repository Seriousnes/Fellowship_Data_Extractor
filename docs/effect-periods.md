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

`ApplyAscensionScaling` is read by the healing calculation alone. Its scale is the curve row
`GlobalAscensionReduceHealingScalerKey` names.
