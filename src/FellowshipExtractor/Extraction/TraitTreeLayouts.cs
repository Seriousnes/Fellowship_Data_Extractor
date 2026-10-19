using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.UObject;

namespace FellowshipExtractor.Extraction;

public sealed class TraitTreeLayouts
{
    public const string Chooser = "UW_WeaponInscribe";
    public const string ChooserFunction = "OnSlottedItemChanged";
    public const string NodeWidget = "UW_WeaponTrait";
    public const string ColorFunction = "UpdateTraitCategoryColor";
    public const string NodesFunction = "Set Nodes VM";

    private const string MatchesTagFunction = "MatchesTag";
    private const string CreateFunction = "Create";
    private const string HasTraitTagFunction = "HasTraitTag";
    private const string SetColorFunction = "SetNamedColorOverride";

    private const string NamedColorStruct = "CRNamedColor";
    private const string ItemWidgetClass = "UW_GenericItemWidget_C";
    private const string CanvasSlotClass = "CanvasPanelSlot";
    private const string ImageClass = "CRImage";
    private const string SizeBoxClass = "CRSizeBox";

    private readonly Dictionary<string, string> _widgetByTag = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _otherwise;
    private readonly Dictionary<string, TraitTreeLayout> _byWidget = new(StringComparer.OrdinalIgnoreCase);

    public TraitTreeLayouts(AbstractFileProvider provider)
    {
        _otherwise = Chosen(provider);

        var (width, height) = NodeSize(provider);

        foreach (var widget in _widgetByTag.Values.Append(_otherwise).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            _byWidget[widget] = Read(provider, widget) with { NodeWidth = width, NodeHeight = height };
        }

        Colors = NodeColors(provider);
    }

    public TraitNodeColorRows Colors { get; }

    public IEnumerable<string> Textures =>
        _byWidget.Values.SelectMany(layout => layout.RootLayers).Select(layer => layer.Texture).OfType<string>();

    public TraitTreeLayout For(TraitTreeEntry tree)
    {
        var widget = _widgetByTag
            .Where(pair => tree.Tag.Equals(pair.Key, StringComparison.OrdinalIgnoreCase) ||
                           tree.Tag.StartsWith(pair.Key + ".", StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair.Value)
            .FirstOrDefault() ?? _otherwise;

        var layout = _byWidget[widget];

        var unplaced = tree.Nodes
            .Where(node => node.ParentNodes.Count > 0 && !layout.Nodes.ContainsKey(node.UITreeIndex))
            .Select(node => node.UITreeIndex)
            .ToArray();

        if (unplaced.Length > 0 || tree.Nodes.Count(node => node.ParentNodes.Count > 0) != layout.Nodes.Count)
        {
            throw new InvalidOperationException(
                $"{Chooser}.{ChooserFunction} draws {tree.Tag} with {widget}, which places {layout.Nodes.Count} " +
                $"nodes, and the tree has {tree.Nodes.Count - 1} beside its root" +
                (unplaced.Length > 0 ? $", {string.Join(", ", unplaced)} of them unplaced" : string.Empty) + ".");
        }

        return layout;
    }

    private string Chosen(AbstractFileProvider provider)
    {
        string? pending = null;
        var otherwise = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var statement in Bytecode(provider, Chooser, ChooserFunction))
        {
            foreach (var expression in Flattened(statement, 0))
            {
                switch (Called(expression))
                {
                    case (MatchesTagFunction, var arguments):
                        pending = arguments.SelectMany(argument => Flattened(argument, 0))
                            .OfType<EX_NameConst>()
                            .Select(name => name.Value.Text)
                            .FirstOrDefault() ?? pending;
                        break;

                    case (CreateFunction, var arguments):
                        if (arguments.OfType<EX_ObjectConst>().Select(made => Widget(made)).FirstOrDefault()
                            is not { } widget) break;

                        if (pending is null) otherwise.Add(widget);
                        else _widgetByTag.TryAdd(pending, widget);

                        pending = null;
                        break;
                }
            }
        }

        return otherwise.Count == 1
            ? otherwise.Single()
            : throw new InvalidOperationException(
                $"{Chooser}.{ChooserFunction} creates {otherwise.Count} tree widgets for an item matching none of " +
                $"{string.Join(", ", _widgetByTag.Keys)}, so the layout of every other tree is unnamed.");
    }

    private static string? Widget(EX_ObjectConst made) =>
        PropertyWalk.AsString(made.Value) is { } path && path.EndsWith("_C", StringComparison.Ordinal)
            ? Path.GetFileNameWithoutExtension(AbilityPresentationIndex.PackagePath(path))
            : null;

    private static TraitTreeLayout Read(AbstractFileProvider provider, string widget)
    {
        var exports = Exports(provider, widget);
        var byPath = exports.ToDictionary(export => export.GetPathName(), StringComparer.OrdinalIgnoreCase);

        var (width, height) = CanvasSize(exports, byPath, widget);

        var slots = exports.Where(export => export.ExportType == CanvasSlotClass).ToArray();

        var nodes = new SortedDictionary<int, TraitTreePoint>();
        var placed = NodeOrder(exports, widget);

        for (var index = 0; index < placed.Count; index++)
        {
            if (placed[index] is not { } name) continue;

            var slot = slots.FirstOrDefault(candidate =>
                    Referenced(candidate, "Content", byPath)?.Name.Equals(name, StringComparison.Ordinal) is true)
                ?? throw new InvalidOperationException(
                    $"{widget}.{NodesFunction} lists {name} as node {index}, and no canvas slot holds it.");

            nodes[index] = Offset(slot);
        }

        var root = slots
            .Select(slot => (Slot: slot, Content: Referenced(slot, "Content", byPath)))
            .Where(entry => entry.Content is not null)
            .Select(entry => (entry.Slot, Layers: Layers(entry.Content!, byPath, null, true).ToArray()))
            .Where(entry => entry.Layers.Any(layer => layer.Item) && entry.Layers.Any(layer => layer.Texture is not null))
            .ToArray();

        if (root.Length != 1)
        {
            throw new InvalidOperationException(
                $"{widget} has {root.Length} canvas slots drawing an item inside a framed image, so the root of the " +
                "tree is unplaced.");
        }

        return new TraitTreeLayout(widget, width, height)
        {
            Root = Offset(root[0].Slot),
            RootLayers = root[0].Layers,
            Nodes = nodes,
        };
    }

    private static (double Width, double Height) CanvasSize(
        IReadOnlyList<UObject> exports, IReadOnlyDictionary<string, UObject> byPath, string widget)
    {
        var tree = exports.FirstOrDefault(export => export.ExportType == "WidgetTree");

        return tree is not null && Referenced(tree, "RootWidget", byPath) is { ExportType: SizeBoxClass } box &&
               Overrides(box) is { } size
            ? size
            : throw new InvalidOperationException(
                $"{widget}'s root widget is no {SizeBoxClass} with a width and height, so the tree's canvas is " +
                "unsized.");
    }

    private static (double Width, double Height) NodeSize(AbstractFileProvider provider)
    {
        var sized = Exports(provider, NodeWidget)
            .Where(export => export.ExportType == SizeBoxClass)
            .Select(Overrides)
            .OfType<(double, double)>()
            .ToArray();

        return sized.Length == 1
            ? sized[0]
            : throw new InvalidOperationException(
                $"{NodeWidget} has {sized.Length} {SizeBoxClass}es overriding both edges, so a trait node is " +
                "unsized.");
    }

    private static (double Width, double Height)? Overrides(UObject box) =>
        PropertyWalk.AsNumber(PropertyWalk.Get(box, "WidthOverride")?.GenericValue) is { } width &&
        PropertyWalk.AsNumber(PropertyWalk.Get(box, "HeightOverride")?.GenericValue) is { } height
            ? (width, height)
            : null;

    private static IReadOnlyList<string?> NodeOrder(IReadOnlyList<UObject> exports, string widget)
    {
        var function = exports.OfType<UStruct>()
            .FirstOrDefault(export => export.Name.Equals(NodesFunction, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"{widget} compiles no {NodesFunction}, so its nodes are unordered.");

        var made = (function.ScriptBytecode ?? []).OfType<EX_SetArray>().FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"{widget}.{NodesFunction} makes no array, so its nodes are unordered.");

        return [.. made.Elements.Select(element => element is EX_NoObject ? null : DynamicTextFloats.Name(element))];
    }

    private static TraitTreePoint Offset(UObject slot)
    {
        var offsets = PropertyWalk.GetStruct(slot, "LayoutData") is { } layout
            ? PropertyWalk.GetStruct(layout, "Offsets")
            : null;

        return new TraitTreePoint(
            offsets is null ? 0 : PropertyWalk.AsNumber(PropertyWalk.Get(offsets, "Left")?.GenericValue) ?? 0,
            offsets is null ? 0 : PropertyWalk.AsNumber(PropertyWalk.Get(offsets, "Top")?.GenericValue) ?? 0);
    }

    private static IEnumerable<TraitTreeRootLayer> Layers(
        UObject widget, IReadOnlyDictionary<string, UObject> byPath, (double Width, double Height)? box, bool filled)
    {
        if (widget.ExportType == ItemWidgetClass)
        {
            yield return new TraitTreeRootLayer(box?.Width ?? 0, box?.Height ?? 0) { Item = true };
            yield break;
        }

        if (widget.ExportType == ImageClass)
        {
            var brush = PropertyWalk.GetStruct(widget, "Brush");
            var drawn = (brush is null ? null : PropertyWalk.Get(brush, "ImageSize")?.GenericValue) switch
            {
                FVector2D image => image,
                FScriptStruct { StructType: FVector2D image } => image,
                _ => (FVector2D?)null,
            };
            var texture = brush is null ? null : PropertyWalk.GetString(brush, "ResourceObject");

            if (texture is not null)
            {
                var (width, height) = filled && box is { } within ? within : (drawn?.X ?? 0, drawn?.Y ?? 0);

                yield return new TraitTreeRootLayer(width, height)
                {
                    Texture = AbilityPresentationIndex.AssetName(texture),
                };
            }

            yield break;
        }

        if (widget.ExportType == SizeBoxClass && Overrides(widget) is { } size) box = size;

        if (PropertyWalk.Get(widget, "Slots")?.GenericValue is not UScriptArray slots) yield break;

        box ??= slots.Properties
            .Select(entry => PropertyWalk.AsString(entry?.GenericValue) is { } path && byPath.TryGetValue(path, out var slot)
                ? Referenced(slot, "Content", byPath)
                : null)
            .Where(content => content?.ExportType == SizeBoxClass)
            .Select(content => Overrides(content!))
            .FirstOrDefault(size => size is not null);

        foreach (var entry in slots.Properties)
        {
            if (PropertyWalk.AsString(entry?.GenericValue) is not { } path ||
                !byPath.TryGetValue(path, out var slot) ||
                Referenced(slot, "Content", byPath) is not { } content) continue;

            var padding = PropertyWalk.GetStruct(slot, "Padding");
            var outset = padding is null ? 0 : -(PropertyWalk.AsNumber(PropertyWalk.Get(padding, "Left")?.GenericValue) ?? 0);
            var fills = PropertyWalk.GetString(slot, "HorizontalAlignment")?.EndsWith("Fill", StringComparison.Ordinal)
                ?? true;

            foreach (var layer in Layers(
                content, byPath, box is { } within ? (within.Width + outset * 2, within.Height + outset * 2) : null, fills))
            {
                yield return layer;
            }
        }
    }

    private static UObject? Referenced(UObject holder, string field, IReadOnlyDictionary<string, UObject> byPath) =>
        PropertyWalk.GetString(holder, field) is { } path && byPath.TryGetValue(path, out var found) ? found : null;

    private static TraitNodeColorRows NodeColors(AbstractFileProvider provider)
    {
        var locals = new Dictionary<string, string>(StringComparer.Ordinal);
        var byCategory = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var otherwise = new HashSet<string>(StringComparer.Ordinal);
        string? pending = null;

        foreach (var statement in Bytecode(provider, NodeWidget, ColorFunction))
        {
            if (statement is EX_Let { Assignment: EX_StructConst { Struct.Name: NamedColorStruct } colour } let &&
                DynamicTextFloats.Name(let.Variable) is { } local &&
                Flattened(colour, 0).OfType<EX_NameConst>().FirstOrDefault() is { } row)
            {
                locals[local] = row.Value.Text;
                continue;
            }

            foreach (var expression in Flattened(statement, 0))
            {
                switch (Called(expression))
                {
                    case (HasTraitTagFunction, var arguments):
                        pending = arguments.SelectMany(argument => Flattened(argument, 0))
                            .OfType<EX_NameConst>()
                            .Select(name => name.Value.Text)
                            .FirstOrDefault();
                        break;

                    case (SetColorFunction, var arguments):
                        if (arguments.Select(DynamicTextFloats.Name).OfType<string>().FirstOrDefault() is not { } name ||
                            !locals.TryGetValue(name, out var colourRow)) break;

                        if (pending is null) otherwise.Add(colourRow);
                        else byCategory.TryAdd(pending, colourRow);

                        pending = null;
                        break;
                }
            }
        }

        if (byCategory.Count == 0 || otherwise.Count != 1)
        {
            throw new InvalidOperationException(
                $"{NodeWidget}.{ColorFunction} colours {byCategory.Count} trait categories and falls back to " +
                $"{otherwise.Count} colours, so a node's category colour is unread.");
        }

        return new TraitNodeColorRows(otherwise.Single()) { ByCategory = byCategory };
    }

    private static (string Name, IReadOnlyList<KismetExpression> Arguments)? Called(KismetExpression expression) =>
        expression switch
        {
            EX_FinalFunction call => (call.StackNode.Name, call.Parameters),
            EX_VirtualFunction call => (call.VirtualFunctionName.Text, call.Parameters),
            _ => null,
        };

    private static IEnumerable<KismetExpression> Flattened(KismetExpression? expression, int depth)
    {
        if (expression is null || depth > 16) yield break;

        yield return expression;

        var children = expression is EX_SetArray made ? made.Elements : ResourceGains.Children(expression);

        foreach (var child in children)
        {
            foreach (var nested in Flattened(child, depth + 1)) yield return nested;
        }
    }

    private static UObject[] Exports(AbstractFileProvider provider, string asset)
    {
        var file = provider.Files.Values.FirstOrDefault(candidate =>
            candidate.Path.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase) &&
            Path.GetFileNameWithoutExtension(candidate.Path).Equals(asset, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"No {asset} package in the build, so the trait tree is undrawn.");

        return [.. provider.LoadPackage(file).GetExports()];
    }

    private static KismetExpression[] Bytecode(AbstractFileProvider provider, string asset, string function) =>
        Exports(provider, asset).OfType<UStruct>()
            .FirstOrDefault(export => export.Name.Equals(function, StringComparison.Ordinal))?.ScriptBytecode
        ?? throw new InvalidOperationException($"{asset} compiles no {function}, so the trait tree is undrawn.");
}
