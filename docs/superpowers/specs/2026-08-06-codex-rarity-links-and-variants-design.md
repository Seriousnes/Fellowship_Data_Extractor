# Codex rarity, links and variants: design

Three changes to `src/FellowshipExtractor.Api`, in the order each depends on the one before it: what
colour a name is drawn in, what colour a link is drawn in, and one list row per id rather than per
reading.

`docs/superpowers/specs/2026-08-05-item-level-rarity-and-tempering-design.md` is the authority for
the rungs an item comes in. This design reads them; it does not restate how they are decided.

---

## 1. Goal

An item is named in the colour of the rung it tops out at, and never in a hero colour. A link carries
the colour the build gives its row, and the app's one link colour where the build gives it none. A
list names an id once, with its other names beside it as icon links, and every count a reader sees
counts ids.

---

## 2. What a name is drawn in

### 2.1 The rung

The ladder files a rung under one word and prints another: ordinal 4 is filed `Champion` and prints
"Heroic", ordinal 5 is filed `Heroic` and prints "Regal". `DesignTokens.Rarity` is keyed by the
filed word, and the shipped palette scatters the ladder over two key namespaces, reaching the two
renamed rungs through `game.game.rarity-*` and the other five through `game.rarity-*`.

`CodexView` gains `Rung(Rarity)`. It finds the entry in `store.Vocabulary.Rarities` whose `Tier` is
that rung's ordinal, asks `DesignTokens.Rarity` for that entry's `Name`, and answers through
`Readable`. The generated `Rarity` enum carries each rung's shipped tier as its value, so a
document's rung is its ordinal and the lookup joins on a number. A build that renames a rung keeps
drawing the right colour, and no rung word is spelled in this app's own code.

- **Ordinal 5**, printed "Regal", resolves to `game.game.rarity-heroic`, `#BE2525`.
- **Ordinal 6**, `Legendary`, resolves to `game.rarity-legendary`, `#FF8000`.

`Rung` answers null where the palette names no colour for the rung, which is the answer
`DesignTokens` gives on every miss.

### 2.2 The reading

`CodexView.NameColour(EntityDocument)` answers in this order:

1. The row is an `ItemDocument` stating the rungs it comes in. The colour is the top of `Rarities`,
   the highest rung the item is found at. A dynamic item is Regal, a legendary is Legendary, and a
   hero item is the rung its own row states.
2. The row states a `Rarity` of its own. The colour is that rung's. This is the 24 gem tiers.
3. The row states a `GemTypeName`. The colour is `DesignTokens.Gem` for it. This is the 6 gem types
   and the 60 gem traits.
4. Otherwise `core.text-primary`, or `core.text-secondary` where the document carries no `Name`.
   Rules 1 through 3 fall here too where the palette names no colour for what they found.

A gem tier states a rarity, so rule 2 answers it and rule 3 never sees it: Ruby's four tiers are
drawn in their rungs' colours, and the ruby colour is drawn on the gem type itself and on its ten
traits.

No branch reads a hero, so no name on any surface is drawn in a hero colour. The two branches that
read one today, `ItemDocument { Heroes: [{ } hero] }` and `TalentSlotDocument`, go, and `HeroTint`
goes with them.

### 2.3 Where a hero colour is drawn

Hero colour is worn by chips, pills, roster entries, and the variant links of section 4.3: the
"Worn by" chips in `Browse.razor`, the hero pills in `Particulars.razor`, the front-page roster in
`Home.razor`, and the per-version roster in `Versions.razor`. Each of those keeps
`CodexView.HeroColour` and `CodexView.HeroSwatch` unchanged.

### 2.4 The entity heading

`EntityPage.razor` draws its heading as `<h1 style="color:@Ui.NameColour(Shown)">`, computed once on
the server, so it holds still while the reader turns the rarity knob beside it. The tooltip in the
same view re-tints through `--fx-title`, and the heading follows the knob the same way.

Two custom properties, so the served colour survives being overridden. The page renders section
2.2's colour as `--cx-name` on the entity wrapper, and the heading becomes
`color: var(--cx-rung, var(--cx-name))`, so with no rung pressed the served colour shows through
untouched.

`codex.js` sets `--cx-rung`. Its rarity click handler already clears `aria-pressed` and
`dataset.value` across the pressed button's knob group before calling `stage()`; in that same
handler it sets `--cx-rung` on the heading from the pressed button's own computed colour, and
removes the property when the press leaves no rung chosen. `Knobs.razor` paints each rarity button
with an inline colour from `CodexView.Readable(Ui.Tokens.Rarity(rung.Name))`, so the value is read
from the same place the button was painted from. The button's `data-choice` carries the rung's
ordinal rather than its colour, so the colour is read off the element's computed style.

`arrived()` clears `--cx-rung`, so a rung pressed before a hero is chosen does not carry its colour
across the navigation the hero knob performs, whether the document is replaced or Blazor's enhanced
navigation keeps the element.

---

## 3. What a link is drawn in

A link carries the colour the build gives its row, and no inline colour at all where the build gives
it none.

`CodexView` gains `LinkColour(EntityDocument)`, which is section 2.2's rules 1 through 3 and null
where none of them answers. `NameColour` becomes `LinkColour(document)` falling back to rule 4, so
one table serves the link, the heading and the showcase card, and the rule is written once.

`EntityLink.razor` writes an inline colour only when it is given one. A link to an item, a gem type,
a gem tier or a gem trait carries its own colour. A link to an ability, an effect, a talent, a
talent slot, a creature, a mount, a trait, a power, a finesse, a currency or a companion pet emits
no `style` attribute, and the page stylesheet's own rule stands: `codex.scss` sets
`a { color: var(--gold-bright); }` inside `@scope (body) to (.fx-tip)`, which resolves through
`--core-primary-200` to `#E3B468`.

`EntityLink.Tint` goes. A caller passing `Tint="false"` was asking for the stylesheet's colour,
which is now what a row the build gives no colour receives anyway. `Home.razor`'s showcase card is
its one caller, and it already draws its own name span through `NameColour`.

Crumbs, facet links, pills, tabs and the pager are unchanged. `RichText` renders the shipped style
rows and builds no entity links, so game text needs no change.

---

## 4. One row per id

### 4.1 What a list draws

A list row is one id. Browse, the omnibox, `/api/entities` and the front-page showcase each draw an
id once, whatever number of readings the export carries for it.

5,744 item rows stand behind 3,789 item ids, and 188 of those ids read differently per hero. 216
talent slot rows stand behind 19 ids, each read once per hero. Every other type is one row per id.

*Sinbinding Stone*, item 5250, is nine rows today, one for each hero that reads it, and is one row
after the change.

### 4.2 The primary reading

The reading a row is named and pictured by is whichever answers the filter in force.

- In Browse, a `hero` or an `armor` selection narrows an id's readings and the first survivor in
  export order is the primary. A reader filtered to Meiko sees item 5191 as *Betrayer's Dragonhide
  Gloves* with Meiko's art.
- In the omnibox, the typed text is the filter: the primary is the highest-scoring reading of that
  id under `SearchIndex.Score`, so typing a plate name opens the row named by the plate reading.
- With nothing narrowing, the first reading in export order is the primary.

`SearchIndex` goes on indexing, scoring and answering readings, because a reader typing a plate name
is typing one reading's name, and because `CodexSearch.Run` narrows what `Search` hands it by the
reader's own facets: a fold before that narrowing would drop the reading the facets would have kept.
Its candidates stay document indices and its `Hit` stays a document.

The omnibox folds in `Suggestions.For`, after `Search` answers and before the per-type grouping and
the limit, so it answers `limit` ids: a word matching four readings of one id returns one suggestion
and leaves three places for other ids.

In `CodexSearch.Run` the fold happens after `Order` and before the page slice, so `Total` is the
number of ids the filter reaches and a page holds a fixed number of rows. The three re-filtered
arrays `Run` builds beside it, `Without` for each facet, `across` for the type tabs and `unbounded`
for the measure rail, fold the same way, so section 4.5's counts agree with the list.

### 4.3 The variant links

Beside the primary, a row draws one icon-only link per **other distinct name** the id reads under.

- 67 of the 188 split item ids read under a single name and draw no variant link at all.
- 115 read under four names and draw three.
- Six read under two, three or five names and draw one, two or four.
- All 19 talent slot ids read under one name and draw none, so a talent slot collapses from twelve
  rows to one without gaining a link.

A variant link carries the art of that name's first reading in export order, and addresses the
entity page under that same reading's hero, so the page opens on the name the icon showed:
`/entity/item/5191?hero=Meiko`. Each link carries its name as `title` and as its accessible label.

Of the 546 distinct names the 188 split ids read under, 36 belong to one hero and take that hero's
colour through `CodexView.HeroColour`. Those 36 are the readings of the ten legendary items that
read differently per hero, one name per hero. The other 510 carry no tint, because the shipped
palette names no colour for an armour type and every colour this app draws is read from the build.

### 4.4 The collapsed row

A row's name, art, primary stat and set are answers about an id under a filter rather than
properties of one document, so the row is its own record:

```csharp
public sealed record Listing(EntityDocument Shown, IReadOnlyList<Listing.Variant> Variants)
{
    public sealed record Variant(string Name, string? Icon, string Href, string? Colour);
}
```

`Icon` is the art's file name as the reading states it, which the browser resolves through
`CodexView.Icon` as it already does for a row's own art.

`Variant` is nested for the reason `Versions.razor` nests its own `Version`: `Fellowship.SDK.Variant`
is in scope in every file of this assembly, through the project's global `using Fellowship.SDK`.

`CodexResults.Rows` becomes `IReadOnlyList<Listing>`, `CodexSearch.Run` builds them, and
`Suggestions` folds its hits the same way. `CodexView.Name`, `Href`, `Tip`, `NameColour` and
`LinkColour` keep taking an `EntityDocument` and are handed `Listing.Shown`, as do
`BrowseView.Columns` and the `ShowSlot`, `ShowWorn` and `ShowSchools` predicates in `Browse.razor`.

### 4.5 Counts

Every count a reader sees counts ids, so each number agrees with the list beneath it:

- `CodexResults.Total`, `Types` and `AcrossTypes`, which are the headline "N entities" line and the
  type tabs in `Browse.razor`
- pagination, which takes `Pages` and its slice from `Total`, so a page holds a fixed number of rows
- the rail's facet counts through `Codex.View`, each reading "how many ids have a reading matching
  this value"
- `Codex.Count`, which answers a new public count of the store's distinct `(type, id)` keys. It is
  the front page's "N entries", the sentence `ApiDocument.Describe` writes into the OpenAPI
  description, and the offer `NotFound.razor` makes.
- `Codex.Types`, the per-type counts behind the front page's menu cards and `MegaMenu`, and the link
  counts in `Codex.Menu`
- the omnibox's `SearchResponse.Total` and its per-type `SearchGroup.Count`
- the "More like this" counts in `EntityPage.Nearby`

`EntityStore.Count` stays the number of rows the export holds, which is what the store logs on
startup and what both suites assert.

No "More like this" link is lost to the change. Across the facets `Nearby` offers, no facet-and-value
pair falls from two or more rows to fewer than two ids.

---

## 5. What the build states

Every number above is measured over `output/export/entities.jsonl` and `vocabulary.json`, and the
tests in section 6 hold them.

**The ladder.** Seven rungs, each an entry of `tier`, `name`, `displayName` and `gemDisplayName`,
carrying no colour of its own. Ordinals 4 and 5 are the two whose filed and printed words disagree.
The palette holds `game.game.rarity-champion` and `game.game.rarity-heroic` and, under the shorter
namespace, `game.rarity-common`, `game.rarity-uncommon`, `game.rarity-rare`, `game.rarity-epic` and
`game.rarity-legendary`, so all seven rungs resolve and each resolves through exactly one of the two
namespaces.

**Rarity.** 24 rows state a `Rarity` of their own, all of them gem tiers, four per gem type over six
types. 37 item rows carry `ruleSet: Legendary`, and those same 37 are the only item rows carrying a
scalar rarity today. They fall on 11 ids: ten that read differently per hero, of two to five
readings each, and one read once. 1,218 item rows carry per-rarity art, every one of them over the
same five keys, `Uncommon`, `Rare`, `Epic`, `Champion` and `Heroic`.

**Variants.** Of the 188 item ids that read differently per hero, what differs across readings of
one id is the name on 121, the icon on 113, the per-rarity art on 110, the primary stat on 144, the
armour type on 121, and the set the reading counts towards on 20. No two readings of one id differ
in any stat slot other than the primary one. Names per split id are 1 on 67 ids, 2 on 2, 3 on 2, 4 on 115 and 5 on 2. Those
counts sum to 547 while the distinct names number 546, because *Slaughtervine Wraps* is read by both
id 5148 and id 5384. Of the 546, 36 belong to one hero, and every one of the 36 is a reading of a
legendary item.

**Talent slots.** 216 rows over 19 ids, one row per hero for eighteen of them. 8 of the 216 rows
carry a name a player reads, so the other 208 print as their noun and their id.

**Every other type.** Ability, companion pet, creature, currency, effect, finesse, gem tier, gem
trait, gem type, legendary stat, mount, mount power, relic power, set bonus, set bonus power,
talent, trait, trait power and weapon power are each one row per id, so a fold leaves them exactly
as they are drawn today.

---

## 6. Tests

New cases in `src/FellowshipExtractor.Api.Tests`, in the shape the project already renders and
asserts components in: xunit.v3 facts named as snake_case sentences, bunit's `BunitContext`, the
`Corpus` collection fixture where the real export is wanted, and `Services.AddSingleton(export.View)`
where a component needs the view.

**Colour.** A dynamic item resolves to ordinal 5's colour; a legendary item to ordinal 6's; a gem
tier to its stated rung's; a gem type and a gem trait to their type's; an item carrying one hero to
`core.text-primary`, which is the case that fails if the hero branch survives. A rarity colour is
looked up by ordinal against the shipped ladder, so a test renames a rung in a fixture ladder and
asserts the colour still resolves.

**Links.** `EntityLink` emits an inline colour for an item, a gem type, a gem tier and a gem trait,
and emits no `style` attribute at all for an ability, an effect, a talent, a talent slot, a creature
and a trait.

**Variants.** A list draws id 5250 once and id 5191 once. Id 5191 under `hero=Meiko` is named
*Betrayer's Dragonhide Gloves* and draws three variant links; under no filter it is named by its
first reading. A variant link addresses a hero who reads the name it showed. Id 5250 draws no
variant link, having one name. A hero-owned name carries that hero's colour and a shared name
carries none. A talent slot id draws once and draws no variant link.

**Counts.** The row count of a filtered result equals its stated total, and the total equals the
number of distinct ids the filter reaches. A facet count equals the number of ids a click on it
returns. `Codex.Count` equals the number of distinct `(type, id)` keys the store holds, and
`EntityStore.Count` still equals the number of rows. `Nearby` offers the same links it offers over
rows.
