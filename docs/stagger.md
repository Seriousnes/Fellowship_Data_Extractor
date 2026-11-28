# Stagger

`settings.json` has `stagger`: the pool every hero's `CRStaggerActorComponent` holds, its cap and each rule that
removes Stagger from it. An ability document has `staggerDeltas`: each delta the ability's Blueprint assigns to a
target's pool. The pool's rules are native. Every address is from the build 26320967 shipping executable.

## `stagger` in the settings

- **`period`**, **`buckets`**, **`maxHealthPercentage`**: the rows `BP_PlayerCharacter`'s `StaggerActorComponent` binds
  to `StaggerPeriodRef`, `StaggerDurationRef` and `StaggerMaxHealthPercentage`, each with its row name.
- **`damageEffect`**, **`damageSetByCallerTag`**: the effect of a tick and the tag of its damage.
- **`maxHealthAttribute`**: `MaxHealth`, the attribute the cap multiplies.
- **`damageAdditionFactorAttribute`**: `StaggerDamageAdditionFactor`, read off a hit's target.
- **`healReductionFactorAttribute`**: `StaggerHealReductionFactor`, read off a heal's target.
- **`healAffectStaggerTag`**: `EffectLogic.HealAffectStagger`, the tag of a heal the heal reduction applies to.
- **`damageIgnoreStaggerTags`**: `EffectType.Environmental` and `EffectLogic.DamageIgnoreStagger`. The pool takes no
  part of a hit with either tag.
- **`modifyExecution`**, **`modifySetByCallerTag`**: `CRModifyStaggerExecution` and `SetByCaller.Stagger`.
- **`loggedResourceType`**: 7, the resource type of the combat log line of a `CRModifyStaggerExecution` delta.
- **`displayText`**, **`capText`**, **`removalText`**, **`loggedChangeText`**: the bucket rule, the cap, the removals
  and the log line, in words.

## The pool setter

`CurrentStagger` (component +0x140) changes in one routine, `AuthUpdateStagger` 0x1452C3540. It takes the delta by
pointer and writes back the delta the pool took.

```
new = max(CurrentStagger + delta, 0)                      # 0x1452C3570 to 0x1452C357C
if new == CurrentStagger: return                          # 0x1452C3580
if delta >= 0:                                            # 0x1452C3589
    cap = StaggerMaxHealthPercentage * owner.MaxHealth    # 0x1452C35CD to 0x1452C35EF
    if cap < CurrentStagger: delta = 0; return            # 0x1452C35F3 to 0x1452C3602
    if new > cap: delta -= new - cap; new = cap           # 0x1452C3604 to 0x1452C3618
CurrentStagger = new                                      # 0x1452C3629
OnStaggerChanged                                          # vtable +0x4B8, 0x1452C363C
if the caller asks: UpdateStaggerDamage(delta)            # 0x1452C364E, the bucket rule
```

The owner is the component's character (+0x98), cast to `CRFellowshipCharacter`. `owner.MaxHealth` is the
character's virtual +0xA38 (0x1452EC9A0): the current value of `MaxHealth` in its attribute set (character +0x898,
attribute +0x38). The cap is therefore read at each add, with every modifier of `MaxHealth` at that moment.

The cap applies to an add alone. A pool above the cap takes no part of an add and keeps its Stagger. A removal has
no cap.

## Every writer of the pool

| Writer | Delta | Buckets | Where |
| --- | --- | --- | --- |
| Damage hook | `H × StaggerDamageAdditionFactor`, H the hit's Health damage after the absorption effects | added | 0x1452C85F0, call 0x1452C87E4 |
| Heal hook | `−clamp(heal × StaggerHealReductionFactor, 0, CurrentStagger)` | scaled | 0x1452C8900, call 0x1452C8B0B |
| `CRModifyStaggerExecution` | the spec's `SetByCaller.Stagger` magnitude | added or scaled | 0x1451091E0, call 0x145109383 |
| Tick | `−bucket 0` | the first bucket popped | 0x1452CDD70, call 0x1452CDE4D |
| Hero death | `AuthClearStagger` | emptied | 0x145312310, call 0x145312378 |

The four natives `AuthClearStagger`, `AuthUpdateStagger`, `AuthUpdateStaggerAndLog` and `UpdateStaggerDamage` have
exec thunks (0x14505E495, 0x14505E568, 0x14505E69B, 0x14505EE5F), and no Blueprint of the build calls one.

### Damage hook

The factor is `StaggerDamageAdditionFactor` of the hit's target (getter 0x1452C76E0). The hook passes the part to
the setter and subtracts from H the delta the pool took (0x1452C87E9 to 0x1452C87FA). A hit at the cap therefore
keeps the rest of its part as Health damage. Each entry of `CachedStaggerContributions` writes a `DAMAGE_ABSORBED`
line of the taken delta times the entry's weight (0x1452C887C).

### Heal hook

The hook runs on the server for a heal whose target is the component's owner. A heal with `EffectType.Environmental`
returns unchanged, and so does a heal without `EffectLogic.HealAffectStagger` (0x1452C897A, 0x1452C89F2). The factor
is `StaggerHealReductionFactor` of the heal's target (getter 0x1452C7790), and a factor of 0 or less returns
(0x1452C8AB6). Otherwise the heal loses `clamp(heal × factor, 0, CurrentStagger)` and the pool loses the same amount
(0x1452C8ACE to 0x1452C8B0B).

Both factors are attributes of `CRHeroAttributeSet` (+0x218 and +0x228). The constructor 0x14500A430 stores 0 as the
base and the current value of each (0x14500A503, 0x14500A50A), and no attribute table has a row for either. Effect
2739, `GE_Lisa_Stagger_Attributes`, is the build's one modifier of them: `StaggerDamageAdditionFactor` plus row
`Lisa.Stagger.DamageStaggerAdditionFactor` (0.3), and `StaggerHealReductionFactor` plus row
`Lisa.Stagger.HealStaggerReductionFactor` (0). With a factor of 0 a heal removes no Stagger.

### `CRModifyStaggerExecution`

The execution reads the transient magnitude of `SetByCaller.Stagger` (tag global 0x14A3CA5E8) and calls
`AuthUpdateStaggerAndLog` on the component of the spec's target (character +0xF78). It multiplies by nothing: no
stack count, no partial tick factor and no attribute. The effects running it have `setByCallerModifiers` or
`calculationModifiers` with the execution's name.

## The deltas abilities assign

`staggerDeltas` on an ability document has one entry for each `SetByCaller.Stagger` magnitude the ability's Blueprint
assigns to a spec of an effect running `CRModifyStaggerExecution`.

- **`effect`**: the effect of the spec.
- **`dataTag`**: the set-by-caller tag.
- **`formula`**: the expression in one line. A row is named by its last segment.
- **`expression`**: the same expression as nodes. A node is an `op` over `operands`, a `constant`, a constants `row`
  with its `value`, an `attribute` of `of`, or a `read` of a native getter or an event field of `of`. `of` is `Source`
  for the ability's owner, `Target` for a character of the target data, or the name of the event.

| Ability | Effect | Formula |
| --- | --- | --- |
| Amend Fate, 1964 | 2826 | `min(StaggerRemovalIntCoefficient × Source.Intellect × (Source.Expertise + 1) × RandomFloatInRange(StaggerRemovalSpreadMin, StaggerRemovalSpreadMax), Target.GetStagger) × -1` |
| Restore Continuity, 1973 | 2827 | the same expression over its own rows, once for each party member |
| Temporal Barrage, 1926 | 3117 | `Target.GetMaxHealth × StaggerRemovalInPercentage × -1` |
| `GA_Lisa_Passive_CastedHeal_HealToStagger`, 1972 | 3117 | `HealthChangedEvent.TotalHealthChange × HealToStaggerMultiplier` |

Amend Fate applies effect 2826 at every cast on a hero, a cast on an empty pool included. Restore Continuity applies
effect 2827 to a member whose cleanse times `StaggerToHealMultiplier` is above 0. Temporal Barrage assigns its delta
for each bolt on an ally while the cast has the Paradoxical Twist proc. The passive assigns its delta for a heal of
its owner with the asset tag `CustomAssetTags.Lisa.CastedHeal.Heal`, while the owner has no
`CustomGrantedTags.Lisa.Talent.CastedSingleTargetProjectile.CastedHealFromPreviousDamageProc.Proc`.

Effect 2818, Echoes of Divinity, has its delta on the effect: `calculationModifiers` has the coefficient
`−Lisa.Talent.StaggerRemovalOverTime.PercentageOfMaxHealth` of the Source's `MaxHealth` for each tick. Effects 2826
and 2827 apply it through their `AdditionalEffectsGameplayEffectComponent` under the source tag
`Talents.Lisa.StaggerRemovalOverTime`. Its period is divided by the instigator's Haste and its duration is not, as
`effect-periods.md` states for every periodic effect.

Effect 3117, `GE_Shared_ModifyStagger`, has Echoes of Divinity in the deprecated
`UGameplayEffect.ConditionalGameplayEffects` list (+0x2A0) alone. It has no `AdditionalEffectsGameplayEffectComponent`.
In the ability module the constructor and the destructor are the only routines with that offset off an effect
(0x144A63B62, 0x144A6518C).

## The combat log line

`AuthUpdateStaggerAndLog` 0x1452C3670 calls the setter and then the combat logger at 0x145298560. The logger queues
the event variant 0xA, the variant of the attribute resource writer 0x145297E00, a `RESOURCE_CHANGED` line. The
record has:

| Field | Value | Where |
| --- | --- | --- |
| resource type | 7 | 0x145298694 |
| amount | the pool after the delta | 0x145298685 to 0x1452986B9 |
| change | the pool after the delta minus the pool before it | 0x145298690 to 0x1452986C5 |
| maximum, overcap | −1 | 0x1452986CB to 0x1452986DD |
| source, target, ability | of the execution's spec | 0x145298638, 0x145298678, 0x1452988FD |

`CRModifyStaggerExecution` is the routine's one caller. A hit's part, a heal's removal, a tick and the death clear
call the setter directly and write no such line.
