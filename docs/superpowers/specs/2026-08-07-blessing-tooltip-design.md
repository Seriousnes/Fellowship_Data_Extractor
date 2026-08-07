# Blessing tooltip design

**Goal:** A blessing is drawn by a component of its own, reading the way the game's own blessing
tooltip reads: a small BLESSING heading over a gold name, a hair-thin gold rule, the rune on its
plate at the size the game draws it, a subheader band saying Hero Blessing and Equipped Tier, and
centred prose whose numbers come out of the build. The plate and rune are one component, so the
codex row draws the same icon the card does.

## Measurements

The reference is the in-game blessing tooltip for *The Mystic*, unallocated, screenshotted by the
user on 2026-08-07 and held at
`~/.claude/projects/G--source-Fellowship-Data-Extractor/*.jsonl`. Its card runs x 9..337 and
y 8..281, so it is 329px wide.

The card this app draws is 274px wide. **Every geometry number below is a reference measurement
times 0.833** (329 to 274), rounded to the nearest half pixel. Colours are sampled straight and are
not scaled.

| What | Reference | Read as |
|---|---|---|
| BLESSING cap height | 10px | ArnoPro 11.5px, uppercase, tracked |
| BLESSING colour | `#909BA6` | `core.text-secondary` |
| THE MYSTIC cap height | 13px | ArnoPro 15px stands, and 10/13 of it sets the heading |
| THE MYSTIC colour | `#B08E58` | `core.primary-main` |
| Rule under the title | one row of `#B08F5A` at y 67 | `T_GoldDivider` drawn 1px tall |
| Rule extent | x 26..322, fading over 74px each end | inset 14px, the texture's own fade |
| Plate | x 138..209, y 77..169, so 72 x 93 | 60 x 76px, aspect 148:188 from the texture |
| Rune | 45% of plate width, centred | 27px, fitted by least mean absolute difference |
| Band | y 178..209, flat `#1D2934` fading over 70px each end | `T_Backgrounds_TooltipHeader_Subheader`, 26px tall, full width |
| Band text | x 26..321 | 14px side padding |
| Hero Blessing | `#909BA6` | `core.text-secondary` |
| Equipped Tier | `#777C81` | `core.text-info` |
| 0 / 4 | `#949392` | `core.text-disabled` |
| Prose line pitch | 16px | 13.3px |
| Prose cap height | 9px | 10.5px font size |
| Prose colour | `#909BA6` | `core.text-secondary` |
| Prose block | lines wrap between 259px and 287px wide | 23px side padding |
| Card fill | `#0D151F` at the edges rising to `#101B24` | `T_Borders_WindowBackground_Windowed`, which matches |

The rich text inside the prose already matches: `rt.absorb` samples `#D3A55A`, `rt.warning`
`#E08600` and `rt.bold` `#EAE3DB`, which are the colours `textStyles` ships.

`T_Backgrounds_TooltipHeader_Subheader` backs the band, not the head. The reference runs the card
fill's smooth gradient the whole way from the top to y 178, and the flat `#1D2934` starts there.

## Components

**`Components/Tooltip/Blessing.razor`** draws the whole card. `Tooltip.razor` renders it when the
document is a `BlessingDocument`, so `EntityPage.razor` and the `/api/tooltip/{type}/{id}` endpoint
reach it unchanged. Every blessing branch leaves the base: `DrawsItsOwnArt`, `StateFrame`,
`StateArt` and the `plate` crest family answer for hero traits alone.

**`Components/Tooltip/BlessingRune.razor`** draws the plate with the rune on it, taking the
blessing, the state and the plate width. The card asks for 60px and the codex row for 26px; the
rune is 45% of whichever width it is given and the plate is 188/148 of it tall.

**`Components/Tooltip/_Blessing.razor.scss`** holds the card's rules, reached from
`Styles/tooltip.scss` by a second `@use` beside the one already there, so `wwwroot/css/tooltip.css`
keeps carrying everything `TooltipView.Style()` serves.

**`Components/Tooltip/_Card.scss`** holds what both cards share: the `.fx-tip` root type and
colour, the window border and fill, and the rich text hooks. `_Tooltip.razor.scss` and
`_Blessing.razor.scss` both `@use` it.

## The card, top to bottom

```
+-- 274px ------------------------------------------+
|                     BLESSING                      |  11.5px ArnoPro, core.text-secondary
|                    THE MYSTIC                     |  15px ArnoPro, core.primary-main
| ------------------------------------------------- |  1px T_GoldDivider, inset 14px
|                                                   |
|                      (rune)                       |  plate 60 x 76, rune 27, centred
|                                                   |
|###################################################|  26px subheader band
|# Hero Blessing            Equipped Tier   0 / 4 #|  10.5px, 14px side padding
|###################################################|
|      Your POWER abilities gain 0.3% / 0.5% /      |  10.5px / 13.3px, centred,
|    0.8% / 1.2% increased power for every 3%       |  core.text-secondary,
|      Spirit you have, up to 50% Spirit.           |  23px side padding
+---------------------------------------------------+
```

Vertical rhythm, each figure the reference distance times 0.833: 14px above the heading, the name
directly under it, 2px under the name to the rule, 8px to the top of the plate, 7px under the plate
to the band, 8px under the band to the first line of prose, 12px under the last line.

## Wording

The build ships every word this card prints, in the `ST_Abilities` locres namespace:

| Key | Text | Where it draws |
|---|---|---|
| `Abilities_Feature_Finesse_Name` | Blessing | the heading over the name |
| `Abilities_Tooltip_Finesse_HeroBlessing` | Hero Blessing | the left of the band |
| `Abilities_Tooltip_Finesse_TierLevel` | Equipped Tier | the right of the band |

`Blessings` in `src/FellowshipExtractor/Tooltip/` reads them the way `ItemSlotNames` reads
`ItemType_*`, opening `fellowship/Content/Localization/Game/en/Game.locres` and stopping with a
message that names the namespace when it holds none of them.

The same reader states how many tiers a blessing has. Every one of the 43 rows the
`FinesseConstants` curve table ships carries the columns `1`, `2`, `3`, `4`, so the ceiling is the
column count those rows agree on, reached through `ConstantsTable.AllUnder("Finesse")`.

`Vocabulary` gains a `blessings` section carrying all four, and `VocabularyFile` in the SDK reads
it back:

```json
"blessings": {
  "name": "Blessing",
  "heroBlessing": "Hero Blessing",
  "tierLevel": "Equipped Tier",
  "tiers": 4
}
```

Nothing the SDK or the API declares, prints or addresses says finesse or rune. Those are the build's
words and stay inside the shipped keys, paths and texture names the extractor quotes.

## The equipped tier

`TooltipQuery` gains `EquippedTier`, read from an `equippedTier` whole-number query parameter and
described in `TooltipQuery.Parameters` so the OpenAPI document carries it. Absent reads as 0, and
the band draws `Equipped Tier 0 / 4`, which is what the reference shows for a blessing the reader
has not allocated.

## The numbers in the prose

`Sole` in `TooltipResolver` reads a single value out of a placeholder's own constants. It answers
that value when the constants hold one `double`, and it answers that value when the constants hold
one `double?[]` whose entries are every one non-null and equal. A ladder whose entries differ stays
unresolved, because the tier it should read at is not a property of the placeholder.

That resolves *The Mystic*: `spiritpercentage` binds
`Finesse.IncreasedPowerPerCurrentSpirit.SpiritAmountPerPowerInPercentage`, which is `[3,3,3,3]`, and
`MaxSpiritPercentage` binds `SpiritThreshold`, which is `[0.5,0.5,0.5,0.5]` and carries
`percent: true`, so the card reads `for every 3% Spirit you have, up to 50% Spirit`. It also
resolves `{cooldown}` on The Wayfarer, `{chance}` on The Heretic, `{increase}` on The Philosopher
and `{critthreshold}` on The Vehement, and tokens beyond blessings wherever a flat ladder backs one.

The array stays on the placeholder's `constants`, so the Knobs and Substitutions views keep printing
the whole ladder.

`entities.jsonl` carries the resolved values, so `export-all` runs once the change is in.

## The codex row

`Browse.razor`'s art cell renders `BlessingRune` for a `BlessingDocument`, allocated, at a plate 26px
wide and 33px tall with a 12px rune, which is the icon the card draws at the size the 34px cell
holds. Every other document keeps the `Icon` it draws today.

## Verification

The card is checked against the reference screenshot with the browser, at
`/entity/blessing/706` and `/entity/blessing/706?state=unallocated`, and the row at
`/browse?type=blessing`. bunit covers the structure: the heading, the band's three strings, the
prose being centred, and `BlessingRune` drawing both layers at the width it is given. Neither
substitutes for looking at it, because what is being matched is compositing.
