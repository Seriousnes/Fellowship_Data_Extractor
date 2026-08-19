# Placeholders backed by a multiplicative modifier

## The transform

A `{Token}` whose number is the magnitude of a multiplicative modifier holds the multiplier, and the
card prints the size of the change. An `IncomingDamageScaler` of 0.85 prints as `15%` in a sentence
that already says "reduced by".

`Reductions.Prints` marks such a placeholder with `Format = "reduction"` when all of these hold:

- the placeholder carries a `Value`, a `Percent` flag and no `Coefficient`
- no other `Format` is already set
- the value is below 1
- the placeholder already carries `Multiplies`, or the entity declares a modifier whose op
  multiplies and whose magnitude is that same value

The second half of that last clause is what reaches a token that resolves through its own
`Magnitude.ScalableFloatMagnitude`, which returns before the effect route that would have set
`Multiplies`.

## Which ops multiply

`EGameplayModOp` in this build's mappings runs `Additive` 0, `Multiplicitive` 1, `Division` 2,
`Override` 3, `MultiplyCompound` 4, `AddFinal` 5, `Max` 6. Two members multiply and the export uses
both: 255 modifiers on `Multiplicitive` and 245 on `MultiplyCompound`.

`GameplayEffects.Multiplies` is the one test, and it resolves both member names through
`EnumOrdinals` rather than comparing against a literal ordinal. Both callers use it:
`GameplayEffects.Multiplied`, which scopes an effect's curve bindings to the modifier that owns
them, and `DescriptionTokens.Multiplied`, which reads a token's own `ModifierOp`. Testing against
the ordinal 1 alone reached the 255 `Multiplicitive` modifiers and none of the 245 on
`MultiplyCompound`, which left the `Multiplies` flag on 14 placeholders where it belongs on 24.

`Reductions.MultiplyingOps` validates the same two names against the mappings once per run and
throws if either stops being declared, rather than letting a rename silently print raw multipliers.

`PlaceholderService` and `CodexViewService` then print `1 - value` through the `UnitPercent` format.
The vocabulary matches `RankRuns.Apply`, which already distinguishes `reduction` = `(1 - value)`
from `delta` = `(value - 1)`.

## What settles the transform

`PerkConstants` states it outright. The scaler and the designer's comment sit on the same row:

| Row | Value | Comment |
| --- | --- | --- |
| `Perk.InstantHealDamageReduction.XSmall.IncomingDamageScaler` | 0.94 | `6% dr` |
| `Perk.InstantHealDamageReduction.Small.IncomingDamageScaler` | 0.9 | `10% dr` |
| `Perk.InstantHealDamageReduction.Medium.IncomingDamageScaler` | 0.88 | `12% dr` |
| `Perk.InstantHealDamageReduction.Big.IncomingDamageScaler` | 0.85 | `15% dr` |

Ability 996, Luminous Barrier, says it a second way: its own description carries the literal text
"reducing all damage taken by 15%" for the effect whose scaler is 0.85.

## The five entities it applies to

| Effect | Tag | Scaler | Printed |
| --- | --- | --- | --- |
| 1512 | `GE_Meiko_Talent_DamageSoakBuff_IncreasedIncomingHealing_Buff` | 0.9 | 10% |
| 2650 | `GE_Sune_Talent_ReducedIncomingMagicDamage_Buff` | 0.7 | 30% |
| 2838 | `GE_Vigor_SingleTargetAbsorb_UponRemovalBuff` | 0.85 | 15% |
| 3259 | `GE_Lisa_Talent_Passive_OverhealToStackingDamageReduction_DamageReductionBuff` | 0.88 | 12% |
| 3027 | `GE_MC_BossGuardian_Rune_IconTickingDamage` | 0.5 on `MoveSpeed` | 50% |

3259's 0.88 is `Perk.InstantHealDamageReduction.Medium`'s value, so the comment table names its
printed 12% directly. 3027 prints the same 50% under either reading, since its multiplier is a half.

## Multipliers above 1

Two placeholders hold a multiplier above 1 and carry no `Multiplies` flag, because their token
resolves through its own magnitude rather than through the effect. They print the raw multiplier and
are left as they stand, since no shipped text or comment says which reading the card shows:

| Tag | Value | Description |
| --- | --- | --- |
| `GE_Lisa_Talent_ResourceThresholdBuffs_DamageIncrease` | 1.15 on `DamageScale` | "{Damage} increased damage all none-spender abilities." |
| `GE_DungeonModifier_AnomalousOrbs_BlockAllSkills_SelfDebuff` | 4 on `IncomingDamageMultiplier` | "Taking {IncomingDmgIncrease} damage." |

The dungeon modifier's sentence names no direction, so 300% and 400% are both readable from it.
