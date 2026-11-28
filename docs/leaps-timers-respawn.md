# Leaps, spawned actors, timers and respawn

Four timing rules of the build 26320967 shipping executable: the arrival of an AI leap, the `Success` of an actor
spawned from an ability, the first expiry of a Blueprint timer with the tick of Spirited Vortex's actor, and the
respawn of a dead player. Every address is of that build.

`settings.json` has `respawn`:

- **`cooldownEffect`**: `GE_Shared_RespawnCooldown`, the `RespawnCooldownEffect` of the `CRGameModeDungeon` Blueprint.
- **`cooldownDuration`**, **`cooldownDurationRow`**: 30 seconds, from `Shared.RespawnCooldown.Duration`.
- **`cooldownTag`**: `Cooldown.Respawn`, the tag the effect grants.
- **`displayText`**: the respawn rule, in words.

## AI leap

`CRAbilityTask_AILeapTo` has two nodes (parameter records 0x148A96DF0 and 0x148A970B0):

```
AILeapToLocation(OwningAbility, GoalLocation, LeapTime, MaxHeight, Timeout, AcceptanceRadius, LeapToGround, DrawDebug)
AILeapToTarget(OwningAbility, GoalActor, LeapTime, MaxHeight, Timeout, AcceptanceRadius, LeapToGround, DrawDebug)
```

The leap arrives `LeapTime` seconds after the task's `Activate`. Distance, speed and Haste are absent from the
arrival time. `AILeapToTarget` copies the location of `GoalActor` once, in the factory, and is the same task from
there.

```
factory:                                                 # 0x14510EB10, target 0x14510EC70
    if LeapToGround: goal = ground under goal, within 500 units   # 0x14522EF60
    task.GoalLocation = goal                             # +0xE8
    task.MaxHeight = clamp(MaxHeight, 0, 1)              # +0x100, 0x14510EBEC to 0x14510EC15
    task.Timeout = Timeout                               # +0x104
    task.AcceptanceRadius = AcceptanceRadius             # +0x108
    task.LeapTime = LeapTime                             # +0x150, class default 1.5 (0x14510DF35)

Activate:                                                # 0x14510F4A0
    start = world.TimeSeconds                            # +0x130, 0x14510F4E6
    OnActivate.Broadcast()                               # 0x14510F50A
    d = |actor location - goal|                          # 0x145110650, 0x14511085F
    goal += (actor location - goal) / d * min(AcceptanceRadius, d)   # 0x145110866 to 0x1451108CF
    if d <= AcceptanceRadius: EndTask(); OnArrived.Broadcast(); return   # 0x1451108D8
    velocity = launch velocity from actor to goal with arc MaxHeight     # 0x1439CCB40
    if velocity is zero: EndTask(); OnNoPath.Broadcast(); return         # 0x145110A50
    curve = projectile path of velocity at 20 Hz, to the point nearest goal   # 0x14524FFF0, task +0x110
    OnStartMoving.Broadcast()                            # 0x145110BB6
    if Timeout > 0: SetTimer(Timeout) -> EndTask(); OnTimeout.Broadcast()   # 0x14510F6C0, 0x145115080

TickTask:                                                # 0x145117E30
    elapsed = world.TimeSeconds - start                  # 0x145117E7A to 0x145117E8A
    actor location = curve(clamp(elapsed / LeapTime, 0, 1) * curve end time)   # 0x145117E8E to 0x145117F6F
    if LeapTime - elapsed <= 0: EndTask(); OnArrived.Broadcast()   # 0x145117FAB to 0x145117FD3
```

`world.TimeSeconds` is `UWorld` +0x6B8, the value `GameplayStatics::GetTimeSeconds` returns (0x1439DC5F4).
`OnArrived` is on the first server frame at or after `LeapTime`. A world without navigation data ends the task with
`OnNoPath` inside `Activate`.

## Actor spawned from an ability

The `SpawnActorFromAbility` node of `CRAbilityTask_SpawnActorFromAbility` is four calls in one run of the ability
graph:

```
task = SpawnActorFromAbility(OwningAbility, SpawnTransform, Class, Owner, Instigator, DestroyConditions)   # 0x14512BF10
if task.BeginSpawningActor(OwningAbility, SpawnTransform, Class, out SpawnedActor):                        # 0x1451267D0
    SetXPropertyByName(SpawnedActor, pin, value) for each exposed pin
    task.FinishSpawningActor(OwningAbility, SpawnTransform, SpawnedActor)                                  # 0x145127440
```

`BeginSpawningActor` spawns the actor deferred on the authority (0x14512690F). With no authority, with an owner
failing a `DestroyConditions` test at the spawn (0x145143C50) or with no actor from the spawn, it broadcasts
`DidNotSpawn` and returns false (0x145126A2D to 0x145126A46).

`FinishSpawningActor` calls `AActor::FinishSpawning` with `SpawnTransform` (0x145127477), then `EndTask`
(0x14512748A), then broadcasts `Success` with the actor (0x1451274A2). The actor's construction and `BeginPlay` are
inside `FinishSpawning`. `Success` is 0 seconds after the spawn, before the statement after `FinishSpawningActor`.

## Blueprint timer

`KismetSystemLibrary::K2_SetTimerDelegate(Delegate, Time, bLooping, bMaxOncePerFrame, InitialStartDelay,
InitialStartDelayVariance)` is 0x143ABCF70 (exec thunk 0x143AFFB80).

```
InitialStartDelay += InitialStartDelayVariance * (rand() & 0x7FFF) * (2 / 32767) - InitialStartDelayVariance
                                                         # 0x143ABD02B to 0x143ABD060
FirstDelay = Time + InitialStartDelay                    # 0x143ABD16C, 0x143ABD173
TimerManager.SetTimer(handle of Delegate, Delegate, rate = Time, bLooping, bMaxOncePerFrame, FirstDelay)   # 0x144042D80

SetTimer:
    clear the timer the handle has                       # 0x144042E0E
    if rate <= 0: return                                 # 0x144042E20
    if FirstDelay < 0: FirstDelay = rate                 # 0x144042F2F to 0x144042F39
    ExpireTime = InternalTime + FirstDelay               # 0x144042F64
```

The first expiry of a timer, looping or one-shot, is `Time + InitialStartDelay + U(-Variance, +Variance)` after the
call. With both delay arguments 0 it is one `Time` after the call. A looping timer expires every `Time` after that.

### Spirited Vortex

Spirited Vortex (ability 1120, `GA_Meiko_DurationalAoeDamageBuff`) has no timer. Its damage is the `ReceiveTick` of
`BP_Meiko_DurationalAoeDamageBuff_Actor`.

Each cast, in one frame:

1. `Prepare Spawning of Actor` destroys the vortex actor attached to Meiko (`K2_DestroyActor`, offset 740).
2. The ability makes the damage spec and the Enfeebled spec, and spawns a new actor through `SpawnActorFromAbility`.
3. The actor's `BeginPlay` (ubergraph offset 521) calls `SetLifeSpan(Life Time)`,
   `SetActorTickInterval(Tick Interval x Scaler)` and `SetActorTickEnabled(true)`, and applies the buff icon effect
   with duration `Life Time`.

- **`Life Time`**: the ability's `Duration`, 30 (`Meiko.DurationalAoeDamageBuff.Duration`).
- **`Tick Interval`**: 2 (`Meiko.DurationalAoeDamageBuff.TickInterval`).
- **`Scaler`**: `1 / Haste` at the spawn, from `BPL_Hero_Utils.GetDefaultTimingScalerBasedOnHaste`.

`SetActorTickInterval` writes the actor's tick interval (0x1435CDA11). `SetActorTickEnabled` (0x1435BE680) calls
`FTickFunction::SetTickFunctionEnable` (0x144055450), and `FTickTaskLevel::AddTickFunction` (0x144027480) adds the
function to the level's enabled set. An enabled function ticks at the next queueing and then has a cooldown of its
interval (0x14405202C to 0x1440520DD). A function ending its cooldown ticks and has the next cooldown
`TickInterval - (DeltaSeconds - CumulativeCooldown)` (0x144051DCF to 0x144051DF4).

The actor therefore ticks on the frame after the cast and every `2 / Haste` seconds from that tick, with Haste as it
was at the spawn, until its lifespan of 30 seconds. Each tick with Meiko free of `CharacterStatus.Unsensible` acquires
the targets in the radius, sets the falloff scaler and applies both specs.

A recast at time `r` ends the old actor's ticks at `r`. The new actor ticks at `r`, `r + 2 / Haste(r)` and so on to
`r + 30`. The buff icon effect is `AggregateBySource` with a stack limit of 1, so the second application is a stack
refresh (0x144A6A93B) and its duration is 30 again.

`BP_Mosse_DurationalAoeHealPlacement_AoeActor` sets its tick interval the same way.

## Respawn

A dead player revives on that player's own respawn request. The build has no timer from a wipe to a revive.

`CRGameModeDungeon.OnTeamRespawnedAfterWipe` (+0x438, record 0x148BD2A00) is a `BlueprintAssignable` delegate. The
executable has no broadcast of it, and no cooked Blueprint binds it.

### Wipe

```
CRGameModeBase::Tick:                                    # 0x145349320, vtable 0x148BD4978 +0x500
    if HasMatchStarted(): wipe check                     # vtable +0x7B8, +0xA38

wipe check:                                              # 0x145349640
    if the wiped flag is set: return                     # +0x3C9
    for each PlayerState of the game state:
        if its pawn is a CRFellowshipCharacter and alive: return   # vtable +0xA10, 0x145349708
    if every pawn is a CRFellowshipCharacter:
        wipe count += 1                                  # +0x3CC, 0x14534972B
        wiped flag = true                                # 0x145349731
        wipe virtual()                                   # vtable +0xA30
        reset                                            # 0x145347CC0
```

`CRGameModeDungeon`'s wipe virtual (0x1453465D0) passes the wipe count to its backend component. The reset destroys
the actors registered for a dungeon reset (+0x3D0), cancels the active `AbilityLogic.ActivatableWhileDead` abilities
of each dead AI character (0x145154DE0) and destroys the AI characters with the flag at +0xDE8.

### Request

The Respawn button of `UW_DeathRecap_Widget` calls `ServerRequestRespawn` (ubergraph offset 2884).

```
ServerRequestRespawn:                                    # 0x14532E770, player controller vtable 0x148D05A10 +0x1028
    if character.IsAlive(): return                       # 0x14532E847
    if boss component.CurrentBossFightIndex >= 0: return # 0x1453450D0, component +0xD8
    if character has Cooldown.Respawn: return            # tag global 0x14A3CA740, 0x14532E8C4
    Respawn(controller, in place = false, cooldown = !HasMatchEnded())   # 0x145347EA0

Respawn:
    restart the player at a player start                 # vtable +0x898, 0x1453480E7
    if cooldown: apply RespawnCooldownEffect to the character   # game mode +0x3A8, 0x14534812A to 0x14534822A
    wiped flag = false                                   # 0x14534827C
```

The widget's `RefreshState` disables the button under the same two conditions, `IsInAnyBossFight` and
`Cooldown.Respawn`.

`RespawnCooldownEffect` is a `CRGameModeBase` property. `GameMode_BP`, the `CRGameModeDungeon` Blueprint, sets it to
`GE_Shared_RespawnCooldown` (effect 109, Respawn Cooldown).

### Boss fight end

`CRAICharacter::OnAllThreatLost` (0x145156CD0) of a character with the tag `ActorTypeTags.Boss` (tag global
0x14A3CA6B8) calls the game mode boss component (0x14533BF00), and that ends the boss fight as not completed
(0x145337AC0): the bosses of the fight reset, and `CurrentBossFightIndex` is -1 (0x145337D4F, setter 0x145348BA0).

The time from a wipe to a revive is the player's reaction time, at least the time to the boss fight's end and at least
the rest of that player's `Cooldown.Respawn`.
