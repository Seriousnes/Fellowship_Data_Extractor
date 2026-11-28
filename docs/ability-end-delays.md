# Ability end delays

A creature ability is active from its activation to its `K2_EndAbility`. While it is active the engine blocks the
activation of each ability of the same creature whose `AbilityTags` has a tag of its `BlockAbilitiesWithTag`, or a tag
under one. A creature skill with `BlockAbilitiesWithTag` `AbilityType.AutoAttack` therefore stops the creature's auto
attack until its graph reaches `K2_EndAbility`.

## Routes to K2_EndAbility

A Blueprint ability's graph is one ubergraph function with many entry offsets. The activation event enters at one
offset. A task's delegate, a latent action's linkage and a timer's delegate each enter at another. The statements from
an entry offset run to their return in one frame, so a route from activation to `K2_EndAbility` is a list of waits:

| Wait | In the bytecode | Continues at |
| --- | --- | --- |
| Ability task | a task factory call such as `CRAbilityTask_WaitDelay::WaitDelayCR`, then `Bind` and `+=` on one of the task's delegates | the bound event's ubergraph offset |
| Latent action | a call with a `LatentActionInfo` argument, such as `KismetSystemLibrary::Delay` | the `LatentActionInfo`'s linkage offset |
| Timer | `K2_SetTimerDelegate` with a bound delegate | the bound event's ubergraph offset |

`EndAbilityPaths.cs` reads each route into the ability document's `endAbilityPaths`. A task has the factory's name in
`task` and the delegate's name in `delegate`. A statement reached only past a failed `K2_CommitAbility` belongs to no
route.

Ice Spike (`GA_FK_IceGolem_SpikeAOE`, ability 720) has one route:

| `task` | `delegate` | `seconds` | `variables` | `scaleTimeWithAbilitySpeed` |
| --- | --- | --- | --- | --- |
| `ServerStartCasting` | `OnFinished` | | | |
| `WaitDelayCR` | `OnFinish` | 1.3 | `AbilityEndDelay` 1.3 | true |

The ability ends 1.3 seconds after its cast finishes.

An ability has one route for each way its graph reaches `K2_EndAbility`. The delegate names tell the routes apart:
`SpawnActorFromAbility.DidNotSpawn`, `AILeapToLocation.OnNoPath` and `ServerTargetedNPCCast.OnFailed` are the routes of
a failed step. A route with no task is an end in the activation frame, such as a target out of range.

## Seconds of a wait

`seconds` is the `Time` argument of `WaitDelayCR`, `WaitDelay` and `K2_SetTimerDelegate`, and the `Duration` argument
of `Delay`. Each is a float, parameter 1 of its function. The reader computes it from literals, the class default of
each variable the argument reads and the constants row of each `FScalableFloat`, through `Add`, `Subtract`,
`Multiply`, `Divide`, `FMax` and `FMin`. `variables` has each variable read, with its class default. Razor Trap's first
wait is `Spawn Delay` 0.4 less a literal 0.05, so its `seconds` is 0.35.

`CRAbilityTask_AILeapTo::AILeapToLocation(OwningAbility, GoalLocation, LeapTime, MaxHeight, Timeout,
AcceptanceRadius, LeapToGround, DrawDebug)` and `AILeapToTarget` have `seconds` too. The task's `TickTask`
(0x145117E30) broadcasts `OnArrived` on the first tick with `LeapTime` (task +0x150) passed since `Activate`
(0x14510F4A0), and nothing scales `LeapTime`. `OnArrived` has `LeapTime` as its `seconds`, and `OnTimeout` has
`Timeout`. A leap starting within `AcceptanceRadius` of its goal broadcasts `OnArrived` inside `Activate`. Ice
Breaker's leap arrives after its `Leap Time` of 0.25 seconds, and the ability ends its `WaitInAbilityDelay` of 1
second later.

A wait has no `seconds` where the argument reads a value the graph computes while the ability runs. Maelstrom's last
wait is `SuckAnimationLength` (2.3) less the time since its activation, so it has `variables` alone and the ability
ends 2.3 seconds after its activation, or when the last pull arrives if that is later.

A float or double variable a Blueprint class declares has 0 on a class default object without the property, because a
cooked class default object leaves out a property equal to its zero value. `StayInAbilityDelay` on most melee auto
attacks is such a variable, so the wait is 0 seconds.

The task's `Activate` (0x145125D70) compares `Time` with 0 at 0x145125D9C. A `Time` above 0 sets a timer. A `Time` of
0 or less sets none: `Activate` ends the task and broadcasts the delegate at task +0x80 in the same call (0x14512600D
to 0x145126028). An ability with a 0 second `WaitDelayCR` before `K2_EndAbility` therefore ends in the frame of its
hit.

## WaitDelayCR and ability speed

`CRAbilityTask_WaitDelay::WaitDelayCR(OwningAbility, Time, ScaleTimeWithAbilitySpeed)` is at 0x14512E940 in the
26320967 shipping exe.

| Step | Where |
| --- | --- |
| `ScaleTimeWithAbilitySpeed` tested | `test bpl,bpl` 0x14512EA06 |
| True: the ability's time factor read, `Time / factor` stored in the task's `Time` (+0x90) | call 0x14512EA0E, `divss xmm1,xmm0` 0x14512EA19, store 0x14512EA1D |
| False: `Time` stored unchanged | 0x14512EA2D |
| Parameter name literals | `ScaleTimeWithAbilitySpeed` 0x148AD1A80, `WaitDelayCR` 0x148AD1AA0 |

The time factor routine is at 0x1450E06E0:

| Step | Where |
| --- | --- |
| Factor starts at 1.0 | 0x1450E0727 |
| `ScaleTimeWithHaste` (ability +0x3B8 bit 0): factor is the owner's `Haste` | test 0x1450E0710, attribute read 0x1450E077B |
| `ScaleTimeWithAttackSpeed` (bit 1): factor is `factor + AttackSpeed - 1` | test 0x1450E07A1, `addss` 0x1450E07F0, `subss` 0x1450E07F7 |
| No owner component: 1.0 | 0x1450E083D |

The factory divides once, so the wait keeps the factor of the moment the task starts. An ability with neither flag
waits `Time` itself, whatever the argument. `Delay` is the engine's latent action and has no factor.

The document has the two flags as `castTimeScalesWithHaste` (`ScaleTimeWithHaste`) and `scaleTimeWithAttackSpeed`.

## InterruptCooldownDuration

`InterruptCooldownDuration` is an `FScalableFloat` of `CRGameplayAbility`, beside `InterruptCooldownEffectBaseClass`.
The ability document has its value in seconds as `interruptCooldownDuration`, and `fieldSources` names its constants
row. A creature's ability slot has its own `interruptCooldown`, from the phase data.
