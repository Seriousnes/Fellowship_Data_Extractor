using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;

using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

public sealed class TooltipWidgetText
{
    private const string EmptySlotWidget = "UW_RerollableEmptySlot";
    private const string SignBlock = "CRTextBlock_QuestionSign";
    private const string EmptyTextBlock = "Text_StatName";
    private const string TreeWidget = "UW_TraitTreeTooltipBlock";
    private const string TreeHeaderBlock = "TextHeader";
    private const string TextField = "Text";

    public EmptyModifierSlot EmptySlot { get; }

    public string TraitTreeHeader { get; }

    public TooltipWidgetText(AbstractFileProvider provider, IReadOnlyList<FAssetData> assets)
    {
        var empty = Exports(provider, assets, EmptySlotWidget);
        EmptySlot = new EmptyModifierSlot(
            Text(empty, EmptySlotWidget, SignBlock), Text(empty, EmptySlotWidget, EmptyTextBlock));

        TraitTreeHeader = Text(Exports(provider, assets, TreeWidget), TreeWidget, TreeHeaderBlock);
    }

    private static UObject[] Exports(AbstractFileProvider provider, IReadOnlyList<FAssetData> assets, string widget)
    {
        var asset = assets.FirstOrDefault(asset => asset.AssetName.Text.Equals(widget, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"No cooked asset is named {widget}, so no tooltip text reads from it.");

        if (!provider.TryLoadPackage(asset.PackageName.Text, out var package))
        {
            throw new InvalidOperationException($"{asset.PackageName.Text} does not load, so no tooltip text reads from it.");
        }

        return [.. package.GetExports()];
    }

    private static string Text(UObject[] exports, string widget, string block) =>
        exports.FirstOrDefault(export => export.Name == block) is { } text &&
        PropertyWalk.GetText(text, TextField) is { Length: > 0 } written
            ? written
            : throw new InvalidOperationException($"{widget} has no {block} with a {TextField}.");
}
