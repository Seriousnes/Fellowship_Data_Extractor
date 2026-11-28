# Movement, tick and combat event tasks

Four ability tasks on the routes of `endAbilityPaths` have no `seconds`: `AbilityTask_FellowshipMoveTo`,
`AbilityTask_MoveToLocation`, `CRAbilityTask_TickAsync` and `CRAbilityTask_WaitForCombatEvents`. Each rule below is
from the build 26320967 shipping executable, and every address is of that build.

| Task | Delegate | Time from the task's start |
| --- | --- | --- |
| `FollowActor`, `FellowshipMoveTo`, `FellowshipMoveToLocation` | `OnTimeout` | `Timeout` seconds, for a `Timeout` above 0 |
| `FellowshipMoveTo`, `FellowshipMoveToLocation` | `OnArrived` | 0 within `ProximityTolerance` of the goal, otherwise path length / walk speed |
| `FollowActor`, `FellowshipMoveTo`, `FellowshipMoveToLocation` | `OnNoPath` | the frame of a failed path query, a dead goal or a missing goal |
| `MoveToLocation` | `OnTargetLocationReached` | `max(Duration, 0.001)` seconds |
| `TickAsync` | `OnFinished` | `TotalTime + 0.05` seconds |
| `TickAsync` | `OnTick` | every `TickInterval` seconds |
| `WaitForCombatEvents` | `EventReceived` | each gameplay event with one of `EventTags` |

## AbilityTask_FellowshipMoveTo

The class has three nodes (parameter records 0x148A998D0, 0x148A99B90 and 0x148A99E30, natives 0x145111AC0,
0x145111BA0 and 0x145112AE0):

```
FellowshipMoveTo(OwningAbility, Controller, Goal, ProximityTolerance, Timeout, CancelIfTargetDies, DisableCollision, AllowPartialPaths)
FellowshipMoveToLocation(OwningAbility, Controller, Goal, ProximityTolerance, Timeout, DisableCollision, DoTick, AllowPartialPaths)
FollowActor(OwningAbility, Controller, Goal, ProximityTolerance, Timeout, CancelIfTargetDies, DisableCollision, AllowPartialPaths)
```

`Goal` is an actor for `FellowshipMoveTo` and `FollowActor`, and a location for `FellowshipMoveToLocation`. The
delegates are `OnTimeout` (+0x80), `OnArrived` (+0x90), `OnNoPath` (+0xA0), `OnStartMoving` (+0xB0), `OnTick` (+0xC0)
and `OnFollowTick` (+0xD0).

```
factory:
    task.Goal = Goal                                     # actor +0xE8, location +0xF0
    if Goal implements CRInteractable: ProximityTolerance += GetInteractionRadius(Goal)   # actor goals, 0x145112B4C
    task.ProximityTolerance = ProximityTolerance         # +0x10C
    task.Timeout = Timeout                               # +0x110
    task.Controller = Controller                         # +0x118
    task ticks every frame                               # FellowshipMoveToLocation: with DoTick alone

Activate:                                                # 0x14510F060, vtable 0x148A9A790 +0x2C8
    if DisableCollision: disable the pawn's collision    # 0x14510F0D7
    if actor goal and Goal is gone: EndTask(); OnNoPath.Broadcast(); return     # 0x14510F1A8
    if not FollowActor:
        if no Controller: EndTask(); OnNoPath.Broadcast(); return
        if 2D distance(pawn, goal) <= ProximityTolerance: EndTask(); OnArrived.Broadcast(); return   # 0x14510F21C
    start move                                           # 0x145110BE0
    if the task has not ended: SetTimer(Timeout) -> EndTask(); OnTimeout.Broadcast()   # 0x14510F3CF, 0x145115040

start move:                                              # 0x145110BE0
    if no Controller, pawn or navigation system: EndTask(); OnNoPath.Broadcast(); return   # 0x14511105A
    if path following is not idle: abort its move        # 0x145110C84
    if actor goal and Goal is gone: EndTask(); OnNoPath.Broadcast(); return     # 0x145110EC3
    query = path from the pawn to the goal, end location navigable unless AllowPartialPaths   # 0x1443C4530
    result = FindPathSync(query)                         # 0x1443D3B40
    if result is not Success: EndTask(); OnNoPath.Broadcast(); return           # 0x145110E74, 0x14511100B
    OnStartMoving.Broadcast()                            # 0x145110E83
    request = path following.RequestMove(goal, AcceptanceRadius = ProximityTolerance)   # 0x145110F63
    path following.OnRequestFinished -> request finished # 0x145110FA3

request finished:                                        # 0x145114AB0
    if FollowActor: return                               # 0x145114AB8
    if result has Success: EndTask(); OnArrived.Broadcast()                     # 0x145114AC1
    else if result has InvalidPath: EndTask(); OnNoPath.Broadcast()             # 0x145114AD7

TickTask:                                                # 0x145117C60, +0x2D8
    if CancelIfTargetDies and Goal is a CRCharacterBase and not alive: EndTask(); OnNoPath.Broadcast(); return   # 0x145117CA8, alive virtual +0xA10
    if FollowActor and 2D distance(pawn, Goal) >= ProximityTolerance + 20:      # 0x145117D98
        if a request is running and path following is not idle: OnFollowTick.Broadcast(); return   # 0x145117DD4
        start move                                       # 0x145117E0C
    OnTick.Broadcast()                                   # 0x145117E11
```

The timer is `FTimerManager::SetTimer` (0x144042D80, through 0x1440430A0) with `Timeout` for its rate. A rate of 0 or
less sets no timer (0x144042E20), so a task with a `Timeout` of 0 has no `OnTimeout`.

A path following result other than Success and InvalidPath broadcasts nothing. A blocked or aborted move therefore
leaves a `FellowshipMoveTo` task running until its `Timeout`.

`FollowActor` has no arrival. Its request result is not read, and its `Activate` has no distance test. The task runs
until `OnNoPath`, `OnTimeout` or the end of its ability. On each frame with the pawn at least
`ProximityTolerance + 20` units from the goal in 2D and path following idle, it starts a new move, and that move's
path query is the path check.

### Walk speed

Path following moves the pawn at its `MaxWalkSpeed` (movement component +0x268). The character writes it from its
combined move speed virtual (+0xA50, writer 0x1452ED990). For `CRAICharacter` (vtable 0x148AEB930) the virtual is
0x145155D80:

```
MaxWalkSpeed = GlobalNPCBaseMoveSpeed x MoveSpeed x PassiveMoveSpeed x AI speed scale
```

- **`GlobalNPCBaseMoveSpeed`**: 900, ability system globals +0x310, from `[/Script/fellowship.CRAbilitySystemGlobals]`
  in `DefaultGame.ini`. `GlobalHeroBaseMoveSpeed` (+0x308) is 700.
- **`MoveSpeed`**, **`PassiveMoveSpeed`**: the attributes at attribute set +0x168 and +0x178.
- **AI speed scale**: character +0xEB0, 1.0 from the constructor (0x145152975), set by 0x14515A7B0.

## AbilityTask_MoveToLocation

`AbilityTask_MoveToLocation::MoveToLocation(OwningAbility, TaskInstanceName, Location, Duration,
OptionalInterpolationCurve, OptionalVectorInterpolationCurve)` is the engine's task (exec thunk 0x144A3BDE0, vtable
0x1489758E0). The move has a fixed time, whatever the distance.

```
factory:
    task.DurationOfMovement = max(Duration, 0.001)       # +0xD0, 0x144A3C1C0
    task.TimeMoveWillEnd = world.TimeSeconds + DurationOfMovement   # +0xD8, 0x144A3C200

TickTask:                                                # 0x144A26EA0
    if world.TimeSeconds >= TimeMoveWillEnd:             # 0x144A26F62
        actor location = Location; OnTargetLocationReached.Broadcast(); EndTask()
```

Cold-blooded Twist (ability 747) passes a `Duration` of 0.5.

## CRAbilityTask_TickAsync

`CRAbilityTask_TickAsync::TickAsync(OwningAbility, TaskInstanceName, TickInterval, TotalTime, TickAtStart)` is
0x145122B00 (exec thunk 0x144F02F50, parameter records 0x148A9EE80, vtable 0x148A9F6C8). The delegates are `OnTick`
(+0x80), `OnCancelled` (+0x90) and `OnFinished` (+0xA0).

```
factory:
    task.TickInterval = TickInterval                     # +0xB0
    task.TotalTime = TotalTime                           # +0xB4
    task.TickAtStart = TickAtStart                       # +0xB8
    task ticks every frame if TickInterval == 0          # 0x145122C0B

Activate:                                                # 0x14511CE40
    if TickInterval != 0: SetTimer(rate = TickInterval, looping, first delay = TickAtStart ? 0 : TickInterval) -> OnTick.Broadcast()   # 0x14511D023
    SetTimer(TotalTime + 0.05) -> OnFinished.Broadcast(); EndTask()             # 0x14511D0F4, 0x14511D25C, 0x14511F3D0
    if TickInterval == 0 and TickAtStart: OnTick.Broadcast()                    # 0x14511D31D

TickTask: OnTick.Broadcast()                             # 0x145120A50
ExternalCancel: OnCancelled.Broadcast()                  # 0x14511E670
```

`OnFinished` is `TotalTime + 0.05` seconds after `Activate`. Most creature abilities pass a `TickInterval` of 0.02 and
a `TotalTime` of 1000, and reach `K2_EndAbility` from an `OnTick` handler.

## CRAbilityTask_WaitForCombatEvents

`CRAbilityTask_WaitForCombatEvents::WaitForCombatEvents(OwningAbility, EventTags, OptionalTarget, OnlyTriggerOnce,
OnlyMatchExact, OnlyFromCurrentLife)` is 0x14512EDD0 (exec thunk 0x144F284A0, parameter records 0x148AD0E20, vtable
0x148AD2680). `WaitForCombatEventsWithParams` (0x14512EEA0) takes the three bools and an `AssetTagsRequirements` in a
`CRWaitForCombatEventParams`. The task has no time.

```
Activate:                                                # 0x1451260E0
    component = OptionalTarget's ability system component, or the ability's own   # +0x158, +0x70
    OnlyMatchExact: bind each tag of EventTags in component.GenericGameplayEventCallbacks   # component +0x2A0
    otherwise: bind the tags with their child tags

event:                                                   # 0x1451275B0
    if the payload's OptionalObject is a CRCombatEvent:
        if OnlyFromCurrentLife: the event's CRCharacterBase test passes         # 0x145127634 to 0x1451276A4, test 0x145246590
        if AssetTagsRequirements has tags: the event's tags meet them           # 0x1451276AA
        EventReceived.Broadcast()                        # +0x80, 0x145127760
    if OnlyTriggerOnce: EndTask()                        # 0x145127792
```

The native event tags include `Event.Damage`, `Event.Healing`, `Event.Incoming.Damage` and `Event.Incoming.Healing`,
each with children such as `Event.Incoming.Damage.Blocked` (literals from 0x148D40360).

## Slicing Blades

Slicing Blades (ability 737, `GA_FK_Elf_Duelist_SpinningIceSword`) starts `FollowActor` in the `OnActivate` of its
channel:

```
FollowActor(self, controller, cast target, ProximityTolerance = 200, Timeout = 0,
            CancelIfTargetDies = true, DisableCollision = false, AllowPartialPaths = false)
```

`ProximityTolerance` is `FK_ElfDuelist.SpinningIceSwordStorm.SpinProximityTolerance`. `OnNoPath` and `OnTimeout` both
reach `K2_EndAbility` (ubergraph offset 844).

- **`OnTimeout`** has no timer, because `Timeout` is 0.
- **`OnNoPath`** ends the channel early when the cast target is dead on a frame, when the target is gone, or when a
  path query from the Blade to the target has a result other than Success. `AllowPartialPaths` is false, so a target
  with no complete navmesh path to it fails the query. The query runs at the channel's start, and again on each frame
  with the Blade at least 220 units plus the target's interaction radius from the target and its path following idle.

With a living target on the navmesh the channel ends at `ServerStartChanneling.OnFinished`. The Blade walks at
`900 x MoveSpeed x PassiveMoveSpeed` units a second, and the ability's self slow multiplies `MoveSpeed` by 0.375.
