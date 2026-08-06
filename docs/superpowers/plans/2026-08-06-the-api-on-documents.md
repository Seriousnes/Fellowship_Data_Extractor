# The API on Documents Implementation Plan

The extractor now decides what type every row is and writes a typed document per row.
`docs/superpowers/specs/2026-08-05-entity-types-models-and-postgres-design.md` is the authority for
every count and every rule this plan cites; section 7 is the section it implements.

`EntityStore` still reads `entities.jsonl` into `Entity`, so the app does not start against the
export the extractor now writes. This plan moves the API onto the documents: the store reads them,
`PublicTypes` keys on `EntityType`, and every surface downstream reads the reader's reading off the
model rather than deriving it per request.

The store reads the JSONL file. Postgres lands after this plan, and section 7's store then reads
Postgres through the same shape this plan gives it. That is a deliberate reordering of section 10's
steps 4 and 5, on the user's instruction, so that the browser is working before the database moves
under it.

## Global Constraints

- .NET 10, C# 13. `net10.0` everywhere except `Fellowship.SDK.Generators`, which stays
  `netstandard2.0`.
- No polyfills for missing runtime functionality. Use the modern API directly.
- No em dashes or en dashes anywhere: code, comments, XML, output, commit messages.
- Never hardcode a list, id or data fact a canonical source already states. `EntityType` and
  `EntityTypes.Slug` are the canonical source for what a type is and how it is spelled. `vocabulary.json`
  is the canonical source for heroes, rarities, armour types, primary stats, gem types, colours,
  print formats and text styles.
- Comments only where they carry significant value. The API's existing doc comments record measured
  facts about the build; where a fact still holds, keep the sentence. Where the move makes a sentence
  untrue, rewrite it to what is now true rather than deleting it.
- Match the surrounding code's idiom: expression-bodied members, collection expressions, pattern
  matching, `StringComparer` named explicitly.
- **Do not commit** unless the task says to. **Do not run `export-all`**; it takes about seven
  minutes and `output/export` is current.
- `FELLOWSHIP_USMAP` must be set for the extractor's test suite. The API's suite does not need it.

### The wave

Tasks 3 through 6 change one API layer each and the API does not build until Task 6 closes. Each
still commits, and each is reviewed on its diff. `dotnet build src/FellowshipExtractor.slnx` is
green at the end of Tasks 1, 2, 6 and 7, and is expected to fail on `FellowshipExtractor.Api` at the
end of Tasks 3, 4 and 5. An implementer in the wave verifies by building the projects its task
touches and by reading the call sites its change breaks, and records the breakage its task leaves in
its report so the next task's implementer has the list.

## File structure

| File | What happens |
|---|---|
| `src/Fellowship.SDK/Entity.cs` | `Placeholder` gains `Key`. |
| `src/FellowshipExtractor/Tooltip/DocumentProjection.cs` | `Spent` orders by where the text spends each token, and carries the key. |
| `src/Fellowship.SDK/Documents/EntityDocument.cs` | Gains the polymorphic read. |
| `src/Fellowship.SDK/EntityType.cs` | Gains slug parsing and the document class per member. |
| `src/FellowshipExtractor.Api/PublicTypes.cs` | Keys on `EntityType`. All 21 types are addressable. |
| `src/FellowshipExtractor.Api/EntityStore.cs` | Reads documents, keyed by `(EntityType, Id)`. |
| `src/FellowshipExtractor.Api/PublicEntity.cs` | The document serialised. |
| `src/FellowshipExtractor.Api/Codex.cs`, `CodexQuery.cs`, `CodexView.cs`, `BrowseView.cs` | Read documents. |
| `src/FellowshipExtractor.Api/SearchIndex.cs`, `Suggestions.cs` | Index and rank documents. |
| `src/FellowshipExtractor.Api/TooltipView.cs`, `Placeholders.cs`, `StatMagnitudes.cs`, `Bearings.cs` | Read documents. |
| `src/FellowshipExtractor.Api/Program.cs` | Routes address a type by its slug. |
| `src/FellowshipExtractor.Api/Components/**` | Draw documents. |
| `src/FellowshipExtractor.Api.Tests/**` | Cover the store, the routes and the tooltips over documents. |

---

### Task 1: A placeholder carries its key, in the order the text spends it

Section 2 of the spec moves "the order a description spends its tokens" onto the model as
`Placeholders`, an ordered list. `DocumentProjection.Spent` writes `[.. tokens.Values]`, which is
the order a dictionary enumerates in, and drops the key the dictionary filed each token under.

`RichTextRenderer.Write` substitutes a `{Token}` by looking the text between the braces up in a
dictionary of substitutions. With no key on the document there is nothing to look up, so a
description on a document renders every one of its numbers as the sentence saying the number is
missing.

Ordering the list by where the text spends each token, and pairing the nth `{...}` with the nth
entry, does not answer it either. Measured over `output/export/entities.jsonl`, across the 212 rows
that carry a token or a placeholder list and the 363 `{Token}` occurrences in them:

- Ability 1855, *Brilliant Flash*, spends `{BrillFlashHeal}` twice in one description. Positional
  pairing consumes two entries for one token.
- 14 rows carry more placeholders than their text spends any of. Talent 390, *Unscarred Soul*, and
  abilities 1814, 1969 spend none at all while carrying one; abilities 1616, 2290 and 2291 spend two
  and carry three.

So the key stays, and it costs nothing a reader can see: the description the document already
carries publishes `{BrillFlashHeal}` in its own text, so the spelling is on the line either way, and
carrying it beside the token is what lets a renderer put a number where the sentence asks for one.

**`src/Fellowship.SDK/Entity.cs`.** `Placeholder` gains a `Key` property. It is the name between the
braces, which is what the row's dictionary files the token under.

**`src/FellowshipExtractor/Tooltip/DocumentProjection.cs`.** `Spent` takes the text the row spends
its tokens in as well as the dictionary, and returns the tokens ordered by where that text first
spends each one, then by key ordinal, each carrying its key. A token the text never spends sorts
after every token it does. This is the rule `CodexView.Spent` and `CodexView.Spends(string, string)`
already state; read them and move the rule rather than writing a second one.

The text a row spends its tokens in is its description and its detail lines joined by a newline,
which is what `CodexView.Spends(Entity)` builds. A gem type's overflow placeholders are spent in the
overflow sentence and are ordered against that instead.

**Verification.** `dotnet build src/FellowshipExtractor.slnx` 0 errors 0 warnings.
`dotnet test src/FellowshipExtractor.Tests` green with `FELLOWSHIP_USMAP` set. Then
`dotnet run --project src/FellowshipExtractor -- export-all` is **not** run; instead add a test to
`src/FellowshipExtractor.Tests` that builds a row with two tokens spent in reverse alphabetical order
and asserts the projected list is in text order and carries both keys. Prove the test fails if the
ordering is removed.

**Commit** when green.

---

### Task 2: The SDK reads a document back

A line names its type in a `type` field holding `EntityTypes.Slug(...)`, written by
`JsonlOutputFormat`. Nothing reads it back.

**`src/Fellowship.SDK/EntityType.cs`.** `EntityTypes` gains:

- `Parse(string slug)` and `TryParse`, resolving a slug to its member. `EntityTypeSlugConverter`
  already holds this map; move it onto `EntityTypes` so the converter and every other reader share
  one, and leave the converter reading through it.
- `Document(EntityType type)`, the `System.Type` of the document class for a member. Every document
  class is named exactly its member, so resolve it from the assembly by that name rather than
  listing 21 pairs. Throw naming the member when the assembly holds no such class, so a member added
  without its class fails at the first read rather than deserialising to nothing.

**`src/Fellowship.SDK/Documents/EntityDocument.cs`.** A `JsonConverter<EntityDocument>` that reads
the `type` property, resolves the member, and deserialises the object into that member's document
class. It writes by writing the document's own runtime type with `type` first, which is the shape
`JsonlOutputFormat` writes today, so a line written by the extractor and a line written by the SDK
are the same line.

**Tests, in `src/FellowshipExtractor.Tests`.** Over the real export at `output/export/entities.jsonl`:

- Every one of the 12,437 lines deserialises to a document, and the runtime class of each is
  `EntityTypes.Document` of the type its `type` field names.
- The per-type counts read back are the counts in spec section 3.4, which `TypeResolverTests`
  already states. Read them from there rather than restating them.
- Every member of `EntityType` resolves a document class, so all 21 are covered whether or not the
  corpus holds one today.
- One line of each type, written back out, equals the line that was read.

**Verification.** `dotnet build src/FellowshipExtractor.slnx` 0 errors 0 warnings.
`dotnet test src/FellowshipExtractor.Tests` green.

**Commit** when green.

---

### Task 3: The types a reader meets

Spec section 7: `PublicTypes` keys on type, and the types the augmentation table used to hide are
addressable. The drop rules in section 3.3 already keep a row a reader cannot meet out of the export,
so the corpus is the reader's corpus and every type in it is public.

**`src/FellowshipExtractor.Api/PublicTypes.cs`.**

- `PublicType` becomes `(EntityType Type, string Slug, string Label, string Noun, string Group)`.
  `Slug` is `EntityTypes.Slug(Type)` and is never written out beside it.
- `All` names every member of `EntityType`. Build it by projecting `Enum.GetValues<EntityType>()`
  through the label, noun and group table below, so a member added to the enum without an entry
  fails here rather than going missing from the menu.
- `IsPublic`, `TableOf` and `SlugOf` go. `Find(EntityType)` replaces `Find(string?)`, and the
  fallbacks it carried for a non-public table go with them: every type has a label, a noun and a
  group.
- `NameOf` and `DisplayNameOf` take an `EntityDocument`. `EntityDocument.Name` is already the name a
  reader is shown, and is already null where the build prints none a reader would read, because
  `DocumentProjection` applied the `WrittenAsWords` test when it filled it. So `DisplayNameOf` is the
  document's `Name`, `WrittenAsWords` moves out, and `NameOf` is that name or the type's noun and the
  document's id.

The label, the noun and the menu column resist derivation, because a type's name says what it holds
and not what a reader would look under. The column order is the order the groups first appear.

| Type | Label | Noun | Group |
|---|---|---|---|
| `Item` | Items | item | Items |
| `Trait` | Traits | trait | Items |
| `SetBonus` | Set Bonuses | set bonus | Items |
| `SetBonusPower` | Set Bonus Powers | set bonus power | Items |
| `WeaponPower` | Weapon Powers | weapon power | Items |
| `RelicPower` | Relic Powers | relic power | Items |
| `TraitPower` | Trait Powers | trait power | Items |
| `LegendaryStat` | Legendary Stats | legendary stat | Items |
| `Currency` | Currencies | currency | Items |
| `GemType` | Gem Types | gem type | Gems |
| `GemTier` | Gem Tiers | gem tier | Gems |
| `GemTrait` | Gem Traits | gem trait | Gems |
| `Ability` | Abilities | ability | Abilities |
| `Effect` | Effects | effect | Abilities |
| `Finesse` | Finesses | finesse | Abilities |
| `Talent` | Talents | talent | Characters |
| `TalentSlot` | Talent Slots | talent slot | Characters |
| `Creature` | Creatures | creature | Characters |
| `CompanionPet` | Companion Pets | companion pet | Characters |
| `Mount` | Mounts | mount | Characters |
| `MountPower` | Mount Powers | mount power | Characters |

`Titled` capitalises the first word of a noun, and the nouns above are two words in places, so a
name reads "Set bonus power 4012". That is the sentence a reader gets; capitalising every word would
read as a title the build never gave the row.

**Verification.** `dotnet build src/Fellowship.SDK` is green. `FellowshipExtractor.Api` does not
build; list in your report every call site the change breaks, by file and line, from the compiler's
own output.

**Commit** the file and the report.

---

### Task 4: The store reads documents

**`src/FellowshipExtractor.Api/EntityStore.cs`.**

- The corpus is `IReadOnlyList<EntityDocument> Entities`, read through Task 2's converter, in the
  order the file writes them, which is the order the browser paginates by.
- The index is `Dictionary<(EntityType, int), EntityDocument[]>`. `All` and `Resolve` take an
  `EntityType` and an id.
- `Count` stays. `PublicCount` goes: every row is public now, so the two counts were the same number
  written twice.
- `Tables` becomes `Types`, an `IReadOnlyList<EntityType>` derived from the corpus, largest first
  then by slug ordinal, so a type the extractor starts writing appears in the browser's tabs without
  being named here.
- `Reads(EntityDocument, HeroEntry)` keeps its first clause. `Item` carries `Heroes` as
  `IReadOnlyList<Hero>?` and `ArmorType`/`PrimaryStat` as enums, so the comparison is against the
  hero's own fields parsed to the same enums. **Read the doc comment before porting it**: the clause
  answering for the eighteen Back-slot cloaks is load-bearing and a reading that names its heroes has
  already answered the question, so the armour and stat pair is only consulted where it names none.
  Only `Item` carries the three discriminators; a document of any other type reads for every hero.
- `Census` walks the fields each document type carries rather than one shape. `Item` carries `Stats`,
  the token-bearing types carry `ScalesOff` and `Placeholders`. Keep the sentence recording that the
  census is 82 names and check it still is; if the number moved, say so in your report and write the
  number this corpus gives.
- `Wearer`, `Hero`, `Rarity`, `StatName`, `StatDisplay`, `StatValue` and `StatParameters` are
  unchanged except where they name `Entity`.

`Vocabulary` and its case-insensitive rebuild are unchanged.

**Verification.** `dotnet build src/Fellowship.SDK` green. Report the call sites this task leaves
broken, from the compiler's output, added to the list Task 3 reported.

**Commit** the file and the report.

---

### Task 5: Every surface reads the model

The files: `PublicEntity.cs`, `Codex.cs`, `CodexQuery.cs`, `CodexView.cs`, `BrowseView.cs`,
`SearchIndex.cs`, `Suggestions.cs`, `TooltipView.cs`, `Placeholders.cs`, `StatMagnitudes.cs`,
`Bearings.cs`, `ChromeAssets.cs`, `Program.cs`.

The move is the same in each: a row is an `EntityDocument`, a type is an `EntityType`, and a field
the API used to derive is read off the document. Spec section 2 names what each one stops deriving:

| What it stops deriving | Where it stops | What it reads instead |
|---|---|---|
| The name a reader is shown | `PublicTypes.NameOf`, `DisplayNameOf` | `EntityDocument.Name` |
| The damage schools a row is | `Codex.Schools` over the tag list | `Schools` on the document |
| Art per rarity | `PublicEntity.Art` | `Item.IconByRarity` |
| Who lent a row its name and art | `PublicReference.From` | `NameBorrowedFrom`, `ArtBorrowedFrom` |
| An attribute key as the word the game prints | `Codex.Scaling`, `EntityStore.StatName` | the printed word on the document |
| The order a description spends its tokens | `CodexView.Spends` | `Placeholders` in the order the list gives them |
| Which rows a reader may meet | `PublicTypes.IsPublic` | the type itself |

`Codex.Spaced` stays, because `StatName` still falls back to it for the 67 attribute keys the shipped
table has no row for.

`Placeholders.Resolve` keeps taking a key-to-token map and returning a key-to-substitution map: the
renderer looks a token up by the name between the braces. Build the map from the document's ordered
list on `Placeholder.Key`.

`Program.cs`:

- `/api/ability/{abilityId:int}` and `/api/item/{itemId:int}` pass `EntityType.Ability` and
  `EntityType.Item` rather than the strings `"ability"` and `"items"`.
- `/api/tooltip/{slug}/{id:int}`, `/api/entity/{slug}/{id:int}` and the `type=` query value resolve
  through `EntityTypes.TryParse`. The route parameter is renamed from `netId` to `id`, which is what
  a document calls it.
- `/api/catalog` faceted over the whole corpus, since every row is public.
- `Describe` writes an item's pair from the document's enums.

**Verification.** `dotnet build src/FellowshipExtractor.slnx` builds every project but the Razor
components in `FellowshipExtractor.Api`. Report what the components still break on.

**Commit** the files and the report.

---

### Task 6: The browser draws documents

Every `.razor` under `src/FellowshipExtractor.Api/Components` that names `Entity`, a table or a
`netId`. The list from the compiler is authoritative; the ones known to name it are `Pages/Browse.razor`,
`Pages/EntityPage.razor`, `Pages/Home.razor`, `Pages/NotFound.razor`, `Layout/Omnibox.razor`,
`Shared/Detail.razor`, `Shared/EntityLink.razor`, `Cx/Chip.razor`, `Cx/Facts.razor`, every
`Sections/*.razor` and every `Tooltip/*.razor`.

A section that drew a field off `Entity` draws it off the document instead, and a section whose field
belongs to one type is placed by matching on the document's runtime type. A row's type is
`PublicTypes` keyed on `EntityType`, and a link is built from `EntityTypes.Slug` and the document's
`Id`.

Follow the project's existing component pattern rather than inventing one: read the surrounding
components first and match their parameter naming, their `Cx` primitives and their CSS isolation.

**Verification.** `dotnet build src/FellowshipExtractor.slnx` 0 errors 0 warnings.

**Commit** when green.

---

### Task 7: The suite, and the app answering

**`src/FellowshipExtractor.Api.Tests`.** The existing suite builds a store over a fixture export;
`Corpus.cs` and `Export.cs` are what write it, and both write `Entity`-shaped lines. They write
documents instead, through the SDK's own writer so a fixture line and an export line are the same
shape.

Add, per spec section 9:

- **The corpus is not empty.** The store's per-type counts equal spec section 3.4's counts over the
  real export, and `Count` is 12,437. This is the assertion that fails when the store deserialises
  every line and indexes none of them.
- **Every id resolves.** For every `(type, id)` in the corpus, `All` answers at least one document,
  and `Resolve` with the wearer's name answers `Found`.
- **Tooltips render their numbers.** For every row carrying a placeholder, the rendered tooltip
  contains no unresolved-token marker. Ability 1855's description spends `{BrillFlashHeal}` twice and
  both occurrences carry the same number.
- **The routes answer.** `/api/tooltip/{slug}/{id}` answers 200 for one id of each of the 21 types,
  and `/api/entity/{slug}/{id}` answers a record whose `type` is the slug asked for.

**Verification.** `dotnet build src/FellowshipExtractor.slnx` 0 errors 0 warnings.
`dotnet test src/FellowshipExtractor.Tests` and `dotnet test src/FellowshipExtractor.Api.Tests` both
green. Then start the app and record what it logs:
`dotnet run --project src/FellowshipExtractor.AppHost`, or the API alone where the AppHost needs a
connection string this task does not provide. The startup line naming the entity count is the
evidence, and the count it names must be 12,437.

**Commit** when green.

---

## Notes for whoever executes this

- `EntityTypeSlugConverter` in `src/Fellowship.SDK/EntityType.cs` already resolves a slug to a member
  and derives both directions from `EntityTypes.Slug`. It is the map to reuse, not a second one to
  write beside.
- Spec section 7 spells nine of the slugs in the plural: `gem-traits`, `trait-powers`,
  `set-bonus-powers`, `weapon-powers`, `relic-powers`, `legendary-stats`, `mount-powers`, `finesses`.
  `EntityTypes.Slug` derives the singular, the export's 12,437 lines and its 2,570 nested references
  already carry the singular, and they join on it. The singular is what ships; correct section 7 to
  match in the task that touches `PublicTypes`.
- Route addresses change spelling with the slugs: `/api/tooltip/items/5146` becomes
  `/api/tooltip/item/5146`, `traits` becomes `trait`, `creatures` becomes `creature`. The `README`
  documents the old spellings and the old export; bring it current in the last task that changes a
  route.
- Section 9's strongest test, that the store answers the same tooltip for the same id and hero as it
  answered from the file before, needs a baseline the current export no longer holds. Build the
  baseline from commit `2fb80e1` in a separate worktree if it is wanted; otherwise the per-type
  counts and the rendered-token assertions in Task 7 are what stands in for it, and say so.
