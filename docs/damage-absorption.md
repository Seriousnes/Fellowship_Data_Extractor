# Damage absorption

`settings.json` has `damageAbsorption`: the split of one hit between the target's absorption effects
and its Health, and the amount the combat log writes for the hit. The rule is native. Every address
is from the build 26320967 shipping executable.

- **`displayText`**: the split, in words.
- **`loggedAmountText`**: the amount, hit points and absorption of the hit's combat log line.
- **`symbols`**: the symbols of both texts.
- **`effectTag`**: `EffectType.Absorption`, the tag of an effect the hit is split over.
- **`poolMagnitude`**: `SetByCaller.System.EffectAbsorptionPool`, the set-by-caller magnitude with an
  effect's pool.
- **`consumptionScalerMagnitude`**: `SetByCaller.System.EffectAbsorptionConsumptionScaler`, the share
  of absorbed damage the pool loses.
- **`defaultConsumptionScaler`**: 1, the scaler of an effect with no such magnitude.
- **`deductionCritScaler`**: `DefaultAbsorbDeductionCritScaler` of `[/Script/fellowship.CRCharacterBase]`
  in `DefaultGame.ini`.
- **`critChanceAttribute`**: `CritChance`, read from the effect's instigator.
- **`mostPasses`**: 30.

## The damage driver

`CRDamageCalculationBase`'s execution is 0x1450DCF40. It rolls the attack table, runs the source
formula once (0x1451079A0, called at 0x1450DD1EF) and then loops:

```
taken = []; crits = {}; T = 0; A = 0
for pass in 0..29:
    D = target formula(Raw)                              # 0x145107A70, called at 0x1450DD293
    if D <= 0: break
    rest = D
    AbsorptionPass(taken, crits, &rest, &any, &ranOut)   # 0x1450E4BE0, called at 0x1450DD2ED
    if not any: T += D; break                            # 0x1450DD381
    A += D - rest; T += D - rest                         # 0x1450DD30D to 0x1450DD31E
    if not ranOut: T += rest; break                      # 0x1450DD37B
    Raw = Raw / PreBlock * rest                          # 0x1450DD326 to 0x1450DD34F
    gather the target's attributes again                 # vtable +0x2D8, 0x1450DD354
    block flat = 0; block multiplier = 1                 # 0x1450DD35C, 0x1450DD366
Total = round(T * DamageMitigationScale)                 # 0x1451078C0, called at 0x1450DD3DE
H = max(Total - A, 0)                                    # 0x1450DD3E9 to 0x1450DD3FA
the source's hooks, then the target's, each on H         # 0x1450D4920 at 0x1450DD441 and 0x1450DD475
AbsorbedDamage = A + the hooks' reductions of H          # 0x1450DD48D
event amount = -(H + A)                                  # 0x1450DD4C8, 0x1450DD6E2
Damage attribute += H                                    # 0x1450DD501 to 0x1450DD56F
```

The attribute set's `PostGameplayEffectExecute` (0x1450C45C0) reads `Damage`, sets it to 0 and calls
0x1450C4DB0, and Health becomes `clamp(Health - Damage, MinHealth, MaxHealth)` (0x1450C4E2C to
0x1450C4E47).

## The absorption pass

0x1450E4BE0 takes the hit's remaining damage by pointer.

```
if target.CurrentAbsorption <= 0: return                 # character +0xD38, 0x1450E4C7C
effects = active effects with the tag EffectType.Absorption   # tag global 0x14A3C9E88, 0x1450E4D1A
sort effects by capacity, least first                    # 0x1450D4FE0, predicate 0x1450D9480
share = rest / effects.Count                             # 0x1450E4D84 to 0x1450E4D99
for effect in effects, skipping those in taken:
    any = true
    pool = target.CachedAbsorptionValuesMap[effect]      # character +0xA88, getter 0x1452EAC00
    f = ConsumptionScaler * crit factor                  # 0x1450E4E3F, 0x1450E4E4F, 0x1450E4E5E
    if pool > f * share:                                 # 0x1450E4E6A
        pool -= f * share                                # 0x1450E4E73, setter 0x1452E4DC0
        absorbed += share; taken.add(effect)
        DAMAGE_ABSORBED line of share                    # 0x1452959F0 at 0x1450E4ECC
    else:
        pool = 0                                         # 0x1450E4F0A
        absorbed += pool / f                             # 0x1450E4F06
        DAMAGE_ABSORBED line of pool / f                 # 0x1452959F0 at 0x1450E4F38
        ranOut = true; remove the effect; break          # 0x1450E4F71 to 0x1450E4F87
rest -= absorbed                                         # 0x1450E4FCB
```

Capacity is `pool / (ConsumptionScaler * crit factor)`. The effects are in capacity order, so a pass
either ends an effect at its first step or gives every effect the share.

The crit factor is 0x1450D90F0. It is rolled once for each effect and hit and kept in a map of the
hit. The instigator is the effect context's `GetInstigator`. With `c` the instigator's `CritChance`,
the factor is `DefaultAbsorbDeductionCritScaler` (character +0xCF8) with chance `2c / (1 + c)`
(0x1450D934E to 0x1450D937C) and 1 otherwise. An effect with no instigator character has the factor 1.

The setter 0x1452E4DC0 writes the pool to the effect's `SetByCaller.System.EffectAbsorptionPool`
magnitude (tag global 0x14A3CA668) and sets `CurrentAbsorption` to the sum of the pools.

`CrAddOnAbsorbValue` (0x14521BE40) adds to a pool. Under `UseAbsorbMultipliers` the added value is
multiplied by the target's `IncomingAbsorptionMultiplier` (0x14521BFEA), the instigator's
`OutgoingAbsorptionMultiplier` (0x14521BFFA) and, for a hero, 1 plus Expertise (0x14521C02F).

## The hooks

Each character has a map of listeners for outgoing damage (+0xC78) and one for incoming damage
(+0xC90). A listener's virtual +0x60 takes the Health damage by pointer.

- **Stagger** (0x1452C85F0, `StaggerActorComponent` at character +0xF78): damage with the tag
  `EffectType.Environmental` or `EffectLogic.DamageIgnoreStagger` passes unchanged. The staggered part
  is `H * StaggerDamageAdditionFactor` (0x1452C87C9). `CurrentStagger` (+0x140) takes it up to
  `StaggerMaxHealthPercentage * MaxHealth` (0x1452C3540). H loses the part taken (0x1452C87FA). Each
  entry of `CachedStaggerContributions` writes a `DAMAGE_ABSORBED` line of the part times the entry's
  weight (0x1452C887C).
- **Damage redirect** (0x14527DD90): H loses `H * factor / count`, and the part is queued for the
  redirect's target with a `DAMAGE_ABSORBED` line (0x14527E080).
- **Death prevention** (0x14527E150): when H is above Health by more than 1, H becomes `Health - 1`.

## The combat log line

The driver calls 0x14527C080, and that calls the combat log writer 0x145296FB0 before the `Damage`
attribute is applied. For damage the writer fills the record this way:

| Field | Value | Where |
| --- | --- | --- |
| `Absorbed` | `AbsorbedDamage`, truncated | 0x145297613 |
| `Amount` | `clamp(abs(event amount) - CurrentAbsorption, 0, Health - MinHealth)`, truncated | 0x1452976FD to 0x145297732 |
| hit points | `Health - Amount`, truncated | 0x14529772D, 0x145297736 |
| absorption | `CurrentAbsorption`, read after the pass | 0x1452976E7 |
| `OverAmount` | −1 while hit points are above 0, otherwise `abs(event amount) - Absorbed - Amount` | 0x145297744 to 0x14529784A |
| `Blocked` | `BlockedDamage`, truncated | 0x145297395 |
| `RawDamage` | `RawDamage`, the source formula's result, truncated | 0x14529739D |

The record's fields are `HitType`, `Amount`, `OverAmount`, `Absorbed`, `Blocked` and `RawDamage`
(reflection records 0x148AAA9B0 to 0x148AAAAD0). The `DAMAGE_ABSORBED` record's `AmountAbsorbed` is the
damage the effect absorbed (0x145295CD1).

With absorption left after the hit, the effects absorbed the whole hit, Health is unchanged, and
`Amount` is `max(Absorbed - absorption left, 0)`. With no absorption left, `Amount` is the whole hit
and Health loses `Amount` minus the damage the effects absorbed.
