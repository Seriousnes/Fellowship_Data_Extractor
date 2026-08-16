using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.UE4.Objects.UObject;

using FellowshipExtractor.Tooltip;

namespace FellowshipExtractor.Tests;

public sealed class DisplayTests
{
    private const string Template = "Your abilities gain power for every {spiritpercentage}% Spirit you have.";
    private const string Shipped = "+10 Damage Reduction";

    private static UObject Augmentation(params FPropertyTag[] properties) => new([.. properties]);

    private static FPropertyTag Named(string name, FPropertyTagType tag) =>
        new()
        {
            Name = new FName(name),
            Tag = tag,
            TagData = new FPropertyTagData { Type = tag.GetType().Name },
        };

    private static FPropertyTag DynamicDescription(string text) =>
        Named(
            "DynamicDescription",
            new StructProperty(new FScriptStruct(new FStructFallback([Named("Text", new StrProperty(text))]))));

    [Fact]
    public void AnAssetCarryingOnlyADynamicDescriptionReadsItsText()
    {
        var asset = Augmentation(DynamicDescription(Template));

        Assert.Equal(Template, Display.From(null, asset).Description);
    }

    [Fact]
    public void AnAssetCarryingBothShapesReadsTheShippedDescription()
    {
        var asset = Augmentation(DynamicDescription(Template), Named("Description", new StrProperty(Shipped)));

        Assert.Equal(Shipped, Display.From(null, asset).Description);
    }

    [Fact]
    public void AnAssetCarryingNeitherShapeReadsNoDescription()
    {
        Assert.Null(Display.From(null, Augmentation()).Description);
    }

    [Fact]
    public void ADynamicDescriptionWithNoTextReadsNoDescription()
    {
        var asset = Augmentation(
            Named("DynamicDescription", new StructProperty(new FScriptStruct(new FStructFallback()))));

        Assert.Null(Display.From(null, asset).Description);
    }

    [Fact]
    public void ARowStillPrefersItsDynamicDescriptionOverItsDetailedDescription()
    {
        var row = new FStructFallback([
            DynamicDescription(Template),
            Named("DetailedDescription", new StrProperty(Shipped)),
        ]);

        Assert.Equal(Template, Display.From(row, null).Description);
    }
}
