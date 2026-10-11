# Cooldown Haste scaling

Ability, relic power and weapon power documents state how Haste, Cooldown Reduction and Cooldown
Recovery reach a cooldown. The game text calls Cooldown Recovery "Cooldown Acceleration".

The documents carry the following fields:

- **`cooldownDurationScalesOff`**: the attributes the cooldown's duration calculation reads when the
  cooldown starts.
- **`cooldownTimeRateScalesOff`**: the attributes the hero's time rate modifiers add to the cooldown
  while it runs.
- **`cooldownHasteGrantedBy`**: the talents and legendary powers granting the tag that lifts the
  ability above one charge, onto a charge effect with Haste in its time rate.
- **`cooldownAccelerationGrantedBy`**: the augmentations whose granted effect adds Cooldown Acceleration
  to every cooldown in the ability's category.
- **`castTimeScalesWithHaste`**: `CRGameplayAbility.ScaleTimeWithHaste`. The codex divides the cast
  time by 1 plus the Haste percentage when it is set.

A cooldown with `Haste` in either list scales with Haste. The list holding it says where Haste enters,
and the two placements compound differently.

## Timing effect

An ability with more than one charge recharges on its charge effect, and every other ability cools down
on its cooldown effect. `CRGameplayAbility` names both classes: `ChargeGameplayEffectClass` and
`CooldownEffectBaseClass`. The charge count is the document's `charges`.

The reader takes both classes from the ability's class default object, and from its Blueprint parents
when the ability declares neither.

## Duration

A cooldown effect's `DurationMagnitude` is a custom calculation. Its `CalculateBaseMagnitude` returns
the factor of the ability's `CooldownDuration`:

| Cooldown effect | Calculation | Factor |
| --- | --- | --- |
| `GE_Shared_Cooldown_HasteScale` | `GEC_CooldownReductionAndHasteMagnitude` | `(1 - CooldownReduction) / Haste` |
| `GE_Shared_Cooldown_DefaultScale` | `GEC_CooldownReductionMagnitude` | `1 - CooldownReduction` |
| `GE_Shared_Cooldown_DefaultScale_NoCooldownRecovery` | `GEC_CooldownReductionMagnitude` | `1 - CooldownReduction` |
| `GE_Shared_Cooldown_Static` | `GEC_CooldownStatic` | `1` |
| `GE_Shared_Cooldown_RelicScale` | `GMMC_GemAugmentation` | `1` minus the relic gem's reduction |

`cooldownDurationScalesOff` lists the captured attributes the calculation reads on its path to the
return value. `GEC_CooldownStatic` captures `CooldownReduction` and returns the constant 1, so a static
cooldown lists nothing.

`Haste` and `CooldownRecovery` hold 1 plus the percentage, so 30% Haste is 1.3. `GE_HeroAttributesBase`
subtracts 1 from each before it reaches a time rate.

A charge's duration is computed in C++. `CRChargeEffect` is native, and its Blueprint subclasses
declare tags alone, so a document recharging on a charge effect carries no `cooldownDurationScalesOff`.
Cooldown Reduction still shortens a charge: Prickly Vine's fastest recharges in issue #23 group by
Cooldown Reduction.

## Time rate

`GE_HeroAttributesBase` declares two `TimeRate` modifiers in its `CREffectAttributeGEComponent`, each
targeting the effects carrying one tag:

| Effect tag | Modifier magnitude | Attribute listed |
| --- | --- | --- |
| `EffectLogic.TimeRateInfluencedByCooldownRecovery` | `CooldownRecovery - 1` | `CooldownRecovery` |
| `EffectLogic.TimeRateInfluencedByHaste` | `Haste - 1` | `Haste` |

`cooldownTimeRateScalesOff` maps the timing effect's asset tags through that table. The timing effects
list the following attributes:

- **`CRChargeEffect`**: `CooldownRecovery` and `Haste`.
- **`GE_Shared_ChargeCooldownNoHaste`**: `CooldownRecovery`.
- **`GE_Shared_ChargeCooldownNoCooldownRecovery`**: `Haste`.
- **`GE_Shared_Cooldown_HasteScale`** and **`GE_Shared_Cooldown_DefaultScale`**: `CooldownRecovery`.
- **`GE_Shared_Cooldown_DefaultScale_NoCooldownRecovery`**, **`GE_Shared_Cooldown_Static`** and
  **`GE_Shared_Cooldown_RelicScale`**: no attribute.

`CRChargeEffect` sets its tags in C++. Its two Blueprint subclasses each record their combined tags and
the tag they remove, and `CombinedTags + Removed - Added` gives the parent's tags. Both subclasses give
`EffectLogic.System`, `EffectLogic.TimeRateInfluencedByCooldownRecovery` and
`EffectLogic.TimeRateInfluencedByHaste`.

## Haste placement

`GE_Shared_Cooldown_HasteScale` divides the duration by Haste once, when the cooldown starts:
`GEC_CooldownReductionAndHasteMagnitude` captures both attributes with `bSnapshot` set. The cooldown
then runs at the Cooldown Recovery time rate. `CRChargeEffect` takes Haste into the time rate beside
Cooldown Recovery for the whole recharge.

A cooldown on `GE_Shared_Cooldown_HasteScale` divides by Haste and by the Cooldown Recovery rate in
turn. The time rate modifiers combine in C++. Cold Snap's cast pairs in issue #23 fit a single rate of
`1 + (Haste - 1) + (CooldownRecovery - 1)` on `CRChargeEffect`.

## Talents and legendary powers

An ability's `GetMaximumCharges` tests a talent tag. With the tag held, the following abilities rise
from one charge to two and recharge on `CRChargeEffect`:

| Ability | Tag | Granted by |
| --- | --- | --- |
| Heart Bloom (1075) | `Talents.Mosse.DurationalAoeHealPlacement.Charges` | Flower Power, talent 31 |
| Luminous Barrier (996) | `Talents.Vigor.SingleTargetAbsorb.Charges` | Sacred Barrier, talent 566 |
| Ironleaf Ward (116) | `Talents.Mosse.CastedSingleTargetDamageReduction.AddCharges` | Unrelenting Thicket, legendary power 609 |
| Hold the Line (975) | `Talents.Warmaster.MoveSpeedBuff.Charges` | Iron Sentinel, legendary power 625 |
| Chain Lightning (1237) | `Talents.Ink.BouncyProjectile.Charged` | Ace of Spades, talent 675 |
| Heart Splitter (2294) | `Talents.Gunde.HeavyMeleeDotBased.Charges` | Bleeding Hearts, legendary power 698 |

Luminous Barrier, Chain Lightning and Heart Splitter cool down on `GE_Shared_Cooldown_HasteScale`
without the tag. With it, Haste moves from their duration to their time rate.

`cooldownHasteGrantedBy` names the augmentation that grants the tag. An augmentation is listed when its
tag lifts the ability from one charge to more, and the ability's charge effect has Haste in its time
rate. Apocalypse rises to two charges on `GE_Shared_ChargeCooldownNoHaste`, so its talent brings no
Haste.

## Category acceleration

A Major ability's cooldown carries `CooldownType.Regular.Major`. The shipping binary declares the tag,
and it is the only category tag under `CooldownType.Regular`.

`cooldownAccelerationGrantedBy` lists an augmentation when the following conditions hold:

- An effect in its `GrantedGameplayEffects` has a `TimeRate` modifier for a tag under `CooldownType`.
- The tag's last segment is the ability's `AbilityCategory`.
- The ability has a cooldown effect.

The Monarch (blessing 705) meets them for every Major ability. Its effect,
`GE_Finesse_MajorIncreasedCooldownRecoveryPerHaste`, adds the following to the time rate of a
`CooldownType.Regular.Major` cooldown:

```text
CooldownAcceleration + min(Haste - 1, HasteThreshold) x BonusIncreasePerHasteInPercentage
```

The constants are rows under `Finesse.MajorIncreasedCooldownRecoveryPerHaste` in `FinesseConstants`:

| Row | Rank 1 | Rank 2 | Rank 3 | Rank 4 |
| --- | --- | --- | --- | --- |
| `CooldownAcceleration` | 0.03 | 0.05 | 0.08 | 0.12 |
| `HasteThreshold` | 0.5 | 0.5 | 0.5 | 0.5 |
| `BonusIncreasePerHasteInPercentage` | 0.1 | 0.1 | 0.1 | 0.1 |

The Haste term is at most 0.05, reached at 50% Haste.

`GMMC_Finesse_MajorIncreasedCooldownRecoveryPerHaste` subtracts 1 from its first captured attribute,
and its class defaults list no captured attribute. The blessing's text, "increased by +0.1% for every 1%
Haste", names that attribute as Haste.

## Cast time flag

`castTimeScalesWithHaste` is `CRGameplayAbility.ScaleTimeWithHaste`, read through the ability's
Blueprint parents. Every named hero ability with a cast time sets it. The codex divides a cast time by 1
plus the Haste percentage when it is set.

The cooldown follows the cooldown fields. Prickly Vine sets `ScaleTimeWithHaste` and recharges on
`GE_Shared_ChargeCooldownNoHaste`.
