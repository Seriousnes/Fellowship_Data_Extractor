# Task: find the asset the game draws behind an item icon

**Report findings back to session `818e0ae7-1f8a-4416-b485-6841c0a9ff2a`.**

Needs the installed build and `output/fellowship.usmap`.

## The question

An item slot's background carries its rarity: copper for Legendary, red for Regal. We draw the item
icon's own neutral grey background instead. Find the asset and the draw call that produce the
coloured background, so `export-all` can write it and the slot can draw it.

## Ground truth

Two screenshots the user pasted, recoverable from this repo's session transcripts under
`~/.claude/projects/G--source-Fellowship-Data-Extractor/*.jsonl` by pulling the base64 out of the
user messages' `image` content blocks, and cached at
`~/.claude/image-cache/818e0ae7-1f8a-4416-b485-6841c0a9ff2a/`:

- `1.png`, a character sheet with every slot at Regal.
- `2.png`, 128x128, item 5217 for Helena, `Linked Heavy Metal Cloak`, Legendary, icon
  `Icon_Warmaster_Cloak01_Legendary01_Back_R0_T0`.
- `3.png`, our render of the same icon, byte-identical to the exported PNG.

The user describes the real background as deep orange, nearly copper, approaching white near the
edges. The measurements agree: it asymptotes near `#E19478` and its brightest background pixels are
`#FFB9AF` and `#F3C3C3`.

## Established, do not redo

`fellowship/Content/ui/DesignSystem/Widgets/Shared/UW_GenericItemWidget.uasset` is the slot. Its
`CRImage` layers:

| Layer | Texture |
| --- | --- |
| `EmptyImage` | `T_Borders_EmptyWithoutFrame` |
| `RarityGlow` | `T_Borders_RarityGlow` |
| `OutlineImage` | `T_Borders_RarityFrame` |
| `OuterFrameImage` | `T_Borders_GlassFrame` |
| `HoverHighlightImage` | `T_Borders_SlotHoverHighlight` |
| `IsNewGlowImage` | `T_Borders_Glows_ItemSquare` |
| `Image_UpgradeLEvel` | `T_Borders_ItemRomanNumerals_I` to `_VIII` |
| `EmptyGemSocketImage` | `T_Icons_Gems_EmptySlot` |

`OnRarityChanged` forwards to `ExecuteUbergraph_UW_GenericItemWidget`, which calls
`BPL_DS_ColorsFunctions.GetRarityColor` once and applies it three times:

```
CallFunc_GetRarityColor_Color -> OutlineImage.SetColorAndOpacity
CallFunc_GetRarityColor_Color -> RarityGlow.SetColorAndOpacity
CallFunc_GetRarityColor_Color -> SetOverrideColor
```

The icon texture's alpha is an inverted mask. On the sample above (PF_DXT5, 128x128): 238 distinct
values, **0 over the cloak art**, low but non-zero over the background, one solid pixel in 16,384.
It marks where the background shows. Used as opacity it erases the art.

## Ruled out, with evidence

| Hypothesis | Evidence against |
| --- | --- |
| `RarityGlow` composed over the icon | Fits at 29.51 RMS against 30.97 for the untouched icon. Its alpha is 0 within 18px of centre, mean 29.5 across the 18px to 24px ring, 13.1 beyond, so it lights an inner ring while the screenshot's difference climbs to the rim. |
| Source-over of any one colour through the icon's alpha | Binned by alpha, the implied colour runs 660 red at alpha 23 down to 226 at alpha 245. One colour cannot do that. |
| Additive of `game.rarity-legendary` | 23.56 RMS. |
| The screenshot being an upscale | Mean neighbour gap 31.14 across against ours at 32.04; 13.7% identical horizontal neighbours against ours at 43.4%. It is native. |
| Any per-pixel function of our pixels | The game's background holds **8,288 distinct colours against our 1,306**. It carries detail our DXT5 texture does not, so it is a separate layer, not a recolouring. |

That last row is the important one. Do not fit further curves to the screenshot; find the asset.

## Where to look

1. `UpdateItemIcon` and `OnItemIconChanged` in `ExecuteUbergraph_UW_GenericItemWidget`. If the icon
   brush is set to a **material instance** rather than a texture, that material is the answer and its
   parameters will name both the background and the rarity colour.
2. Any material or material instance taking a rarity colour parameter. `M_`, `MI_` under
   `ui/DesignSystem`.
3. `CRImage`'s own class defaults, in case the background brush belongs to the widget class rather
   than the instance.
4. The `CROverlaySlot` ordering in `WidgetTree`, which also settles where `RarityGlow` sits against
   the icon. That is recorded as unsettled.
5. Whether the slot renders the item live to a render target rather than drawing the baked thumbnail.
   The extra colour detail would follow from that.

## Tools

Dump a widget's exports, brushes and `NamedOverrideColor` rows:

```
dotnet run --project src/FellowshipExtractor -- dump UW_GenericItemWidget 2
```

Walk a Blueprint function's bytecode. The gotcha that cost an hour: `KismetExpression` exposes its
operands as **fields**, not properties. A property-only walk prints `Token` and nothing else.

```csharp
#:project G:/source/Fellowship_Data_Extractor/src/FellowshipExtractor/FellowshipExtractor.csproj

var exports = await provider.LoadPackageAsync(package);

foreach (var export in exports.GetExports())
{
    if (export is not UStruct structure || structure.ScriptBytecode is null) continue;

    foreach (var statement in structure.ScriptBytecode) Walk(statement, "  ");
}
```

`Walk` recurses through `type.GetFields(BindingFlags.Public | BindingFlags.Instance)` before
properties, prints `FPackageIndex.Name` as `-> name`, and stops at depth 9.

Export a texture keeping its alpha, which the normal path discards for collection renders:

```csharp
TextureExporter.Export(texture, path, ETextureFormat.Png, discardAlpha: false);
```

Working probes from this session, ephemeral, under
`C:\Users\Sean\AppData\Local\Temp\claude\G--source-Fellowship-Data-Extractor\818e0ae7-1f8a-4416-b485-6841c0a9ff2a\scratchpad`:
`bytecode.cs`, `slots.cs`, `alpha.cs`, `transfer.cs`, `sharp.cs`.

## What a good answer states

- The asset drawn in the background region, by package path.
- The draw call or material that composites it, and where the rarity colour enters.
- Whether it is one texture tinted per rarity, or one per rarity, or generated.
- Enough for `export-all` to write it and for the slot to draw it.

## How to report

Write the finding to `docs/superpowers/findings/2026-08-09-<slug>.md` and commit it, naming session
`818e0ae7-1f8a-4416-b485-6841c0a9ff2a` in the commit body so this handoff is traceable.

Note: `docs/superpowers/findings/` was deleted in the working tree by a concurrent session and needs
`git checkout -- docs/superpowers` before writing there. Check with whoever owns that sweep first.

## Related

`docs/superpowers/specs/2026-08-09-storage-backed-app-design.md` carries the frame's tint, which is
settled, and carries no claim about the background. The withdrawn model is in commit `8199a6b`.
