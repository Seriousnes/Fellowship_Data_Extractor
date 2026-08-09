# The rarity fill is a layer we never export

**Finding:** an item slot is a stack of seven `CRImage` layers in `UW_GenericItemWidget`, two of
which the game tints with the rarity colour at runtime. We export neither of the two textures that
carry the fill, so our slot draws the item icon's own grey background where the game draws rarity
colour. The item icon's alpha channel is not involved.

Gathered 2026-08-09 against the installed build with `output/fellowship.usmap`, prompted by a
character sheet screenshot where every Regal slot reads red and ours reads grey.

## The stack

`fellowship/Content/ui/DesignSystem/Widgets/Shared/UW_GenericItemWidget.uasset`:

| Layer | Texture | Exported today |
| --- | --- | --- |
| `EmptyImage` | `T_Borders_EmptyWithoutFrame` | yes |
| `RarityGlow` | `T_Borders_RarityGlow` | **no** |
| `OutlineImage` | `T_Borders_RarityFrame` | yes |
| `OuterFrameImage` | `T_Borders_GlassFrame` | yes |
| `HoverHighlightImage` | `T_Borders_SlotHoverHighlight` | **no** |
| `IsNewGlowImage` | `T_Borders_Glows_ItemSquare` | **no** |
| `Image_UpgradeLEvel` | `T_Borders_ItemRomanNumerals_III` | **no** |
| `EmptyGemSocketImage` | `T_Icons_Gems_EmptySlot` | **no** |

`Image_UpgradeLEvel` is the roman numeral the screenshot reads as `VIII`, and
`EmptyGemSocketImage` is the socket in the bottom right corner.

## How the rarity colour reaches them

`OnRarityChanged` forwards to `ExecuteUbergraph_UW_GenericItemWidget`, which calls
`BPL_DS_ColorsFunctions.GetRarityColor` once and applies the result three times:

```
CallFunc_GetRarityColor_Color -> OutlineImage.SetColorAndOpacity
CallFunc_GetRarityColor_Color -> RarityGlow.SetColorAndOpacity
CallFunc_GetRarityColor_Color -> SetOverrideColor
```

`SetColorAndOpacity` multiplies the brush texture by the colour and leaves alpha alone, which is the
same operation `ChromeAssets.Tint` performs. So the game tints the frame and the fill with one
colour, and `T_Borders_RarityGlow` is what makes a Regal slot read red.

The serialized defaults agree: `RarityGlow` carries `NamedOverrideColor` = `game.rarity-common` and
`IsNewGlowImage` carries `core.primary-200`, both rows in `DT_DS_NamedColors_All`. `OutlineImage`
carries no override in the defaults because the ubergraph sets it.

## The item icon's alpha is not the fill mask

`Icon_Warmaster_Cloak01_Legendary01_Back_R0_T0` (PF_DXT5, 128x128, item 5217 for Helena,
`Linked Heavy Metal Cloak`) reads 238 distinct alpha values, **0 over the cloak art**, low but
non-zero over the background, and exactly one fully solid pixel in 16,384. Drawn as opacity it would
erase the art, which is what [[skin-render-icons-carry-their-own-background]] recorded. Discarding it
is correct and stays correct.

The vignette that separates a game screenshot of that cloak from our render - `+44` red at the outer
ring, `~0` at centre, `~0` wherever the art is - is `RarityGlow` tinted `game.rarity-legendary`, not
anything recoverable from the icon.

## What is not settled

The z-order of `RarityGlow` against the item icon. The exports list alphabetically and the
`CROverlaySlot` entries were not read. An edge-weighted glow looks the same over a centred item
either way, so the screenshot does not separate them; the overlay order does.

## Reproduction

```
dotnet run --project src/FellowshipExtractor -- dump UW_GenericItemWidget 2
```

writes the layer stack with each `Brush.ResourceObject` and `NamedOverrideColor`. The colour wiring
needs a bytecode walk over `ExecuteUbergraph_UW_GenericItemWidget`, reading `KismetExpression`
**fields** rather than properties - a property-only walk prints `Token` and nothing else.
