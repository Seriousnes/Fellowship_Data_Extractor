# Ability activation and its event data

A hero's ability input finds the ability's spec by input ID and activates it through the hero.

## Targeting ability

A `CRGameplayAbility` with `TargetingAbility` gives that class as a second spec in `OnGiveAbility`. The second spec
has the ability's input ID with bit 16 set.

- An input for an ability with a targeting spec activates the targeting spec. The targeting ability acquires its
  target and activates the ability with a `FGameplayEventData`, so the ability's `CurrentEventData.TargetData` and
  `PreActivateTargetData` hold the target.
- An input for an ability with no targeting spec activates the ability with no event data. The engine's `PreActivate`
  then leaves `CurrentEventData` as it was, and the game's `PreActivate` empties `PreActivateTargetData`.

An ability's class default object states `TargetingAbility` and `TargetAcquisitionPolicy`. An ability without them
acquires its targets in its own graph, and its `CurrentEventData.TargetData` has no actor.

## Listeners reading the event data

A passive reading `CurrentEventData.TargetData` of an activated ability gets no actor for an ability activated with
no event data, whatever the ability's `AbilityTags`.
