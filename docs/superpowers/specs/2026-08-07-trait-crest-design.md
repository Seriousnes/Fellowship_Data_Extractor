# Trait tooltip design

**Goal:** A hero trait is drawn by a card of its own, and its node is drawn by a component of its
own, with the trait's art cut to the hexagon the node plate leaves open, so the gold edge bounds the
art on all six sides and the square corners the art would otherwise push over the frame are gone.

## Measurements

Read off `T_Buttons_TraitNode_SelectedDefault`, `_AvailableDefault` and `_UnavailableDefault` in
`output/export/ui/` on 2026-08-07.

- **The plate is 208x240 and opaque.** Every pixel of its middle row reads alpha 255 and its centre
  reads `#0C1722`. Art drawn under it is invisible, so the gold bounds the art by clipping the art,
  not by painting over it.
- **The well is a hexagon 154x180, concentric on the canvas.** Flood-filling `#0C1722` from the
  canvas centre gives `x 27..180, y 30..209` in all three states, centred on (103.5, 119.5), which
  is the canvas centre. Its area is 20832 pixels against 20790 for a hexagon of that bounding box,
  and its widest row is y75, a quarter of the way down, so it is a pointy-top hexagon with flat
  left and right sides. As a share of the canvas that is 74.04% wide by 75.00% tall.
- **The art is square.** All 40 trait rows that carry an icon ship at 256x256 or 512x512.

At the drawn node width of 72px the rect is 83.08px tall, the well is 53.31x62.31px, and a centred
square of side 62.31px reaches the hexagon corner to corner, with 4.5px cut off each flank.

## The card

`src/FellowshipExtractor.Api/Components/Tooltip/Trait.razor` draws a hero trait's whole card, beside
`Blessing.razor` and built the same way. `Tooltip.razor` renders it for a `TraitDocument`, so
`EntityPage.razor` and the `/api/tooltip/{type}/{id}` endpoint reach it unchanged, and every trait
branch leaves the base.

The card is a `fx-tip plain fx-trait` window carrying the composed sheet and one `.card`:

```
+-- 274px ------------------------------------------+
|                    HIDDEN POWER                   |  15px ArnoPro, core.text-primary
| ------------------------------------------------- |  1px T_GoldDivider, inset 14px
|                                                   |
|                      (node)                       |  node 72 x 83.08, art 62.31, centred
|                                                   |
|  Your abilities have a chance to grant you 1      |  12px, core.text-primary,
|  stack of Hidden Power for 60 seconds.            |  11px side padding
+---------------------------------------------------+
```

- the head holds the trait's name alone, so `.plain .head` draws `T_GoldDivider` under it
- the crest is drawn for a trait that carries an icon, 12px under the head and 4px above the prose
- `TipProse` draws the description for a trait that carries one, inside the `.lines` column the
  window already styles
- the card asks for no rarity, so a trait always draws the plain rounded window

`Components/Tooltip/_Trait.razor.scss` holds the card's rules, reached from `Styles/tooltip.scss` by
a third `@use` beside the two already there. It carries the `trait-crest` mixin and the 12px above
and 4px below that the crest sits at.

`TooltipView` keeps `Variables`, `Style` and `Text` for the card, and answers the questions the
window asks for every other row.

## The crest

`src/FellowshipExtractor.Api/Components/Tooltip/TraitCrest.razor`, beside `BlessingCrest.razor` and
built the same way: it injects `CodexView`, takes `Entity` (a `TraitDocument`), `State` and a
`Width` defaulting to 72, and reads `Ui.Chrome` for the plate and the padlock and `Ui.Icon` for the
art. It carries the plate URL and its width as inline `--fx-node` and `--fx-node-width`.

Its markup is a `figure.fx-trait-crest` holding a `div.well` wrapping the art, and, when the state
is `unavailable`, the padlock as a sibling of that wrapper and a `locked` class on the figure.

A padlocked node draws its art desaturated, the way `UW_WeaponTrait` draws the icon it is not
allocated: that widget's `Image_TraitIconNotAllocated` carries `M_DesaturateTexture` as its brush
where `Image_TraitIconAllocated` carries the icon itself. The material's graph is editor-only and
stripped from the cooked build, and its name qualifies the desaturation by no amount, so the crest
takes it to the full `grayscale(1)`.

`Trait.razor` draws `<TraitCrest Entity="Entity" State="@Query.State" />` for a trait that carries
an icon.

`Browse.razor` is untouched, so a trait's codex row keeps the square icon it draws today.

## The rules that move

`ChromeAssets` gains `Padlocked(string? state)`, which reads true only for `unavailable`, beside the
`NodeStates` list it already owns. The crest reaches `ChromeAssets.TraitNode` and
`ChromeAssets.LockGlyph` itself.

## The styling

A `trait-crest` mixin in `_Crest.scss` beside `blessing-crest`, included from
`_Trait.razor.scss`, which adds `@use "Crest" as *` and keeps the 12px above and 4px below that the
crest sits at.

`_Crest.scss` holds `$node-well-width: 154` and `$node-well-height: 180` beside
`$node-brush-width: 208` and `$node-brush-height: 240`, and the mixin derives every number from
those four:

- the figure is `var(--fx-node-width)` wide at `aspect-ratio: 208 / 240`, with the plate as its
  background at `100% 100%`
- `.well` is `inset: 0` on that figure and carries the clip, so the polygon's percentages resolve
  against the node rect
- the polygon's flank inset is `(208 - 154) / 2 / 208`, its vertex inset is `(240 - 180) / 2 / 240`
  and its shoulder is `180 / 4 / 240`, giving `12.980769%`, `12.5%` and `18.75%`, and the six
  vertices `50% 12.5%`, `87.019231% 31.25%`, `87.019231% 68.75%`, `50% 87.5%`, `12.980769% 68.75%`
  and `12.980769% 31.25%`
- the art is centred in `.well` at `width: 180 / 208` of the figure, `aspect-ratio: 1`,
  `object-fit: contain`
- the padlock keeps the 16px size and the position it has today, 4px above the node rect and
  horizontally centred, as a sibling of `.well` so the hexagon's top vertex does not cut it
- `.locked .well img` carries `filter: grayscale(1)`

## Tests

- `StateChromeTests` gains a flood fill of all three node plates asserting the well is 154x180,
  concentric on 208x240, and identical across the states. This pins the Sass constants to the
  texture.
- `StateChromeTests` asserts `ChromeAssets.Padlocked` reads true for `unavailable` and false for
  `available`, `selected`, null and a word the family does not name.
- `StateTests` asserts only the padlocked node carries the `locked` class and that the sheet cuts
  its art to grayscale.
- `StateTests` and `InsetArtTests` read the crest through `.fx-trait-crest`, and the node's state
  through `ChromeAssets.TraitNode` and `ChromeAssets.Padlocked`.
- `InsetArtTests` keeps asserting that a trait carries its art inside the card and that every other
  row keeps its art beside the card.
- `TraitCardTests` asserts the card is the trait's own component, that the type and not the art
  chooses it, that the crest sits in the card at 72px, that the prose carries the styles the build
  ships, that a trait the build writes nothing about draws no prose, and that the card asks for none
  of the rarity chrome.
- `SheetTests` keeps every selector in the composed sheet under `.fx-tip`, which `_Trait.razor.scss`
  answers by nesting its rules in `.fx-tip.fx-trait`.

## Verification

Markup assertions are blind to compositing, so the evidence that this is right is a screenshot of
the trait tooltip in the running app, at the zoom of the reference the user pasted on 2026-08-07,
showing the art cut on the hexagon's six edges with no square corner over the gold, a trait that
carries no icon drawing its name over its prose, and the padlocked node drawing its art
desaturated.
