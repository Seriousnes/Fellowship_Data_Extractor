# Codex component library: design

A component library for the Fellowship Codex web app, in the shape MudBlazor has but written only
for this app's own `cx-` design system.

---

## 1. Goal

Every page in `src/FellowshipExtractor.Api` builds the same handful of shapes by hand: a bordered
panel with a head and a count, a pill list, a facts table, a key/value list, a rule, a sub-head, a
note, an empty-state paragraph, a chip, a knob. `Detail.razor` and `EntityPage.razor` each hold
several hundred lines of markup in which those shapes are re-spelled per site, so a change to a
shape means finding every copy of it. This library expresses the shipped `cx-` design system as a
set of primitive components that take plain values and inject nothing, and expresses each panel of
`Detail.razor` and `EntityPage.razor` as a section component that reads its record from a cascading
parameter. The shape's styling moves with it: the one 540-line global stylesheet becomes a
`.razor.scss` beside each component that draws a class of its own, compiled by Sass and scoped by
Blazor's CSS isolation, leaving global only the design tokens, the resets and the two classes more
than one component renders. After adoption every consuming page is a flat list of component tags,
the design system has exactly one definition per shape in markup and in styling, and a bUnit test
project renders each component and asserts its markup, which is the only thing that catches the
failures this app can suffer silently: attributes `codex.js` reads by selector, and whitespace
inside `white-space: pre-line` prose.

---

## 2. Architecture

### 2.1 Two layers

**Primitives** are the design system. Each one renders a fixed skeleton of `cx-` markup from plain
parameters: strings, booleans, `RenderFragment`. A primitive has no `@inject` directive, reads no
service, resolves no colour, builds no link and knows nothing about `Entity`. That is what makes its
test a one-liner and what makes it reusable on `Browse`, `Home` and `NotFound` as readily as on the
entity page.

**Sections** are the app. Each one owns exactly one `@if` block that is inlined in `Detail.razor` or
`EntityPage.razor` today: its guard, its panel head, its badge, its note and its body. A section
reads the record it draws from a cascading parameter, injects the services it genuinely needs, and
composes primitives.

Both layers own their styling as well as their markup: a component that draws a class draws the rule
for it, in a `.razor.scss` beside it. Section 2.8 gives the mechanism and the rule that decides
which stylesheet a rule lands in.

### 2.2 Two cascades, one per level of meaning

**The entity cascade.** `Detail` keeps `Entity` as an explicit `[Parameter, EditorRequired]` and
wraps its own body in a `<CascadingValue Value="Entity">`. `Detail` renders itself
recursively, once for `Entity.Grants` and once per `Entity.LegendaryStats` row, so the cascade has
to be re-supplied at every nesting level: a single cascade declared on the page would flow past the
nested `<Detail Entity="grants" />` unchanged, and the nested sections would draw the outer item's
description, facts and stats while their guards were evaluated on the granted ability. The outer
entity's `Grants` guard is truthy, so a nested `Detail` reading the outer entity also renders
another Grants panel, and recurses until Blazor's recursion limit stops it. Re-supplying at each
level makes each `Detail` instance the sole source of truth for everything below it.

`<CascadingValue>` renders only its child content and emits no element, so `Detail`'s panels stay
siblings at the component root.

**Neither cascade sets `IsFixed`.** Measured on bUnit 2.8.6 and .NET 10: with `IsFixed="true"`, a
component re-rendered with a different value keeps drawing the first one. A probe rendering a
self-nesting component under `<CascadingValue Value="Entity" IsFixed="true">` with `first`, then
re-rendering the same instance with `second`, still rendered `first`. Removing `IsFixed` makes it
follow. The optimisation buys nothing here (each `Detail` has at most eight descendants reading the
cascade) and costs correctness on any re-render, so it is left off and the re-render test in section
8.5 is what holds that decision in place.

**The nested `<Detail>` inside the legendary-stats loop is keyed on the loop index**, so a `Detail`
instance is never carried from one roll to another across iterations. It is not keyed on the roll:
`Entity` is a `record`, whose synthesized value equality makes two structurally equal rolls produce
the same key, and Blazor throws `More than one sibling of component 'Detail' has the same key value`
from `RenderTreeDiffBuilder` on any keyed diff. That was measured too, on two structurally equal
rows re-rendered once.

**The page cascade.** `EntityPage` supplies a second cascade of type `Bearings`, a record carrying
the page's own facts: the tooltip stage address, this row's own page href, the type's browse href
and label, the network id, the readings, the reading on screen, the type's row count and the
related-list rows. It is a distinct type from `Entity`, so the two cascades never collide: Blazor
keys cascading values by type.

The five page-level sections (`Stage`, `Knobs`, `Identity`, `Readings`, `Nearby`) read `Bearings`
and are placed only by `EntityPage`. None of them appears anywhere under `Detail`.

### 2.3 The injection rule

`CodexView` is constructed from seven services, two of which reach a blob container and the export
folder. Any component that injects it forces every one of its tests to stand up that graph.

- A primitive injects nothing. Colours, hrefs, labels and counts arrive already computed.
- A section injects only what it genuinely resolves. Seven sections need `CodexView` (`Wording`,
  `Particulars`, `StatSlots`, `Ranks`, `Overflow`, `Knobs`, `Readings`), one also needs
  `EntityStore` (`Knobs`), and the remaining six inject nothing.
- Where a name can come from the static `PublicTypes.NameOf`, it does, exactly as `Home.razor`
  already does. `CodexView.Name(Entity)` is that static; calling it directly keeps `Legendaries`
  free of injection.

### 2.4 No section derives an address from its entity

`Entity.Table` and `Entity.NetId` are set on a top-level entity and absent from anything nested
inside one: none of the 200 granted abilities or 111 legendary stat lines in the export carries
either. A section that built a link out of them would produce an empty href on every nested row.

**No section component reads `Entity.Table` or `Entity.NetId`.** Every link that depends on a row's
identity arrives as a parameter or on the `Bearings` cascade:

| link | computed by | delivered as |
|---|---|---|
| the school pill href (`/browse?type={slug}&school=…`) | `Detail.Filter` | `Particulars.Schools` |
| the borrowed-art lender's href and tip | `Detail`, via `CodexView.Addresses` | `BorrowedArt.Href`, `BorrowedArt.Tip` |
| the fact-cell href and tip | `CodexView.Facts` | `Particulars.Facts` |
| the tooltip stage address | `EntityPage`, via `CodexView.Tip` | `Bearings.Address` |
| this row's own page | `EntityPage`, via `CodexView.Href` | `Bearings.Page` |
| the type's browse href and label | `EntityPage` | `Bearings.Under`, `Bearings.Label` |
| the related-list hrefs | `EntityPage.Nearby()` | `Bearings.Related` |

The browse filters a section builds from a value it is drawing (`/browse?hero=…`,
`/browse?set=…`) read no identity and stay where the value is drawn, spelled exactly as they are
spelled today.

### 2.5 File layout

```
src/FellowshipExtractor.Api/
  Bearings.cs                                   the page cascade's record
  sasscompiler.json                             the Sass build's configuration
  Styles/
    codex.scss                                  compiles to wwwroot/css/codex.css
    _shared.scss                                Sass variables and mixins; emits no stylesheet
  Components/
    _Imports.razor                              gains two @using lines
    App.razor                                   gains the scoped bundle's <link>
    Cx/                                         primitives
      Panel.razor       Panel.razor.scss
      CountBadge.razor  CountBadge.razor.scss
      Pills.razor       Pills.razor.scss
      Pill.razor        Pill.razor.scss
      Facts.razor       Facts.razor.scss
      Fact.razor        Fact.razor.scss
      KeyValues.razor   KeyValues.razor.scss
      KeyValue.razor    KeyValue.razor.scss
      SubHead.razor     SubHead.razor.scss
      Note.razor        Note.razor.scss
      Empty.razor       Empty.razor.scss
      Rule.razor        Rule.razor.scss
      Prose.razor                               no stylesheet: see section 2.9
      Chip.razor        Chip.razor.scss
      Knob.razor        Knob.razor.scss
      Icon.razor        Icon.razor.scss
    Sections/                                   sections
      Wording.razor
      Particulars.razor   Particulars.razor.scss
      StatSlots.razor     StatSlots.razor.scss
      Ranks.razor
      Overflow.razor      BorrowedArt.razor   Grants.razor   Legendaries.razor
      Stage.razor         Stage.razor.scss
      Knobs.razor         Knobs.razor.scss
      Identity.razor      Readings.razor      Nearby.razor
    Layout/                                     existing, each gaining a stylesheet
      MainLayout.razor.scss  MegaMenu.razor.scss  Omnibox.razor.scss
    Pages/                                      existing, each gaining a stylesheet
      Browse.razor.scss  Home.razor.scss  EntityPage.razor.scss
    Shared/                                     existing, each gaining a stylesheet
      Crumbs.razor.scss  EntityLink.razor.scss
```

Twenty-seven `.razor.scss` files: fifteen of the sixteen primitives, four of the thirteen sections,
and eight of the components that already exist. The nine sections with no stylesheet compose
primitives and render no class of their own; `Prose` renders the one class the design system keeps
global. Sections 3 and 4 give each file's contents, and section 2.9 gives the two under `Styles/`.

Every generated `.css` is ignored by git: see section 2.8.

The primitives folder is named `Cx`, after the prefix every class in the design system carries.
It is not named `Codex`: `FellowshipExtractor.Api` already contains a service type called `Codex`,
and a namespace `FellowshipExtractor.Api.Components.Codex` would be found by the enclosing-namespace
search from every component under `Components/`, before the search ever reaches
`FellowshipExtractor.Api`. `Codex.Spaced(school)` in `Detail.razor`, `Codex.Label(row.Table!)` in
`Browse.razor` and `Codex.Label(e.Table)` in `Home.razor` would all resolve `Codex` to a namespace
and stop compiling. `Cx` and `Sections` collide with no type in `FellowshipExtractor.Api`,
`FellowshipExtractor.Tooltip` or the SDK's implicit usings.

The rank-progression section is named `Ranks` rather than `RankRuns` for the same class of reason:
`FellowshipExtractor.Tooltip.RankRuns` is a static class and `FellowshipExtractor.Tooltip` is in
`_Imports.razor`, so a second `RankRuns` in scope would be ambiguous.

`src/FellowshipExtractor.Api/Components/_Imports.razor` gains, in the existing alphabetical run:

```razor
@using FellowshipExtractor.Api.Components.Cx
@using FellowshipExtractor.Api.Components.Sections
```

There is exactly one `_Imports.razor` in the project and no per-folder ones. Without those two
lines nothing in either folder resolves by tag name anywhere. Components take their namespace from
the project's root namespace plus their folder path, which is already how `Components/Shared`
works, so no `@namespace` directive is needed.

### 2.6 Comments

Markup stays bare. Every `@code` block carries XML doc comments on its parameters and on any choice
that is not obvious from reading it, matching the depth the `.cs` files in this project are
documented to and the doc comments the razor files already carry on `Slug`, `Under`, `Schools`,
`Filter`, `scope`, `All` and `ShowSchools`.

### 2.7 Two Razor rules that shape every call site

**A component parameter takes one expression, never mixed content.** `Href="/browse?hero=@x"` is a
compile error on a component; every interpolated value is written `@($"…")` or, when the
interpolation itself contains a string literal, computed into a local first:

```razor
@foreach (var set in sets)
{
    var where = $"/browse?set={Uri.EscapeDataString(set)}";
    <Pill Href="@where">@set</Pill>
}
```

Locals are used for every such value in this spec, because a `"` inside an `@(…)` inside a
`"`-delimited attribute is the kind of thing that compiles today and stops compiling after an
unrelated edit.

**Whitespace-only text nodes containing a newline are removed; whitespace on the same line as its
neighbours is kept.** That is why `@label <span class="cx-num">` renders a space and

```razor
@Ui.Name(reading)
<span class="cx-num">…</span>
```

does not. Every piece of markup in this spec is written with that rule in mind, and
`@preservewhitespace` is set nowhere.

**A component and a member may share a name.** `Particulars` has a `Facts` parameter and renders a
`<Facts>` tag. The Razor compiler emits component types fully qualified, and C# type-name lookup
considers only types, so the tag resolves to `FellowshipExtractor.Api.Components.Cx.Facts` and the
expression `Facts` resolves to the parameter. Both are the right domain word for what they name and
both keep it.

### 2.8 Styling: Sass, compiled per component

Styling is written in Sass and split the way the components are. There are two layers of stylesheet
and no hand-written `.css` anywhere.

**Per-component sheets.** A `Foo.razor.scss` beside `Foo.razor` is compiled by
`AspNetCore.SassCompiler` into `Foo.razor.css`, which the Razor SDK picks up as scoped CSS, rewrites
with the component's scope attribute and bundles into `FellowshipExtractor.Api.styles.css`. The
package's targets declare
`BeforeTargets="Build;ResolveScopedCssInputs;BundleMinify;ResolveProjectStaticWebAssets"`, so Sass
runs before the Razor SDK gathers scoped CSS and the chain needs no custom MSBuild. The package
carries its own dart-sass binaries per RID, so there is no npm, no node and no PATH dependency.

**The global sheet.** `Styles/codex.scss` compiles to `wwwroot/css/codex.css`, the path `App.razor`
already links. It holds the custom properties, the `html` and `body` rules, the resets inside the
`@scope (body) to (.fx-tip)` wrapper, and the handful of rules a single component cannot own.
`Styles/_shared.scss` is a partial: it declares Sass variables and mixins and emits no stylesheet of
its own, so every sheet that `@use`s it costs nothing in output.

`sasscompiler.json` at the project root. Configuration is required: with none, nothing compiles and
nothing says so.

```json
{
  "Source": "Styles",
  "Target": "wwwroot/css",
  "GenerateScopedCss": true,
  "ScopedCssFolders": [ "Components" ],
  "Configurations": {
    "Debug": { "Arguments": "--style=expanded --no-source-map" }
  }
}
```

`--no-source-map` is what keeps a `.css.map` from appearing beside every generated stylesheet, and
`.gitignore` carries `*.css.map` as well so that any configuration which emits one stays out of the
index. `FellowshipExtractor.Api.csproj` gains
`<PackageReference Include="AspNetCore.SassCompiler" Version="1.102.0" />`.

**`App.razor` links the scoped bundle.** Scoped styles are served as `{AssemblyName}.styles.css` at
the web root, and `App.razor` links only `css/codex.css` today. It gains, immediately after it:

```html
<link rel="stylesheet" href="FellowshipExtractor.Api.styles.css" />
```

**Without that line no scoped rule loads at all, and every component renders unstyled while the
build stays green.** It is the single most consequential line in this section.

**`.gitignore` gains `*.css` and `*.css.map`.** `.scss` is the only committed source of styling.
The generated files are produced by the `Compile Sass` target, which runs before `Build`, so a fresh
clone produces every one of them on its first build with no extra step.
`wwwroot/css/codex.css` is tracked today, and a pattern in `.gitignore` does not untrack a tracked
file, so the change that adds the pattern also runs `git rm --cached
src/FellowshipExtractor.Api/wwwroot/css/codex.css`.

#### The scope attribute lands on the *last* element of a selector

This is what shapes both catalogues. Blazor rewrites a scoped rule by appending the scope attribute
to the **final** element of the selector, and it puts the attribute only on elements **the Razor
compiler itself emitted**. Measured rendered markup:

```html
<p class="cx-prose" b-8pl17hwq38><span class="rt-fire">burns</span></p>
<ul class="cx-pills" b-lo48lvy7g4><li b-x0f0z8c3o4><a href="#" b-x0f0z8c3o4>Head</a></li></ul>
```

Two consequences, both silent:

**Content injected as `MarkupString` carries no scope attribute.** The `<p>` has `b-8pl17hwq38`; the
`<span class="rt-fire">` inside it, which came from a `MarkupString`, has nothing. Written as
ordinary nested Sass, `.cx-prose { .rt-fire { } }` generates `.cx-prose .rt-fire[b-8pl17hwq38]`,
which asks for the attribute on an element that never has it, so **the rule never matches**. That is
the path every description, detail line, gem-overflow sentence and rank-run value takes, because
they are all rendered through `CodexView.Text`.

**A child component's elements carry the child's hash.** `Pills` is `b-lo48lvy7g4` and the `Pill`
inside it is `b-x0f0z8c3o4`, so `.cx-pills { li a { } }` written in `Pills.razor.scss` generates
`.cx-pills li a[b-lo48lvy7g4]` and never matches the `<a>` the `Pill` rendered. The same holds for
child content: a `RenderFragment` is compiled by the component that wrote it, so its elements carry
the *caller's* hash, not the receiving component's.

`::deep` is the fix. It moves the attribute to the **first** element and leaves the descendants
unqualified, and it works as a Sass nesting block. Measured generated output:

```css
.cx-prose[b-8pl17hwq38]  .rt-ice  { color: cyan; }
.cx-pills[b-lo48lvy7g4]  li a     { color: silver; }
```

#### The ownership rule

Every rule in the shipped stylesheet is assigned by asking which component renders the element the
rule targets.

1. **One component renders it: that component's `.razor.scss` styles it directly, with no `::deep`.**
   This is the preferred shape and it is what most of the shipped stylesheet decomposes into,
   because each rule's target element belongs to exactly one component. `.cx-pills li a` becomes an
   `a` rule inside `Pill.razor.scss`; `.cx-panel-head .cx-num` becomes a rule inside
   `CountBadge.razor.scss`; `.cx-kv .cx-k` becomes a rule inside `KeyValue.razor.scss`. A class
   passed *into* a primitive counts as the primitive's: `Class="cx-rail"` lands on the `<aside>`
   `Panel` renders, so `.cx-rail` is styled in `Panel.razor.scss`.
2. **The element is `MarkupString` content, or a descendant of child content: `::deep`.** Three
   rules in the whole stylesheet are in this position, all of them a `<code>` inside content a
   caller passed in: `.cx-note code`, `.cx-facts code` and `.cx-kv code`.
3. **Two or more components render it: the rule is global.** A class with two owners cannot be
   scoped to one of them without silently dropping the other. Two families are in this position:
   `.cx-card`, which `Home` renders itself and also passes to `EntityLink` as `Class="cx-card"`, and
   `.cx-prose`, which `Prose` puts on a `<p>` and `KeyValue` puts on its `<span class="cx-v">`.

Sections 3 and 4 name the rule that governs each assignment.

#### The cascade

Two facts hold the shipped appearance in place, both measured in the browser this app is read in,
with `@scope` confirmed supported by a control:

- **Specificity is compared before scope proximity.** A rule inside the `@scope` wrapper and a rule
  in a `.razor.scss` meet on specificity, exactly as two rules in one flat sheet do today.
- **The scope attribute adds a class-unit of specificity to every scoped rule.** So a component rule
  that outranked a global element rule in the flat stylesheet outranks it by one more unit here.
  `.cx-tab:hover` was `(0,2,0)` against `a:hover`'s `(0,1,1)`; it is now `(0,3,0)`.

Proximity is consulted only when specificity ties, and no rule in the shipped stylesheet ties across
the boundary: every equal-specificity pair that source order decides between, such as
`.cx-roster a:hover` and `.cx-roster a[aria-current]`, lands in one file and keeps its order.

The `@scope (body) to (.fx-tip)` wrapper keeps doing what it does: it stops the global sheet's
element-level rules at the tooltip fragment's root class. Scoped rules need no such bound. The
fragment's markup is rendered by the API into a shadow root that `codex.js` attaches to both the
`#cx-tip` layer and the `[data-stage]` div, and it carries no scope attribute of this project's, so
nothing in the bundle can select it.

### 2.9 The global stylesheet

`src/FellowshipExtractor.Api/Styles/codex.scss`, compiled to `wwwroot/css/codex.css`.

```scss
@use "shared" as *;

/*
    Values for the properties and variables here are set in App.razor from data-mined tokens.
*/
:root {
  --ground: var(--core-secondary-1000);
  --panel: var(--core-secondary-900);
  --raised: var(--core-secondary-800);
  --sunken: #08111a;
  --line: var(--core-secondary-700);
  --line-soft: #16212c;
  --ink: var(--core-text-primary);
  --muted: var(--core-text-secondary);
  --faint: var(--core-text-info);
  --gold: var(--core-primary-main);
  --gold-bright: var(--core-primary-200);
  --gold-deep: var(--core-primary-500);

  --display: ArnoPro, "Arno Pro", "Iowan Old Style", "Palatino Linotype", Palatino, "Book Antiqua",
             "Times New Roman", serif;
  --sans: "Noto Sans", system-ui, -apple-system, "Segoe UI", Roboto, sans-serif;
  --mono: ui-monospace, "Cascadia Mono", "JetBrains Mono", Menlo, Consolas, monospace;

  --rail: 268px;
  --wide: 1440px;
}

html { scrollbar-gutter: stable; }

body {
  margin: 0;
  background:
    radial-gradient(1200px 600px at 50% -280px, #16222e 0%, transparent 70%),
    var(--ground);
  color: var(--ink);
  font: 400 14px/1.55 var(--sans);
  font-variant-numeric: tabular-nums;
  min-height: 100vh;
}

@scope (body) to (.fx-tip) {

  *, *::before, *::after { box-sizing: border-box; }

  a { color: var(--gold-bright); text-decoration: none; }
  a:hover { text-decoration: underline; }

  :focus-visible { outline: 2px solid var(--gold-bright); outline-offset: 2px; }

  h1 {
    font-family: var(--display); font-weight: 400; letter-spacing: .04em;
    font-size: clamp(26px, 4vw, 40px); line-height: 1.1; margin: 6px 0 14px;
  }
  h2 {
    font-family: var(--display); font-weight: 400; letter-spacing: .07em; text-transform: uppercase;
    font-size: 19px; margin: 0 0 10px; color: var(--core-secondary-000);
  }

  /* Two components draw a card: Home's own menu-entry anchor, and the EntityLink the showcase
     passes Class="cx-card" to. A class with two owners is styled here, where no scope attribute
     narrows it to one of them. */
  .cx-card {
    @include plated;
    display: block; color: var(--ink);
  }
  .cx-card:hover { text-decoration: none; }

  /* Prose is styled here for the same reason - a <p> from Prose and a <span class="cx-v cx-prose">
     from KeyValue both wear the class - and for a second one. App.razor emits
     Ui.RichTextCss("cx-prose") into the page's inline <style>, which writes one .cx-prose .rt-xxx
     rule per shipped text-style row at runtime. Those values are mined from the build and nothing
     here knows them, so the colour table cannot move into a stylesheet. Keeping the rest of the
     family beside it keeps one specificity story for the whole of .cx-prose. The runs themselves
     arrive as a MarkupString and carry no scope attribute, which is the other reason these three
     rules cannot be scoped. */
  .cx-prose { white-space: pre-line; color: var(--ink); }
  .cx-prose .sub { font-weight: 600; border-bottom: 1px dotted currentColor; cursor: help; }
  .cx-prose .tok { color: var(--semantic-negative); font-family: var(--mono); }
  .cx-prose .rt-unstyled { color: inherit; }

  /* Drawn by no component today. Kept whole, because the panels this markup belongs to come back
     with the shape they had: a Panel As="details" for the record, a token card for the
     substitutions. */
  .cx-token { border-bottom: 1px solid var(--line-soft); padding: 8px 0; }
  .cx-token:last-child { border-bottom: 0; }
  .cx-token-name { font-family: var(--mono); color: var(--gold-bright); font-size: 12.5px; }
  .cx-token-read { color: var(--ink); font-weight: 600; }
  .cx-token-work { color: var(--faint); font-size: 12px; }

  pre.cx-raw {
    margin: 0; padding: 10px 12px; background: var(--sunken); border: 1px solid var(--line);
    overflow-x: auto; font: 12px/1.5 var(--mono); color: var(--core-secondary-100);
  }
}
```

**The custom properties are runtime values.** `--core-secondary-900`, `--core-primary-main`,
`--semantic-negative`, `--game-stat-primary`, `--chrome-panel` and the rest are written into an
inline `<style>` by `App.razor` from `Ui.Tokens.CssVariables()` and `Ui.Chrome`, out of data mined
from the shipped build. Sass never sees a value for any of them. Every stylesheet in the project
therefore refers to them as `var(--…)` and no Sass variable shadows one, precomputes one, or is
named after one: a `$gold: #d4af37` anywhere in this project would be a second, wrong palette that
the build cannot tell apart from the real one.

`src/FellowshipExtractor.Api/Styles/_shared.scss` holds what genuinely is a build-time constant:
the literals the stylesheet repeats and the idioms it re-spells.

```scss
/// The page gutter: the masthead bar, the mega-menu columns, <main> and the footer all use it.
$gutter: clamp(12px, 2.4vw, 28px);

/// The ink a gold slab is printed in. A literal rather than a token, because the shipped palette
/// carries no colour for text on gold.
$on-gold: #0b141d;

/// The client's own nine-sliced texture, the same one the tooltip draws itself on: a 2px warm gold
/// border round a #121D28 interior. Worn by a panel and by a card.
@mixin plated {
  border: 2px solid transparent;
  border-image: var(--chrome-panel) 2 fill stretch;
  padding: 14px 16px;
}

/// A field sunk into the ground: the omnibox, the range inputs, the knobs, a roster entry.
@mixin sunken {
  background: var(--sunken);
  border: 1px solid var(--line);
  color: var(--ink);
}

/// What a sunken field does when it takes focus.
@mixin edged {
  border-color: var(--gold-deep);
  outline: none;
}

/// A gold slab: the apply button, the omnibox's view-all row, a pressed chip, the current page.
@mixin gold-slab {
  background: var(--gold-deep);
  color: $on-gold;
}

/// The small uppercase caption over a list, worn by a sub-head and by a mega-menu section head.
@mixin eyebrow {
  margin: 0 0 3px;
  font-size: 10px;
  letter-spacing: .14em;
  text-transform: uppercase;
  color: var(--faint);
}
```

A `.razor.scss` that needs them opens with `@use "../../Styles/shared" as *;`, which dart-sass
resolves relative to the importing file. `codex.scss` opens with `@use "shared" as *;`.

---

## 3. The primitive catalogue

Every primitive lives in `Components/Cx/`, has no `@inject`, and types `ChildContent` as
`RenderFragment?`. Fifteen of the sixteen carry a `.razor.scss` beside them, given in full below;
`Prose` is the exception and section 3.13 says why.

### 3.1 `Panel`

The bordered frame every section sits in. `cx-panel` is always the outermost class and
`cx-panel-head` is always the first child of it. The badge is always the last child of the head,
because `.cx-panel-head .cx-num { margin-left: auto }` is what pushes it to the right edge.

```razor
@if (As == "details")
{
    <details class="@Classes" aria-label="@Aria">
        @if (Head is not null)
        {
            <summary class="cx-panel-head">
                @Head
                @if (Badge is not null)
                {
                    <CountBadge Text="@Badge" />
                }
            </summary>
        }
        @ChildContent
    </details>
}
else if (As == "aside")
{
    <aside class="@Classes" aria-label="@Aria">
        @if (Head is not null)
        {
            <div class="cx-panel-head">
                @Head
                @if (Badge is not null)
                {
                    <CountBadge Text="@Badge" />
                }
            </div>
        }
        @ChildContent
    </aside>
}
else
{
    <div class="@Classes" aria-label="@Aria">
        @if (Head is not null)
        {
            <div class="cx-panel-head">
                @Head
                @if (Badge is not null)
                {
                    <CountBadge Text="@Badge" />
                }
            </div>
        }
        @ChildContent
    </div>
}
```

The head's children sit on their own lines. Every whitespace node between them contains a newline,
so the compiler removes it and the head renders as `<div class="cx-panel-head">Stat slots<span
class="cx-num">3</span></div>`.

| parameter | type | default | meaning |
|---|---|---|---|
| `Head` | `string?` | `null` | the head's text. Null renders no head element at all. |
| `Badge` | `string?` | `null` | the `cx-num` trailing the head. Null renders no badge. |
| `Class` | `string?` | `null` | extra classes, appended after `cx-panel`. |
| `As` | `string` | `"div"` | `"div"`, `"aside"` or `"details"`. |
| `Aria` | `string?` | `null` | the `aria-label`. Null omits the attribute. |
| `ChildContent` | `RenderFragment?` | `null` | the panel body. |

```csharp
private string Classes => Class is null ? "cx-panel" : $"cx-panel {Class}";
```

Variants covered: headless (`EntityPage`'s knobs panel, `Browse`'s error and empty-results panels,
`NotFound`'s panel); head with no badge; head with an integer badge; head with a string badge
(`Grants an ability` names the granted ability, `The roster` reads `12 heroes`); the `aside` root
with an extra class and an `aria-label` (`Browse`'s filter rail); the `details`/`summary` root.

Call sites replaced: every `<div class="cx-panel">` in `Browse`, `EntityPage`, `Home`, `NotFound`
and `Detail`.

Two rendered differences from the markup it replaces, both verified equivalent:

- `Browse`'s rail renders `class="cx-panel cx-rail"` rather than `class="cx-rail cx-panel"`. Class
  order is not read by any selector in the design system.
- The head renders no literal space before the badge. `.cx-panel-head` is `display: flex` with
  `gap: 10px`, and the badge carries `margin-left: auto`, so the head text and the badge are
  separate flex items and a text node's trailing space collapses.

`Panel.razor.scss`:

```scss
@use "../../Styles/shared" as *;

.cx-panel {
  position: relative;
  @include plated;
}

.cx-panel + .cx-panel { margin-top: 16px; }

/* The entity page's side column is a grid and spaces its own children, so the panels stacked in it
   drop the adjacency margin. Both elements in the selector are this component's, and the attribute
   lands on the second, which is the one being styled. */
.cx-entity-side .cx-panel + .cx-panel { margin-top: 0; }

.cx-panel-head {
  display: flex; align-items: center; gap: 10px; margin: -14px -16px 12px;
  padding: 9px 16px;
  background: var(--chrome-header) center/100% 100% no-repeat;
  font-family: var(--display); font-size: 15px; letter-spacing: .1em; text-transform: uppercase;
  color: var(--gold-bright);
}

/* Browse's filter rail is a Panel with As="aside" and Class="cx-rail". The class is the caller's
   word but the <aside> it lands on is this component's element, so the rule lives here. */
.cx-rail { position: sticky; top: 66px; max-height: calc(100vh - 82px); overflow-y: auto; }

/* One column below the rail's width, and the results first: a filter panel a reader has to scroll
   past to reach what they asked for is a filter panel in the way. The grid half of this rule is in
   Browse.razor.scss, which owns .cx-browse. */
@media (max-width: 1080px) {
  .cx-rail { position: static; max-height: none; order: 2; }
}

/* The As="details" variant: a summary that opens and shuts rather than a head that only names. */
.cx-raw-record > summary { cursor: pointer; list-style: none; }
.cx-raw-record > summary::-webkit-details-marker { display: none; }
.cx-raw-record > summary::after { content: "\002B"; margin-left: auto; color: var(--gold-deep); }
.cx-raw-record[open] > summary::after { content: "\2212"; }
.cx-raw-record[open] > summary { margin-bottom: 12px; }
```

`.cx-panel-head .cx-num` is not here. The badge is a `CountBadge`, a child component whose span
carries its own hash, so the rule that pushes it to the right edge lives in `CountBadge.razor.scss`.

### 3.2 `CountBadge`

```razor
<span class="cx-num">@Text</span>
```

| parameter | type | default |
|---|---|---|
| `Text` | `string` `[Parameter, EditorRequired]` | `string.Empty` |

The `cx-num` span appears in four positions, all sharing the class and differing only in what CSS
does with it: trailing a panel head (`margin-left: auto`), trailing a pill anchor (border,
background and padding stripped, 4px of left padding added), trailing a facet value
(`margin-left: auto`) and trailing a browse tab. `Panel` and `Pill` render it internally; `Browse`'s
tabs, facet values and `MegaMenu`'s counts render it directly.

Call sites replaced: all twelve `<span class="cx-num">` emissions, across `Detail`, `EntityPage`,
`Home`, `Browse` and `MegaMenu`.

`CountBadge.razor.scss`. All five `.cx-num` rules in the shipped stylesheet gather here, because
every one of them targets this span and this component is the only thing that renders it. Each
positional rule keeps its ancestor selector, which is matched on the class alone and needs no
attribute of its own:

```scss
.cx-num { font-size: 10.5px; color: var(--faint); font-variant-numeric: tabular-nums; }

.cx-panel-head .cx-num { margin-left: auto; }
.cx-facet-values .cx-num { margin-left: auto; }
.cx-tab[aria-current] .cx-num { color: var(--gold-deep); }
.cx-pills .cx-num { border: 0; background: none; padding: 0 0 0 4px; }
```

The `.cx-pills` rule is what supplies the 4px gap before a pill's badge. Its `border: 0` and
`background: none` had a second job in the flat stylesheet, undoing the pill's own border and
ground; a `Pill` now styles only the elements it renders, so the badge never picks those up in the
first place. Both declarations are kept: the rule reads as one thing and states what the badge is.

### 3.3 `Pills`

```razor
<ul class="cx-pills">
    @ChildContent
</ul>
```

| parameter | type | default |
|---|---|---|
| `ChildContent` | `RenderFragment?` | `null` |

`ChildContent` rather than an `Items` list, because the "Offered to" list is one `<ul>` fed by two
different collections: `Entity.AllowedRoles` first, then `Entity.AllowedAttributes`. A single-
collection parameter cannot express that.

Call sites replaced: the seven `<ul class="cx-pills">` openings.

`Pills.razor.scss`:

```scss
.cx-pills { display: flex; flex-wrap: wrap; gap: 5px; margin: 0; padding: 0; list-style: none; }
```

That is the whole of it. `.cx-pills li a` and `.cx-pills li span` are the `Pill`'s elements and are
styled there; written here they would compile to `.cx-pills li a[b-pills]` and match nothing.

### 3.4 `Pill`

```razor
@if (Href is null)
{
    <li>
        <span style="@Tone">
            @ChildContent
            @if (Badge is not null)
            {
                <CountBadge Text="@Badge" />
            }
        </span>
    </li>
}
else
{
    <li>
        <a href="@Href" style="@Tone">
            @ChildContent
            @if (Badge is not null)
            {
                <CountBadge Text="@Badge" />
            }
        </a>
    </li>
}
```

with `private string? Tone => Colour is null ? null : $"color:{Colour}";`

| parameter | type | default | meaning |
|---|---|---|---|
| `Href` | `string?` | `null` | null renders a `<span>`, non-null an `<a>`. |
| `Colour` | `string?` | `null` | null omits `style` entirely; any other value renders `style="color:{Colour}"`. |
| `Badge` | `string?` | `null` | the trailing `cx-num`. |
| `ChildContent` | `RenderFragment?` | `null` | the pill's label. |

`Colour` distinguishes "no style attribute" from `color:inherit`, and both are in use today: the
hero pills omit the attribute when the token is missing and fall back to the stylesheet's
`var(--muted)`, while the role pills always emit `style="color:inherit"` and take the panel's ink
colour. Callers keep their own `?? "inherit"` where they have one; `Pill` only decides between
omitting the attribute and writing it.

Rendered difference from the markup it replaces: no literal space between the label and the badge.
`.cx-pills .cx-num { padding: 0 0 0 4px }` supplies the gap, which is why the five reading pills
already render with no space today and look right.

Call sites replaced: roughly twenty `<li>` emissions across seven pill lists.

`Pill.razor.scss`. `Pill` renders the `<li>`, the `<a>` and the `<span>`, so the two shipped rules
drop the `li` from their selector and keep the `.cx-pills` ancestor:

```scss
.cx-pills a, .cx-pills span {
  display: inline-block; padding: 2px 9px; border: 1px solid var(--line); border-radius: 999px;
  font-size: 12px; color: var(--muted); background: var(--sunken);
}

.cx-pills a:hover { border-color: var(--gold-deep); color: var(--gold-bright); text-decoration: none; }
```

The `<ul class="cx-pills">` is the `Pills` component's element and carries a different hash, so the
ancestor is matched on its class alone and needs no attribute. It is kept rather than dropped for a
bare `a, span`, because the shipped stylesheet says a pill is styled *in a pill list* and a bare
selector would state something wider than that.

`.cx-pills span` reaches this component's own label span and not the `CountBadge` beside it, which
carries a different hash again. That is the isolation doing the work the `.cx-pills .cx-num` reset
used to do by hand.

### 3.5 `Facts`

```razor
<table class="cx-facts">
    @ChildContent
</table>
```

| parameter | type | default |
|---|---|---|
| `ChildContent` | `RenderFragment?` | `null` |

No `<thead>` and no `<tbody>`: the rows are bare `<tr>` children of `<table>`, which is what both
existing tables emit and what `.cx-facts tr` and `.cx-facts th` select. With no children it renders
`<table class="cx-facts"></table>`, which the Details panel depends on: an entity with exactly one
fact renders a full first table and an empty second one, and `.cx-pairs`
(`grid-template-columns: repeat(auto-fit, minmax(280px, 1fr))`) keeps two columns because of it.

`Facts.razor.scss`:

```scss
.cx-facts { width: 100%; border-collapse: collapse; font-size: 13px; }
```

The row, cell and unit rules belong to `Fact`, which renders those elements.

### 3.6 `Fact`

```razor
<tr>
    <th>@Label</th>
    <td>
        @ChildContent
        @if (Unit is not null)
        {
            <span class="cx-unit">@Unit</span>
        }
    </td>
</tr>
```

| parameter | type | default | meaning |
|---|---|---|---|
| `Label` | `string` `[Parameter, EditorRequired]` | `string.Empty` | the `<th>`. |
| `Unit` | `string?` | `null` | the unit suffix. Null renders no span. |
| `ChildContent` | `RenderFragment?` | `null` | the `<td>` body. |

There is no whitespace between the value and the unit span, matching both existing sites: a duration
reads `2.5s`, not `2.5 s`.

Rendered difference from the markup it replaces: `Detail`'s facts table emits no
`<span class="cx-unit"></span>` for a fact with no unit. An empty inline span with no padding and no
content renders nothing, and `EntityPage`'s identity rows already emit none.

`Fact.razor.scss`:

```scss
tr { border-bottom: 1px solid var(--line-soft); }

th {
  text-align: left; font-weight: 600; color: var(--muted); padding: 6px 12px 6px 0; width: 1%;
  white-space: nowrap;
}

td {
  padding: 6px 0;

  /* A <code> in a fact cell arrives as the caller's child content and carries the caller's scope
     attribute, so it is reached through ::deep. */
  ::deep code { font: 12px/1.5 var(--mono); color: var(--core-secondary-100); }
}

.cx-unit { color: var(--faint); }
```

The `<tr>`, `<th>`, `<td>` and unit span are all this component's, so the shipped `.cx-facts tr`,
`.cx-facts th`, `.cx-facts td` and `.cx-facts .cx-unit` drop their ancestor selector. A `<tr>`
outside a `.cx-facts` table is not a position this component can be rendered in, so the bare
selectors state nothing wider than the shipped ones do.

`.cx-facts code` is the one rule of the five that cannot lose its ancestor: the `<code>` is not this
component's element. It is written on the `<td>`, which narrows it from anywhere in the table to the
value cell. That is the whole of its reach in practice: the `<th>` is rendered by `Fact` from the
`Label` string, so no caller can put markup of any kind in a header cell.

### 3.7 `KeyValues`

```razor
<ul class="cx-kv">
    @ChildContent
</ul>
```

| parameter | type | default |
|---|---|---|
| `ChildContent` | `RenderFragment?` | `null` |

`KeyValues.razor.scss`:

```scss
.cx-kv { list-style: none; margin: 0; padding: 0; font-size: 13px; }
```

### 3.8 `KeyValue`

One line, with no literal text anywhere in it, because one of its two callers renders into a
`.cx-prose` context:

```razor
<li><span class="cx-k">@Key</span><span class="@Classes">@ChildContent</span></li>
```

with

```csharp
private string Classes =>
    "cx-v" + (Class is null ? null : " " + Class) + (Missing ? " cx-miss" : null);
```

| parameter | type | default | meaning |
|---|---|---|---|
| `Key` | `string` `[Parameter, EditorRequired]` | `string.Empty` | the `cx-k` span. |
| `Missing` | `bool` | `false` | adds `cx-miss` to the value span, which marks a value as absent or disputed rather than present. |
| `Class` | `string?` | `null` | extra classes on the value span. The rank-progression "Printed" row passes `"cx-prose"`. |
| `ChildContent` | `RenderFragment?` | `null` | the `cx-v` span's body. |

Renders `class="cx-v"`, `class="cx-v cx-prose"` or `class="cx-v cx-miss"`, matching the three
spellings in use.

`KeyValue.razor.scss`:

```scss
li {
  display: flex; gap: 10px; padding: 4px 0; border-bottom: 1px solid var(--line-soft);

  &:last-child { border-bottom: 0; }

  /* A <code> in a row is the caller's child content, so it is reached through ::deep. Written on
     the <li> rather than on .cx-v, which is what .cx-kv code reaches today. */
  ::deep code { font: 12px/1.5 var(--mono); color: var(--core-secondary-100); }
}

.cx-k { color: var(--muted); flex: 1 1 auto; min-width: 0; word-break: break-word; }
.cx-v { flex: 0 0 auto; font-weight: 600; }
.cx-v.cx-miss { font-weight: 400; color: var(--faint); font-style: italic; }
.cx-miss { color: var(--faint); font-style: italic; }
```

`Missing="true"` is the only way `cx-miss` reaches the page, so both `cx-miss` rules live here even
though the second carries no `.cx-kv` ancestor in the shipped stylesheet.

`.cx-prose` is not here. When `Class="cx-prose"` is passed, the span wears a class two components
render, so it is styled globally: see section 2.9.

### 3.9 `SubHead`

```razor
<p class="cx-sub-head">@ChildContent</p>
```

| parameter | type | default |
|---|---|---|
| `ChildContent` | `RenderFragment?` | `null` |

`ChildContent` rather than a string, because two call sites compose their text from an expression:
`Schools`/`School` by count, and `Set` with an optional ` (4 pieces)` suffix.

Call sites replaced: the seven `<p class="cx-sub-head">` emissions.

`SubHead.razor.scss`:

```scss
@use "../../Styles/shared" as *;

.cx-sub-head { @include eyebrow; }
```

The shipped stylesheet writes this rule as `.cx-mega-sec-head, .cx-sub-head`, a selector list whose
two halves belong to different components. The mixin is what keeps the two halves the same rule
after they are split: `MegaMenu.razor.scss` includes it too.

### 3.10 `Note`

```razor
<p class="cx-note">@ChildContent</p>
```

| parameter | type | default |
|---|---|---|
| `ChildContent` | `RenderFragment?` | `null` |

`ChildContent` rather than a string, because the borrowed-art note contains an `<a data-tip>` inline.

Call sites replaced: the three `<p class="cx-note">` emissions.

`Note.razor.scss`:

```scss
.cx-note {
  color: var(--muted); font-size: 12px; margin: 0 0 8px;

  /* The note's body is child content, so a <code> in it carries the caller's scope attribute. */
  ::deep code { font: 12px/1.5 var(--mono); color: var(--core-secondary-100); }
}
```

The borrowed-art note's `<a data-tip>` is child content too, and needs no rule: the global anchor
styling reaches it.

### 3.11 `Empty`

```razor
<p class="cx-empty">
    @if (Said is not null)
    {
        <b>@Said</b><br />
    }
    @ChildContent
</p>
```

| parameter | type | default | meaning |
|---|---|---|---|
| `Said` | `string?` | `null` | the bold headline and its `<br />`. Null renders neither. |
| `ChildContent` | `RenderFragment?` | `null` | the detail line. |

`Said` is the same word `NotFound.razor` already uses for this sentence. Three of the four call
sites follow the `<b>headline</b><br />detail` shape; the tooltip stage's `Loading...` placeholder
is the fourth and passes no `Said`.

Call sites replaced: `Browse`'s unreadable-filter and no-results paragraphs, `EntityPage`'s stage
placeholder, `NotFound`'s paragraph.

`Empty.razor.scss`:

```scss
.cx-empty {
  padding: 40px 16px; text-align: center; color: var(--muted);

  b { color: var(--ink); }
}
```

The `<b>` is rendered by `Empty` from `Said`, not passed in, so it needs no `::deep`.

### 3.12 `Rule`

```razor
<hr class="cx-rule" />
```

No parameters. Always a leading separator inside a panel body, never a trailing one, which is what
makes a Facts-less entity show a rule directly under the "Details" head with nothing above it. That
rendering is preserved: see section 4.2.

Call sites replaced: the nine `<hr class="cx-rule" />` emissions.

`Rule.razor.scss`:

```scss
.cx-rule {
  height: 8px; border: 0; margin: 12px 0;
  background: var(--chrome-rule) 0 0/100% 100% no-repeat;
}
```

### 3.13 `Prose`

```razor
<p class="cx-prose">@ChildContent</p>
```

| parameter | type | default |
|---|---|---|
| `ChildContent` | `RenderFragment?` | `null` |

`cx-prose` is a generated-CSS scope name, not merely a class. `App.razor` emits
`Ui.RichTextCss("cx-prose")` into the page's inline `<style>`, and `RichText.cs` writes one rule per
shipped style row prefixed with that scope, so every `rt-*` class the game's rich text produces is
styled only as `.cx-prose .rt-xxx`. **The element that directly contains the rich-text
`MarkupString` must itself carry `class="cx-prose"`.** Wrapping the markup in one more level, or
renaming the class, unstyles every coloured run in every description with no error and no warning.

The global stylesheet also sets `.cx-prose { white-space: pre-line }`, so newlines that reach the browser
inside it are rendered line breaks. `Prose`'s markup is therefore one line and contains no literal
text, and its call sites are written on one line for the same reason.

Call sites replaced: the description paragraph, each detail-line paragraph, and the gem-overflow
paragraph. The fourth `.cx-prose` element is a `<span class="cx-v cx-prose">`, which `KeyValue`
renders via its `Class` parameter.

**`Prose` has no `.razor.scss`.** It is the one primitive whose class the design system keeps
global, for two reasons that both come out of the finding in section 2.8. `.cx-prose` has two
component owners, this `<p>` and `KeyValue`'s span, so a scoped rule would style one and silently
drop the other. And every run inside it arrives as a `MarkupString` with no scope attribute, so
`.cx-prose .sub` scoped here would compile to `.cx-prose[b-prose] .sub` while the runtime colour
table stayed at `.cx-prose .rt-fire`: one family of rules on the same runs, at two specificities,
maintained in two places. The four `.cx-prose` rules therefore sit in `Styles/codex.scss` beside the
comment explaining where the colour table comes from. Section 2.9 gives them.

The class on the element is still the contract. Nothing about the move changes what this component
must render.

### 3.14 `Chip`

```razor
<span class="cx-chip" style="@Tone">@ChildContent</span>
```

with `private string? Tone => Colour is null ? null : $"color:{Colour}";`

| parameter | type | default |
|---|---|---|
| `Colour` | `string?` | `null` |
| `ChildContent` | `RenderFragment?` | `null` |

The chip's styling is scoped: `table.cx-rows .cx-chip` is the only rule that gives it a border,
padding and a size. **A `Chip` styles only inside the browse results table.** Rendering one anywhere
else produces an unstyled inline span.

Call sites replaced: `Browse`'s armour-type, primary-stat, hero, overflow-count and school chips.

`Chip.razor.scss`:

```scss
/* The chip's whole appearance is this one rule, and it selects only inside the results table.
   The ancestor is Browse's element and is matched on its class; the span is this component's and
   is what the scope attribute lands on. */
table.cx-rows .cx-chip {
  display: inline-block; padding: 0 6px; border: 1px solid var(--line); border-radius: 2px;
  font-size: 11px; color: var(--muted); margin: 1px 3px 1px 0; white-space: nowrap;
}
```

### 3.15 `Knob`

```razor
@if (As == "div")
{
    <div class="cx-knob">
        <span>@Caption</span>
        @ChildContent
    </div>
}
else
{
    <label class="cx-knob">
        <span>@Caption</span>
        @ChildContent
    </label>
}
```

| parameter | type | default | meaning |
|---|---|---|---|
| `Caption` | `string` `[Parameter, EditorRequired]` | `string.Empty` | the `<span>` caption, which `.cx-knob > span` styles. |
| `As` | `string` | `"label"` | `"label"` when the knob holds one form control, `"div"` when it holds a button group. |
| `ChildContent` | `RenderFragment?` | `null` | the control. |

The element genuinely varies: a `<label>` wrapping a single `<select>` or `<input>` is correct HTML
and gives the caption its click target, and a `<label>` wrapping a group of buttons is not.

Call sites replaced: the four `cx-knob` emissions on the entity page.

`Knob.razor.scss`:

```scss
.cx-knob {
  display: block; margin-bottom: 10px;

  > span {
    display: block; font-size: 11px; letter-spacing: .12em; text-transform: uppercase;
    color: var(--faint); margin-bottom: 3px;
  }
}
```

The caption span is this component's. The `<select>`, the `<input>`s and the rung buttons are the
`Knobs` section's child content, so `.cx-knob select`, `.cx-knob input` and their focus rules live
in `Knobs.razor.scss`, written on the elements that section renders. That is rule 1 of section 2.8,
not `::deep`: the control has a component that owns it.

### 3.16 `Icon`

```razor
<img alt="" loading="lazy" src="@Src" />
```

| parameter | type | default |
|---|---|---|
| `Src` | `string` `[Parameter, EditorRequired]` | `string.Empty` |

Call sites replaced: the three byte-identical `<img alt="" loading="lazy" src="…" />` emissions in
`Browse`, `Home` and `Omnibox`. The `Omnibox` one renders interactively; see section 6.6.

`Icon.razor.scss`. Three rules from three different pages converge here, because the `<img>` is this
component's element wherever it is placed:

```scss
table.cx-rows .cx-art img {
  display: block; width: 34px; height: 34px; object-fit: contain;
  background: var(--chrome-slot) center/100% 100% no-repeat; padding: 2px;
}

.cx-omnibox-hit img {
  width: 30px; height: 30px; object-fit: contain; flex: 0 0 auto;
  background: var(--chrome-slot) center/100% 100% no-repeat;
}

.cx-linked img {
  width: 30px; height: 30px; object-fit: contain;
  background: var(--chrome-slot) center/100% 100% no-repeat;
}
```

The three sizes genuinely differ, so they stay three rules rather than one. Each ancestor class is
still owned by the page that renders it and is matched on the class alone.

---

## 4. The section catalogue

Every section lives in `Components/Sections/` and guards itself, so a consuming page places it
unconditionally.

Four sections carry a `.razor.scss`, and they are the four that render markup no primitive covers:
`Particulars` (the facts pairing grid), `StatSlots` (the stat lines), `Stage` (the tooltip stage
div) and `Knobs` (the rung group and the form controls a `Knob` wraps). The other nine compose
primitives and draw no class of their own. The entity page's own grid, `.cx-entity`,
`.cx-entity-top` and `.cx-entity-side`, belongs to `EntityPage.razor.scss` in section 6.2;
`.cx-entity-tip` sits with `Stage` in section 4.9, which is the component that renders it.

The eight entity sections read

```csharp
[CascadingParameter] private Entity Entity { get; set; } = new();
```

and the five page sections read

```csharp
[CascadingParameter] private Bearings Here { get; set; } = new();
```

The cascaded entity type is whatever type `Detail.Entity` is declared as, so the two stay in step.

### 4.1 `Wording`

- **Guard**: `Entity.Description is not null || Entity.Detail is { Count: > 0 }`
- **Head**: `Text`. No badge, no note.
- **Reads**: `Entity.Description`, `Entity.Detail`, `Entity.Placeholders`, `Entity.Tags`
- **Injects**: `CodexView Ui`
- **Parameters**: none

```razor
@inject CodexView Ui

@if (Entity.Description is not null || Entity.Detail is { Count: > 0 })
{
    <Panel Head="Text">
        @if (Entity.Description is not null)
        {
            <Prose>@Ui.Text(Entity.Description, Entity.Placeholders, Entity.Tags)</Prose>
        }
        @foreach (var line in Entity.Detail ?? [])
        {
            <Rule />
            <Prose>@Ui.Text(line, Entity.Placeholders, Entity.Tags)</Prose>
        }
    </Panel>
}
```

The `?? []` on the loop is kept: the guard is an `||`, so the loop must tolerate a null `Detail`
when `Description` alone is set.

### 4.2 `Particulars`

- **Guard**:
  ```csharp
  Facts.Count > 0 || Schools.Count > 0 || Entity.Heroes is { Count: > 0 } ||
  Entity.SetBonus is { Count: > 0 } || Entity.ScalesOff is { Count: > 0 } ||
  Entity.AllowedRoles is { Count: > 0 } || Entity.AllowedAttributes is { Count: > 0 }
  ```
- **Head**: `Details`. No badge, no note.
- **Reads from the cascade**: `Entity.Heroes`, `Entity.SetBonus`, `Entity.SetCount`,
  `Entity.ScalesOff`, `Entity.AllowedRoles`, `Entity.AllowedAttributes`
- **Injects**: `CodexView Ui`, for `Ui.HeroColour` and `Ui.Tokens.Role`
- **Parameters**:

| parameter | type | default |
|---|---|---|
| `Facts` | `IReadOnlyList<(string Label, string Value, string? Unit, string? Href, string? Tip)>` | `[]` |
| `Schools` | `IReadOnlyList<(string Label, string Href)>` | `[]` |

Six subsections. Every one except Facts opens with a `<Rule />` inside its own guard, so an entity
with no facts but with schools shows a rule immediately under the panel head. That is the rendering
today and it is preserved deliberately.

`AllowedAttributes` is in the panel guard but has no subsection of its own: it shares the "Offered
to" list with `AllowedRoles`, roles first. An entity carrying attributes and no roles opens the
Details panel and renders only the head. That is preserved.

```razor
@inject CodexView Ui

@if (Facts.Count > 0 || Schools.Count > 0 || Entity.Heroes is { Count: > 0 } ||
     Entity.SetBonus is { Count: > 0 } || Entity.ScalesOff is { Count: > 0 } ||
     Entity.AllowedRoles is { Count: > 0 } || Entity.AllowedAttributes is { Count: > 0 })
{
    <Panel Head="Details">
        @if (Facts.Count > 0)
        {
            <div class="cx-pairs">
                @for (var half = 0; half < 2; half++)
                {
                    var split = (Facts.Count + 1) / 2;
                    var slice = half == 0 ? Facts.Take(split) : Facts.Skip(split);

                    <Facts>
                        @foreach (var fact in slice)
                        {
                            <Fact Label="@fact.Label" Unit="@fact.Unit">
                                @if (fact.Href is null)
                                {
                                    @fact.Value
                                }
                                else
                                {
                                    <a href="@fact.Href" data-tip="@fact.Tip">@fact.Value</a>
                                }
                            </Fact>
                        }
                    </Facts>
                }
            </div>
        }

        @if (Schools.Count > 0)
        {
            <Rule />
            <SubHead>@(Schools.Count == 1 ? "School" : "Schools")</SubHead>
            <Pills>
                @foreach (var school in Schools)
                {
                    <Pill Href="@school.Href">@school.Label</Pill>
                }
            </Pills>
        }

        @if (Entity.Heroes is { Count: > 0 } heroes)
        {
            <Rule />
            <SubHead>Heroes</SubHead>
            <Pills>
                @foreach (var hero in heroes)
                {
                    var where = $"/browse?hero={Uri.EscapeDataString(hero)}";
                    <Pill Href="@where" Colour="@Ui.HeroColour(hero)">@hero</Pill>
                }
            </Pills>
        }

        @if (Entity.SetBonus is { Count: > 0 } sets)
        {
            <Rule />
            <SubHead>Set@(Entity.SetCount is { } pieces ? $" ({pieces} pieces)" : "")</SubHead>
            <Pills>
                @foreach (var set in sets)
                {
                    var where = $"/browse?set={Uri.EscapeDataString(set)}";
                    <Pill Href="@where">@set</Pill>
                }
            </Pills>
        }

        @if (Entity.ScalesOff is { Count: > 0 } scaling)
        {
            <Rule />
            <SubHead>Scales off</SubHead>
            <Pills>
                @foreach (var stat in scaling)
                {
                    <Pill>@Codex.Spaced(stat)</Pill>
                }
            </Pills>
        }

        @if (Entity.AllowedRoles is { Count: > 0 } roles)
        {
            <Rule />
            <SubHead>Offered to</SubHead>
            <Pills>
                @foreach (var role in roles)
                {
                    var tone = Ui.Tokens.Role(role) ?? "inherit";
                    <Pill Colour="@tone">@role</Pill>
                }
                @foreach (var stat in Entity.AllowedAttributes ?? [])
                {
                    <Pill>@stat</Pill>
                }
            </Pills>
        }
    </Panel>
}
```

The `(Facts.Count + 1) / 2` split puts the extra row in the first column on an odd count, and
renders an empty second `<table class="cx-facts">` when there is exactly one fact. Both are kept.

The half-split loop is a `@for` with two `var` declarations before the `<Facts>` tag. Those
declarations stay inside the loop body.

`Particulars.razor.scss`:

```scss
.cx-pairs { display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 0 28px; }
```

### 4.3 `StatSlots`

- **Guard**: `Entity.Stats is { Count: > 0 } slots`
- **Head**: `Stat slots`; **badge**: `slots.Count`
- **Note**, verbatim: `A slot's weight is its share of the item's stat budget. The magnitude is not
  in the build: a dynamic item rolls it from its level and rarity when it drops, which is what the
  tooltip above computes.`
- **Reads**: `Entity.Stats` (`StatSlot.Stat`, `.Kind`, `.Weight`)
- **Injects**: `CodexView Ui`, for `Ui.Store.PrimaryAttributes`
- **Parameters**: none

```razor
@inject CodexView Ui

@if (Entity.Stats is { Count: > 0 } slots)
{
    var badge = slots.Count.ToString();
    <Panel Head="Stat slots" Badge="@badge">
        <Note>
            A slot's weight is its share of the item's stat budget. The magnitude is not in the build:
            a dynamic item rolls it from its level and rarity when it drops, which is what the tooltip
            above computes.
        </Note>
        <ul class="cx-stat-lines">
            @foreach (var slot in slots)
            {
                <li class="@(Ui.Store.PrimaryAttributes.Contains(slot.Stat) || slot.Stat == "Stamina" ? "cx-primary" : "cx-secondary")">
                    <span>@Codex.Spaced(slot.Stat)</span>
                    <span class="cx-w">@slot.Kind &middot; weight @slot.Weight</span>
                </li>
            }
        </ul>
    </Panel>
}
```

`ul.cx-stat-lines` has one call site and stays as markup inside this section, so its whole rule
family does too.

`StatSlots.razor.scss`:

```scss
.cx-stat-lines {
  list-style: none; margin: 0; padding: 0; font-size: 13px;

  li { display: flex; gap: 8px; padding: 3px 0; }

  .cx-w { color: var(--faint); margin-left: auto; font-size: 11px; }
  .cx-primary { color: var(--game-stat-primary); }
  .cx-secondary { color: var(--game-stat-secondary); }
  li.cx-primary { color: var(--game-stat-primary); }
  li.cx-secondary { color: var(--game-stat-secondary); }
}
```

The list, the rows and both spans are this section's own elements, so nothing here needs `::deep`.
The class pair is kept in both spellings, on the row and on a span inside it, exactly as the shipped
stylesheet carries them.

### 4.4 `Ranks`

- **Guard**: `Entity.RankRuns is { Count: > 0 } runs`
- **Head**: `Rank progression`; **badge**: `runs.Count`
- **Note**, verbatim: `The slash-separated numbers the description prints, one entry per run of
  them, matched to the curve behind it. The printed row is what the game shows; the values are what
  it computes with.`
- **Reads**: `Entity.RankRuns` (`.Printed`, `.Values`, `.Transform`, `.Ticks`, `.Disagrees`) and
  `Entity.Tags`
- **Injects**: `CodexView Ui`
- **Parameters**: none

```razor
@inject CodexView Ui

@if (Entity.RankRuns is { Count: > 0 } runs)
{
    var badge = runs.Count.ToString();
    <Panel Head="Rank progression" Badge="@badge">
        <Note>
            The slash-separated numbers the description prints, one entry per run of them, matched to
            the curve behind it. The printed row is what the game shows; the values are what it
            computes with.
        </Note>
        @foreach (var run in runs)
        {
            <Rule />
            <KeyValues>
                <KeyValue Key="Printed" Class="cx-prose">
                    @for (var rank = 0; rank < run.Printed.Count; rank++)
                    {
                        @if (rank > 0)
                        {
                            <span> / </span>
                        }
                        @Ui.Text(run.Printed[rank], null, Entity.Tags)
                    }
                </KeyValue>
                @if (run.Values is { Count: > 0 })
                {
                    <KeyValue Key="Shipped values">@string.Join(" / ", run.Values.Select(v => v is null ? "-" : Codex.Number(v.Value)))</KeyValue>
                }
                @if (run.Transform is not null)
                {
                    <KeyValue Key="Arithmetic">@run.Transform@(run.Ticks is { } ticks ? $" over {ticks} ticks" : "")</KeyValue>
                }
                @if (run.Disagrees == true)
                {
                    <KeyValue Key="Note" Missing="true">printed and computed values disagree</KeyValue>
                }
            </KeyValues>
        }
    </Panel>
}
```

The `Printed` row is the sharpest whitespace case in the app. Two things hold it together:

1. `<span> / </span>` is a literal text node containing a non-whitespace character, so the spaces
   around the slash survive Razor's whitespace handling and are the rendered separator. It is not
   rewritten as bare text, as `&nbsp;/&nbsp;`, or as a CSS `::before`.
2. Every other text node in the `@for` body is whitespace containing a newline, so the compiler
   removes it and the source indentation never reaches the browser. Introducing any literal text
   next to those markup nodes turns the surrounding indentation into rendered output, which under
   `white-space: pre-line` becomes blank lines on screen.

`KeyValue` is written on one line for the same reason.

### 4.5 `Overflow`

- **Guard**: `Entity.GemOverflow is { } overflow`
- **Head**: `Past max power`. No badge, no note.
- **Reads**: `Entity.GemOverflow.Description`, `.Placeholders`, `Entity.Tags`
- **Injects**: `CodexView Ui`
- **Parameters**: none

```razor
@inject CodexView Ui

@if (Entity.GemOverflow is { } overflow)
{
    <Panel Head="Past max power">
        <Prose>@Ui.Text(overflow.Description, overflow.Placeholders, Entity.Tags)</Prose>
    </Panel>
}
```

### 4.6 `BorrowedArt`

- **Guard**: `Href is not null`
- **Head**: `Borrowed art`. No badge.
- **Note**, verbatim, containing a link: `This record ships no art of its own. The image above
  belongs to <a>{Name}</a>, which is where it was taken from.`
- **Reads from the cascade**: nothing
- **Injects**: nothing
- **Parameters**:

| parameter | type | default |
|---|---|---|
| `Href` | `string?` | `null` |
| `Tip` | `string?` | `null` |
| `Name` | `string?` | `null` |

The guard is a link-availability test: the panel disappears when the lending row's table is not
public, so no address is ever built out of a shipped table name. `Detail` resolves the lender
through `CodexView.Addresses` and passes the three strings in; a null `Href` renders nothing.

```razor
@if (Href is not null)
{
    <Panel Head="Borrowed art">
        <Note>
            This record ships no art of its own. The image above belongs to
            <a href="@Href" data-tip="@Tip">@Name</a>,
            which is where it was taken from.
        </Note>
    </Panel>
}
```

### 4.7 `Grants`

- **Guard**: `Entity.Grants is { } grants`
- **Head**: `Grants an ability`; **badge**: `grants.DisplayName`, a name rather than a count, and
  absent when the granted ability has no display name
- **Reads**: `Entity.Grants`, `Entity.Grants.DisplayName`
- **Injects**: nothing
- **Parameters**: none

```razor
@if (Entity.Grants is { } grants)
{
    <Panel Head="Grants an ability" Badge="@grants.DisplayName">
        <Detail Entity="grants" />
    </Panel>
}
```

The nested `<Detail>` re-supplies the entity cascade with `grants`, so everything below this panel
draws the granted ability.

### 4.8 `Legendaries`

- **Guard**: `Entity.LegendaryStats is { Count: > 0 } legendaries`
- **Head**: `Legendary stats`; **badge**: `legendaries.Count`
- **Reads**: `Entity.LegendaryStats`
- **Injects**: nothing; the roll's name comes from the static `PublicTypes.NameOf`
- **Parameters**: none

```razor
@if (Entity.LegendaryStats is { Count: > 0 } legendaries)
{
    var badge = legendaries.Count.ToString();
    <Panel Head="Legendary stats" Badge="@badge">
        @for (var index = 0; index < legendaries.Count; index++)
        {
            var roll = legendaries[index];

            <Rule />
            <SubHead>@PublicTypes.NameOf(roll)</SubHead>
            <Detail Entity="roll" @key="index" />
        }
    </Panel>
}
```

The `@for` exists to give the key a value. The key is the index and never the roll: `Entity` is a
`record`, two structurally equal rolls compare equal, and duplicate sibling keys throw out of
`RenderTreeDiffBuilder`.

The rolls are drawn in export order, never sorted. Game-authored sequences keep the order the build
wrote them in; only name-keyed lookup dictionaries get an ordinal-ignore-case sort. That split
applies throughout: `Entity.Heroes`, `Entity.SetBonus`, `Entity.ScalesOff`, `Entity.AllowedRoles`,
`Entity.Stats`, `Entity.RankRuns`, `Entity.LegendaryStats` and `Readings` are all export order, and
`Store.Vocabulary.Rarities` is the tier ladder in file order.

### 4.9 `Stage`

- **Guard**: none; the page places it unconditionally
- **Reads**: `Here.Address`
- **Injects**: nothing
- **Parameters**: none

```razor
<div class="cx-entity-tip" data-stage="@Here.Address" aria-live="polite">
    <Empty>Loading...</Empty>
</div>
```

`codex.js` finds this element with `document.querySelector('[data-stage]')`, splits
`box.dataset.stage` on `?` into an address and a carried query, fetches the result, and attaches a
shadow root to this div. Three consequences the component must respect:

1. `data-stage` is a plain HTML attribute, emitted verbatim. It is never a Blazor directive.
2. `Here.Address` is the complete tooltip URL including the `?hero=` the server wants carried;
   `CodexView.Tip` already appends it. It is computed once by the page and passed down, never
   recomputed from a nested record.
3. The div is a real element with a stable identity across enhanced navigations, so
   `host.shadowRoot ?? host.attachShadow(...)` finds the root it attached last time. It carries no
   `@key` and its element type does not change.

`Stage.razor.scss`:

```scss
.cx-entity-tip { flex: 0 0 auto; min-width: 340px; }
```

The class reads as part of the entity page's grid and is listed with it in the shipped stylesheet,
but the div is this component's, so the rule lives here. `EntityPage.razor.scss` owns the flex
container round it.

The tooltip fragment that `codex.js` paints into this div goes into a shadow root, and neither this
sheet nor any other in the bundle enters one. The fragment keeps supplying its own styling exactly
as it does now.

### 4.10 `Knobs`

- **Guard**: `PicksHero || Wearable || IsGem`
- **Panel**: headless
- **Reads**: `Here.Shown`, `Here.Readings`, `Here.Reading`
- **Injects**: `EntityStore Store`, `CodexView Ui`
- **Parameters**: none

```csharp
private bool IsGem => Here.Shown.GemTier is not null;

private bool Wearable => Here.Shown.Stats is { Count: > 0 };

private bool PicksHero => Here.Readings.Count > 1 || Here.Shown.Heroes is { Count: > 1 };
```

```razor
@inject CodexView Ui
@inject EntityStore Store

@if (PicksHero || Wearable || IsGem)
{
    <Panel>
        @if (PicksHero)
        {
            <Knob Caption="Hero">
                <select data-knob="hero"
                        onchange="location.search = '?hero=' + encodeURIComponent(this.value)">
                    @if (Here.Readings.Count == 1)
                    {
                        <option value="">Any</option>
                    }
                    @foreach (var who in Store.Vocabulary.Heroes
                        .Where(h => Here.Readings.Any(r => r.Heroes is null || r.Heroes.Contains(h.Name)))
                        .OrderBy(h => h.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        <option value="@who.Name" selected="@(who.Name == Here.Reading)">
                            @who.Name (@who.ArmorType, @who.PrimaryStat)
                        </option>
                    }
                </select>
            </Knob>
        }

        @if (Wearable || IsGem)
        {
            <Knob Caption="Rarity" As="div">
                <div class="cx-rungs">
                    @foreach (var rung in Store.Vocabulary.Rarities)
                    {
                        var colour = CodexView.Readable(Ui.Tokens.Rarity(rung.Name));
                        <button type="button" data-knob="rarity" data-choice="@rung.Tier"
                                aria-pressed="false"
                                style="@(colour is null ? null : $"color:{colour}")">
                            @(IsGem ? rung.GemDisplayName : rung.DisplayName)
                        </button>
                    }
                </div>
            </Knob>
        }

        @if (Wearable)
        {
            <Knob Caption="Item level">
                <input type="number" min="1" max="1000" step="1"
                       value="@StatMagnitudes.MaximumItemLevel" data-knob="itemLevel" />
            </Knob>

            <Knob Caption="Tempered">
                <input type="number" min="0" max="20" step="1" placeholder="none" data-knob="upgrades" />
            </Knob>
        }
    </Panel>
}
```

Requirements this section carries, every one of which compiles cleanly if broken:

- `onchange="location.search = '?hero=' + encodeURIComponent(this.value)"` is a plain HTML attribute
  containing JavaScript, written verbatim in markup. It is not `@onchange`, and it is not passed
  through a component parameter. It performs a full navigation, which is the only way the hero can
  change on a statically rendered page, because the server has to re-resolve the shown reading, the
  name, the name colour, the detail and the related list.
- `data-knob`, `data-choice` and `aria-pressed` are plain HTML attributes. `data-knob`'s value is
  the query-parameter name sent to `/api/tooltip/…`: `hero`, `rarity`, `itemLevel`, `upgrades`.
  Those four strings are an API contract.
- The rarity rungs are `<button type="button">`, because `codex.js` selects `button[data-knob]`, and
  `type="button"` keeps them out of form submission.
- `aria-pressed="false"` is present on the server-rendered HTML of every rung. It is the only visual
  state `.cx-rungs button[aria-pressed="true"]` has, and the toggle-off path reads the attribute.
- The whole rung family shares one `data-knob="rarity"` value, so `querySelectorAll` can clear its
  siblings.
- `<option value="">Any</option>` appears only when `Here.Readings.Count == 1`. That is the case
  where `PicksHero` was satisfied by the shown reading listing more than one hero, so clearing the
  hero is meaningful. Showing it always would let a reader pick a hero-less reading of a per-hero
  item.
- `selected="@(who.Name == Here.Reading)"` compares against the reading actually on screen, which is
  the asked-for hero or the one derived from the row, so a fresh load selects what it is showing.
- The hero list is sorted ordinal-ignore-case; the rarity ladder is file order and is never sorted.
- The gem label switches on `IsGem` alone while the rung block renders for `Wearable || IsGem`: gems
  and items print the same tier under different words, and that is domain vocabulary.

`Knobs.razor.scss`. The `<select>`, the two `<input>`s and every rung button are this section's own
elements even though a `Knob` wraps them, so the shipped rules keep their `.cx-knob` ancestor and
need no `::deep`:

```scss
@use "../../Styles/shared" as *;

.cx-knob select, .cx-knob input {
  width: 100%; appearance: none;
  @include sunken;
  font: 400 13px/1 var(--sans); padding: 7px 9px; border-radius: 2px;
}
.cx-knob select:focus, .cx-knob input:focus { @include edged; }

.cx-rungs {
  display: flex; flex-wrap: wrap; gap: 4px;

  button {
    appearance: none; cursor: pointer; background: var(--sunken); border: 1px solid var(--line);
    border-radius: 2px; padding: 5px 9px; font: 600 12px/1 var(--sans); color: var(--muted);

    /* The only visual state a pressed rung has. codex.js writes the attribute; the server renders
       it false on every rung. */
    &[aria-pressed="true"] { border-color: currentColor; background: var(--raised); }
  }
}
```

A rung takes `color: var(--muted)` rather than the ink a sunken field takes, so it spells its ground
and border out instead of including the mixin.

### 4.11 `Identity`

- **Guard**: none
- **Head**: `Identity`. No badge.
- **Reads**: `Here.Under`, `Here.Label`, `Here.NetId`, `Here.Readings`, `Here.Kind`, `Here.Address`
- **Injects**: nothing
- **Parameters**: none

```razor
<Panel Head="Identity">
    <Facts>
        <Fact Label="Type"><a href="@Here.Under">@Here.Label</a></Fact>
        <Fact Label="Network id">@Here.NetId</Fact>
        <Fact Label="Readings of this id">@Here.Readings.Count@(Here.Kind is { } kind ? $" of {kind.Count:N0} under {Here.Label}" : null)</Fact>
        <Fact Label="Tooltip"><a href="@Here.Address">@Here.Address</a></Fact>
    </Facts>
    <Readings />
</Panel>
```

The readings list belongs inside this panel, directly under the row that counts them, so `Identity`
places `<Readings />` itself and `EntityPage` stays flat.

### 4.12 `Readings`

- **Guard**: `Here.Readings.Count > 1`
- **Sub-head**: `Every reading`, preceded by a `<Rule />`
- **Reads**: `Here.Readings`, `Here.Shown`, `Here.Page`
- **Injects**: `CodexView Ui`, for `Ui.Reading`
- **Parameters**: none

```razor
@inject CodexView Ui

@if (Here.Readings.Count > 1)
{
    <Rule />
    <SubHead>Every reading</SubHead>
    <Pills>
        @foreach (var reading in Here.Readings)
        {
            var who = Ui.Reading(reading);
            var where = Here.Page + (who is null ? null : "?hero=" + Uri.EscapeDataString(who));
            var tone = reading == Here.Shown ? "var(--gold-bright)" : null;
            var badge = $"{reading.ArmorType ?? "any"} / {reading.PrimaryStat ?? "any"}";

            <Pill Href="@where" Colour="@tone" Badge="@badge">@PublicTypes.NameOf(reading)</Pill>
        }
    </Pills>
}
```

The readings are drawn in export order, which is the order the store read them.

### 4.13 `Nearby`

- **Guard**: `Here.Related.Count > 0`
- **Head**: `More like this`. No badge.
- **Reads**: `Here.Related`
- **Injects**: nothing
- **Parameters**: none

```razor
@if (Here.Related.Count > 0)
{
    <Panel Head="More like this">
        <Pills>
            @foreach (var (label, href, count) in Here.Related)
            {
                var badge = count.ToString("N0");
                <Pill Href="@href" Badge="@badge">@label</Pill>
            }
        </Pills>
    </Panel>
}
```

---

## 5. The page cascade record

`src/FellowshipExtractor.Api/Bearings.cs`, namespace `FellowshipExtractor.Api`, which
`_Imports.razor` already imports.

```csharp
using FellowshipExtractor.Tooltip;

namespace FellowshipExtractor.Api;

/// <summary>
/// What the entity page knows about the address it is answering, cascaded to the sections that draw
/// its own chrome. Every address in it is built once by the page: a nested row carries no table and
/// no id of its own, so a section that computed one would link nowhere on half the records it is
/// given.
/// </summary>
public sealed record Bearings
{
    /// <summary>The reading on screen, which is the asked-for hero's or the first one read.</summary>
    public Entity Shown { get; init; } = new();

    /// <summary>Every reading of this id, in the order the export was written.</summary>
    public IReadOnlyList<Entity> Readings { get; init; } = [];

    /// <summary>The hero whose reading is shown, asked for or derived.</summary>
    public string? Reading { get; init; }

    /// <summary>The tooltip address, complete with any query the page wants carried into it.</summary>
    public string Address { get; init; } = string.Empty;

    /// <summary>This row's own page, which the reading pills hang their hero query off.</summary>
    public string Page { get; init; } = string.Empty;

    /// <summary>The browser, narrowed to this row's type.</summary>
    public string Under { get; init; } = string.Empty;

    /// <summary>The word this row's type is published under.</summary>
    public string Label { get; init; } = string.Empty;

    public int NetId { get; init; }

    /// <summary>The type this row is filed under, counted, or null when it is filed under none.</summary>
    public EntityType? Kind { get; init; }

    /// <summary>Where else a reader can be sent from here, already labelled and counted.</summary>
    public IReadOnlyList<(string Label, string Href, int Count)> Related { get; init; } = [];
}
```

---

## 6. The page compositions

### 6.1 `Detail.razor`, in full

```razor
@inject CodexView Ui

<CascadingValue Value="Entity">
    <Wording />
    <Particulars Facts="Facts" Schools="Schools" />
    <StatSlots />
    <Ranks />
    <Overflow />
    <BorrowedArt Href="@(Lender?.Href)" Tip="@(Lender?.Tip)" Name="@(Lender?.Name)" />
    <Grants />
    <Legendaries />
</CascadingValue>

@code {
    [Parameter, EditorRequired] public Entity Entity { get; set; } = new();

    private IReadOnlyList<(string Label, string Value, string? Unit, string? Href, string? Tip)> Facts = [];

    /// <summary>
    /// The damage schools this entry is, each with the filter it links into. The schools are the one
    /// part of an ability's tag list a player meets; the rest of the list is the build's own
    /// classification and is drawn nowhere.
    /// </summary>
    private IReadOnlyList<(string Label, string Href)> Schools = [];

    /// <summary>
    /// The row this entry's art belongs to, resolved here because a section is given a record and
    /// never an address. Null when the record ships its own art, and null when the lending row is
    /// filed under a table no reader can open.
    /// </summary>
    private (string Href, string? Tip, string Name)? Lender;

    private const string SchoolFacet = "school";

    protected override void OnParametersSet()
    {
        Facts = Ui.Facts(Entity);

        // Read through the school facet's own selector rather than repeating the rule for what a
        // school is, because these pills link into that facet: a second copy could offer a value
        // the filter behind the link does not recognise.
        IEnumerable<string> schools = Ui.Codex.Facet(SchoolFacet)?.Values(Entity) ?? [];

        Schools = [.. schools.Select(school => (Codex.Spaced(school), Filter(school)))];

        Lender = Entity.IconInheritedFrom is { } source && Ui.Addresses(source.Table, source.NetId) is { } found
            ? (found.Href, found.Tip, Ui.Name(source.Table, source.NetId))
            : null;
    }

    /// <summary>
    /// A school pill's destination. A nested row - a granted ability, a legendary stat line -
    /// carries no table of its own, so the type half of the query is left off rather than sent
    /// empty, and a row of a non-public table can name no type at all.
    /// </summary>
    private string Filter(string school) =>
        (PublicTypes.SlugOf(Entity.Table) is { } slug ? $"/browse?type={slug}&" : "/browse?") +
        $"{SchoolFacet}={Uri.EscapeDataString(school)}";
}
```

`Detail` is the only component that reads `Entity.Table`, and it reads it only to build the school
filter. That is what keeps every section reusable on a nested row, and it is preserved exactly.

`<CascadingValue>` emits no element, so the eight sections' panels remain siblings at the component
root and `.cx-panel + .cx-panel { margin-top: 16px }` still supplies the gap between them. `Detail`
must never gain a wrapper element.

### 6.2 `EntityPage.razor`, in full

```razor
@page "/entity/{slug}/{netId:int}"
@inject CodexView Ui
@inject Codex Catalog
@inject EntityStore Store
@inject NavigationManager Nav

@if (Readings.Count > 0)
{
    <PageTitle>@Name - Fellowship Codex</PageTitle>
    <Crumbs Trail="@(new List<(string, string?)> { (PublicTypes.Label(table), Under), (Name, null) })" />

    <h1 style="color:@Ui.NameColour(Shown)">@Name</h1>

    <CascadingValue Value="Here">
        <div class="cx-entity">
            <div class="cx-entity-top">
                <Stage />

                <div class="cx-entity-side">
                    <Knobs />
                    <Identity />
                </div>
            </div>

            <Detail Entity="Shown" />

            <Nearby />
        </div>
    </CascadingValue>
}

@code {
    /// <summary>
    /// The type's public slug, which is the only spelling this route answers to. A slug naming
    /// nothing public leaves <see cref="table"/> null and the page answers 404, so a shipped table
    /// name in the address opens no page rather than opening the page under a second name.
    /// </summary>
    [Parameter] public string Slug { get; set; } = string.Empty;

    [Parameter] public int NetId { get; set; }

    [SupplyParameterFromQuery(Name = "hero")] public string? Asked { get; set; }

    /// <summary>The shipped table the slug names, resolved once and never drawn.</summary>
    private string? table;

    private IReadOnlyList<Entity> Readings = [];

    private Entity Shown = new();

    /// <summary>
    /// Everything the page's own sections draw, built in one place so that no section has to work
    /// out an address for itself.
    /// </summary>
    private Bearings Here = new();

    private string Name => Ui.Name(Shown);

    /// <summary>
    /// The browser, narrowed to this row's type. Built from the slug the type is filed under rather
    /// than from the route's own spelling, so the crumb and the type row agree with the menu whatever
    /// case a reader typed.
    /// </summary>
    private string Under => $"/browse?type={PublicTypes.SlugOf(table)}";

    protected override void OnParametersSet()
    {
        table = PublicTypes.TableOf(Slug);
        Readings = table is null ? [] : Store.All(table, NetId);

        if (Readings.Count == 0)
        {
            Shown = new Entity();
            Here = new Bearings();
            Nav.NotFound();
            return;
        }

        Shown = Asked is not null && Store.Hero(Asked) is { } named
            ? Readings.FirstOrDefault(e => Reads(e, named.Name, named.ArmorType, named.PrimaryStat)) ?? Readings[0]
            : Readings[0];

        Here = new Bearings
        {
            Shown = Shown,
            Readings = Readings,
            Reading = Asked ?? Ui.Reading(Shown),
            Address = Ui.Tip(Shown),
            Page = Ui.Href(table, NetId),
            Under = Under,
            Label = PublicTypes.Label(table),
            NetId = NetId,
            Kind = Catalog.Types.FirstOrDefault(t => t.Table == Shown.Table),
            Related = Nearby(),
        };
    }

    private static bool Reads(Entity entity, string hero, string? armor, string? stat) =>
        (entity.Heroes is null || entity.Heroes.Contains(hero)) &&
        (entity.ArmorType is null || entity.ArmorType == armor) &&
        (entity.PrimaryStat is null || entity.PrimaryStat == stat);

    private IReadOnlyList<(string, string, int)> Nearby()
    {
        var nearby = new List<(string, string, int)>();

        void Add(string facet, string? value)
        {
            if (value is null) return;
            if (Catalog.Facet(facet) is not { } definition) return;

            var count = Store.Entities.Count(row =>
                row.Table == Shown.Table && definition.Matches(row, [value]));

            if (count < 2) return;

            nearby.Add((
                $"{definition.Label}: {Codex.Spaced(value)}",
                $"{Under}&{facet}={Uri.EscapeDataString(value)}",
                count));
        }

        // Every axis a reader can be sent along is one the game itself prints: a slot, a set, a gem
        // type, a trait category, a hero, a school of magic. The loot rule set an item rolls under
        // is the one shared field that is a shipped spelling throughout, so it offers no way in.
        Add("slot", Shown.Slot);
        Add("set", Shown.SetBonus?.FirstOrDefault());
        Add("gem", Shown.GemType);
        Add("trait", Shown.TraitCategory);
        Add("hero", Shown.Heroes?.Count == 1 ? Shown.Heroes[0] : null);
        Add("school", Catalog.Facet("school")?.Values(Shown).FirstOrDefault());

        return nearby;
    }
}
```

`.cx-entity`, `.cx-entity-top` and `.cx-entity-side` stay as markup: they are the page's own grid and
`.cx-entity-side .cx-panel + .cx-panel { margin-top: 0 }` selects through them.

`EntityPage.razor.scss`:

```scss
.cx-entity { display: grid; grid-template-columns: minmax(0, 1fr); gap: 18px; }

.cx-entity-top { display: flex; flex-wrap: wrap; gap: 18px; align-items: flex-start; }

.cx-entity-side {
  flex: 1 1 420px; min-width: 0;
  display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); gap: 18px;
  align-items: start;
}
```

The two rules that reach through this grid are elsewhere, each with the component that renders the
element it styles: `.cx-entity-tip` with `Stage`, and `.cx-entity-side .cx-panel + .cx-panel` with
`Panel`. Both ancestors are matched on the class alone, so neither needs an attribute here.

### 6.3 `Browse.razor`

`Browse` adopts the primitives and keeps its table.

Adopted:

- The unreadable-filter panel becomes
  `<Panel><Empty Said="That filter could not be read.">@error</Empty></Panel>`.
- The filter rail becomes `<Panel As="aside" Class="cx-rail" Aria="Filters" Head="Refine">`.
- The no-results panel becomes `<Panel><Empty Said="Nothing in the build answers that.">…</Empty></Panel>`.
- The five `<span class="cx-chip">` emissions become `<Chip>`, all of them still inside
  `table.cx-rows`, because `table.cx-rows .cx-chip` is the only rule that styles them.
- The row art becomes `<Icon Src="@Ui.Icon(row.Icon)" />`.
- Every `<span class="cx-num">` in the tabs and the facet values becomes `<CountBadge>`.

Kept as markup:

- The `<details class="cx-facet">` blocks and their `open="…"` expressions. A facet opens when it is
  in use or when it has eight values or fewer; the Between panel opens when a range is set; the
  grouping and flags panels are unconditionally open.
- `.cx-tabs`, `.cx-chosen`, `.cx-ranges`, `.cx-results-head`, `.cx-table-scroll` and `.cx-pager`.
- **The results rows.** The group-head `<tr class="cx-group-head">` is a sibling emitted before a
  data row, and the decision to emit it depends on a `lastGroup` local carried across loop
  iterations. A child component can neither emit a sibling row nor carry that state, so one result
  row is not a component and the `@foreach` stays as it is, `colspan="12"` included.
- `ShowSlot`, `ShowWorn` and `ShowSchools` each gate a `<th>` and a `<td>` and must stay in lockstep.

`Browse.razor.scss` is the largest of the component sheets, because the rail, the tabs, the results
table and the pager are all this page's own markup:

```scss
@use "../../Styles/shared" as *;

.cx-browse {
  display: grid; grid-template-columns: var(--rail) minmax(0, 1fr); gap: 18px; align-items: start;
}

/* One column below the rail's width, and the results first: a filter panel a reader has to scroll
   past to reach what they asked for is a filter panel in the way. The rail's half of this rule is
   in Panel.razor.scss, which owns the <aside> the cx-rail class lands on. */
@media (max-width: 1080px) {
  .cx-browse { grid-template-columns: minmax(0, 1fr); }
  .cx-browse > section { order: 1; }
}

.cx-facet + .cx-facet { border-top: 1px solid var(--line-soft); margin-top: 10px; padding-top: 10px; }
.cx-facet > summary {
  cursor: pointer; list-style: none; display: flex; align-items: center; gap: 6px;
  font-size: 11px; letter-spacing: .13em; text-transform: uppercase; color: var(--faint);
  padding: 2px 0;
}
.cx-facet > summary::-webkit-details-marker { display: none; }
.cx-facet > summary::after { content: "+"; margin-left: auto; color: var(--gold-deep); font-size: 14px; }
.cx-facet[open] > summary::after { content: "\2212"; }
.cx-facet > summary:hover { color: var(--gold-bright); }

.cx-facet-values { list-style: none; margin: 6px 0 0; padding: 0; max-height: 260px; overflow-y: auto; }
.cx-facet-values li a {
  display: flex; align-items: baseline; gap: 7px; padding: 2px 4px; border-radius: 2px;
  color: var(--core-secondary-000); font-size: 13px;
}
.cx-facet-values li a:hover { background: var(--raised); text-decoration: none; }
.cx-facet-values li a.on { background: var(--raised); color: var(--gold-bright); font-weight: 600; }
.cx-facet-values li a.on::before { content: "\2713"; color: var(--gold-bright); font-size: 11px; }

.cx-ranges { margin-top: 8px; }
.cx-range { display: grid; grid-template-columns: 1fr 1fr; gap: 4px; margin-bottom: 7px; }
.cx-range > span { grid-column: 1 / -1; font-size: 12px; color: var(--core-secondary-000); }
.cx-range input {
  width: 100%; min-width: 0; appearance: textfield;
  @include sunken;
  border-radius: 2px; font: 400 12px/1 var(--sans); padding: 5px 6px;
}
.cx-range input:focus { @include edged; }
.cx-ranges button {
  width: 100%; appearance: none; cursor: pointer; margin-top: 4px;
  @include gold-slab;
  border: 0; border-radius: 2px; font: 600 12px/1 var(--sans); letter-spacing: .06em; padding: 8px;
}
.cx-ranges button:hover { background: var(--gold); }

.cx-chosen { display: flex; flex-wrap: wrap; gap: 5px; margin: 0 0 12px; padding: 0; list-style: none; }
.cx-chosen li a {
  display: inline-flex; align-items: center; gap: 6px;
  background: var(--raised); border: 1px solid var(--line); border-radius: 2px;
  padding: 3px 8px; font-size: 12px; color: var(--ink);
}
.cx-chosen li a:hover {
  border-color: var(--semantic-negative); color: var(--semantic-negative); text-decoration: none;
}
.cx-chosen li a::after { content: "\00d7"; color: var(--faint); }
.cx-chosen .cx-clear-all a { color: var(--muted); font-size: 12px; background: none; border: 0; }
.cx-chosen .cx-clear-all a::after { content: none; }

.cx-tabs {
  display: flex; gap: 2px; overflow-x: auto; border-bottom: 1px solid var(--line); margin-bottom: 12px;
}
.cx-tab {
  display: inline-flex; align-items: baseline; gap: 7px; white-space: nowrap;
  padding: 9px 13px; font-size: 13px; color: var(--muted);
  border-bottom: 2px solid transparent;
}
.cx-tab:hover { color: var(--ink); text-decoration: none; }
.cx-tab[aria-current] { color: var(--gold-bright); border-bottom-color: var(--gold); font-weight: 600; }

.cx-results-head {
  display: flex; align-items: center; gap: 12px; flex-wrap: wrap;
  margin-bottom: 8px; font-size: 12px; color: var(--muted);
}
.cx-results-head .cx-count { margin-right: auto; }
.cx-results-head .cx-count b { color: var(--ink); }

.cx-table-scroll { overflow-x: auto; }

table.cx-rows {
  width: 100%; border-collapse: collapse; font-size: 13px;

  /* Not sticky. The scroll container round the table is what a sticky header would resolve against,
     and an offset inside it pushes the header down over the first row instead of holding it under
     the masthead, which is what it did. */
  thead th {
    text-align: left; font-weight: 600; font-size: 11px; letter-spacing: .1em; text-transform: uppercase;
    color: var(--faint); padding: 7px 10px; border-bottom: 1px solid var(--line);
    background: var(--sunken); white-space: nowrap;

    a { color: inherit; display: inline-flex; align-items: center; gap: 4px; }
    a:hover { color: var(--gold-bright); text-decoration: none; }
  }
  thead th[aria-sort] a { color: var(--gold-bright); }

  tbody td { padding: 5px 10px; border-bottom: 1px solid var(--line-soft); vertical-align: middle; }
  tbody tr:hover td { background: #131f2a; }

  .cx-numeric { text-align: right; font-variant-numeric: tabular-nums; color: var(--muted); }
  .cx-art { width: 40px; }
  .cx-group-head td {
    background: var(--raised); font-family: var(--display); letter-spacing: .09em; text-transform: uppercase;
    color: var(--gold-bright); font-size: 13px; padding: 7px 10px;
  }
  .cx-tags { max-width: 230px; }
  .cx-unit { color: var(--faint); }
}

.cx-pager {
  display: flex; align-items: center; gap: 6px; justify-content: flex-end; margin-top: 12px; font-size: 12px;
}
.cx-pager a, .cx-pager span {
  padding: 5px 10px; border: 1px solid var(--line); border-radius: 2px; color: var(--muted);
}
.cx-pager a:hover { border-color: var(--gold-deep); color: var(--gold-bright); text-decoration: none; }
.cx-pager [aria-current] {
  @include gold-slab;
  border-color: var(--gold-deep); font-weight: 600;
}
.cx-pager .cx-gap { border: 0; padding: 5px 2px; }
```

Four rules that read as this page's leave it, each following the element they style: `.cx-rail`
and its narrow-screen override to `Panel`, `table.cx-rows .cx-art img` to `Icon`,
`table.cx-rows .cx-chip` to `Chip`, and `table.cx-rows .cx-nm` to `EntityLink`, which is what the
name cell is. `.cx-empty` goes with `Empty` and every `.cx-num` with `CountBadge`.

### 6.4 `Home.razor`

- The roster panel becomes `<Panel Head="The roster" Badge="@Roster">`, where `Roster` is a computed
  `$"{Store.Vocabulary.Heroes.Count} heroes"`.
- Each menu group becomes `<Panel Head="@group.Label">`.
- The showcase panel becomes `<Panel Head="A sample of what is in there">`.
- The showcase card's image becomes `<Icon Src="@Ui.Icon(entity.Icon)" />`.

`.cx-hero-band`, `.cx-roster`, `.cx-cards`, `.cx-card`, `.cx-card-name`, `.cx-card-count`,
`.cx-card-links` and `.cx-linked` stay as markup; they have one call site each and no variants.
`<EntityLink Entity="entity" Class="cx-card" Tint="false">` with child content is unchanged.

`Home.razor.scss`:

```scss
@use "../../Styles/shared" as *;

.cx-hero-band { padding: clamp(22px, 5vw, 54px) 0 26px; text-align: center; }
.cx-hero-band h1 { margin: 0 0 8px; }
.cx-hero-band p { color: var(--muted); max-width: 62ch; margin: 0 auto; }

.cx-cards { display: grid; grid-template-columns: repeat(auto-fill, minmax(230px, 1fr)); gap: 14px; }

/* The card's own frame is global: this page draws one card itself and hands the class to
   EntityLink for the other. The name inside a card is always this page's element, so the hover
   rule that tints it lives here, matching the card ancestor on its class. */
.cx-card:hover .cx-card-name { color: var(--gold-bright); }
.cx-card-name {
  font-family: var(--display); font-size: 17px; letter-spacing: .05em; text-transform: uppercase;
}
.cx-card-count { font-size: 12px; color: var(--muted); }
.cx-card-links {
  margin: 8px 0 0; padding: 0; list-style: none; font-size: 12.5px; columns: 2; column-gap: 14px;
}
.cx-card-links li { break-inside: avoid; }

.cx-roster { display: flex; flex-wrap: wrap; gap: 8px; }
.cx-roster a {
  display: inline-flex; align-items: center; gap: 8px; padding: 6px 12px 6px 8px;
  @include sunken;
  border-radius: 2px; font-size: 13px;
}
.cx-roster a:hover { text-decoration: none; border-color: currentColor; }
/* The chosen hero, marked the way a pressed rarity rung is: the entity page draws its roster with
   one entry current, and the raised ground is what tells that apart from the hovered entry, which
   the border alone already means. Same specificity as the hover rule, so it is placed after it. */
.cx-roster a[aria-current] { border-color: currentColor; background: var(--raised); }
.cx-pip { width: 10px; height: 10px; border-radius: 50%; flex: 0 0 auto; }
.cx-who { font-weight: 600; }
.cx-code { color: var(--faint); font-family: var(--mono); font-size: 11px; }

.cx-linked { display: flex; align-items: center; gap: 9px; }
```

The hover and current rules on a roster entry are the one equal-specificity pair in the shipped
stylesheet that source order decides, and they stay in one file in that order. `.cx-linked img` is
an `Icon` and is styled there.

### 6.5 `NotFound.razor`

```razor
<Panel>
    <Empty Said="@Said"><a href="@Where">@Offer</a></Empty>
</Panel>
```

The `@code` block is unchanged.

`NotFound` gets no stylesheet. Its `<h1>` takes the global heading rule and everything else it draws
is a `Panel` and an `Empty`.

### 6.6 `MegaMenu.razor` and `Omnibox.razor`

`MegaMenu` adopts `CountBadge` for its two `<span class="cx-num">` emissions. Nothing else in it
changes: `.cx-mega`, `.cx-mega-group` and `.cx-mega-open` are read by `codex.js` and
`.cx-mega-group { position: static }` resolves the full-width dropdown against
`.cx-masthead { position: sticky }`, so no wrapper element is introduced between the masthead and
the panel.

`Omnibox` adopts `Icon` for its result image. It is the one component in the project carrying
`@rendermode InteractiveServer`, so a primitive placed inside it renders interactively rather than
statically. That is safe for every primitive in this library: none of them declares an event
handler, a `@bind`, or any state, so each renders the same markup under either mode. `Omnibox` keeps
its own `@bind`, `@onclick` and `@onkeydown`, which are legitimate only because of that render mode.

Scoped styling is compile-time and is unaffected by render mode: `Omnibox.razor.scss` is rewritten
with the same kind of attribute as every other sheet and works identically under
`InteractiveServer`.

`MegaMenu.razor.scss`:

```scss
@use "../../Styles/shared" as *;

.cx-mega { flex: 0 1 auto; min-width: 0; }
.cx-mega-bar {
  display: flex; gap: 2px; list-style: none; margin: 0; padding: 0;
  overflow-x: auto; scrollbar-width: none;
}
.cx-mega-bar::-webkit-scrollbar { display: none; }

/* Static, so the panel below resolves its full width against the sticky masthead rather than
   against the group. */
.cx-mega-group { position: static; }

.cx-mega-open {
  appearance: none; background: transparent; border: 0; cursor: pointer;
  color: var(--muted); font: 600 12px/1 var(--sans); letter-spacing: .09em; text-transform: uppercase;
  padding: 18px 12px; white-space: nowrap;
  border-bottom: 2px solid transparent;
  transition: color .12s ease, border-color .12s ease;
}
.cx-mega-open:hover,
.cx-mega-group[data-open] .cx-mega-open { color: var(--gold-bright); border-bottom-color: var(--gold); }

.cx-mega-panel {
  position: absolute; left: 0; right: 0; top: 100%;
  display: none;
  background: linear-gradient(180deg, #101c27 0%, #0c1620 100%);
  border-top: 1px solid var(--gold-deep);
  border-bottom: 1px solid var(--line);
  box-shadow: 0 26px 40px -24px #000;
}
.cx-mega-group[data-open] .cx-mega-panel { display: block; }

.cx-mega-cols {
  max-width: var(--wide); margin: 0 auto; padding: 18px $gutter 22px;
  columns: 210px 5; column-gap: 26px;
  max-height: min(70vh, 620px); overflow-y: auto;
}

.cx-mega-head {
  display: flex; align-items: baseline; gap: 7px; break-inside: avoid; break-after: avoid;
  font-family: var(--display); font-size: 15px; letter-spacing: .06em; text-transform: uppercase;
  color: var(--gold-bright); padding-bottom: 6px; margin: 0 0 8px;
  border-bottom: 1px solid var(--line);
}
.cx-mega-head + .cx-mega-sec { break-before: avoid; }
.cx-mega-sec { margin: 0 0 14px; break-inside: avoid; }
.cx-mega-sec-head { @include eyebrow; }
.cx-mega-sec ul { list-style: none; margin: 0; padding: 0; }
.cx-mega-sec li a {
  display: flex; align-items: baseline; gap: 6px; padding: 1px 0;
  color: var(--core-secondary-000); font-size: 13px;
}
.cx-mega-sec li a:hover { color: var(--gold-bright); text-decoration: none; }
```

`data-open` is written only by `codex.js` and read only by these two rules, which is why it stays a
plain attribute on a plain element.

`Omnibox.razor.scss`:

```scss
@use "../../Styles/shared" as *;

.cx-omnibox {
  position: relative; flex: 1 1 260px; min-width: 0;
  display: flex; align-items: center; margin-left: auto;
}
.cx-omnibox svg {
  position: absolute; left: 10px; width: 15px; height: 15px; color: var(--faint); pointer-events: none;
}
.cx-omnibox input {
  width: 100%; appearance: none;
  @include sunken;
  border-radius: 3px; font: 400 13px/1 var(--sans); padding: 9px 30px 9px 32px;
}
.cx-omnibox input::placeholder { color: var(--faint); }
.cx-omnibox input::-webkit-search-cancel-button { display: none; }
.cx-omnibox input:focus { @include edged; box-shadow: 0 0 0 1px var(--gold-deep); }
.cx-omnibox-clear {
  position: absolute; right: 4px; appearance: none; background: transparent; border: 0;
  color: var(--faint); font-size: 18px; line-height: 1; padding: 4px 8px; cursor: pointer;
}
.cx-omnibox-clear:hover { color: var(--ink); }

.cx-omnibox-drop {
  position: absolute; top: calc(100% + 6px); right: 0; width: min(640px, 92vw);
  background: #0d1721; border: 1px solid var(--line); border-top-color: var(--gold-deep);
  box-shadow: 0 26px 40px -22px #000; z-index: 70;
  max-height: min(70vh, 560px); overflow-y: auto;
}
.cx-omnibox-chips {
  display: flex; gap: 4px; padding: 8px; flex-wrap: wrap; border-bottom: 1px solid var(--line-soft);
}
.cx-omnibox-chip {
  appearance: none; cursor: pointer; border: 1px solid transparent; border-radius: 2px;
  background: var(--raised); color: var(--muted); font: 600 11px/1 var(--sans);
  padding: 6px 9px; letter-spacing: .04em;
}
.cx-omnibox-chip[aria-pressed="true"] { @include gold-slab; }
.cx-omnibox-chip:hover { color: var(--ink); }
.cx-omnibox-chip[aria-pressed="true"]:hover { color: $on-gold; }

.cx-omnibox-hit {
  display: flex; align-items: center; gap: 10px; padding: 6px 10px;
  border-bottom: 1px solid var(--line-soft); color: var(--ink);
}
.cx-omnibox-hit:hover,
.cx-omnibox-hit[data-active] { background: var(--raised); text-decoration: none; }
.cx-omnibox-hit .cx-t { flex: 1 1 auto; min-width: 0; }
.cx-omnibox-hit .cx-nm {
  display: block; font-weight: 600; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
}
.cx-omnibox-hit .cx-sub {
  display: block; font-size: 11px; color: var(--faint); font-family: var(--mono);
  overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
}
.cx-omnibox-hit .cx-ty { flex: 0 0 auto; font-size: 11px; color: var(--muted); }
.cx-omnibox-hit mark { background: transparent; color: var(--gold-bright); text-decoration: underline; }
.cx-omnibox-all {
  display: block; text-align: center; padding: 10px; font-weight: 600; letter-spacing: .05em;
  @include gold-slab;
}
.cx-omnibox-all:hover, .cx-omnibox-all[data-active] { background: var(--gold); text-decoration: none; }
.cx-omnibox-none { padding: 18px; text-align: center; color: var(--muted); }
```

The `<mark>` runs are emitted by this component from `Suggestions.Mark`, element by element rather
than as one `MarkupString`, so they carry its attribute and need no `::deep`. `.cx-omnibox-hit img`
is an `Icon` and is styled there. `.cx-omnibox-hit .cx-nm` stays here and is a different element
from the `.cx-nm` in the results table, which is an `EntityLink`.

### 6.7 `MainLayout.razor`, `Crumbs.razor` and `EntityLink.razor`

These three keep their markup exactly as it is and gain a stylesheet each.

`MainLayout.razor.scss` owns the page shell: the skip link, the masthead, `<main>`, the hover-tooltip
layer and the footer.

```scss
@use "../../Styles/shared" as *;

.cx-skip {
  position: absolute; left: -9999px; top: 0; z-index: 100;
  background: var(--panel); color: var(--ink); padding: 10px 16px; border: 1px solid var(--gold);
}
.cx-skip:focus { left: 8px; top: 8px; }

.cx-masthead {
  position: sticky; top: 0; z-index: 60;
  background: linear-gradient(180deg, #101b26 0%, #0b141d 100%);
  border-bottom: 1px solid var(--line);
  box-shadow: 0 1px 0 #00000060, 0 10px 24px -18px #000;
}

.cx-bar {
  max-width: var(--wide); margin: 0 auto; padding: 0 $gutter;
  display: flex; align-items: center; gap: clamp(10px, 2vw, 26px); min-height: 54px;
}

.cx-brand { display: inline-flex; align-items: center; gap: 9px; flex: 0 0 auto; text-decoration: none; }
.cx-brand:hover { text-decoration: none; }
.cx-brand-mark {
  width: 18px; height: 18px;
  background: var(--chrome-arrow) center/contain no-repeat;
  filter: drop-shadow(0 0 4px #00000090);
  opacity: .9;
}
.cx-brand-name {
  font-family: var(--display); font-size: 18px; letter-spacing: .11em; text-transform: uppercase;
  color: var(--core-secondary-000); font-weight: 400;
}
.cx-brand-name b { color: var(--gold-bright); font-weight: 600; }

main { max-width: var(--wide); margin: 0 auto; padding: 14px $gutter 90px; }

/* Where the fragment a hovered link fetches is drawn. Only the layer is styled: what goes inside it
   is the API's own markup under .fx-tip, in a shadow root no stylesheet here enters. */
.cx-tip-layer {
  position: fixed; left: 0; top: 0; z-index: 90;
  pointer-events: none;
  filter: drop-shadow(0 16px 26px #000000b0);
}
.cx-tip-layer[hidden] { display: none; }

.cx-foot {
  border-top: 1px solid var(--line); margin-top: 40px;
  padding: 22px $gutter 40px; color: var(--faint); font-size: 12px;
}
.cx-foot p { max-width: var(--wide); margin: 0 auto 6px; }
.cx-foot b { color: var(--muted); font-weight: 600; }
.cx-foot-links { display: flex; gap: 14px; }
```

`main` is this layout's element, so the bare element selector reaches only the one `<main>` the
layout renders.

`Crumbs.razor.scss`:

```scss
.cx-crumbs { margin: 0 0 4px; }
.cx-crumbs ol {
  list-style: none; display: flex; flex-wrap: wrap; gap: 6px; margin: 0; padding: 0; font-size: 12px;
}
.cx-crumbs li + li::before { content: "\203A"; color: var(--faint); margin-right: 6px; }
.cx-crumbs a { color: var(--muted); }
.cx-crumbs a:hover { color: var(--gold-bright); }
.cx-crumbs span { color: var(--ink); }
```

`EntityLink.razor.scss`:

```scss
/* The results table's name cell is an EntityLink carrying Class="cx-nm", so the anchor the weight
   is set on is this component's element and the rule follows it here. */
table.cx-rows .cx-nm { font-weight: 600; }
```

That is the whole file, and it is the clearest case of the rule in section 2.8: read off the shipped
stylesheet the rule looks like part of the results table, and the element it styles is rendered
three files away.

---

## 7. The JavaScript contract

`wwwroot/js/codex.js` is loaded as a module from `App.razor`, after `blazor.web.js`. It binds
nothing to elements: it uses document-level delegated listeners plus `querySelector` by attribute.

Every page except `Omnibox.razor` is statically server rendered. `Routes.razor` declares no render
mode, and `Omnibox.razor` is the only file in the project carrying `@rendermode InteractiveServer`.
**On a statically rendered component `@onclick`, `@onchange`, `@oninput` and `@bind` compile without
error and do nothing at runtime.** Every attribute in the table below is therefore a plain HTML
attribute, emitted verbatim, and stays one.

| `codex.js` selector or read | satisfied by | owned by |
|---|---|---|
| `document.querySelector('[data-stage]')` | `data-stage="@Here.Address"` on `div.cx-entity-tip` | `Stage` |
| `box.dataset.stage.split('?')` | the address carries its own `?hero=` from `CodexView.Tip` | `Bearings.Address`, built by `EntityPage` |
| `box.setAttribute('aria-busy', …)`, `paint(box, …)` attaching a shadow root | the stage div is a plain element, no `@key`, stable element type | `Stage` |
| `document.querySelectorAll('[data-knob]')` | `data-knob="hero"` on `<select>`, `data-knob="rarity"` on each `<button>`, `data-knob="itemLevel"` and `data-knob="upgrades"` on the `<input>`s | `Knobs` |
| `knob.dataset.value ?? knob.value` | the razor emits no `data-value`; the script writes it on the buttons | `Knobs` |
| `asked.set(knob.dataset.knob, value)` | the four attribute values `hero`, `rarity`, `itemLevel`, `upgrades` are the tooltip API's query-parameter names | `Knobs` |
| `event.target.closest('button[data-knob]')` | `<button type="button" data-knob="rarity">` | `Knobs` |
| `rung.getAttribute('aria-pressed')`, `setAttribute('aria-pressed', …)` | `aria-pressed="false"` present on the server-rendered HTML | `Knobs` |
| `rung.dataset.value = rung.dataset.choice` | `data-choice="@rung.Tier"` | `Knobs` |
| `document.addEventListener('change', …)` on `[data-knob]` | the native `change` event, which needs no Blazor circuit | `Knobs` |
| the hero `<select>`'s full-page navigation | the literal `onchange="location.search = '?hero=' + encodeURIComponent(this.value)"` | `Knobs` |
| `event.target.closest('[data-tip]')` on `pointerover` and `focusin` | `data-tip` on the entity anchor, the fact-cell anchor, the borrowed-art anchor and the omnibox result | `EntityLink`, `Particulars`, `BorrowedArt`, `Omnibox` |
| `document.getElementById('cx-tip')` | `<div id="cx-tip" class="cx-tip-layer" hidden>` after `<footer>`, outside `<main>` | `MainLayout` |
| `document.querySelectorAll('.cx-mega-group')`, `.cx-mega-open`, `.cx-mega` | the mega menu's `<li>`, `<button>` and `<nav>` classes; `data-open` is written only by the script | `MegaMenu` |
| `document.getElementById('cx-omnibox')` for the `/` hotkey | `<input id="cx-omnibox" type="search">` | `Omnibox` |
| `window.Blazor?.addEventListener('enhancedload', arrived)` | enhanced navigation re-runs the stage fetch; the stage div's identity and attribute set stay stable so its shadow root survives | `Stage` |

`Omnibox` keeps its `@bind`, `@onclick` and `@onkeydown`; they work only because
`@rendermode InteractiveServer` is on its first line.

`table.cx-rows .cx-chip` is not a script contract but is the same class of hazard: the chip's entire
appearance comes from that scoped rule, so `Chip` is rendered only inside the browse results table.

---

## 8. Testing

### 8.1 The test project

`src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <ProjectReference Include="..\FellowshipExtractor.Api\FellowshipExtractor.Api.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="bunit" Version="2.8.6" />
    <PackageReference Include="xunit.v3" Version="3.2.2" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.8.1" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />
  </ItemGroup>

</Project>
```

`Microsoft.NET.Sdk.Razor`, not `Microsoft.NET.Sdk`: without it the `.razor` fixtures in the test
project are not compiled. The repo has no `Directory.Packages.props` and no central package
management, so each version is written literally in this csproj, exactly as the other four projects
do.

The test project references `AspNetCore.SassCompiler` nowhere and carries no `sasscompiler.json`.
Scoped styling is compiled and bundled by the Api project, and the scope attribute is baked into the
components at that project's build, so a test renders exactly what a browser gets.

`src/FellowshipExtractor.slnx` gains a fifth line:

```xml
<Project Path="FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj" />
```

`src/FellowshipExtractor.Api.Tests/_Imports.razor`:

```razor
@using Microsoft.AspNetCore.Components
@using Microsoft.AspNetCore.Components.Web
@using FellowshipExtractor.Api
@using FellowshipExtractor.Api.Components.Cx
@using FellowshipExtractor.Api.Components.Sections
@using FellowshipExtractor.Api.Components.Shared
@using FellowshipExtractor.Tooltip
```

### 8.2 The bUnit v2 API

bUnit v2 differs from every v1 tutorial. The API used is:

- The base class is `BunitContext`, not `TestContext`: `public class PanelTests : BunitContext`.
- Rendering is `Render<T>(…)`, not `RenderComponent<T>(…)`.
- Parameters: `Render<Panel>(p => p.Add(x => x.Head, "Identity").Add(x => x.Badge, "3")
  .AddChildContent("<p>body</p>"))`.
- Assertions: `cut.MarkupMatches(expected)` and `cut.Markup`.
- **Cascading values**: `p.AddCascadingValue(value)` on the same parameter builder. Every one of the
  thirteen section tests uses it:
  ```csharp
  var cut = Render<Wording>(p => p.AddCascadingValue(new Entity { Description = "one two" }));
  var cut = Render<Nearby>(p => p.AddCascadingValue(new Bearings { Related = [("Slot: Head", "/browse?slot=Head", 9)] }));
  ```
- **Re-rendering an existing component**: `cut.Render(p => p.Add(x => x.Entity, other))`, which is
  what test 2 below uses.

Every line above was compiled and run against bunit 2.8.6, xunit.v3 3.2.2 and SDK
10.0.400-preview.0.26322.102 before this spec was written.

A component under test must be nameable from the test assembly. Components take their namespace from
the Api project's root namespace plus their folder, which the `_Imports.razor` above lists. Without
that, `Render<T>` binds to the `RenderFragment` overload instead of the generic component one and
produces a cascade of confusing overload-resolution errors on top of `CS0246`.

### 8.3 Assertions: `MarkupMatches`, `cut.Markup` and the scope attribute

`cut.MarkupMatches(expected)` is a semantic HTML comparison that **normalises insignificant
whitespace**. `cut.Markup` is the exact rendered string.

The global stylesheet sets `.cx-prose { white-space: pre-line }`, so inside a `cx-prose` element
whitespace is rendered output and not formatting. Therefore:

- **Structural components** (`Panel`, `Pills`, `Pill`, `Facts`, `Fact`, `KeyValues`, `SubHead`,
  `Note`, `Empty`, `Rule`, `Chip`, `Knob`, `Icon`, `CountBadge`, and every section that renders no
  prose) are asserted with `cut.MarkupMatches(…)`.
- **Anything that renders into a `cx-prose` context** is asserted with
  `Assert.Equal(expected, cut.Markup.Bare())`. That is `Prose`, `KeyValue` when `Class="cx-prose"`,
  `Wording`, `Ranks` and `Overflow`. `MarkupMatches` silently forgives exactly the whitespace
  regression a reader sees on screen as spurious blank lines, so it is the wrong assertion for these
  five and using it there is the single most likely way this refactor ships broken.

**bUnit renders the scope attribute, and its semantic comparison already forgives it.** Every
component in the library carries one, so it is in every rendered fragment a test sees:

```html
<p class="cx-prose" b-8pl17hwq38><span class="rt-fire">burns</span></p>
<ul class="cx-pills" b-lo48lvy7g4><li b-x0f0z8c3o4><a href="#" b-x0f0z8c3o4>Head</a></li></ul>
```

It is rendered valueless, and the hash is `b-` plus ten base-36 characters. Measured on bunit 2.8.6:
`MarkupMatches` passes when the expected markup carries no such attribute, passes when it carries a
*different* one, and fails on any other extra attribute, `data-extra` and `data-extra="1"` both
included. So the comparison singles this attribute out, and:

**Every `MarkupMatches` assertion in this spec is written without the attribute and needs no change
at all.** Expected markup stays readable, and no hash is ever written into a test.

`cut.Markup` is a raw string and forgives nothing, so the five exact-markup assertions strip the
attribute first, through one helper the test project owns:

`src/FellowshipExtractor.Api.Tests/Scoped.cs`

```csharp
using System.Text.RegularExpressions;

namespace FellowshipExtractor.Api.Tests;

/// <summary>
/// The rendered markup without the scope attributes Blazor's CSS isolation puts on every element a
/// component emits. Only the assertions that compare an exact string need it: bUnit's semantic
/// comparison already ignores the attribute, and an assertion that reads one deliberately does it
/// through Find and GetAttribute, which are unaffected.
/// </summary>
internal static partial class Scoped
{
    /// <summary>b- and ten base-36 characters, valueless, followed by a space or the tag's end.</summary>
    [GeneratedRegex(@"\sb-[a-z0-9]{10}(?=[\s>])")]
    private static partial Regex Attribute();

    public static string Bare(this string markup) => Attribute().Replace(markup, string.Empty);
}
```

The lookahead is what keeps it to attributes: the pattern can only match where a tag continues or
closes, so no run of game text can be eaten by it. Stripping the attribute cannot disturb a
whitespace assertion either, because the attribute is never inside a text node.

Assertions that read an attribute on purpose, which is every knob test in section 8.5, use
`cut.Find(selector)` and `GetAttribute(name)` and see the DOM rather than a string, so they are
untouched.

Every claim in this subsection was measured against bunit 2.8.6 and the components as this spec
specifies them, before the spec was written.

### 8.4 The `CodexView` helper

Seven sections and `Detail` inject `CodexView`. The test project owns one helper that builds it:

`src/FellowshipExtractor.Api.Tests/Export.cs`

```csharp
using FellowshipExtractor.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace FellowshipExtractor.Api.Tests;

/// <summary>
/// A CodexView over the export the app itself reads, built once for the whole run. Chrome and the
/// blob client are left null: they are reached only by CodexView.Icon and CodexView.Chrome, no
/// section draws art, and constructing ChromeAssets measures the PNG corpus in the export's ui
/// folder.
/// </summary>
public sealed class Export
{
    public CodexView View { get; }

    public EntityStore Store { get; }

    public Export()
    {
        var folder = Environment.GetEnvironmentVariable("FELLOWSHIP_EXPORT") ?? Found();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["FELLOWSHIP_EXPORT"] = folder })
            .Build();

        var paths = new ExportPaths(configuration);

        Store = new EntityStore(paths, NullLogger<EntityStore>.Instance);

        var tokens = new DesignTokens(Store.Vocabulary.Colors, Store.Vocabulary.TextStyles);
        var richText = new RichTextRenderer(Store.Vocabulary.TextStyles);

        View = new CodexView(Store, new Codex(Store, tokens), tokens, chrome: null!, richText,
            new Placeholders(Store, richText), icons: null!);
    }

    /// <summary>
    /// The repo's own export, found by walking up from the test binary. The same folder the AppHost
    /// hands the API, so a section is tested against the corpus it renders in the browser rather
    /// than against a second copy of the shapes that could drift from it.
    /// </summary>
    private static string Found()
    {
        for (var here = new DirectoryInfo(AppContext.BaseDirectory); here is not null; here = here.Parent)
        {
            var export = Path.Combine(here.FullName, "output", "export");

            if (File.Exists(Path.Combine(export, "entities.jsonl"))) return export;
        }

        throw new InvalidOperationException(
            "No output/export above the test binary and FELLOWSHIP_EXPORT is not set. It names the " +
            "folder the extractor's export-all command wrote, which is the same folder the app reads.");
    }
}
```

Registered per test through bUnit: `Services.AddSingleton(export.View);` and, for `Knobs`,
`Services.AddSingleton(export.Store);`.

Shared through an xUnit class fixture so the 4 MB `entities.jsonl` is parsed once per run. Tests of
the sixteen primitives and of the six sections that inject nothing take no fixture at all and run
with no I/O.

### 8.5 Required tests

**Every primitive** gets at least one render test asserting its exact markup, and one per variant:

| component | variants tested |
|---|---|
| `Panel` | headless; head only; head with badge; `As="aside"` with `Class` and `Aria`; `As="details"` rendering `<details>`/`<summary class="cx-panel-head">`; badge is the last child of the head |
| `CountBadge` | renders `<span class="cx-num">` |
| `Pills` | wraps children in `<ul class="cx-pills">` |
| `Pill` | null `Href` renders `<li><span>`; non-null renders `<li><a href>`; null `Colour` omits `style`; `Colour="inherit"` renders `style="color:inherit"`; `Badge` renders a trailing `cx-num` with no space before it |
| `Facts` | renders `<table class="cx-facts">`; **with no children renders `<table class="cx-facts"></table>`** |
| `Fact` | `<tr><th><td>` with no `<thead>`/`<tbody>`; `Unit` renders `<span class="cx-unit">` with no space before it; null `Unit` renders no span |
| `KeyValues` | renders `<ul class="cx-kv">` |
| `KeyValue` | `class="cx-v"`; `Missing` gives `cx-v cx-miss`; `Class="cx-prose"` gives `cx-v cx-prose`, asserted with `Assert.Equal` on `cut.Markup.Bare()` |
| `SubHead`, `Note`, `Rule` | exact markup |
| `Empty` | `Said` renders `<b>…</b><br />` then the child content; null `Said` renders neither |
| `Prose` | asserted with `Assert.Equal` on `cut.Markup.Bare()`; a two-word child renders with exactly one space and no newline |
| `Chip` | null and non-null `Colour` |
| `Knob` | `As="label"` and `As="div"`; the caption span is the first child |
| `Icon` | `<img alt="" loading="lazy" src>` |

**Every section** gets at least one render test: one asserting it renders nothing when its guard is
false, and one asserting its head, badge, note and body when the guard is true. `Particulars` also
gets a test per subsection.

**The named hazards** get tests of their own. These are the reason the project exists:

1. **A nested `Detail` renders the nested entity, not the outer one.** Render `Detail` with an outer
   entity whose `Description` is one string and whose `Grants` has a different `Description`, a
   different `Stats` list and a different `RankRuns` list. Assert that the Grants panel's body
   contains the inner description and none of the outer entity's facts; that the outer description
   appears exactly once in the whole markup; and that `Grants an ability` appears exactly once, which
   is what proves the recursion terminates rather than drawing the outer record again at every level.
2. **A `Detail` re-rendered with a different entity draws the new one.**
   ```csharp
   var cut = Render<Detail>(p => p.Add(x => x.Entity, first));
   cut.Render(p => p.Add(x => x.Entity, second));
   ```
   Assert the second entity's text is present and the first's is gone. This test is what holds
   `IsFixed` off the two cascades: with `IsFixed="true"` it fails, measured, and the markup keeps
   showing the first entity.
3. **A keyed nested `Detail` in the legendary loop draws its own roll.** Render `Detail` with two
   legendary stat rows carrying different descriptions and assert both appear, each under its own
   sub-head. Then render it with two structurally equal rows and re-render, which is the case that
   throws `More than one sibling of component 'Detail' has the same key value` if the key is ever
   changed from the loop index to the roll.
4. **The knob attributes are plain HTML attributes.** Render `Knobs` over a `Bearings` whose `Shown`
   carries stats and more than one hero, and assert on the rendered DOM, not on a substring:
   `cut.Find("select[data-knob=hero]")` exists;
   `cut.Find("select[data-knob=hero]").GetAttribute("onchange")` equals
   `location.search = '?hero=' + encodeURIComponent(this.value)` exactly, read as a decoded attribute
   value so the assertion holds whether or not the renderer escaped the apostrophes; every rarity
   rung is a `button` with `type="button"`, `data-knob="rarity"`, a non-empty `data-choice` and
   `aria-pressed="false"`; both number inputs carry `data-knob="itemLevel"` and
   `data-knob="upgrades"`.
5. **The tooltip stage emits `data-stage` verbatim**, including a carried `?hero=` query, and its
   body is `<p class="cx-empty">Loading...</p>`.
6. **`Detail` emits its panels as siblings with no wrapper.** Assert that the rendered root of a
   `Detail` with two satisfied guards is two adjacent `div.cx-panel` elements, which is what
   `.cx-panel + .cx-panel` needs.
7. **The Details panel's leading rule.** Render `Particulars` with no facts and one school, and
   assert the first child after the panel head is `<hr class="cx-rule" />`.
8. **The half-split facts table.** Render `Particulars` with exactly one fact and assert two
   `table.cx-facts` elements, the second empty.
9. **One pill list from two sources.** Render `Particulars` with two `AllowedRoles` and two
   `AllowedAttributes` and assert one `<ul class="cx-pills">` containing four `<li>`s, roles first,
   the roles carrying `style="color:…"` and the attributes carrying no `style`.
10. **`<option value="">Any</option>` appears only for a single reading.** Two `Knobs` renders, one
    with one reading and one with two.
11. **The rank-progression separator.** Render `Ranks` over a run with three printed values and
    assert with `Assert.Equal` on `cut.Markup.Bare()` that the value span is exactly
    `<span class="cx-v cx-prose">…<span> / </span>…<span> / </span>…</span>` with no newline
    anywhere in it.
12. **`Prose` carries `cx-prose` on the element containing the markup.** Assert the rich-text runs
    are direct children of the `.cx-prose` element and not nested one level deeper.
13. **`Bare` strips a scope attribute and nothing else.** Assert it removes the attribute from a
    rendered `Prose`, and that it leaves a `data-` attribute, a class value and the text between two
    elements untouched. The helper is what five whitespace assertions read through, so its shape is
    pinned rather than assumed.
14. **The generated stylesheets put the attribute on the component's own element.** Read the
    `.razor.css` files the build wrote beside `Note.razor`, `Fact.razor` and `KeyValue.razor`,
    found by walking up from the test binary the way `Export.Found` walks up to the export, and
    assert that no selector in them ends `code[b-`. The three `::deep` rules are the only ones in
    the library that reach markup a caller wrote, and a nested rule written without `::deep`
    compiles, bundles and matches nothing. This is the one automated guard for the finding in
    section 2.8, and criterion 21 is what holds the rest of the census in place.

---

## 9. Acceptance criteria

1. `dotnet build src/FellowshipExtractor.slnx` completes with 0 errors and 0 warnings. The only
   informational message is `NETSDK1057` for the preview SDK.
2. `dotnet test src/FellowshipExtractor.slnx` runs the new test project and every test passes.
3. `src/FellowshipExtractor.Api/Components/Cx/` contains the sixteen primitives, and
   `src/FellowshipExtractor.Api/Components/Sections/` contains the thirteen sections, each in its own
   file named after the component.
4. `src/FellowshipExtractor.Api/Components/_Imports.razor` carries
   `@using FellowshipExtractor.Api.Components.Cx` and
   `@using FellowshipExtractor.Api.Components.Sections`.
5. `Detail.razor`'s markup is the eight section tags inside one `<CascadingValue>`, and its `@code`
   block holds only `Entity`, `Facts`, `Schools`, `Lender`, `SchoolFacet`, `OnParametersSet` and
   `Filter`.
6. `EntityPage.razor`'s markup is the page title, the crumbs, the `<h1>`, and the five section tags
   inside the `.cx-entity` grid, all inside one `<CascadingValue>`.
7. No component under `Components/Cx/` contains an `@inject` directive.
8. No component under `Components/Sections/` reads `Entity.Table` or `Entity.NetId`.
9. `grep` over `src/FellowshipExtractor.Api/Components` finds no `@onclick`, `@onchange`,
   `@oninput` or `@bind` outside `Omnibox.razor`, and finds `@preservewhitespace` nowhere.
10. Before any component is written, the baseline is captured: the app is run and the rendered HTML
    of each page named in criterion 11 is saved to
    `C:\Users\Sean\AppData\Local\Temp\claude\...\scratchpad\baseline\`. A concurrent session is
    editing `Detail.razor`, `EntityPage.razor`, `Browse.razor`, `Home.razor` and `NotFound.razor`, so
    a baseline taken after the refactor starts is not a baseline.
11. The app runs and the following pages render identically to the captured baseline, except for the
    four differences enumerated in sections 3.1, 3.4 and 3.6 (the rail's class order, no literal
    space before a panel-head badge, no literal space before a pill badge, no empty
    `<span class="cx-unit">`). They are compared side by side in a browser:
    - an entity with knobs: a hero item, which shows the hero select, the rarity rungs, the item
      level and the tempered inputs, and whose tooltip stage repaints when a rung is clicked and
      when a number is changed;
    - an entity with `Grants` and `LegendaryStats`: a legendary item, whose granted ability and each
      legendary roll draw their own text, facts and stats and not the item's;
    - an entity with `RankRuns`: a trait or an ability, whose Printed row shows its values separated
      by ` / ` on one line with no blank lines and with its coloured rich-text runs styled;
    - `/browse` ungrouped and `/browse?group=…` grouped, with the group-head rows, the chips and the
      facet rail intact;
    - `/`, with the roster, each menu group and the showcase;
    - `/not-found`, and an entity address with an unknown id.
12. Candidate ids are found from the export rather than guessed:
    ```powershell
    $rows = Get-Content output/export/entities.jsonl
    $rows | Where-Object { $_ -match '"legendaryStats"' } | Select-Object -First 1
    $rows | Where-Object { $_ -match '"rankRuns"' }       | Select-Object -First 1
    $rows | Where-Object { $_ -match '"stats"' }          | Select-Object -First 1
    ```
    Each row's `table` and `netId` give the address, under the slug `PublicTypes` files that table
    under.
13. The app is run with Docker running and from the repo root:
    ```
    aspire run
    ```
    which uses `aspire.config.json` and starts the AppHost. The API cannot be started standalone:
    `Program.cs` calls `AddAzureBlobContainerClient("icons")` and `IconSeeder` is an `IHostedService`
    that calls `CreateIfNotExistsAsync`, so a missing or unreachable blob endpoint fails host start.
    The AppHost supplies the Azurite container, the `icons` connection string and
    `FELLOWSHIP_EXPORT`. Ports are derived per worktree from the base directory, so the URL is read
    from the Aspire dashboard, or pinned with `FELLOWSHIP_PORT_OFFSET=0` for
    `http://localhost:5280`.
14. `output/export/` is populated before running. It is, at `G:\source\Fellowship_Data_Extractor\output\export`.
15. **No `.css` file is committed.** `git ls-files "*.css"` and `git ls-files "*.css.map"` both
    return nothing, and `git status` is clean after a build.
16. `.gitignore` carries `*.css` and `*.css.map`. The same change runs
    `git rm --cached src/FellowshipExtractor.Api/wwwroot/css/codex.css`, because the file is tracked
    today and a pattern added to `.gitignore` does not untrack a tracked file.
17. **A fresh clone plus `dotnet build` produces every stylesheet.** After
    `git clone` and `dotnet build src/FellowshipExtractor.slnx` in a directory that has never been
    built, `src/FellowshipExtractor.Api/wwwroot/css/codex.css` exists, a `.razor.css` sits beside
    each of the twenty-seven `.razor.scss` files, and
    `obj/Debug/net10.0/scopedcss/bundle/FellowshipExtractor.Api.styles.css` carries their rules with
    scope attributes on them. No step beyond the build is needed and `sasscompiler.json` is what
    makes it happen.
18. `App.razor` links `css/codex.css` and `FellowshipExtractor.Api.styles.css`. With the app running,
    the second one is requested by the page and answered 200 with a non-empty body. Without that
    link the build is still green and every page is unstyled, so this is checked in the browser's
    network panel rather than inferred.
19. **Rich text is still coloured.** On an entity whose description carries coloured runs, each
    `rt-*` span renders in its token colour, a `.sub` run keeps its dotted underline and help
    cursor, a `.tok` run keeps the mono face and the negative colour, and an `.rt-unstyled` run takes
    the surrounding ink. The same is checked on a rank-progression Printed row, which draws its runs
    inside a `<span class="cx-v cx-prose">` rather than a `<p>`. This is the regression the finding
    in section 2.8 predicts, it is invisible to the compiler and to every bUnit assertion, and grey
    text is the only symptom. The three `::deep` rules are read the same way, wherever a `<code>`
    reaches a note, a fact cell or a key/value row: it takes the mono face and the pale ink, not the
    body font.
20. **Links still behave.** Hover a browse type tab, a facet value, a pill, a crumb, a results-table
    column header and a pager link: none of them underlines, and each takes its own hover colour.
    Those six rules exist only to beat the global `a:hover`, and they now sit in a different
    stylesheet from it.
21. Every rule in the stylesheet the refactor starts from appears in exactly one `.scss`, and the
    stylesheet is deleted rather than left beside its replacement. Sections 2.9, 3, 4 and 6 name the
    owner of each one.
