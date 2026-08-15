# Export Truth Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Finish the bytecode-truth audit's open items so every value the export ships is the value the game computes, and every shipped name and description reaches the record that owns it.

**Architecture:** One shared bare-state Kismet evaluator serves charges, magnitudes, tooltip payloads and placeholders. Values claimed from evaluated bytecode take precedence over naming conventions; table readers follow the pattern ItemTraits already set. Every phase ends with a full export whose diff against the previous export is inspected field by field, plus a pinned-value test that fails before the change and passes after.

**Tech Stack:** .NET 10, CUE4Parse (Kismet bytecode via `UStruct.ScriptBytecode`, `KismetExpression.StatementIndex` for jump targets), System.Text.Json, xUnit.

**Spec:** The verified audit at https://claude.ai/code/artifact/814be6d4-de99-4eb0-afe9-24cd5d31dc79, cross-checked by a three-lane adversarial review of this plan on 2026-08-15. Every expected value is copied into this plan, so the plan stands alone.

## Global Constraints

- Work happens on the `effect-uidata` worktree branch at `G:\source\Fellowship_Data_Extractor.effect-uidata`, which already carries four commits (f502c10 UIData component, 84cadb8 magnitude durations, 0305b54 curve-less values, 6252933 grantor joins). Phase 0 rebases it onto the ref where the charge work lands.
- Never run `git stash` in any form. Never build or run anything in the main checkout at `G:\source\Fellowship_Data_Extractor` while another session may hold it.
- Never add a comment of any syntax to any file. Never use an em dash or en dash in any output.
- Every export run: `$env:FELLOWSHIP_USMAP = "G:\source\Fellowship_Data_Extractor\output\fellowship.usmap"; dotnet run --project src/FellowshipExtractor` from the worktree. Copy `output/export/data/entities.jsonl` aside before each run so the diff always has a baseline.
- The export diff protocol after every phase: key records by `$type/tag/id` (tag alone collides on three Mara interrupt pairs: `GA_Mara_MeleeInterrupt` is abilities 1167 and 1200, `GE_Mara_MeleeInterrupt_Interrupt` is effects 1642 and 1704, `GE_Mara_MeleeInterrupt_Lockout` is effects 1641 and 1703; `$type/id` alone collides on item variants and talent slots). Tally gained, changed and lost per field; require lost = 0 and changed = only the rows the phase names. A task that adds a field or a document type also asserts that field's fill count or that type's record count explicitly, because the generic diff never looks at fields it does not know.
- Test-failure gate: Phase 0 records the full-suite failure list to `docs/superpowers/plans/baseline-failures.txt` before any change; every later phase's suite run must produce a failure set no larger than that file. The known stale pins include `EntityStoreCorpusTests.TheStoreHolds12501Entities` against an export that already has 12,507 lines.
- The bare tooltip state everywhere: `IsValid` is true, every tag test is false (`HasMatchingGameplayTag`, `HasAnyMatchingGameplayTags`, `HasAllMatchingGameplayTags`, `HasTag`, `HasAnyTags`, `HasAllTags`), `SelectFloat`/`SelectInt` take parameter index 1, a `JumpIfNot(IsValid)` tail returning a constant is a null guard, and a row name never predicts which branch is bare.
- Scripts are PowerShell. Python is not installed.
- New export fields are additive and nullable; `Fellowship.SDK` document types and `DocumentProjection` change together in the same commit as the reader that feeds them.

---

## Phase 0: Rebase and consolidate the evaluators

Do this first, immediately after the charge work merges. Everything later assumes the merged shape.

### Task 0.1: Rebase effect-uidata onto the charge work

**Files:**
- Modify: `src/FellowshipExtractor/Tooltip/TooltipResolver.cs` (one conflict region)

**Interfaces:**
- Consumes: the merged charge work: `Numbers.Split(IEnumerable<CurveBinding>, string?)` with no charge parameters, `MaximumCharges.Of(UObject cdo, ConstantsTable constants, TypeMappings mappings)` returning `record ChargeCounts(double? Charges, IReadOnlyDictionary<string, double>? ByLevelType)` with `ChargeCounts.None = new(null, null)`, `Supplement(split, tuning)`, `GrantedCharges.cs` deleted, `Entity.ChargesByLevelType` and `AbilityDocument.ChargesByLevelType` added, `_mappings = provider.MappingsContainer.MappingsForGame` on the resolver.
- Produces: a branch where both change sets compile and the export carries both.

- [ ] **Step 1: Record the pre-change baselines**

```powershell
Copy-Item output/export/data/entities.jsonl docs/superpowers/plans/pre-rebase-entities.jsonl
dotnet test src/FellowshipExtractor.Tests --nologo 2>&1 |
  Select-String '\[FAIL\]' | ForEach-Object { $_.Line.Trim() } | Sort-Object |
  Set-Content docs/superpowers/plans/baseline-failures.txt
```

- [ ] **Step 2: Establish where the charge work landed, then rebase onto that ref**

As of 2026-08-15, `origin/master` (2814bfc) is NOT an ancestor of local `master` (e40878f) in the main checkout, and the remote tip carries a different commit titled `Interactive Tooltips` than the one local history has. Rebasing onto the wrong ref replays seven commits into an unrelated conflict storm. So:

```powershell
git -C G:\source\Fellowship_Data_Extractor.effect-uidata fetch origin
git -C G:\source\Fellowship_Data_Extractor.effect-uidata log --oneline -3 origin/master
git -C G:\source\Fellowship_Data_Extractor.effect-uidata log --oneline -3 master
```

The ref that contains a commit whose diff removes `GrantedCharges.cs` and adds `MaximumCharges.cs` is the target. Then:

```powershell
git -C G:\source\Fellowship_Data_Extractor.effect-uidata rebase <that ref>
```

- [ ] **Step 3: Resolve the single conflict**

The rebase stops exactly once, at 84cadb8, with one conflict region in `TooltipResolver.Body`. `Numbers.cs` auto-merges (git keeps both the charge branch's deletion of the charge fallback loop and this branch's `None` guard with no marker), and the `var split = Numbers.Split(bindings, tuning);` call site auto-merges to the charge version. The one region to hand-resolve is the Supplement call site; the resolution is this branch's supersedes block followed by the charge branch's two-argument call:

```csharp
var supersedes = appliedDurations
    .Where(binding => Supersedes(binding, tuning))
    .Select(binding => binding.Value!.Value)
    .Distinct()
    .ToArray();

if (supersedes.Length == 1 && !split.Named.ContainsKey(Numbers.Duration))
{
    split.Named[Numbers.Duration] = supersedes[0];
}

Supplement(split, tuning);
```

- [ ] **Step 4: Stage the resolved file by path and continue**

```powershell
git -C G:\source\Fellowship_Data_Extractor.effect-uidata add src/FellowshipExtractor/Tooltip/TooltipResolver.cs
git -C G:\source\Fellowship_Data_Extractor.effect-uidata rebase --continue
```

Stage by path: the worktree carries this plan under `docs/` untracked, and a bare `add -A` would sweep it into the conflict commit.

- [ ] **Step 5: Build, export, verify both change sets survived**

Run: `dotnet build src/FellowshipExtractor.slnx -v q` (0 errors), then the export. Confirm in `output/export/data/entities.jsonl`:
- `GE_Bowguy_SelfBuffForAoeProjectile_SelfBuff` duration 4, `GE_Vigor_SingleTargetAbsorb_Absorb` duration 12, `GE_Meiko_TauntTotem_SelfBuff` duration 20, `GE_Firemage_InstantSingleTargetDot_Dot` duration 30, `GE_Ink_SelfDefenceBuff_SelfBuff_First` duration 4, `GA_Warmaster_MeleeInterrupt` duration 4
- effects with `name` at least 2056, with `description` at least 892; trait-power named 40 of 40; set-bonus-power 15 of 15
- abilities with a `charges` key: expect the count the merged charge code produces (35 was observed on the main checkout's post-fix export). The charge diff maps a count of 1 to null, so records like `GA_Vigor_SingleTargetAbsorb` that carried `charges: 1` on this branch lose the key by design: the `charges` field is exempt from this rebase's lost = 0 gate, and every lost `charges` row must be a 1.

- [ ] **Step 6: Diff against the Step 1 baseline with the appendix script; investigate anything outside the expectations above.**

### Task 0.2: Extract the shared bare-state evaluator

`MaximumCharges.Walk` (charge work) and `MagnitudeCalculations.Walk` (this branch) duplicate the statement loop, jump mapping, local binding, `Truth`, `Number` and the ScalableFloat readers. Consolidate before Phases 1 to 3 add two more consumers.

**Files:**
- Create: `src/FellowshipExtractor/Tooltip/BareStateWalk.cs`
- Modify: `src/FellowshipExtractor/Tooltip/MaximumCharges.cs`
- Modify: `src/FellowshipExtractor/Tooltip/MagnitudeCalculations.cs`
- Test: `src/FellowshipExtractor.Tests/TruthTests.cs`

**Interfaces:**
- Produces: `public sealed class BareStateWalk(UStruct function, UObject cdo, ConstantsTable constants, TypeMappings? mappings)` reading `function.ScriptBytecode`, with `public double? Answer()`, `public IReadOnlyDictionary<string, double>? ByLevelType { get; }`, `public bool? Truth(KismetExpression?, int depth = 0)`, `public double? Number(KismetExpression?, int depth = 0)`, and `public Func<string, InvalidOperationException>? Unreadable { get; init; }`. The `UStruct` (not a bare bytecode array) is required because the level-type path's `Enumerated` scans `function.ChildProperties` for the enum property, and `mappings` feeds `EnumOrdinals.Members`; `DynamicTextFloats.Body` returns only `KismetExpression[]` and cannot serve this constructor, so the charge work's `Compiled(cdo)` helper that returns the `UStruct` export moves into the shared class (or a shared static beside it).
- Throw behavior: when `Unreadable` is set, the walk throws through it at all eleven sites the charge walk throws today: unresolvable branch, unbindable statement, missing row, no return, budget exhaustion, unmapped jump target, the `SelectInt` choice, both `Switched` sites and both `Keyed` sites. `Shape` (the message builder) moves too. When `Unreadable` is null every site returns null.
- Vocabulary: the union of both walks. From the magnitude walk (absent in the charge walk): `Multiply_`/`Add_`/`Subtract_`/`Divide_` with the known-zero multiply, `FClamp`/`Clamp`, `FMax`/`FMin`/`Max`/`Min`, `FFloor`/`FCeil`, `Loge`/`Log`, `Conv_`/`MakeLiteral` passthrough, `SelectFloat`/`SelectDouble`, `Not_PreBool`, `BooleanAND`/`BooleanOR`, numeric comparisons, the six-name tag-test family, `EX_SwitchValue` handled directly inside `Number`, `NamedTruth` applied to `EX_LocalFinalFunction`/`EX_FinalFunction`, and `EX_IntConst`/`EX_ByteConst` read as truth. From the charge walk (absent in the magnitude walk): the per-ordinal level-type `EX_SwitchValue` keying (`Switched`/`Keyed`/`Enumerated` building `ByLevelType`) and the throwing mode. `SelectInt`, `FTrunc` and `Round` exist in both already. Where the walks disagree on a shared member's failure behavior (`Scalable` throws on a missing row in the charge walk, returns null in the magnitude walk), the `Unreadable` switch decides.
- Limits: unify at Budget 1024, MaxDepth 32 (the larger of the two pairs, so nothing that reduced before stops reducing).

- [ ] **Step 1: Write the pinned regression tests before touching the walks**

Create `src/FellowshipExtractor.Tests/TruthTests.cs`, reading the export through the suite's shared fixture the way `AbilityOwnerTests` does (`Export.Loaded.Documents`, each document carrying its serialized JSON as `Body`):

```csharp
public class TruthTests
{
    private static JsonObject Record(string tag) =>
        Export.Loaded.Documents
            .First(document => (string?)document.Body["tag"] == tag)
            .Body;

    [Theory]
    [InlineData("GE_Bowguy_SelfBuffForAoeProjectile_SelfBuff", 4)]
    [InlineData("GA_Bowguy_SelfBuffForAoeProjectile", 4)]
    [InlineData("GE_Vigor_SingleTargetAbsorb_Absorb", 12)]
    [InlineData("GE_Meiko_TauntTotem_SelfBuff", 20)]
    [InlineData("GE_Firemage_InstantSingleTargetDot_Dot", 30)]
    [InlineData("GE_Ink_SelfDefenceBuff_SelfBuff_First", 4)]
    [InlineData("GA_Warmaster_MeleeInterrupt", 4)]
    public void ADurationIsWhatTheCalculationReturnsBare(string tag, double expected) =>
        Assert.Equal(expected, (double?)Record(tag)["duration"]);

    [Theory]
    [InlineData("GE_Bowguy_CastedProjectileDamage_Buff", "Steady Hands")]
    [InlineData("GE_Finesse_CurrentSpiritToBuff_Buff", "The Philosopher")]
    [InlineData("GE_Perk_MaxHealthBasedAbsorb_Absorb", "The Dragon's Embrace")]
    public void AnEffectShipsItsOwnUiDataName(string tag, string expected) =>
        Assert.Equal(expected, (string?)Record(tag)["name"]);
}
```

If `EntityDocument.Body` is typed differently than `JsonObject`, copy the exact access idiom from `AbilityOwnerTests` and keep the assertions.

- [ ] **Step 2: Run the new class, expect green.** These pins now guard the consolidation.

- [ ] **Step 3: Move the shared members into BareStateWalk**

Lift the statement loop, `At`, `Bind`, `Bound`, `Truth`, `Number`, `Called`, `Switched`, `Scalable`, `Struct`, `Inline`, `Named` and `Shape`, per the interface block above. Reduce `MaximumCharges.Of` and `MagnitudeCalculations.Of` to thin adapters: the charge adapter passes `Unreadable` and reads `ByLevelType`; the magnitude adapter passes null `mappings` and no `Unreadable`.

- [ ] **Step 4: Build, export, and compare**

```powershell
Compare-Object (Get-Content $before | Sort-Object) (Get-Content output/export/data/entities.jsonl | Sort-Object)
```

Byte parity is the expectation, not an axiom: the unified limits can let a body reduce that previously hit the charge walk's Budget 512 or MaxDepth 24. Read every differing line; a difference is acceptable only when it traces to the limit unification and the new value re-derives by hand from the body.

- [ ] **Step 5: Run TruthTests and the full suite** (failure set within `baseline-failures.txt`), **commit**

```powershell
git add src; git commit -m "Share one bare-state walk between charges and magnitudes"
```

---

## Phase 1: Cost truth

The export's cost is chosen today by `EndsWith("cost")` first-match over CDO property names, which publishes a talent tier price or a proc stack count as the cast cost. The authoritative chain is the ability's own cost effect, adjusted by `ModifyAbilityCostForUI` where a class overrides it, with the ability's own namespace rows as the fallback source.

Known wrong today, the acceptance list: `GA_Vigor_SingleTargetAbsorb` (996), `GA_Vigor_SingleTargetChargedHot` (995), `GA_Vigor_SingleTargetDot` (994) export cost 2 where the bare cost is 1 (`Vigor.*.ResourceFractionCost` is 1 and their `ModifyAbilityCostForUI` bodies return literal 1); `GA_Lisa_InstantSingleTargetDamageAndDot` (1887) exports 10 where the baseline `Lisa.InstantSingleTargetDamageAndDot.Cost` is 9; `GA_Sune_DroppedPulseAoe` exports 8 (a ProcStackCost) where `Sune.DroppedPulseAoe.ManaCost` is 218; talent 568 inherits the wrong 8. `GA_Ink_AoeAttack` exports 0.5 which is a fraction of max Fury (`Ink.AoeAttack.Cost` comment reads "Percentage of Max Rage").

Cost-source facts the implementation must respect, established during plan verification: at least 25 of the 102 dumped cost-exporting abilities declare no `CostGameplayEffectClass` at all, and the namespace leaves in live use include `ResourceFractionCost`, `SpiritPointCost`, `ComboPointCost`, `EnergyCost`, `YellowEnergyCost`, `GreenEnergyCost`, `YellowResourceCost`, `OrbCost`, `BombCost`, `HpCost` and `HealthCostMaxHealthFraction`, so the namespace rule matches by shape, never by a hardcoded leaf list.

### Task 1.1: Settle ModifyAbilityCostForUI semantics from the binary

The override's return feeds the UI either as the cost itself or as a multiplier on a base. The distinction changes `GA_Firemage_CastedDotDetonate`, whose only base is the namespace row `Firemage.CastedDotDetonate.BombCost` (1): under replacement the override's `BombCostFractionRef` read is the answer, under multiplier it multiplies the row. This task runs before Task 1.2 ships.

**Files:**
- Create: `scratch/CostSemantics` in the scratchpad, following the HookProbe pattern: reference `PeImage.cs` and `ReflectionScanner.cs` from `src/FellowshipExtractor/Usmap` plus Iced for disassembly.

- [ ] **Step 1: Anchor the name.** Find the ANSI literal `ModifyAbilityCostForUI` in `fellowship-Win64-Shipping.exe` (present, established by the audit's string scan), the `lea` referencing it, the FName global's sole reader, and that thunk's caller.
- [ ] **Step 2: Read the call site.** A `mulsd` against the base cost means multiplier; a store over it means replacement. Record the finding with addresses in `docs/cost-semantics.md`.
- [ ] **Step 3: Encode the finding** in `AbilityCosts.Adjusted` (Task 1.2). The three Vigor abilities read 1 either way (their base reaches 1 through the cost effect); CastedDotDetonate reads 1 either way once the namespace rule of Task 1.2 covers `BombCost` (1 as replacement, 1 x 1 as multiplier); the disassembly settles which expression ships.

### Task 1.2: Source cost from the cost effect chain

**Files:**
- Create: `src/FellowshipExtractor/Tooltip/AbilityCosts.cs`
- Modify: `src/FellowshipExtractor/Tooltip/TooltipResolver.cs` (Body: claim cost before Supplement, the same shape as the duration supersedes block)
- Modify: `src/FellowshipExtractor/Tooltip/Numbers.cs` (retire the `EndsWith("cost")` property fallback)
- Test: `src/FellowshipExtractor.Tests/TruthTests.cs`

**Interfaces:**
- Produces: `public static class AbilityCosts` with `public static double? Of(UObject cdo, AbstractFileProvider provider, ConstantsTable constants, string? tuning)`.
- Consumes: `BareStateWalk` for `ModifyAbilityCostForUI` and for any `CustomCalculationClass` magnitude on the cost effect.

- [ ] **Step 1: Add the failing pins to TruthTests**

```csharp
[Theory]
[InlineData("GA_Vigor_SingleTargetAbsorb", 1)]
[InlineData("GA_Vigor_SingleTargetChargedHot", 1)]
[InlineData("GA_Vigor_SingleTargetDot", 1)]
[InlineData("GA_Lisa_InstantSingleTargetDamageAndDot", 9)]
public void ACostIsTheCastCostNotATalentRow(string tag, double expected) =>
    Assert.Equal(expected, (double?)Record(tag)["cost"]);
```

Run and confirm they fail against the current export (2, 2, 2, 10).

- [ ] **Step 2: Implement the chain**

```csharp
public static double? Of(UObject cdo, AbstractFileProvider provider, ConstantsTable constants, string? tuning)
{
    var stated = FromCostEffect(cdo, provider, constants) ?? FromNamespace(constants, tuning);

    return Adjusted(cdo, constants, stated);
}
```

`FromCostEffect` loads `CostGameplayEffectClass`, takes the first modifier whose magnitude resolves: a `ScalableFloatMagnitude` handle through `constants.Lookup`, or a `CalculationClassMagnitude` through the magnitude machinery. Many cost effect CDOs serialize zero properties (`CRCostEffect` is native with nothing to inherit) and at least a quarter of cost-exporting abilities declare no cost effect at all; both cases return null and the namespace rule carries them. `FromNamespace` accepts any row directly under the ability's namespace whose leaf, normalized, ends in `cost`, excluding any row containing a `Talent` or `Talents` segment, and returns null when the surviving candidates disagree on their value. `Adjusted` evaluates `ModifyAbilityCostForUI` under the bare state when the class compiles it, applying the Task 1.1 semantics.

- [ ] **Step 3: Retire the property-name fallback**

In `Numbers.Field`, delete the `EndsWith(CostSuffix)` return so a CDO property spelling can no longer claim cost; in `Body`, claim `split.Named["cost"]` from `AbilityCosts.Of` before `Supplement` runs. The namespace shape rule lives only in `AbilityCosts.FromNamespace` now.

- [ ] **Step 4: Export and read every cost change**

The acceptance rows change (2 to 1 three times, 10 to 9); `GA_Sune_DroppedPulseAoe` resolves to whatever its cost effect states, with 218 and 8 the candidates. Abilities whose only source is a non-Talent namespace cost row (the Warmaster and Meiko melee families among the 25 with no cost effect) keep their values; a lost cost anywhere fails the phase.

- [ ] **Step 5: Fix the talent inheritance**

In `Fill`, a merged number keeps its granting ability's provenance, and two granted abilities that disagree merge nothing, the same contest rule `EffectNameSources` uses. Talent 568 then reports the corrected cost or none.

- [ ] **Step 6: Run TruthTests and the suite, commit**

```powershell
git add src; git commit -m "Source ability cost from the cost effect chain"
```

### Task 1.3: Fractional costs

**Files:**
- Modify: `src/FellowshipExtractor/Tooltip/TooltipResolver.cs`, `src/Fellowship.SDK/Models/Entity.cs`, `src/Fellowship.SDK/Documents/AbilityDocuments.cs`, `src/FellowshipExtractor/Tooltip/DocumentProjection.cs`

- [ ] **Step 1: Find the flag's static source.** `FAbilityTooltipData` carries `IsCostInPercentage` and `CostType`. Dump `GA_Ink_AoeAttack` plus two absolute-cost abilities; grep the CDO JSON for `CostType`, `AbilityCostType`, `Percentage`; record which property distinguishes them and its spelling.
- [ ] **Step 2: Export `costIsFraction` and `costType`.** Add both to Entity and AbilityDocument (bool? and string?), `costIsFraction` projected only when true, `costType` as the enum member name through `EnumOrdinals` when the property exists. Pin `GA_Ink_AoeAttack` cost 0.5 with `costIsFraction` true.
- [ ] **Step 3: Export; assert the fill counts of both new fields in the diff step** (`costIsFraction` at least 1; `costType` count recorded in the commit message), **commit.**

---

## Phase 2: The tooltip payload arguments

`GetDynamicTooltipData` (45 classes) feeds the card through four native helpers whose arguments are the displayed values. The reader takes the argument expressions instead of guessing property names.

Failing-pin acceptance list: `GA_Gunde_Talent_DotTransfer_Aoe` (2280) range becomes 1000 (bare branch of `SelectFloat(MaxRange 2000, talent radius 1000, HasTag SavedDotDamage.SelfBuff)`); `GA_Mount_MountUp_Base` (3) gains castTime 1.5 (`CastingTaskData` fed to the casting helper, row `PlayerAction.Mount.CastingDuration`); `GA_Warmaster_InstantMeleeCleave` (981) gains range 500 (`CleaveRadiusRef`, row `Warmaster.InstantMeleeCleave.Damage.CleaveRadius`).

Parity guards, already correct on this branch through the curve-less value read and required to stay correct once the hook becomes the authority: `GA_Bowguy_ChanneledMultiProjectileDebuff` (1312) channelTime 2 (the hook computes it through `Get Channel Duration` plus `SetDynamicDuration`; the struct member it would otherwise read is stale by construction) and `GA_Rime_ChanneledBeamSingleDamage` (1027) channelTime 2 (a MakeStruct override).

### Task 2.1: The hook reader

**Files:**
- Create: `src/FellowshipExtractor/Tooltip/TooltipPayload.cs`
- Modify: `src/FellowshipExtractor/Tooltip/TooltipResolver.cs` (Body: prepend the payload's bindings the way Computed durations already prepend)
- Test: `src/FellowshipExtractor.Tests/TruthTests.cs`

**Interfaces:**
- Produces: `public static class TooltipPayload` with `public static IReadOnlyList<CurveBinding> Bindings(UObject cdo, ConstantsTable constants)`. Each returned binding carries `Computed = true` and a Property spelled so `Numbers.Field` claims the right slot: `CastingData.CastingDuration` for cast, `ChannelingData.ChannelingDuration` for channel, `ChannelingData.ChannelingTickInterval` for tick, `MaxRange` for range.
- Consumes: `BareStateWalk` extended with same-class virtual dispatch: a `LocalVirtualFunction` whose name matches another `UStruct` export in the same package evaluates that body inline, one level deep.

- [ ] **Step 1: Pin the three failing rows and the two parity guards**

```csharp
[Theory]
[InlineData("GA_Gunde_Talent_DotTransfer_Aoe", "range", 1000)]
[InlineData("GA_Mount_MountUp_Base", "castTime", 1.5)]
[InlineData("GA_Warmaster_InstantMeleeCleave", "range", 500)]
[InlineData("GA_Bowguy_ChanneledMultiProjectileDebuff", "channelTime", 2)]
[InlineData("GA_Rime_ChanneledBeamSingleDamage", "channelTime", 2)]
public void TheTooltipHookArgumentIsTheDisplayedValue(string tag, string field, double expected) =>
    Assert.Equal(expected, (double?)Record(tag)[field]);
```

Run: the first three fail (range 2000, castTime absent, range absent), the two channel rows already pass and stay as guards.

- [ ] **Step 2: Record the four helper signatures from the bodies.** Walk the 45 bodies with a probe: for each call to `AddDynamicCastingInfoToAbilityTooltipData`, `AddChannelingInfoToAbilityTooltipData`, `AddDynamicChannelingInfoToAbilityTooltipData`, `AddDynamicRangeInfoToAbilityTooltipData`, record the parameter positions of the value-bearing arguments (the casting and channeling helpers take a struct property, the range helper takes two evaluated doubles). Write the observed positions into the reader as constants with the class counts beside them: casting 29, channeling 4, dynamic channeling 2, range 9.

- [ ] **Step 3: Implement.** For struct-carrying helpers, resolve the argument to a CDO property name and read `CastingDuration` or `ChannelingDuration`/`ChannelingTickInterval` from that struct through the existing curve-and-value logic, honoring `SetDynamicDuration`: when the body calls it before the helper, the evaluated expression wins over the struct member. For value-carrying helpers, evaluate the expressions with `BareStateWalk`. For a MakeStruct body (Rime), read the `ChannelTime` member expression from the `K2Node_MakeStruct_AbilityTooltipData` assignment.

- [ ] **Step 4: Add `minRange`** to Entity, AbilityDocument and the projection, fed from the range helper's MinRange argument. All nine calls pass 0 today; the field exists so a nonzero one is expressible.

- [ ] **Step 5: Export and read every changed row.** The Gunde range change is expected; any other change must re-derive from its hook body before accepting. Run TruthTests, commit.

### Task 2.2: The remaining payload members

**Files:**
- Modify: `src/Fellowship.SDK/Documents/AbilityDocuments.cs`, `src/Fellowship.SDK/Models/Entity.cs`, `src/FellowshipExtractor/Tooltip/TooltipResolver.cs`, `src/FellowshipExtractor/Tooltip/DocumentProjection.cs`

- [ ] **Step 1: Establish the CDO property names.** Dump `GA_Vigor_SingleTargetAbsorb`, `GA_Mara_ChargedTeleportBehindTarget` and two NPC abilities; grep for `RequiresActiveTarget`, `AllowSelfTarget`, `IsChargeUp`, `ChargeUp`. Record which properties exist and their spelling across CDOs.
- [ ] **Step 2: Export the flags.** `requiresActiveTarget`, `allowSelfTarget`, `isChargeUp` as bool? on ability documents, emitted only when true, read by the exact property names from Step 1. Export; assert each flag's fill count explicitly in the diff step; commit.

---

## Phase 3: Placeholder truth

The singular `ResolveDynamicTextFloat` reader names a property and publishes its raw row value; the arithmetic around the read is dropped. The Range path's `Terms` evaluator already models attribute multiplication and Expertise. Unify them.

Acceptance list: `{VoidTouchDmg}` on `GA_Weapon_InstantDamageAccumulativeDebuff` becomes coefficient 42.5 of PrimaryAttribute with Expertise, not flat 42.5; `{IntimidateDmgBreak}` on `GA_Ink_AoeFear` becomes coefficient 30 of Strength; `{SolarShldAbsrb}` on `GA_Sune_SingleTargetAbsorb` becomes coefficient 22.998 of Strength with Expertise; `{StoneShieldHealth}` on `GA_Meiko_DamageSoakBuff` and `{MaxToughness}` on `GA_Warmaster_MeleeCleave` stop exporting bare. On the Range path, six classes currently fail and must parse: `GA_Lisa_ChanneledHealOrDamageProjectiles` (switch used as the call argument), `GA_Meiko_InstantSelfDefensive` and `GA_Weapon_InstantChargedProjectileHealOrDamage` (`Conv_IntToDouble` wrapping), the two Rime SpendersSwap classes (`Conv_IntToDouble` first, `GetFloatAttributeBase` in the MultiTargets variant), and `GA_Rime_CastedProjectileDamageOrbSpender`.

### Task 3.1: Run Terms over the singular path

**Files:**
- Modify: `src/FellowshipExtractor/Tooltip/DynamicTextFloats.cs`
- Modify: `src/FellowshipExtractor/Tooltip/TooltipResolver.cs` (Declared)
- Test: `src/FellowshipExtractor.Tests/TruthTests.cs`

- [ ] **Step 1: Pin the three wrong placeholders as failing tests** (assert `coefficient` present and `value` absent on those placeholder entries).
- [ ] **Step 2: Widen Terms**: `Conv_` passthrough, `GetFloatAttributeBase` treated as `GetFloatAttribute`, `Round`/`FTrunc` passthrough, `Subtract_` and `Divide_` support, a `Raised` generalization beyond the Expertise-unity idiom to any additive combination whose parts resolve, `EX_SwitchValue` supported inside `Name` when it wraps the scalable read's argument, and case-term matching through `EX_Cast` and one arithmetic node.
- [ ] **Step 3: Multi-term bodies**: extend `Placeholder` with `Terms` (list of attribute plus coefficient pairs) and a flat addend for bodies combining two or more reads across different attributes; `StoneShieldHealth` and `MaxToughness` reduce into it.
- [ ] **Step 4: Export, diff the `placeholders` arrays per record key** (the acceptance list changes, nothing else), run TruthTests, commit.

### Task 3.2: The spread a row declares

**Files:**
- Modify: `src/FellowshipExtractor/Tooltip/Spread.cs`
- Test: `src/FellowshipExtractor.Tests/TruthTests.cs`

`Spread.For` reads the ancestor scope's `.Spread` row and then returns `presets.Band` (the single global 0.2) instead of the value it just read, so 0.4 and 0.1 rows all publish 0.2. The audit left this plausible because no coefficient was traced end to end; this task settles it before changing anything.

- [ ] **Step 1: Trace one coefficient end to end.** Pick a row family declaring `.Spread` 0.4 from `ability-constants.jsonl`, find the exported coefficient it governs, and record whether the export says 0.2. If the export already says 0.4 the finding was wrong; record that in the commit message and end the task.
- [ ] **Step 2: Return the declared value** from `Spread.For`, using `presets.Band` only when no ancestor row declares one. Skull Crusher guards the other direction: its rows declare 0.1 and 0.2 while the game prints plus and minus 10 percent for both, and that band comes from `ResolveDynamicTextFloatRange`'s literal 0.9 and 1.1, which the Range reader already prefers; the declared `.Spread` only reaches placeholders with no Range function.
- [ ] **Step 3: Export, diff spreads per record key, read every change against its declaring row, run, commit.**

---

## Phase 4: Effect modifier magnitudes

39 reducible multiplier-shaped calculations and every plain modifier magnitude have no field. Give the effect document a modifiers list.

### Task 4.1: The modifiers field

**Files:**
- Modify: `src/FellowshipExtractor/Tooltip/GameplayEffects.cs`, `src/Fellowship.SDK/Models/Entity.cs`, `src/Fellowship.SDK/Documents/EffectDocument.cs`, `src/FellowshipExtractor/Tooltip/DocumentProjection.cs`
- Test: `src/FellowshipExtractor.Tests/TruthTests.cs`

**Interfaces:**
- Produces: `EffectDocument.Modifiers`: list of `{ attribute, op, value?, coefficient?, scalesOff? }` where op is the `EGameplayModOp` member name read through `EnumOrdinals`, value is a bare-state scalar (ScalableFloat row or evaluated calculation class), and coefficient plus scalesOff carry the attribute-scaled shape when the calculation multiplies a captured attribute.

- [ ] **Step 1: Pin two known magnitudes as failing tests**: `GE_Vigor_SingleTargetAbsorb_Absorb` carries a modifier on `AbsorbConsumptionScaler` valued 0.5, and `GEC_BlockDamageReductionMultiplier`'s effect carries 0.8 from `Global.Block.IncomingDamageMultiplier`.
- [ ] **Step 2: Implement** over `Modifiers[]`: attribute from `Attribute.AttributeName`, op via `EnumOrdinals`, magnitude by kind (ScalableFloat through constants, CustomCalculationClass through the magnitude machinery, AttributeBased to a null value with scalesOff populated).
- [ ] **Step 3: Give the talent-gated variants a home first, then fill it.** `EffectDocument` gains `Talents` (the same dictionary shape `Entity.Talents` already carries) and `DocumentProjection` copies it; then re-evaluate each calculation with exactly one tag assumed true per gate the body tests (`BareStateWalk` gains `AssumedTags`), emitting differing results keyed the way `Numbers.SplitTalents` keys them.
- [ ] **Step 4: The Amethyst token** (established during the UIData fix, outside the audit): when a description writes a token that `DynamicDescription.Tokens` does not declare and the effect has exactly one modifier with a resolved value, synthesize that placeholder from the modifier. Pin `GE_DungeonModifier_AnomalousOrbs_Buff_Amethyst`'s `{Increase}`.
- [ ] **Step 5: Random-range durations**: `GMMC_Mara_AoeAttackBuilder_Poison_DurationManager` returns `RandomFloatInRange(6, 8)`; give the modifier entry optional `valueLow`/`valueHigh` and emit the pair, so the one calculation whose answer is a range is expressible.
- [ ] **Step 6: SetByCaller durations**: for the 37 effects whose `DurationMagnitude` is SetByCaller, scan the applying ability's bytecode for `AssignTagSetByCallerMagnitude` with the `SetByCaller.Duration` tag and an evaluable magnitude; emit the duration where it reduces, and record the recovered count in the commit message.
- [ ] **Step 7: Export; assert the `modifiers` fill count explicitly; run; commit.**

---

## Phase 5: The remaining text sources

### Task 5.1: Per-hero dynamic text

**Files:**
- Create: `src/FellowshipExtractor/Tooltip/DynamicTextRows.cs`
- Modify: `src/FellowshipExtractor/Tooltip/TooltipResolver.cs`, `src/Fellowship.SDK/Documents/TraitPowerDocument.cs`, `src/FellowshipExtractor/Tooltip/DocumentProjection.cs`

Six trait-power records export the literal string `{uniqueHeroDescription}`. The sentence lives in the 12 `DT_DynamicText_Hero_<Codename>` tables (row struct `CRDynamicTextDataRow`, 6 shared row keys matching the `CAA_Perk_*` tags minus prefix wrinkles such as `Perk_IncreasedDurationOrReducedCooldown`).

- [ ] **Step 1: Reader** following the `ItemTraits` constructor pattern over `TableFinder.ByRowStructure(assets, "/Script/fellowship.CRDynamicTextDataRow")`, keyed (heroCodename, rowKey), value `DynamicText.Text`.
- [ ] **Step 2: Field**: `TraitPowerDocument.DescriptionsByHero`, a map from codename to the sentence with that hero's constants substituted into the sentence's own tokens such as `{increasedDuration}` (the per-hero overrides like `Mosse.ReducedCooldownInSeconds` already sit in the record's placeholder constants). The base `Description` stays.
- [ ] **Step 3: Pin `CAA_Perk_IncreasedDurationOrReducedCooldown` carrying a Bowguy sentence containing "duration is increased"; export; assert `descriptionsByHero` fill count 6; commit.**

### Task 5.2: Modifier documents

**Files:**
- Create: `src/FellowshipExtractor/Tooltip/ModifierInfo.cs`, `src/Fellowship.SDK/Documents/ModifierDocument.cs`
- Modify: `src/FellowshipExtractor/Tooltip/TypeResolver.cs`, `src/FellowshipExtractor/Tooltip/TooltipResolver.cs`, `src/FellowshipExtractor/Tooltip/DocumentProjection.cs`

The 28-id `Modifiers` tag table has zero export presence; `DT_ModifierInfo` (`CRModifierInfoDataRow`) carries DisplayName, Description, UIIcon, Type and MinimumHeroRankRequired per row, joined on `ModifierID.TagName`.

- [ ] **Step 1: Reader plus a `modifier` document type** (id, name, tag, description, icon, kind, minimumHeroRank), resolved for every `Modifiers` table tag.
- [ ] **Step 2: Pin `Modifiers.Ascension.Ascension1` naming "Sahril's Madness"; export; assert 28 `modifier` records; commit.**

### Task 5.3: Progression, customization, pet and mount text

**Files:**
- Create: `src/FellowshipExtractor/Tooltip/ProgressionItems.cs`, `src/FellowshipExtractor/Tooltip/CustomizationItems.cs`
- Modify: `src/FellowshipExtractor/Tooltip/TooltipResolver.cs`, `src/Fellowship.SDK/Documents/ItemDocuments.cs`, the companion-pet and mount documents, `src/FellowshipExtractor/Tooltip/DocumentProjection.cs`

- [ ] **Step 1: DT_HeroProgressionItems** (`CRProgressionItemDataRow`, 5 rows): name, description, icon, rarity onto the existing item records for NetIds 1629, 1630, 1631, 1742, 2110. Pin "Talent Points" on 1630.
- [ ] **Step 2: The five DT_PlayerProfile tables**: rows keyed by the full `ItemID.PlayerCustomization.*` tag; description, rarity, icon, plus `ItemDocument.Acquire` from `AcquireDescription`. Sources differ per table: the Title rows carry no DisplayName, so both the record's name and the `titleText` field come from the same `TitleText` property; the Avatar, AvatarFrame, Banner and NameColor tables' name sources are read from their row structs at implementation time and recorded in the reader. 100 of the 271 tags have rows. Pin `ItemID.PlayerCustomization.Title.DefaultFellow` naming "Fellow" with an acquire line.
- [ ] **Step 3: Pet and mount flavor**: companion-pet documents gain `Acquire` from the pet asset's `UnlockCriteria` (pin Azure George's "Complete Paragon league as a Tank hero."); mount documents gain `Lore` from locres `ST_Items`/`ST_Season3` keys `Mount_<TagSuffix>_Description` and `_DescriptionLong` where the key suffix matches the mount tag (pin Guardian Ghostclaw's lore line containing "spirit-bear").
- [ ] **Step 4: Export; assert record and fill counts for each addition (5 progression names, 100 customization records, pet acquire 6, mount lore count recorded); commit.**

### Task 5.4: Widen effect-name borrowing through the applier scan

**Files:**
- Modify: `src/FellowshipExtractor/Tooltip/EffectNameSources.cs`, `src/FellowshipExtractor/Tooltip/AppliedEffects.cs`, `src/FellowshipExtractor/Tooltip/TooltipResolver.cs`

`GameplayEffects.ReferencedIn` walks CDO properties only; `AppliedEffects.Named` (already computing `appliedBy`) also walks the other package exports and `EX_ObjectConst` literals in bytecode. Up to 150 currently nameless effects have a named applier only the wider scan sees.

- [ ] **Step 1: Widen the scan.** `EffectNameSources`' constructor gains the provider and an asset-path resolver (the `FAssetData` lookup the resolver already holds) so it can call `AppliedEffects.Named` per tag; no construction reordering is needed (`AbilityOwners` is built after it). `AppliedEffects` holds a mutable static memo (`Reachable`, keyed by type, filled by `Reaching`, unsynchronized); the resolver constructor is single-threaded today, and this step adds a one-line lock around the memo so the assumption is enforced rather than inherited.
- [ ] **Step 2: Export, diff names.** Expect up to 150 gained, and expect some previously agreed names to become contested as the wider scan adds disagreeing edges; read every newly contested case and confirm the contest rule nulls only genuinely ambiguous ones. Commit.

### Task 5.5: Hero documents

**Files:**
- Create: `src/FellowshipExtractor/Tooltip/HeroLocres.cs`, `src/Fellowship.SDK/Documents/HeroDocument.cs`
- Modify: `src/FellowshipExtractor/Tooltip/TooltipResolver.cs`, `src/FellowshipExtractor/Tooltip/DocumentProjection.cs`, `src/FellowshipExtractor/Tooltip/TypeResolver.cs`

- [ ] **Step 1: A `hero` document per CharacterID roster entry** (skip variant tags with a further dot): codename, shipped name (already resolved by `HeroNames`), title from locres `ST_Main` key `Hero_<Codename>_Title`, main stat, color. The locres file is already parsed by the `locres` command; lift the lookup into a shared reader rather than re-parsing.
- [ ] **Step 2: Prose where clean keys exist**: Gunde's `Hero_Gunde_Biography`, `_Playstyle` and `_SpiritAbilityDescription` populate `biography`, `playstyle` and `spiritAbility`; the same three fields populate for any other hero the moment a clean key or an established GUID join supplies them.
- [ ] **Step 3: Pin Gunde's title "The Northlander" and Firemage's "The Pyromancer"; export; assert 12 `hero` records; commit.**

---

## Phase 6: Re-pin the corpus

- [ ] **Step 1: Regenerate every pinned count** in `FellowshipExtractor.Tests` (`EntityStoreCorpusTests`, `EntityDocumentJsonTests`, `TypeResolverTests`) and `Fellowship.Codex.Tests` (`NamedTests`) from the Phase 5 export: total lines, per-type counts including the new `modifier` and `hero` types, and any fixture assertions the earlier phases changed.
- [ ] **Step 2: Run both full suites. Expected: zero failures.** Any residual failure is a real regression to fix before this commit.
- [ ] **Step 3: Commit; the branch is ready for PR.**

---

## Verification appendix

The per-phase diff script pattern, keyed to avoid every known collision:

```powershell
$old = @{}
Get-Content $baseline | ForEach-Object { $j = $_ | ConvertFrom-Json; if ($j.'$type' -in 'ability','effect') { $old["$($j.'$type')/$($j.tag)/$($j.id)"] = $j } }
$changes = @{}; $issues = @()
Get-Content output/export/data/entities.jsonl | ForEach-Object {
  $j = $_ | ConvertFrom-Json
  if ($j.'$type' -notin 'ability','effect') { return }
  $k = "$($j.'$type')/$($j.tag)/$($j.id)"
  if (-not $old.ContainsKey($k)) { return }
  $o = $old[$k]
  foreach ($f in 'castTime','channelTime','channelTick','cooldown','range','minRange','radius','duration','period','cost','costIsFraction','costType','charges','maxStacks','infinite','name','description','icon') {
    if ($o.$f -eq $null -and $j.$f -ne $null) { $changes["$f gained"]++ }
    elseif ($o.$f -ne $null -and $j.$f -eq $null) { $issues += "LOST $k $f ($($o.$f))" }
    elseif ($o.$f -ne $null -and $j.$f -ne $null -and "$($o.$f)" -ne "$($j.$f)") { $changes["$f changed"]++; $issues += "CHANGED $k ${f}: $($o.$f) -> $($j.$f)" }
  }
}
$changes.Keys | Sort-Object | ForEach-Object { "$_ : $($changes[$_])" }
$issues
```

Acceptance for every phase: `LOST` empty (Phase 0 waives `charges` rows whose old value was 1), `CHANGED` exactly the rows the phase names, the phase's own record-count and fill-count assertions hold, TruthTests green, and the full-suite failure set within `baseline-failures.txt` (empty after Phase 6).
