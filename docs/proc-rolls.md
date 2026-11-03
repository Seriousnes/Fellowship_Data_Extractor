# Proc-per-minute rolls

Ability, relic passive, talent, legendary power, gem ability, dungeon affix, relic power, weapon
power, sigil, aspect, enchant and protective enchant documents carry `procRolls`: one entry for each `CRRandomStreamComponent.RollProcPerMinute`
call in the entity's Blueprint or in the Blueprints of the abilities it grants.

Each entry has the following fields:

- **`row`**: the tuning row the rate is read from.
- **`procsPerMinute`**: the row's value.
- **`formula`**: the rate the roll is made at. `PPM` is the row's value and every other name is an
  attribute of `scalesOff`. A percentage attribute enters as its fraction, so `PPM × (1 + CritChance)`
  at 25% Crit Chance is 1.25 times the row.
- **`scalesOff`**: the attributes the formula reads, with `Haste` last where the roll is hasted.
- **`scalesWithHaste`**: true when the component multiplies the rate by the hero's Haste attribute,
  and the formula ends in `× (1 + Haste)`.
- **`talent`**: the talent or legendary power holding the tag the Blueprint tests before the roll.
  A roll with none is baseline. Detonate rolls its charge recovery only behind
  `Talents.Firemage.PlacedProjectileAoeHasCharges`, so its roll names Reign of Fire, and Reign of
  Fire's own `procRolls` carries the roll. A roll behind a tag nothing holds is left out.

Dragonbone Dice (relic passive 778) rolls `ArmorSetBonus.PowerBonusOnCrit.PPM × (1 + CritChance)` on
each critical strike, with Haste off. The Blueprint reads the hero's `CritChance`, adds 1, multiplies
the row by the sum and passes `ScaleWithHaste` as false. The description's "0.9 PPM" is a string in
`CA_RelicPassive_PrimaryStatOnCrit`; the row is 1.

## The call

`RollProcPerMinute(ProcTag, RealProcsPerMinute, ScaleWithHaste)` is native. Build 26320967 has 25
calls in 23 Blueprints: 21 pass `ScaleWithHaste` as true, and the four armor set bonus and relic
passive Blueprints pass false. 23 pass a row read straight through `GetScalableFloatValue`,
`GetScalableFloatWithAbilityLevel` or `GetScalableFloatValueAtLevel`; the two `PowerBonusOnCrit`
Blueprints multiply the row by 1 plus `CritChance`.

`ProcRolls` reads each call's second argument as arithmetic over row reads, attribute reads through
`GetFloatAttribute`, constants and locals with one assignment, and its third argument as a literal.
A call it cannot write out is reported at the end of the export.

## The chance

`settings.json` carries `procPerMinuteRoll`, the chance one roll procs as the shipping binary computes
it. Read from `fellowship-Win64-Shipping.exe` of build 26320967, image base `0x140000000`:

| What | Where |
| --- | --- |
| `execRollProcPerMinute`, in the `CRRandomStreamComponent` native table beside `ControlledRandomBool` | `0x145055FF0`, table entry at `0x148C86F08` |
| `CRRandomStreamComponent::RollProcPerMinute(FGameplayTag, float, bool)` | `0x1452CBD20` |
| Hasted rate: `rate × Haste`, the owning character's `Haste` attribute by `FindFProperty` of the name | `mulss` at `0x1452CBF87`, property lookup at `0x1452CC0DA` |
| Base chance: `rate × max(0, t) ÷ 60` | `mulss` by `0.016666668` at `0x1452CBFE1` |
| Drought factor: `max(1, 0.05 × rate × min(d, 1000) − 3.5)` | `0x1452CBFD1` to `0x1452CC003` |
| First-proc floor: `rate ÷ max(FirstProcChanceFloorPPMDivisor, 0.1)` while the stream has never procced | `0x1452CC049` to `0x1452CC069` |
| `FirstProcChanceFloorPPMDivisor` default 8 | constructor store at `0x1452C220F`, component offset `0x14D8` |
| The roll: `rand() & 0x7FFF` times `1 ÷ 32767`, proc when the chance exceeds it | `0x1452CC011` to `0x1452CC083` |

With `R` the rate, `t` the seconds since the stream's previous roll and `d` the seconds since its
previous proc:

```
p = R × t ÷ 60 × max(1, 3 × R × d ÷ 60 − 3.5)
```

`0.05 × R × d − 3.5` is `3 × (d ÷ (60 ÷ R)) − 3.5`: the factor rises above 1 once the drought passes
1.5 times the mean interval between procs. Each stream is keyed by the call's `ProcTag` and holds the
time of its last roll, the time of its last proc and whether it has procced. The hero's `Haste`
attribute holds 1 plus the Haste percentage, as `docs/cooldown-haste.md` states.
