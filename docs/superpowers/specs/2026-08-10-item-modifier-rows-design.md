# Item modifier rows and the imbued traits line design

**Goal:** Draw every modifier an item carries as its own row, in the order the caller states them, under the chrome the game draws them with, and draw the imbued traits count on the slots that carry one.

## The modifier rows

`Components/Tooltip/TipModifiers.razor` draws one row per entry in `TooltipQuery.Modifiers`, in the order the query states, drawing a row for every entry including repeats of the same id. An item carrying `Martial Initiative` twice draws two identical rows.

A row reads `<Kind>: <name> <amount>`, with a glyph ahead of the kind and the amount in bold with a leading `+`:

| Kind token | Label | Entity type | Amount |
| --- | --- | --- | --- |
| `trait` | `Trait` | `EntityType.Trait` | the rank |
| `blessing` | `Blessing` | `EntityType.Blessing` | the rank |
| `essence` | `Imbued Essence` | `EntityType.GemType` | the power |

The name comes from `IEntityStoreService.All(type, id)` through `PublicTypes.NameOf`. An id the store does not carry prints the id itself, so a caller naming an unknown modifier gets a readable row rather than an error.

The rows sit in one `div.grp`. Each row carries its own plate and a decor at its right edge, and a divider sits between each adjacent pair of rows inside the group.

## The imbued traits line

`Components/Tooltip/TipImbuedTraits.razor` draws the single line `Imbued Traits (x/y)` inside a `div.grp`, with the trait glyph ahead of it, the line in `.sec` and the fraction in `.n` with `x` in `.alloc`.

It draws for an `ItemDocument` whose `Slot` is `Weapon` or `Neck`.

`y` is the slot's imbued trait capacity: 5 for `Weapon`, 2 for `Neck`. The build is read for where it states these two capacities, and they reach the component through the vocabulary the way `BlessingEntry.Tiers` does. Where the build states no such table, a named per-slot table in `TooltipView` carries them.

`x` is the number of entries in `TooltipQuery.ImbuedTraits` when the caller supplies that list, `TooltipQuery.AllocatedTraits` when it does not, and `0` when the caller supplies neither.

`TooltipQuery.HasImbuedTraits` overrides the slot. `false` draws no line on any item. `true` draws the line on any item, taking `y` from the slot table when the slot names one and otherwise the largest capacity the table states.

## The query

`TooltipQuery` carries:

- `IReadOnlyList<ItemModifier> Modifiers`, where `ItemModifier` is a `readonly record struct (ModifierKind Kind, int Id, int Amount)` and `ModifierKind` is an enum of `Trait`, `Blessing` and `Essence`.
- `IReadOnlyList<ImbuedTrait> ImbuedTraits`, reusing the existing `ImbuedTrait(int Id, int Rank)`.
- `int? AllocatedTraits`.
- `bool? HasImbuedTraits`.

`ItemTooltipRequest` carries them as:

- `modifiers=trait:412:1,blessing:7:1,essence:3:100`, comma separated, each entry a kind token, an id and an amount separated by colons. An `ItemModifiers.TryParse` written alongside `ImbuedTraits.TryParse` reads it, and an unreadable entry answers 400 through the existing `StatQuery.Unreadable` path.
- `imbuedTraits=412:1,88:2`, read by the existing `ImbuedTraits.TryParse`.
- `allocatedTraits=3`.
- `hasImbuedTraits=true`.

`Endpoints/Documentation/ListDocument.cs` gains an example for `ItemModifiers`.

## The card

`Item.razor` builds its groups in the order Identity, Facts, Rolls, ImbuedTraits, Modifiers, Prose, Granted, Legendaries, Sets. It draws `TipImbuedTraits` when that component's slot rule holds, and `TipModifiers` when `Query.Modifiers` carries an entry.

`Ability.razor` and `Generic.razor` build their groups in the order Identity, Facts, Rolls, Prose, and `Generic.razor` keeps Overflow last. Both `TipModifiers` and `TipImbuedTraits` are drawn only by `Item.razor`, because only an item carries modifiers or an imbued trait capacity.

`Components/Tooltip/TipTraits.razor` is removed, along with `TooltipQuery.Traits`, `TooltipQuery.MaxTraits`, `TooltipQuery.GemPowers`, the `GemPowers` and `GemPower` parsers, and the `Trait`, `GemPowers` and `MaxTraits` properties of `ItemTooltipRequest`.

## The chrome

`IChromeAssetService` gains:

- `string ModifierPlate` for the row background, from `T_Backgrounds_TooltipSlot_Gradient_Fade`.
- `string ModifierDecor` for the right edge decor, from `T_Backgrounds_TooltipSlot_Gradient_Decor`.
- `string ModifierGlyph(ModifierKind kind)`, answering the glyph each kind draws.

The export carries `T_Backgrounds_TooltipSlot_Gradient_Fade` at 4x120 and `T_Backgrounds_TooltipSlot_Gradient_Decor` at 16x44, so the plate stretches horizontally and the decor holds its width at the right edge.

The widget the build draws these rows with is read first, and it is what names the glyph texture for each of the three kinds, the height a row sits at, and the width the decor occupies. The constants that widget names are what `ChromeAssetService` and `_Tooltip.razor.scss` carry, in the manner `SlotBacking`, `SlotFrame` and `SlotBevel` already follow, and the geometry is recorded the way the `tooltip-chrome-geometry` memory records the card's own. The colour of the kind label, the name and the amount come from `IDesignTokenService` against the shipped colour table.

## Tests

`ModifierCardTests` covers `TipModifiers`: a row per entry, entries drawn in the order given, two entries of one id drawing two rows, each kind drawing its label and its amount, and an id the store does not carry printing the id.

`ImbuedTraitsCardTests` covers `TipImbuedTraits`: a weapon drawing `(0/5)`, a neck item drawing `(0/2)`, every other slot drawing nothing, `imbuedTraits` of two entries drawing `2` where `allocatedTraits` says otherwise, `allocatedTraits` supplying the count alone, and both halves of the `hasImbuedTraits` override.

`ItemCardTests` gains the group order, and `AbilityCardTests` and `GenericCardTests` each assert their card draws neither component.

`ParameterTests` covers `ItemModifiers.TryParse` for a single entry, several entries, each kind token, and the entries an unreadable query rejects. Its `GemPowers` cases and the `GemPowers` assertion in `TooltipQueryTests` go with the parser they cover.

`TooltipDumpTests` gains a `Cases` entry carrying one modifier of each kind, since modifiers reach the card from the query rather than the document, and widens `Shapes` with the lowest id whose `Slot` is `Weapon` and the lowest whose `Slot` is `Neck`, so the dump draws both capacities.

The plate and the decor are composited by CSS, which markup assertions do not observe. The rows are read from a screenshot of a drawn card against the in-game tooltips recorded in the `imbued-rows-are-per-slot` memory, and the reading is stated in the handoff.

## Sequencing

This design is written against the card components `Item.razor`, `Ability.razor` and `Generic.razor`, which arrive in the branch `item-and-ability-cards`. The implementation follows that branch landing.
