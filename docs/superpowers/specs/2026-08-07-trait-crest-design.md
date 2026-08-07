# Trait crest design

**Goal:** A hero trait's node is drawn by a component of its own, and the trait's art is cut to the
hexagon the node plate leaves open, so the gold edge bounds the art on all six sides and the square
corners the art would otherwise push over the frame are gone.

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

## The component

`src/FellowshipExtractor.Api/Components/Tooltip/TraitCrest.razor`, beside `BlessingCrest.razor` and
built the same way: it injects `CodexView`, takes `Entity` (a `TraitDocument`), `State` and a
`Width` defaulting to 72, and reads `Ui.Chrome` for the plate and the padlock and `Ui.Icon` for the
art. It carries the plate URL and its width as inline `--fx-node` and `--fx-node-width`.

Its markup is a `figure.fx-trait-crest` holding a `div.well` wrapping the art, and, when the state
is `unavailable`, the padlock as a sibling of that wrapper.

`Tooltip.razor` draws `<TraitCrest Entity="trait" State="@Query.State" />` for a `TraitDocument`
that carries an icon. `TooltipView.DrawsItsOwnArt` stays where it is and keeps `figure.slot`
suppressed for a trait.

`Browse.razor` is untouched, so a trait's codex row keeps the square icon it draws today.

## The rules that move

`ChromeAssets` gains `Padlocked(string? state)`, which reads true only for `unavailable`, beside the
`NodeStates` list it already owns. `TooltipView` loses `StateFrame`, `Locked` and `LockGlyph`; the
crest reaches `ChromeAssets.TraitNode` and `ChromeAssets.LockGlyph` itself.

## The styling

A `trait-crest` mixin in `_Crest.scss` beside `blessing-crest`, included from
`_Tooltip.razor.scss`, which adds `@use "Crest" as *` and keeps the 12px above and 4px below that
the crest sits at today.

`_Tooltip.razor.scss` gains `$node-well-width: 154` and `$node-well-height: 180` beside the existing
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

## Tests

- `StateChromeTests` gains a flood fill of all three node plates asserting the well is 154x180,
  concentric on 208x240, and identical across the states. This pins the Sass constants to the
  texture.
- `StateChromeTests` asserts `ChromeAssets.Padlocked` reads true for `unavailable` and false for
  `available`, `selected`, null and a word the family does not name.
- `StateTests` and `InsetArtTests` move from the `.crest` and `.crest.node` selectors to
  `.fx-trait-crest`, and from `TooltipView.StateFrame` and `TooltipView.Locked` to
  `ChromeAssets.TraitNode` and `ChromeAssets.Padlocked`.
- `InsetArtTests` keeps asserting that a trait carries its art inside the card and that every other
  row keeps its art beside the card.

## Verification

Markup assertions are blind to compositing, so the evidence that this is right is a screenshot of
the trait tooltip in the running app, at the zoom of the reference the user pasted on 2026-08-07,
showing the art cut on the hexagon's six edges with no square corner over the gold.
