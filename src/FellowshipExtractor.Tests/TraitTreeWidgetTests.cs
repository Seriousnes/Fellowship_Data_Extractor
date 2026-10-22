using CUE4Parse.UE4.Assets.Exports.Engine;

using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class TraitTreeWidgetTests
{
    private const string TreeTable = "fellowship/Content/Items/Traits/DT_TraitTrees.DT_TraitTrees";
    private const string TagField = "TreeConfigID";
    private const string WidgetField = "TreeWidget";

    private static IReadOnlyDictionary<string, string> WidgetByTag()
    {
        var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in Export.Provider.LoadPackageObject<UDataTable>(TreeTable).RowMap.Values)
        {
            if (PropertyWalk.GetString(row, TagField) is not { } tag) continue;
            if (PropertyWalk.GetString(row, WidgetField) is not { Length: > 0 } widget) continue;

            found[tag] = Path.GetFileNameWithoutExtension(AbilityPresentationIndex.PackagePath(widget));
        }

        return found;
    }

    [Fact]
    public void EveryNamedTreeIsDrawnWithTheWidgetItsRowNames()
    {
        var widgets = WidgetByTag();
        var trees = Export.Loaded.Settings.TraitTrees;

        Assert.NotEmpty(trees);

        foreach (var tree in trees)
        {
            Assert.Equal(widgets[tree.Tag], tree.Layout!.Widget);
            Assert.Equal(tree.Nodes.Count(node => node.ParentNodes.Count > 0), tree.Layout.Nodes.Count);
        }
    }

    [Fact]
    public void TheNecklaceTreeHasItsOwnWidget()
    {
        var necklace = Export.Loaded.Settings.TraitTrees.Single(tree => tree.Tag == "ItemTraitTrees.EASeason4Necklace");

        Assert.Equal("UW_StaticNeclaceTree_EASeason4", necklace.Layout!.Widget);
        Assert.Equal(8, necklace.Layout.Nodes.Count);
    }
}
