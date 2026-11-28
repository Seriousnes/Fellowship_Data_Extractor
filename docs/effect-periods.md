# Effect periods, refreshes and partial ticks

A periodic gameplay effect ticks on a looping timer. `CRAbilitySystemComponent` sets the timer's rate, the
duration of a refreshed effect and the last tick of an expiring one. Every address is of build 26320967.

## Duration policy

An effect's `DurationPolicy` is `Instant`, `Infinite` or `HasDuration`. A cooked effect states the field only where
it differs from its parent class. The value otherwise comes from the constructor of the effect's native class, in the
byte it stores at `UGameplayEffect` +0x30. An effect document has the result in `durationPolicy`.

| Native class | Constructor | `DurationPolicy` |
| --- | --- | --- |
| `GameplayEffect` | 0x144A63AB0, store 0x144A63ED7 | `Instant` |
| `CRGameplayEffectDamage` | 0x1450E84D0, no store of its own | `Instant` |
| `CRCostEffect` | 0x1450E76D0, store 0x1450E7704 | `Instant` |
| `CRDamageReflectGameplayEffect` | 0x1450E7880, store 0x1450E78B5 | `Instant` |
| `CRInterruptGameplayEffect` | 0x1450E8C30, store 0x1450E8C64 | `Instant` |
| `CRKnockbackEffect` | 0x1450E8DF0, store 0x1450E8E24 | `Instant` |
| `CRLifeStealGameplayEffect` | 0x1450E9090, store 0x1450E90CD | `Instant` |
| `CRReflectHealGameplayEffect` | 0x1450E9B10, store 0x1450E9B4D | `Instant` |
| `CRSetByCallerDamageGameplayEffect` | 0x1450E9F70, store 0x1450E9FA0 | `Instant` |
| `CRSetByCallerHealGameplayEffect` | 0x1450EA350, store 0x1450EA380 | `Instant` |
| `CRAbsorptionEffect` | 0x1450E67B0, store 0x1450E6804 | `Infinite` |
| `CRDisconnectedGameplayEffect` | 0x1450E7E30, store 0x1450E7E62 | `Infinite` |
| `CRGameplayEffectActorDependency` | 0x1450E84A0, store 0x1450E84B5 | `Infinite` |
| `CRGodModeEffect` | 0x1450E8700, store 0x1450E8739 | `Infinite` |
| `CRInfiniteChannelingGameplayEffect` | 0x1450E8C00, store 0x1450E8C15 | `Infinite` |
| `CRProcEffect` | 0x1450E9830, store 0x1450E9864 | `Infinite` |
| `CRCastingGameplayEffect` | 0x1450E69E0, store 0x1450E6A2F | `HasDuration` |
| `CRChannelingGameplayEffect` | 0x1450E6DE0, store 0x1450E70C2 | `HasDuration` |
| `CRChargeEffect` | 0x1450E71E0, store 0x1450E7211 | `HasDuration` |
| `CRGenericAbilityCooldownEffect` | 0x1450E8550, store 0x1450E8584 | `HasDuration` |
| `CRLockoutGameplayEffect` | 0x1450E94F0, store 0x1450E953D | `HasDuration` |

An `Instant` effect executes once inside each application and has no duration, period or tick of its own. A
`duration` and a `period` on its document are rows of the ability or the actor applying it, and `ticks` is on
documents of the `HasDuration` policy alone.

Sanguine Ritual's damage effect, `GE_MC_BloodSummoner_ChannelSpawnBloodAnimation_TickDamage`, states no
`DurationPolicy` and derives from `CRGameplayEffectDamage`. The ability's graph makes its spec and applies it to the
ability's one target on each `OnTick` of the channel task. The ability's `Channeling Data` has `ChannelingDuration`
10, `ChannelingTickInterval` 1, `TickImmediately` false, `Interruptible` true and `EnablePartialTicks` false. The
first `OnTick` is one tick interval after the task's activation, and a channel ended before it has no hit:
`docs/casting-and-channeling.md` has the task.

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
`NeverRefresh` restarts the duration. `effect-stacks.md` has the stack's count, its cap and the fields of an effect
document.

An effect with the asset tag `EffectLogic.CarryOverDuration` and without `EffectLogic.Proc` restarts with
`Duration + min(Duration x GameplayEffectRefreshDurationCarryOverLimit, time remaining)`.

## Damage added to a running periodic effect

`CRBlueprintLibrary::CrAddToAndRecalculatePeriodicEffect(Ability, Source, Target, EffectClass, SetByCallerTag,
AddedValue, TargetingFilters)` spreads `AddedValue` over the ticks of one effect on `Target`. The SetByCaller magnitude
it writes is the amount of one tick. The function returns false on every path.

It first checks `Target` against `TargetingFilters` and does nothing on a failure. It then queries `Target`'s
ability system component for active effects whose context source object is `Source` and whose definition is exactly
`EffectClass`, and takes the first handle with a live effect.

With no such effect it makes a spec from `Ability` and applies it with

`tick = AddedValue / (floor(D / P) + Fmod(D, P) / P + E)`

- **`D`**, **`P`**: the new spec's `Duration` and `Period`. `P` has no Haste term.
- **`E`**: 1 when the effect has `bExecutePeriodicEffectOnApplication`, otherwise 0.

With an effect it applies no spec. It reads the effect's state:

- **`R`**: the effect's time remaining.
- **`Pr`**, **`Pe`**: the remaining and the elapsed time of the effect's period timer. The timer runs at the hasted
  rate, so these are real seconds.
- **`P`**: the active spec's `Period`, with no Haste term.
- **`D`**: the effect's duration.
- **`V`**: the active spec's magnitude for `SetByCallerTag`.

The ticks left are `n + f + z`:

- `n = ceil(clamp(R - Pr, 0, 10000) / P)`
- `f = Fmod(R - Pr, P) / P` when `R >= Pr`, otherwise `(Pe + R) / P`
- `z = 1` when `f` is 0, otherwise 0

The new tick is `((n + f + z) x V + AddedValue) / (floor(D / P) + Fmod(D - Pr, P) / P)`. The function moves the
effect's start time forward by `D - R`, which restarts the duration and leaves the period timer running. It then writes
the new tick to the active spec's `SetByCallerTag` magnitude.

Haste enters the result only through `Pr` and `Pe`. The function reads no Haste attribute and rounds no magnitude.

## Applied-effect listeners

Each application, a refresh included, broadcasts the instigator's `OnGameplayEffectAppliedDelegateToTarget` with the
active effect's own spec and handle.

- A `WaitGameplayEffectAppliedToTarget` task copies that spec in its callback, and its Blueprint event receives the
  copy. A write to the active effect's spec by one listener is in the copy of every listener after it.
- Listeners run from the one registered last to the one registered first. A task registers at `ReadyForActivation`.
- A task's event does not run again for an application its own event makes.
- A write by the applying ability after its apply call runs after every listener of that application.

### Registration order of passive abilities

A `CRGameplayAbilityPassive` has `ActivateAbilityOnGranted`, so `OnGiveAbility` activates it inside the give call. A
task in its `K2_ActivateAbility` therefore registers at the moment the ability is given.

`OnAvatarSet` is the engine's empty body for every `CRGameplayAbility`, and no native routine activates a hero's
talent or item passive a second time.

The hero's `Restart` override gives abilities from two components on the server, the `EquipmentComponent` first and
the `TalentComponent` second. The engine calls `Restart` at each possession of the hero by its controller. The
`EquipmentComponent` does nothing at a `Restart` with the loadout it already has. The `TalentComponent` applies its
talents at every `Restart`.

- The `TalentComponent` loads each talent's augmentation asset synchronously and gives its `GrantedAbilities` inside
  the call, in the order of the loadout's talents. Every application first removes every talent of the hero, so a
  talent change gives every selected talent's abilities again.
- The `EquipmentComponent` requests one asynchronous load of every augmentation asset its items name. It gives their
  `GrantedAbilities` inside the call when the load handle is already complete, and from the handle's completion
  delegate otherwise. The handle is complete at return when every requested asset is in memory at the request. An
  item's `GrantedAbilityAsset` is a soft reference, so its mapping table loads no augmentation asset. An application
  keeps each augmentation already applied, removes each one no longer named and gives each new one, so an equipment
  change gives the abilities of the changed items alone.

A talent's passive and an item's passive therefore have no fixed order. The item's passive is given last when its
asset load completes after the hero's first `Restart`, or when its item is equipped after the last talent
application. The talent's passive is given last when every asset of the equipment's request is in memory at the first
`Restart`, after a talent change, and after each later `Restart`.

`CancelAbilitiesOnCharacterDeath` and `ResetAbilitiesOnFellowshipReset` cancel the `CRGameplayAbilityPassive` classes
of the dungeon's own ability lists alone, and the second activates each again.

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
4. The stack count, 1 for an effect with `silentStack`.
5. `BalancingDamageAura`, under `ApplySourceDamageHealingBalancingAura`.

The product is rounded half away from zero before the target's terms.

The target's terms multiply in this order, each under its preset flag:

1. `CalcDamageReductionFromArmorValue(trunc(Armor))`, under `ApplyTargetArmor`. `HeroItemStatCalculation` has the
   function: `1 - 0.00052 x Armor / (Global.ArmorToDamageReductionMultiplier + 0.00048 x |Armor|)`.
2. `clamp(1 - Avoidance, 0, 1)`, under `ApplyTargetAvoidance`.
3. `IncomingDamageMultiplier`, under `ApplyTargetIncomingDamageHealingMultipliers`.
4. The school pair. For a hit with a source tag at or under `EffectType.Physical`:
   `clamp(1 - PhysicalDamageReduction, 0, 1)` under `ApplyTargetPhysicalDamageReduction` and
   `PhysicalIncomingDamageMultiplier` under `ApplyTargetPhysicalIncomingDamageMultiplier`. Otherwise, for a source
   tag at or under `EffectType.Magic`: `clamp(1 - MagicDamageReduction, 0, 1)` under
   `ApplyTargetMagicDamageReduction` and `MagicalIncomingDamageMultiplier` under
   `ApplyTargetMagicalIncomingDamageMultiplier`.
5. `AoeIncomingDamageMultiplier`, under `ApplyTargetAoeIncomingDamageMultiplier`.

`DamageMitigationScale` multiplies the hit after its absorption passes, under no flag.

The gather captures a target attribute only for a set flag. A reduction is otherwise 0 and a multiplier 1. A school
pair therefore needs the tag and the flag together. High Tide's damage effect has the tag `EffectType.Magic.Water`
and the preset `EnemyPhysicalArea`, so neither pair changes its hit: the tag selects the magic pair and the preset
has both magic flags clear. Arcane Volley's damage effect has the preset `EnemyMagicalArea`, so `Avoidance` and
`AoeIncomingDamageMultiplier` apply to it.

### The school test

The test reads one container, the spec's aggregated captured source tags. The preset's name, the calculation class
and the main stat have no part in it. The container has:

- the effect's asset tags, `effectTags` in the export
- each tag a graph adds to the spec with `AddAssetTag` or `AddAssetTags`
- the `AbilityTags` and the `AbilityInstanceTags` of the ability making the spec
- the source actor's owned tags at the spec's creation

`EffectType.Physical` is tested first, so a hit with a tag of each school takes the physical pair. A hit with a tag
of neither school takes no pair. Shatterstrike's damage effect for a wet target has no asset tags component, its
ability's tags are `AbilityType` tags, and its preset is `EnemyMagicalArea`: the hit takes `Avoidance`,
`IncomingDamageMultiplier` and `AoeIncomingDamageMultiplier`, and neither `MagicDamageReduction` nor
`MagicalIncomingDamageMultiplier`.

Piercing Howl's damage effect has the tag `EffectType.Physical` and the preset `EnemyMagicalArea`. The tag selects
the physical pair, the preset has both physical flags clear, and the magic pair's attributes are captured and
multiplied into nothing. The hit takes no school pair.

Each captured attribute is evaluated against the hit's source tags and target tags. A modifier with
`sourceRequireTags`, `sourceIgnoreTags`, `targetRequireTags` or `targetIgnoreTags` in the export therefore applies to
one hit and not to another: Rot Wave's `IncomingDamageMultiplier` modifier requires the source tag
`EffectType.Magic`.

The hit's flag byte has its own school test, over the spec's asset tags alone and with `EffectType.Magic` first:
bit 0x10 for a magic hit, otherwise bit 0x08 for a physical one. The same byte has bit 0x01 for the source tag
`AbilityType.AutoAttack`, 0x40 for `EffectLogic.NotReflectable`, 0x80 for `EffectType.StaggerDamage`, and 0x04 for a
periodic effect or 0x02 for any other.

The spread roll is `(rand() & 0x7FFF) / 32767 - 0.5`, so `SpreadRoll x Spread + 1` is uniform from `1 - Spread / 2`
to `1 + Spread / 2`.

### An effect's asset tags

An effect's asset tags are the `CombinedTags` of its `AssetTagsGameplayEffectComponent`. At load the component
rebuilds them from the component of the effect's parent class, less its `Removed` tags and plus its `Added` tags,
and copies them to the effect. The cooked component of a child class omits `CombinedTags` equal to its parent's, so
`effectTags` has the tags of the nearest class of the chain stating them. An effect with no such component has no
asset tags.

`CRInterruptGameplayEffect`, `CRLockoutGameplayEffect`, `CRCostEffect`, `CRProcEffect`, `CRChargeEffect` and
`CRAbsorptionEffect` create the component in their constructors with tags of their own, and a Blueprint child's
`CombinedTags` has them. `CRSetByCallerDamageGameplayEffect` creates it with `EffectType.Magic`,
`EffectType.Harmful`, `EffectLogic.System` and `EffectLogic.NoThreatGeneration`.
`CRSetByCallerHealGameplayEffect` creates it with `EffectType.Magic` too.

An instant effect executes inside its application call, so the attributes and the hit outcome of each hit are those
of the moment the ability's graph applies it.

`ApplyAscensionScaling` is read by the healing calculation alone. Its scale is the curve row
`GlobalAscensionReduceHealingScalerKey` names.
