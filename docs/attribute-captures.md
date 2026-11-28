# Attribute captures of a calculation

An execution of a gameplay effect reads each attribute through a capture definition: the attribute, `Source` or
`Target`, and `bSnapshot`. Every address is of build 26320967.

## Fields

| Field | Document | Value |
| --- | --- | --- |
| `relevantAttributesToCapture` | `settings.json` | The `RelevantAttributesToCapture` of each native damage and healing calculation class, keyed by the class name in an effect's `executions`. Each entry has `attribute`, `attributeSource` and `snapshot`. |
| `recaptureSourceAttributesBeforeEveryExecution` | effect | true where the effect states `RecaptureSourceAttributesBeforeEveryExecution` |
| `backingSource`, `backingSnapshot` | effect, each of `calculationModifiers` | The definition of the attribute a modifier's magnitude reads. The effect states it, and the spec captures it with the class's definitions. |

## The rule

- A definition with `snapshot` has a copy of the attribute's aggregator, made at a capture.
- A definition without it has the live aggregator, so each execution reads the attribute's value of that moment.
- A spec captures its `Source` definitions at its creation. An effect with
  `recaptureSourceAttributesBeforeEveryExecution` captures them again before each execution, a periodic tick included.
- A spec captures its `Target` definitions at each application.
- Every `Source` definition of the native calculation classes has `snapshot`. Every `Target` definition is live.

A periodic tick of an effect without `recaptureSourceAttributesBeforeEveryExecution` therefore has the source's
attributes of the spec's creation and the target's attributes of the tick. An application on a stack replaces the
spec, and the stack's ticks have the source's attributes of the latest application's spec from then on.
`effect-stacks.md` has the stack.

## The definitions

A definition is `FGameplayEffectAttributeCaptureDefinition`, 0x40 bytes: `AttributeToCapture`, `AttributeSource` at
+0x38 and `bSnapshot` at +0x39. `EGameplayEffectAttributeCaptureSource` has `Source` 0 and `Target` 1. Three statics
structs have the definitions of the native classes. Each statics constructor finds the attribute's property by name
in `CRBasicAttributeSet` (0x144F04090) or `CRHeroAttributeSet` (0x1450094E0) and stores the two bytes as one word:
0x0100 for `Source` with `bSnapshot`, 0x0001 for `Target` without it.

Shared statics, accessor 0x1450E5720, constructor 0x1450D6E10:

| Attribute | Attribute set | Definition | Offset | Store |
| --- | --- | --- | --- | --- |
| `Strength` | `CRBasicAttributeSet` | `Source`, `bSnapshot` | +0x08 | 0x1450D6FF7 |
| `Agility` | `CRBasicAttributeSet` | `Source`, `bSnapshot` | +0x50 | 0x1450D7089 |
| `Intellect` | `CRBasicAttributeSet` | `Source`, `bSnapshot` | +0x98 | 0x1450D711E |
| `PrimaryAttribute` | `CRBasicAttributeSet` | `Source`, `bSnapshot` | +0xE0 | 0x1450D71B2 |
| `CritChance` | `CRBasicAttributeSet` | `Source`, `bSnapshot` | +0x128 | 0x1450D7246 |
| `CritMultiplier` | `CRBasicAttributeSet` | `Source`, `bSnapshot` | +0x170 | 0x1450D72DA |

Damage statics, accessor 0x1450DCBE0, constructor 0x1450D5390:

| Attribute | Attribute set | Definition | Offset | Store |
| --- | --- | --- | --- | --- |
| `LifeSteal` | `CRBasicAttributeSet` | `Source`, `bSnapshot` | +0x08 | 0x1450D5A12 |
| `DamageScale` | `CRBasicAttributeSet` | `Source`, `bSnapshot` | +0x50 | 0x1450D58ED |
| `SourceDamageReflect` | `CRBasicAttributeSet` | `Source`, `bSnapshot` | +0x98 | 0x1450D5AA7 |
| `DifficultyDamageScale` | `CRBasicAttributeSet` | `Source`, `bSnapshot` | +0x320 | 0x1450D5982 |
| `DamageThreatMultiplier` | `CRBasicAttributeSet` | `Source`, `bSnapshot` | +0x368 | 0x1450D5B3B |
| `Expertise` | `CRHeroAttributeSet` | `Source`, `bSnapshot` | +0x560 | 0x1450D5BCF |
| `BalancingDamageAura` | `CRHeroAttributeSet` | `Source`, `bSnapshot` | +0x5F0 | 0x1450D5C67 |
| `TargetDamageReflect` | `CRBasicAttributeSet` | `Target` | +0xE0 | 0x1450D641F |
| `DodgeChance` | `CRBasicAttributeSet` | `Target` | +0x128 | 0x1450D5CFF |
| `ParryChance` | `CRBasicAttributeSet` | `Target` | +0x170 | 0x1450D5D97 |
| `BlockChance` | `CRBasicAttributeSet` | `Target` | +0x1B8 | 0x1450D5E2F |
| `BlockDamageReductionFlat` | `CRBasicAttributeSet` | `Target` | +0x200 | 0x1450D5EC7 |
| `BlockDamageReductionMultiplier` | `CRBasicAttributeSet` | `Target` | +0x248 | 0x1450D5F5F |
| `IncomingDamageMultiplier` | `CRBasicAttributeSet` | `Target` | +0x290 | 0x1450D5FF7 |
| `Armor` | `CRBasicAttributeSet` | `Target` | +0x2D8 | 0x1450D64B7 |
| `Avoidance` | `CRHeroAttributeSet` | `Target` | +0x3B0 | 0x1450D654F |
| `PhysicalDamageReduction` | `CRBasicAttributeSet` | `Target` | +0x3F8 | 0x1450D608F |
| `MagicDamageReduction` | `CRBasicAttributeSet` | `Target` | +0x440 | 0x1450D6127 |
| `PhysicalIncomingDamageMultiplier` | `CRBasicAttributeSet` | `Target` | +0x488 | 0x1450D61BF |
| `MagicalIncomingDamageMultiplier` | `CRBasicAttributeSet` | `Target` | +0x4D0 | 0x1450D6257 |
| `AoeIncomingDamageMultiplier` | `CRBasicAttributeSet` | `Target` | +0x518 | 0x1450D62EF |
| `DamageMitigationScale` | `CRBasicAttributeSet` | `Target` | +0x5A8 | 0x1450D6387 |

Heal statics, accessor 0x1450E27E0, constructor 0x1450D65C0:

| Attribute | Attribute set | Definition | Offset | Store |
| --- | --- | --- | --- | --- |
| `LifeSteal` | `CRBasicAttributeSet` | `Source`, `bSnapshot` | +0x08 | 0x1450D6859 |
| `HealThreatMultiplier` | `CRBasicAttributeSet` | `Source`, `bSnapshot` | +0x128 | 0x1450D6982 |
| `HealScale` | `CRBasicAttributeSet` | `Source`, `bSnapshot` | +0x170 | 0x1450D68EE |
| `Expertise` | `CRHeroAttributeSet` | `Source`, `bSnapshot` | +0x1B8 | 0x1450D6A16 |
| `BalancingHealAura` | `CRHeroAttributeSet` | `Source`, `bSnapshot` | +0x290 | 0x1450D6AAA |
| `IncomingHealMultiplier` | `CRBasicAttributeSet` | `Target` | +0x50 | 0x1450D6B3B |
| `IncomingHealSign` | `CRBasicAttributeSet` | `Target` | +0x98 | 0x1450D6BD0 |
| `ReflectHeal` | `CRBasicAttributeSet` | `Target` | +0xE0 | 0x1450D6C68 |
| `Health` | `CRBasicAttributeSet` | `Target` | +0x200 | 0x1450D6D00 |
| `MaxHealth` | `CRBasicAttributeSet` | `Target` | +0x248 | 0x1450D6D98 |

### The list of each class

A class's constructor adds definitions to `RelevantAttributesToCapture` (+0x28, 0x40 bytes each). The constructor of
a class with a base class runs the base class's constructor first.

| Class | Constructor | Definitions |
| --- | --- | --- |
| `CRDamageCalculationBase` | 0x1450D7510 | `CritChance`, `CritMultiplier`, and every definition of the damage statics |
| `CRStrengthDamageCalculation` | 0x1450D8910 | the base class's, `Strength` |
| `CRAgilityDamageCalculation` | 0x1450D73F0 | the base class's, `Agility` |
| `CRIntellectDamageCalculation` | 0x1450D84C0 | the base class's, `Intellect` |
| `CRPrimaryAttributeDamageCalculation` | 0x1450D8700 | the base class's, `PrimaryAttribute` |
| `CRUserDefinedDamageCalculation` | 0x1450D8A30 | the base class's |
| `CRHealCalculationBase` | 0x1450D80C0 | `CritChance`, `CritMultiplier`, every definition of the heal statics but `ReflectHeal`, and `DifficultyDamageScale` |
| `CRStrengthHealCalculation` | 0x1450D89A0 | the base class's, `Strength` |
| `CRAgilityHealCalculation` | 0x1450D7480 | the base class's, `Agility` |
| `CRIntellectHealCalculation` | 0x1450D8550 | the base class's, `Intellect` |
| `CRPrimaryAttributeHealCalculation` | 0x1450D8790 | the base class's, `PrimaryAttribute` |
| `CRUserDefinedHealCalculation` | 0x1450D8A60 | the base class's |
| `CRDamageReflectDamageCalculation` | 0x1450D7CC0 | `DamageScale` |
| `CRLifeStealHealCalculation` | 0x1450D85E0 | `DamageScale`, `Expertise` |
| `CRReflectHealCalculation` | 0x1450D8820 | `DamageScale`, `HealThreatMultiplier` |
| `CRManaGainCalculation` | 0x1450D86D0 | none |

### The definitions of the cooked effects

A scoped modifier of an effect's execution states the whole definition of the attribute it modifies, in
`CalculationModifiers[].CapturedAttribute`. The execution finds the modifier's capture by that whole definition, so a
cooked definition equals an entry of the class's list. The 2,600 cooked effects state 1,627 such definitions, and
each equals the entry of its class above. The export run checks every one and stops on a difference.

## A capture

`FActiveGameplayEffectsContainer::CaptureAttributeForGameplayEffect` (0x144A6F7A0) finds the attribute's aggregator on
the component and tests the definition's `bSnapshot` (+0x39).

- With it the capture is a new aggregator, a copy of the component's (0x144AB4A30, 0xB8 bytes).
- Without it the capture is a reference to the component's aggregator (0x144A65E90).

`FGameplayEffectSpec::CaptureDataFromSource` (0x144A6F7D0) captures every `Source` definition of the spec from the
instigator component of the spec's context, and reads the source actor's tags unless its argument says otherwise. It
has four callers:

| Caller | When |
| --- | --- |
| `FGameplayEffectSpec::Initialize` 0x144A80040, call 0x144A801B0 | the spec's creation |
| The periodic tick 0x144A80830, call 0x144A808F1 | each tick of an effect with `RecaptureSourceAttributesBeforeEveryExecution` (Def +0xA10, test 0x144A808C4) |
| `ApplyGameplayEffectSpecToSelf`, call 0x1449FD764 | the execution of an instant application of an effect with the flag (test 0x1449FD735) |
| `FGameplayEffectSpec::SetContext` 0x144A8F240, call 0x144A8F27D | `CRBlueprintLibrary::SetEffectContext` (0x1452432E0, exec thunk 0x144F75580), the one caller of `SetContext` |

`FGameplayAbilityTargetData::ApplyGameplayEffectSpec` (0x144A4F410), the body of
`K2_ApplyGameplayEffectSpecToTarget`, copies the spec for each target (0x144A4F5B9) and assigns a copy of the context
(0x144A4F5D8). It captures nothing, so each target's spec has the captures of the one spec the graph made.

The `Target` definitions are captured from the target's component at each application (0x144A6ACEB in
`ApplyGameplayEffectSpec`, and `CaptureAttributeDataFromTarget` 0x144A6F710).

## The value of an execution

The damage gather (0x1450DB100), the attack table gather (0x1450DB760) and the healing gather (0x1450DB8C0) read
every attribute through `AttemptCalculateCapturedAttributeMagnitude` (0x144AA2990). The damage gather and the attack
table gather read no attribute set and no component. The healing gather multiplies `HealScale` by a factor of the
source and target characters at each execution (0x145362710).

- The routine evaluates the aggregator of the execution's scoped modifiers for a definition with one (0x144A9CAC0),
  and the spec's capture otherwise (`FindCaptureSpecByDefinition` 0x144A73C10, evaluation 0x144A6B950).
- A snapshot is the attribute's modifier list of the capture. The evaluation (0x144AA8310) has a pass over the
  modifiers with the execution's parameters (0x144AA87B0) before it sums them. The parameters are the spec's source
  tags and target tags. `ExecuteActiveEffectsFrom` (0x144A72D10) reads the target actor's tags into the spec at each
  execution, and `CaptureDataFromSource` reads the source actor's tags at a source capture. A modifier of the
  source's attribute with a target tag requirement is therefore tested against the target's tags of the execution,
  and one with a source tag requirement against the source's tags of the capture.
- The attack table gather reads `CritChance` under `CanCrit`, and takes `min(CritChance, 1)` for a preset without
  `CanGrievousCrit`. The roll (0x14510C4D0) and the spread roll are made at each execution.
- The damage gather reads `CritChance` and `CritMultiplier` under `CanCrit`. The source formula (0x1451079A0)
  multiplies by `CritMultiplier` for the `EHitType` `CriticalStrike` (4), and by
  `(CritChance - 1) x G + CritMultiplier` for `GrievousCriticalStrike` (5). `G` is the row
  `GlobalGrievousCritScalerKey` names (ability system globals +0x368, getter 0x1450CDBF0).
- `Expertise` is read under `ApplySourceExpertiseMultiplier`, `BalancingDamageAura` under
  `ApplySourceDamageHealingBalancingAura`, `DamageScale` under `ApplySourceDamageHealingMultipliers` and
  `DifficultyDamageScale` under `ApplySourceDifficultyMultipliers`.

The stack count of a hit is the active effect's own count at the execution, and `SetByCaller.PartialTickFactor` is
written at the last tick. `effect-stacks.md` and `effect-periods.md` have them.
