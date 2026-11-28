# Combat log ids

An effect document has `logIdClass` and `loggedUnder`: the id of the combat log line of the effect's hit. An ability
document has `logAsADifferentAbility`. The rule is native. Every address is from the build 26320967 shipping
executable.

- **`logIdClass`**: `EffectClass` or `AbilityClass`, the member of `CRCombatLogAbilityEffectData` with the line's id.
  An effect with a damage or healing execution has it.
- **`loggedUnder`**: the effect or ability with the line's id, with the ability in `appliedBy` as the hit's
  instigating ability.
- **`visualDisplayName`**, **`alwaysUseVisualDisplayName`**: the two `CRGameplayEffectDamage` members the rule reads.
- **`logAsADifferentAbility`**: the ability's `LogAsADifferentAbility`.

## The ids

`CRCombatLogGuidSubsystem` has one map from a class name to an id (+0x30). Its builder 0x1452ABF80 reads each file of
`Content/guid`. The first line of a file is its kind: 0 for a tag table, 1 for a class table, 2 for a string table.
`GameplayAbility.csv`, `GameplayEffect.csv` and `CRAICharacter.csv` are the class tables, and their rows go in the one
map (0x1452A80A0). A row with the flag 1 is left out (0x1452A813B). The lookup 0x1452AB8D0 takes a class, removes `_C`
from its name and returns the row's id, or −1.

The line has the row's id as it is in the table. No routine of the executable adds 1,000,000 to it. Fellowship Logs
adds 1,000,000 to the id of a line logged under an effect, so effect 2909 is 1002909 there.

## The line

A damage or healing execution fills a `HealthChangedInfo` (0x1450E3600):

| Member | Value | Where |
| --- | --- | --- |
| `InstigatingAbilityClass` +0x40 | class of the effect context's ability (context +0x18), for a `CRGameplayAbility` | 0x1450E3685 to 0x1450E36CE |
| `InstigatingEffectClass` +0x48 | class of the spec's effect | 0x1450E36D2 to 0x1450E36F9 |

The damage driver 0x1450DCF40 writes no line for a spec with the tag `EffectLogic.NoLogging` (0x1450DD5CF).

The log writer 0x145296FB0 copies the two classes to the record's `InstigatingAbilityEffectData`, a
`CRCombatLogAbilityEffectData` (record +0x108). The ability class goes through 0x145299170: an ability whose
`LogAsADifferentAbility` (`CRGameplayAbility` +0x610) names an ability class is replaced by that class (0x145299285 to
0x145299316). The replacement is one step.

`CRCombatLogAbilityEffectData` has the id (+0x00), the name (+0x08), the source ability id (+0x20), `AbilityClass`
(+0x30) and `EffectClass` (+0x38). Its text is `{id}|"{name}"|{source ability id}` (0x1452851D0). One record type,
`CRCombatLogEventData_GenericDamageEvent`, is the line of `SWING_DAMAGE`, `ABILITY_DAMAGE`, `ABILITY_PERIODIC_DAMAGE`,
`ABILITY_HEAL`, `ABILITY_PERIODIC_HEAL` and `ABILITY_LIFESTEAL_HEAL` (0x145275470), so a hit, a tick and a heal have
their id by the one rule.

## The rule

0x1452AE480 sets two flags from the classes and calls the resolver 0x145285FC0.

- **Duration**: the effect's `DurationPolicy` (+0x30) is not `Instant` (0x1452AE5F9).
- **Auto attack**: the ability's `AbilityTags` have `AbilityType.AutoAttack` (tag global 0x14A3C9FA8) or
  `Ability.AutoAttack` (0x14A3CAA10) (0x1452AE694, 0x1452AE6F0).

An ability is named when `CRAbilityDataSubsystem`'s `AbilityDataMap` has its class with a `Name` other than the empty
text and `Invalid Ability Data` (0x145286324 to 0x14528635F). The map's populator 0x1450C7FE0 has each
`CRAbilityDataRow` under its `GameplayAbilityAsset` and each of its `AdditionalGameplayAbilityAssets`.

```
E = EffectClass's default object, for a CRGameplayEffectDamage      # 0x145285FE8 to 0x14528607A
A = AbilityClass's default object, for a CRGameplayAbility          # 0x14528607D to 0x1452860EE

if E and Duration:                                                  # 0x1452860FA
    id = effect; name = E's GameplayEffectUIData_BuffDebuff Name,
                        else E.VisualDisplayName, else "Unnamed DoT"
elif E and not Auto attack and E.AlwaysUseVisualDisplayName
       and E.VisualDisplayName is not empty:                        # 0x1452863D3 to 0x1452863F6
    id = effect; name = E.VisualDisplayName
elif A is named:                                                    # 0x1452865D2 to 0x145286757
    id = ability; name = A's AbilityDataMap Name; source ability id = 0
elif E and E.VisualDisplayName is not empty:                        # 0x145286769 to 0x14528679D
    id = effect; name = E.VisualDisplayName
elif EffectClass is a GameplayEffect class:                         # 0x145286959 to 0x145286A47
    id = effect; name = the class's name
```

A line with the effect's id has the id of a named `A` as its source ability id, and 1 otherwise (0x14528621C,
0x1452863C6).

In the terms of an effect document:

| Case | Condition | Line's id |
| --- | --- | --- |
| Periodic tick | the effect derives from `CRGameplayEffectDamage` and has a `durationPolicy` of `HasDuration` or `Infinite` | the effect's |
| Named hit | the effect derives from `CRGameplayEffectDamage`, is `Instant`, has `alwaysUseVisualDisplayName` and a `visualDisplayName`, and the ability is no auto attack | the effect's |
| Ability's hit | any other effect, with a named instigating ability | the ability's, after `logAsADifferentAbility` |
| Unnamed ability | any other effect, with an unnamed instigating ability or none | the effect's |

`logIdClass` is `EffectClass` for the first two rows and `AbilityClass` for the last two.

The known lines: Final Blow's effect 2909 has `alwaysUseVisualDisplayName` and the name Final Blow, and its line has
the id 2909. Arcane Volley's instant effect 909 has no `alwaysUseVisualDisplayName` and its ability 749 is named, and
its line has the id 749. Arcane Volley's effect 908 has the policy `HasDuration`, and its line has the id 908.

## Aura and cast lines

`EFFECT_APPLIED`, `EFFECT_REFRESHED` and `EFFECT_REMOVED` have a `CRCombatLogEffectData`. Its resolver 0x145286AB0
gives the line the effect's id. The name is the `Name` of the effect's `GameplayEffectUIData_BuffDebuff`, or the
`VisualDisplayName` of a `CRGameplayEffectDamage` with `AlwaysUseVisualDisplayName`. An effect with neither name is
unresolved (0x145286C33).

A cast line has a `CRCombatLogAbilityData`. Its resolver 0x145285DC0 gives the line the id of the ability's class,
after `LogAsADifferentAbility`, and the `AbilityDataMap` name.
