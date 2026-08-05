# Codex rarity, links, variants and kinds: design

Four changes to `src/FellowshipExtractor.Api`, in the order each depends on the one before it: what
colour a name is drawn in, what colour a link is drawn in, one list row per id rather than per
reading, and one public type per kind of row rather than per shipped table.

---

## 1. Goal

An item is named in its rarity colour and never in a hero colour. A link to an item carries that
item's rarity, a link to a gem trait carries its gem type, and every other link carries the app's
one link colour. A list names an id once, with its other names beside it as icon links. The word
Talent names a hero's talent, and each of the other eight kinds of row filed in the augmentation
table is named for what it is.

---

## 2. What a name is drawn in

### 2.1 The rung

Two rungs of the shipped ladder are named by ordinal rather than by spelling, because the ladder
files a rung under one word and prints another: ordinal 4 is filed `Champion` and prints "Heroic",
ordinal 5 is filed `Heroic` and prints "Regal". A colour is looked up by the filed word, so both are
found by reading `VocabularyFile.Rarities` for the entry whose `Tier` is the ordinal and asking
`DesignTokens.Rarity` for that entry's `Name`. A build that renames a rung keeps drawing the right
colour, and no rung word is spelled in this app's own code.

- **Ordinal 5**, printed "Regal", resolves to `game.game.rarity-heroic`.
- **Ordinal 6**, `Legendary`, resolves to `game.rarity-legendary`.

### 2.2 The reading

`CodexView.NameColour(Entity)` answers in this order, and every answer passes through
`CodexView.Readable` as it does today:

1. The row states a `Rarity`. The colour is that rung's. This is the 24 gem tiers, which are the
   only rows in the export that state one.
2. The row is an item whose `RuleSet` is `Legendary`. The colour is ordinal 6's. This is 37 rows,
   exactly the 37 carrying `LegendaryStats`.
3. The row is an item. The colour is ordinal 5's.
4. The row states a `GemType`. The colour is `DesignTokens.Gem` for that type.
5. Otherwise `core.text-primary`, or `core.text-secondary` where `PublicTypes.DisplayNameOf` finds
   no name a player would read.

No branch reads the hero list, so no name on any surface is drawn in a hero colour.

### 2.3 Where a hero colour is drawn

Hero colour is worn by chips, pills, badges and roster entries: the "Worn by" chips in
`Browse.razor`, the hero pills in `Particulars.razor`, the front-page roster in `Home.razor`, and
the per-version roster in `Versions.razor`. Each of those keeps `CodexView.HeroColour` and
`CodexView.HeroSwatch` unchanged.

### 2.4 The entity heading

The heading on `EntityPage.razor` follows the rarity knob and re-tints as the reader turns it, which
is what the tooltip beside it already does through `--fx-title`.

Two custom properties, so the served colour survives being overridden. The page renders the colour
from section 2.2 as `--cx-name` on the entity wrapper, and the heading is
`color: var(--cx-rung, var(--cx-name))`. `stage()` in `codex.js`, which already runs on every knob
change, sets `--cx-rung` on the heading from the pressed rarity button's own colour and removes it
when no rung is pressed, at which point the served `--cx-name` shows through untouched. The rarity
buttons in `Knobs.razor` already carry their rung's colour inline, so the value is read from the
same place the button was painted from.

`arrived()` clears `--cx-rung`, so a rung pressed before a hero is chosen does not carry its colour
across the navigation the hero knob performs, whether the document is replaced or Blazor's enhanced
navigation keeps the element.

---

## 3. What a link is drawn in

Three buckets:

- A link to an **item** carries that item's colour from section 2.2.
- A link to a **gem trait** carries its gem type's colour.
- **Every other link** carries `--gold-bright`, which `codex.scss` already sets on `a` inside the
  page scope.

`CodexView` gains `LinkColour(Entity)`, which answers the colour for those two cases and null for
everything else. `EntityLink.razor` writes an inline colour only when it is given one, so a link to
an ability, an effect, a talent, a creature or a trait emits no inline colour and the stylesheet's
rule stands. Crumbs, facet links, pills, tabs and the pager are unchanged.

`RichText` renders the shipped style rows and builds no entity links, so game text needs no change.

---

## 4. One row per id

### 4.1 What a list draws

A list row is one id. Browse, the omnibox and the front-page showcase each draw an id once, whatever
number of readings the export carries for it. 5,782 distinct item ids stand behind 7,737 item rows,
and 188 ids read differently per hero.

### 4.2 The primary reading

The reading a row is named and pictured by is whichever answers the filter in force.

- In Browse, a `hero` or an `armor` selection narrows an id's readings and the first survivor in
  export order is the primary. A reader filtered to Meiko sees id 5191 as *Betrayer's Dragonhide
  Gloves* with Meiko's art and Agility in its primary slot.
- In the omnibox, the typed text is the filter: the primary is the highest-scoring reading of that
  id under `SearchIndex.Score`, so typing a plate name opens the row named by the plate reading.
- With nothing narrowing, the first reading in export order is the primary.

`SearchIndex` goes on indexing and scoring readings, because a reader typing a plate name is typing
one reading's name. The fold to one entry per id happens after scoring and before the limit is
applied, so `Search(text, limit)` answers `limit` ids: a word matching twelve readings of one id
returns one suggestion and leaves eleven places for other ids. The per-type counts in `Suggestions`
count the folded entries, which is what section 4.5 asks of every count.

### 4.3 The variant links

Beside the primary, a row draws one icon-only link per **other distinct name** the id reads under.

- 67 of the 188 split ids read under a single name and draw no variant link at all.
- 115 read under four names and draw three.
- Five read under two, three or five names and draw one, two or four.

A variant link carries the art of that name's first reading in export order, and addresses the
entity page under that same reading's hero, so the page opens on the name the icon showed. Each link
carries its name as `title` and as its accessible label.

Of the 547 distinct names the 188 ids read under, 36 are one hero's and take that hero's colour. The
rest carry no tint, because the shipped palette names no colour for an armour type and every colour
this app draws is read from the build.

### 4.4 The collapsed row

A row's name, art, primary stat and set are answers about an id under a filter rather than
properties of one `Entity`, so the row is its own record:

```csharp
public sealed record Listing(Entity Shown, IReadOnlyList<Listing.Variant> Variants)
{
    public sealed record Variant(string Name, string Icon, string Href, string? Colour);
}
```

`Variant` is nested for the reason `Versions.razor` nests its own `Version`: the extractor's
`FellowshipExtractor.Tooltip.Variant` is in scope in every file of this assembly.

`CodexResults.Rows` becomes `IReadOnlyList<Listing>`, `CodexSearch.Run` builds them, and
`Suggestions` folds its hits the same way. `CodexView.Name`, `Href`, `Tip`, `NameColour` and
`LinkColour` keep taking an `Entity` and are handed `Listing.Shown`.

### 4.5 Counts

Every count counts ids, so each number agrees with the list beneath it:

- the "N entities" line, the type tabs and `CodexResults.AcrossTypes` in `Browse.razor`
- pagination, so a page holds a fixed number of rows
- the rail's facet counts in `BrowseView`, each reading "how many ids have a reading matching this
  value"
- the "More like this" counts in `EntityPage.Nearby`
- the omnibox's per-type counts in `Suggestions`
- the front page's headline count and its menu-card counts, both reached through `Codex.Count` and
  `Codex.Menu`

No "More like this" link is lost to the change. Across all five facets `Nearby` offers, no
table-and-value pair falls from two or more rows to fewer than two ids.

The doc comment on `EntityStore.Entities` states the reading a browser takes of a split id, and
states this one.

---

## 5. A row's kind

### 5.1 The nine kinds

The 772 rows of `CRCharacterAugmentationAsset` are nine kinds:

| Kind | Rows | Named |
|---|---:|---:|
| Talent | 469 | 268 |
| Mount | 129 | 54 |
| Gem trait | 76 | 60 |
| Trait mechanics | 42 | 17 |
| Set bonus | 25 | 2 |
| Relic power | 12 | 11 |
| Weapon power | 12 | 12 |
| Legendary stat | 3 | 3 |
| not public | 4 | 0 |

"Named" counts the rows carrying a name a player reads, by `PublicTypes.DisplayNameOf`. The 345 rows
without one print as their noun and their id, "Mount 412", which is the convention
`PublicTypes.NameOf` already applies to every public row in that state.

### 5.2 How a kind is decided

Asked of the row, in this order:

1. It states a `GemNode` or a `GemType`: **gem trait**.
2. A `Talent.ID` row's `Augmentation` points at it: **talent**.
3. An `ItemTrait.ID` row's `Augmentation` points at it: **trait mechanics**.
4. Its gameplay tag's first two segments are a prefix in the table below: that kind.
5. Its tag's second segment is a hero codename, read off `VocabularyFile.Heroes`: **talent**.
6. Otherwise the row is not public.

The prefix table:

| Prefix | Kind |
|---|---|
| `CAA_Talent` | Talent |
| `CAA_Finesse` | Talent |
| `CA_GemBonus` | Gem trait |
| `CA_Mount` | Mount |
| `CAA_Trait` | Trait mechanics |
| `CAA_Perk` | Trait mechanics |
| `CA_ArmorSetBonus` | Set bonus |
| `CA_Weapon` | Weapon power |
| `CA_RelicPower` | Relic power |
| `CAA_LegendaryStat` | Legendary stat |

The references come first because they are the build stating the relationship outright, and they
agree with the prefixes wherever both apply: every one of the 215 rows a talent slot points at is
prefixed `CAA_Talent` or a hero codename, and all 40 rows an item trait points at are prefixed
`CAA_Trait` or `CAA_Perk`.

The codename rule is derived rather than listed, so a hero the build adds classifies without this
file being touched. It reaches the 70 codename-prefixed rows no reference claims, across eight
codenames; 36 more codename-prefixed rows are claimed by a talent slot at step 2.

Step 6 is the same fail-closed default `PublicTypes` already applies to a table nobody has named: a
prefix nobody has classified stays invisible rather than landing in a catch-all. It holds four
nameless rows today, prefixed `CA_Winter`, `CA_QuickplayBonus` and `CAA_Shared`.

The tag itself reaches no reader. The prefix is read to choose a public word, in the same way
`CodexView.Knob` reads a key's segments to choose a label.

### 5.3 What a public type is

`PublicType` names the table it draws from and, where a table holds more than one kind, the kind it
covers:

```csharp
public sealed record PublicType(string Table, string Slug, string Label, string Noun, string Group)
{
    /// <summary>The kind of its table's rows this type covers, and null where it covers them all.</summary>
    public string? Kind { get; init; }
}
```

Only the augmentation table is partitioned. Every other table has one type covering all its rows,
and `Kind` is null there.

`PublicTypes` answers about a row rather than about a table string: `TypeOf(Entity)`,
`IsPublic(Entity)`, `SlugOf(Entity)`, `Label(Entity)`, `Noun(Entity)`. `TypeOf(string slug)` answers
the type a route segment names, carrying the table and the kind together.

A caller holding a table and an id rather than a row reads the row first through `EntityStore.All`
and asks about that. This is `CodexView.Href(table, netId)`, `Name(table, netId)` and
`Addresses(table, netId)`, which are the three the augmentation pointer and the gem `Replaces`
pointer travel through. An id the store does not hold leads nowhere, which is what those three
already answer for a table no reader can open.

### 5.4 Addresses, labels, tabs and menus

`PublicTypes.All` grows to thirteen entries, grouped so a column's entries stay adjacent and ordered
largest first within a column, which is the arrangement the file already documents:

| Slug | Label | Noun | Group |
|---|---|---|---|
| `items` | Items | item | Items |
| `gem-traits` | Gem Traits | gem trait | Items |
| `traits` | Traits | trait | Items |
| `trait-mechanics` | Trait Mechanics | trait mechanic | Items |
| `set-bonuses` | Set Bonuses | set bonus | Items |
| `weapon-powers` | Weapon Powers | weapon power | Items |
| `relic-powers` | Relic Powers | relic power | Items |
| `legendary-stats` | Legendary Stats | legendary stat | Items |
| `effect` | Effect | effect | Abilities |
| `ability` | Ability | ability | Abilities |
| `talent` | Talent | talent | Characters |
| `creatures` | Creatures | creature | Characters |
| `mounts` | Mounts | mount | Characters |

`/entity/gem-traits/276` opens the gem trait, and `/entity/talent/276` answers 404, because
`EntityPage` resolves the readings of an id and keeps only those whose kind matches the slug's type.
Augmentation ids are unique per row, so that test turns away a mismatched slug rather than narrowing
a set.

The surfaces that ask a table for its public words ask the row instead: the type tabs and the Type
column in `Browse.razor`, the chosen-filter chips in `BrowseView`, the `type` filter in
`CodexQuery`, the counts and menu in `Codex`, the per-type groups in `Suggestions`, the crumb and
the bearings in `EntityPage`, and `PublicEntity`, whose `Type` field carries a slug to an API
caller. `Codex.Of(table)` becomes a lookup by type rather than by table.

The augmentation pointer's heading in `CodexView.Heading` reads the label of the type its
destination row belongs to, so a gem trait's pointer is headed "Gem Traits" and a talent's "Talent".

`PublicEntity.Type` carries a slug to a caller reading the catalogue as data, and its answer changes
for 303 rows: the 299 that answered `talent` and now answer `gem-traits`, `trait-mechanics`,
`mounts`, `set-bonuses`, `weapon-powers`, `relic-powers` or `legendary-stats`, and the four that
answered `talent` and now answer nothing, being no longer public. The `Type` a nested pointer
carries in `PublicEntity` changes for the same rows. The record now states each row's kind where it
previously stated the asset class they share, which is the whole of the change a caller sees.

---

## 6. What the build states

Every number above is measured over `output/export/entities.jsonl` and `vocabulary.json`, and the
tests in section 7 hold them.

**Rarity.** 24 rows state a `Rarity`, all of them gem tiers, four per gem type over six types. 37
items carry `ruleSet: Legendary`, exactly the 37 carrying `LegendaryStats`. All nine loot rule sets
define all seven rungs, and all 1,218 items carrying per-rarity art carry the same five rungs over
three images, the row's own `Icon` being the top of them. No other row states a rarity.

**Variants.** Of the 188 item ids that read differently per hero, none has two readings differing in
any stat slot other than the primary one: id 5191 is Stamina, the wearer's own attribute, and Haste
for all twelve heroes. What does differ across readings of one id is the name, the art, the primary
stat, and on 20 of the 188 the set the reading counts towards. Names per split id are 1 on 67 ids, 2
on 2, 3 on 2, 4 on 115 and 5 on 2. Of the 547 distinct names, 36 are one hero's.

**Kinds.** The classifier in section 5.2 partitions all 772 augmentation rows into the counts in
section 5.1.

---

## 7. Tests

New cases in `src/FellowshipExtractor.Api.Tests`, in the shape the project already renders and
asserts components in.

**Colour.** An item name resolves to ordinal 5's colour; a `ruleSet: Legendary` item to ordinal 6's;
a gem tier to its stated rung's; a gem trait to its type's; a row with a single hero to
`core.text-primary`, which is the case that fails if the hero branch survives. A rarity colour is
looked up by ordinal against the shipped ladder, so a test renames a rung in a fixture ladder and
asserts the colour still resolves.

**Links.** `EntityLink` emits an inline colour for an item and for a gem trait and emits none for an
ability, an effect, a talent, a creature and a trait.

**Variants.** A list draws id 5252 once and id 5191 once. Id 5191 under `hero=Meiko` is named
*Betrayer's Dragonhide Gloves* and draws three variant links; under no filter it is named by its
first reading. A variant link addresses a hero who reads the name it showed. Id 5252 draws no
variant link, having one name. A hero-specific name carries that hero's colour and a shared name
carries none.

**Counts.** The row count of a filtered result equals its stated total, and the total equals the
number of distinct ids the filter reaches. A facet count equals the number of ids a click on it
returns. `Nearby` offers the same links it offers over rows.

**Kinds.** The classifier reaches the count in section 5.1 for each of the nine kinds and leaves no
row unclassified beyond the four. Every one of the 216 talent-slot references and 40 item-trait
references lands on the kind its prefix also names. `/entity/gem-traits/276` resolves and
`/entity/talent/276` answers 404. No slug resolves to an internal table name, and no label, noun or
name drawn anywhere carries a gameplay tag.

---

## 8. Order of work

Sections 2 and 3 are one piece of work: they touch `CodexView`, `EntityLink`, `EntityPage`,
`Knobs.razor` and `codex.js`, and nothing else depends on them.

Section 4 is a second piece: it changes what a list row is and reaches `CodexSearch`,
`CodexResults`, `BrowseView`, `Browse.razor`, `Suggestions`, `Home.razor` and `Codex`.

Section 5 is a third and much the largest: it changes how a public type is keyed and reaches every
surface that names a type. It wants an implementation plan of its own.
