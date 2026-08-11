# Finding the source of truth for a computed number

A number the game prints and no asset carries is not a mystery to be fitted. It is a value some code
computes, and that code is in the build. This is the order to look in, and the traps that cost a
session of screenshot fitting before the answer turned out to be four instructions.

## The ladder

Work down it. Each rung tells you what the next one is, so skipping a rung means guessing at it.

**1. The shipped `.ini` files name the implementation.** Extract them all with
`dotnet run --project src/FellowshipExtractor -- raw ".ini"` and grep the 140 of them. A
`UDeveloperSettings` subclass has a section named for it, and its entries hand you asset paths and
class paths. `[/Script/fellowship.CRItemSettings] StatPoolCalculation=...HeroItemStatCalculation_C`
is how you learn which class a system runs through. `CRItemStaticDataSubsystem` names every table it
loads. Values that are not in any `.ini` are compiled in, which is a finding, not a dead end.

**2. The reflection schema enumerates the complete input set.** The usmap gives every property of
every class. Read the settings class for the system and you have an exhaustive list: for the dynamic
item stats, `CRDynamicItemSettings` names eight things and there is no ninth. That converts "I have
looked in a lot of places" into "these are all the places". Do this before searching the asset tree,
because it tells you when to stop.

**3. The assets those two rungs point at.** `dump <name>` for a DataTable, CurveTable or DataAsset.
Read the rows, not the file name.

**4. The binary, for anything the first three do not carry.** See below. This is ordinary work, not a
last resort, and it is faster than fitting.

## When to stop fitting and go to the binary

Two signals, either is sufficient:

- You have read every input the schema enumerates and a residual remains. The residual is code.
- The runtime struct carries the **inputs** but not the **output**. `CRHeroItemInstance` stores
  `Level`, `ItemRarity`, `TimesUpgraded` and `DynamicSlots` and no magnitudes, so the client computes
  the printed number and the arithmetic is in the client.

A model fitted to observations will fit those observations and still be wrong. Compensating errors
cancel: three independent mistakes agreed to within 1 per cent on ratings, 6 per cent on the wearer's
attribute and 12 per cent on Stamina, and only the third was visible.

## Three traps that each cost hours

**An omitted property is the C++ constructor default, not zero.** Unversioned serialization writes
only what differs from the class default object, so a field absent from a cooked asset equals whatever
the constructor set. `SecondaryBaseMultiplier` reads as absent and is 72. `PrimaryWeight` reads as
absent and is 0.25. Treating absent as zero, or inventing a fallback of 1, produces a formula that
cannot be right. When a block ships three of eight fields, the other five are live values you have not
read yet.

**A function that exists, is named correctly, and is wired by configuration may still never be
called.** `HeroItemStatCalculation` defines `CalcPrimaryStatPoolFromItemLevel`, and
`CRItemSettings.StatPoolCalculation` points at that very class. The magnitude path calls neither: it
calls the natives `PrimaryStatPowerFromItemLevel` and `SecondaryStatPowerFromItemLevel` on the same
object. Confirm the caller, never the name. `blueprint-functions.jsonl` shows which Blueprint functions
call what; if nothing in it computes your number, the computation is native.

**Ground truth is annotated by a human and the annotation can be wrong.** Two readings recorded as
"declared plus one roll" were declared plus **two** rolls, and one line recorded as carrying a roll
carried none. Let the recovered formula tell you the decomposition; only the printed total is observed.

## Reading the binary

`fellowship-Win64-Shipping.exe` is stripped, so there are no symbols, but UE leaves enough behind.

Tooling already exists. `src/FellowshipExtractor/Usmap/PeImage.cs` maps sections and virtual
addresses and `ReflectionScanner.cs` walks property registration records. Add `#:package Iced@1.21.0`
to a file-based app for an x64 decoder. A harness that disassembles at a virtual address and finds
cross references to one is an hour of work and pays for itself immediately.

The anchor chain that works:

1. Find the UTF-16 literal for a function or property name in `.rdata`.
2. Find the `lea` that references it. It sits in a small stub that constructs an `FName` into a global.
3. Find the sole reader of that global. That is the thunk which resolves the function by name.
4. Find the thunk's callers. You are now inside the routine that wanted it.

For a field rather than a function, take the offset straight from the reflection record and search for
loads at that displacement. Four independent offsets from one record set matching one function's memory
accesses is what turns "this looks like the item instance" into an identification.

Verify a recovered routine by porting it to C# and running it against every reading at once. A port
that reproduces all of them with no tuned constant is the proof; a port with a free parameter solved
for is not, and the two must be reported differently.

## Delegating it

This suits parallel subagents well, because the anchor strategies are independent. What made it work:
one agent building the disassembler harness while others chased anchors; one lead per agent with an
explicit instruction that a dead end is an acceptable answer and that guessing a formula from the
numbers would corrupt the evidence; then adversarial verification on separate lenses, where the
fidelity lens re-decodes every cited address rather than trusting the summary. The refutation lens
found a second gate the synthesis had missed, and the fidelity lens found a double rounding in the
port. Neither would have surfaced from a single pass.
