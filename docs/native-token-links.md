# Native token links

## What the table is

`NativeTokenRows` names the tuning row behind a `{Token}` whose number native code works out. It
holds a link, never a value: the row name is stated, and `ConstantsTable` reads the number back out
of the shipped constants like any other row. A rebalance that moves the number moves the export
with it.

`EntityResolver.Body` consults it after `ResolveDynamicTextFloat` has been tried and before the
coefficient search. It applies only to a token that already carries `FromOwnConstants`, which is
the extractor's record that the token names its own ability rather than a gameplay effect. The
`StatedByNativeLink` counter is printed beside the blueprint-bound and name-bound token counts, so
a run states how many numbers arrived this way.

## When a link is warranted

All three hold:

- The token names its own ability, so there is no effect to read a magnitude off.
- The ability declares no `ResolveDynamicTextFloat` or `ResolveDynamicTextFloatRange`, so the
  bytecode route reaches nothing.
- The number is in the constants under a namespace no property on the entity points at.

The placeholder then falls through every route and renders as `[worked out in game]`.

## The entries

### `GA_Vigor_SingleTargetAbsorb` / `LuminousDmgRed`

Row: `Vigor.Absorbs.DamageReduction.BaseValue` = 0.5.

Luminous Barrier's card reads "Damage absorbed by Luminous Barrier is reduced by 50%." The token
carries `ShowPercent` and no attribute, so the row's 0.5 prints as `50%` through the
`UnitPercent` format.

Three independent readings agree on 0.5:

- `Vigor.Absorbs.DamageReduction.BaseValue`, comment `was 0.2 then 0.65`.
- `GE_Vigor_Shared_Absorb_DamageReduction` (effect 2644) carries a modifier
  `IncomingDamageMultiplier MultiplyCompound 0.5`, applied by the passive
  `GA_Vigor_Passive_AbsorbMonitor` (ability 1906) rather than by the barrier itself.
- The number the game prints.

`Vigor.Absorbs.DamageReduction.TotalIncomingDamageMultiplierLimit` is also 0.5 and is the wrong
row: its comment `was 0.035` is a cap's history, and it bounds stacked reduction rather than
naming the printed magnitude.

The barrier's own namespace, `Vigor.SingleTargetAbsorb.*`, holds duration, cooldown, range, cost,
the absorb coefficient, the baseline charge count and two talent rows. It does not hold the
reduction, and no property the extractor reads on `GA_Vigor_SingleTargetAbsorb` points at
`Vigor.Absorbs.*` or at the shared effect: the barrier and the monitor are joined inside the
shipping binary.
