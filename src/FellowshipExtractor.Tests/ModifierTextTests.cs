using CUE4Parse.UE4.Assets.Exports.Engine;

using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class ModifierTextTests
{
    private const string ModifierTable = "fellowship/Content/Modifiers/DT_ModifierInfo.DT_ModifierInfo";
    private const string PathfinderRow = "Ascension6";
    private const string DescriptionField = "Description";
    private const string DynamicDescriptionField = "DynamicDescription";

    [Fact]
    public void AModifierWhoseDescriptionIsEmptyReadsItsDynamicDescription()
    {
        var row = Export.Provider.LoadPackageObject<UDataTable>(ModifierTable).RowMap
            .Single(entry => entry.Key.Text == PathfinderRow).Value;

        Assert.True(string.IsNullOrEmpty(PropertyWalk.GetText(row, DescriptionField)));

        var dynamic = PropertyWalk.GetString(PropertyWalk.GetStruct(row, DynamicDescriptionField)!, "Text");
        var pathfinder = Export.Loaded.Documents
            .Single(document => document.Type == EntityType.Modifier && document.Name == "Pathfinder's Guidance")
            .Body;

        Assert.Equal(dynamic, pathfinder["description"]?.GetValue<string>());
        Assert.Contains("{HasteValue}", dynamic);

        var haste = pathfinder["placeholders"]!.AsArray().Single(placeholder => placeholder!["key"]?.GetValue<string>() == "HasteValue")!;
        Assert.Equal(0.2, haste["constants"]!["PathFinder.MaxHaste"]!.GetValue<double>());
    }
}
