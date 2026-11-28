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

## Branch conditions of a route

Two routes with the same tasks up to one handler part at a branch of that handler. A Blueprint `Branch` node, an
`Is Valid` node, a cast node and a switch node each compile to `EX_JumpIfNot` or `EX_PopExecutionFlowIfNot` on a boolean
expression. `EndAbilityConditions.cs` walks a handler from its entry offset and gives each statement the branches whose
last test before it has the same side on every path to it. A branch with a path through each side meeting again before
the statement is left out. The test of a `ForEach` or `ForLoop` node reads the loop's counter, a local assigned from
itself. It is kept on its true side, inside the loop's body, and left out on its false side, because a counted loop
always ends through that side.

| Field | Branches between |
| --- | --- |
| `conditions` of a task | the entry of the handler the task starts in, and the task's start |
| `endConditions` of a route | the entry of the route's last handler, and `K2_EndAbility` |
| `conditions` of a `loops` entry | the entry of the handler at `from`, and the start of the task at `to` |

The handler of a route's first task is the activation. Each condition has the expression in `condition` and the side
taken in `value`. `GraphExpressionText.cs` writes the expression in the graph's names:

- A local is replaced by the expression assigned to it, so `CallFunc_Greater_IntInt_ReturnValue` is
  `Greater_IntInt(Array_Length(TargetsLeft), 0)`.
- A function has its name without its library class, and a call on another object has the object before a dot:
  `GetOwningCRCharacterFromActorInfo().HasAnyMatchingGameplayTags([CharacterStatus.Immobilized])`.
- A variable of the ability has its name alone. A parameter of the handler's delegate has the parameter's name, such
  as `SpawnedActor` of `SpawnActorFromAbility.Success`.
- A local filled through an out parameter is the call, a dot and the parameter: `CalculateChargeTargetInfo(...).Success`.
- A loop's counter has the compiler's name for it, `Temp_int_Loop_Counter_Variable`.
- A cast node is `DynamicCast<Class>(object)`, true where the cast succeeds.
- `Not_PreBool` is left out and reverses `value`. A `BooleanAND` taken true and a `BooleanOR` taken false give one
  condition for each operand.

The validity test of the task a route waits on is left out, because a task the factory returns null for has no
delegate. The side of a branch past a failed `K2_CommitAbility` is closed to the walk.

Collapsing Star (`GA_DD_WitheringMagi_CastedRangedProjectileDOT`, ability 772) has three routes after its cast. Two
part in the handler of `SpawnActorFromAbility.Success`:

| Route | Condition | `value` |
| --- | --- | --- |
| ends in the `Success` handler | `endConditions`: `IsValid(SpawnedActor)` | false |
| waits `StayInAbilityDelay` 0.3 seconds, then ends | `conditions` of `WaitDelayCR`: `IsValid(SpawnedActor)` | true |

The third route is the task's other delegate, `DidNotSpawn`.

## Loops of a route

A handler starting a task of an earlier step of its own route is a loop. A route has each task once, and `loops` has
the return: the handler of the task at `from` starts the task at `to` again, under `conditions`. The tasks from `to`
to `from` therefore run once for each turn of the loop.

Bloodhunt (`GA_FK_BossElfDragon_MultiDash`, ability 761) dashes to each character in `TargetsLeft`:

| Step | `task` | `delegate` | `seconds` | `conditions` |
| --- | --- | --- | --- | --- |
| 0 | `WaitDelayCR` | `OnFinish` | 1.5 (`InitialChargeDelay`) | `Greater_IntInt(Array_Length(TargetsLeft), 0)` true |
| 1 | `MonitorTargetValidityWithFilters` | `OnActivate` | | `Greater_IntInt(Array_Length(TargetsLeft), 0)` true |
| 2 | `MoveToActorUsingPathFixedSpeed` | `OnTargetLocationReached` | | |
| 3 | `WaitDelayCR` | `OnFinish` | 0.5 (`ChargeArriveDelay`) | |
| 4 | `WaitDelayCR` | `OnFinish` | 0.7 (`StayInAbilityDelay`) | `Array_RemoveItem(TargetsLeft, CurrentDashTarget)` true, `Greater_IntInt(Array_Length(TargetsLeft), 0)` false |

The route's `loops` has `from` 3 and `to` 1, under `Array_RemoveItem(TargetsLeft, CurrentDashTarget)` true and
`Greater_IntInt(Array_Length(TargetsLeft), 0)` true. The handler of step 3 removes `CurrentDashTarget` from
`TargetsLeft`. With a target left it starts step 1 for the next target, and with none it starts step 4. Steps 1 to 3
run once for each target marked at activation, and `GetMarkedTargetsSortedByRange` fills `TargetsLeft` in the
activation. Each of the three waits has `ScaleTimeWithAbilitySpeed` true.

## Seconds of a wait

`seconds` is the `Time` argument of `WaitDelayCR`, `WaitDelay` and `K2_SetTimerDelegate`, and the `Duration` argument
of `Delay`. Each is a float, parameter 1 of its function. The reader computes it from literals, the class default of
each variable the argument reads and the constants row of each `FScalableFloat`, through `Add`, `Subtract`,
`Multiply`, `Divide`, `FMax` and `FMin`. `variables` has each variable read, with its class default. Razor Trap's first
wait is `Spawn Delay` 0.4 less a literal 0.05, so its `seconds` is 0.35.

`K2_SetTimerDelegate(Delegate, Time, bLooping, bMaxOncePerFrame, InitialStartDelay, InitialStartDelayVariance)`
(0x143ABCF70) first fires `Time + InitialStartDelay` after the call (0x143ABD16C), moved by a roll within the variance
(0x143ABD02B to 0x143ABD060). A timer's `seconds` is that sum, for a timer with a variance of 0. Every timer of a
creature ability in this build passes 0 for both.

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
waits `Time` itself, whatever the argument. `Delay` is the engine's latent action and has no factor. The cast and the
channel tasks read the same factor (`docs/casting-and-channeling.md`).

The document has the two flags as `castTimeScalesWithHaste` (`ScaleTimeWithHaste`) and `scaleTimeWithAttackSpeed`.

## InterruptCooldownDuration

`InterruptCooldownDuration` is an `FScalableFloat` of `CRGameplayAbility` at +0x4A8, beside
`InterruptCooldownEffectBaseClass` at +0x4D0. The ability document has its value in seconds as
`interruptCooldownDuration`, and `fieldSources` names its constants row. The constructor (0x1450D7D60) gives the
property a `Value` of 0 with no table, row or registry (stores 0x1450D7E7E to 0x1450D7EA3), so an ability whose
Blueprint sets none has no interrupt cooldown of its own. A creature's ability slot has its own `interruptCooldown`,
from the phase data.
