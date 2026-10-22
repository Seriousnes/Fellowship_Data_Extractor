using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Objects.Core.i18N;

using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class BlessingWordingTests
{
    private const string TooltipWidget = "fellowship/Content/ui/Hero/UW_BlessingTooltip.uasset";
    private const string TextField = "Text";

    private static IReadOnlySet<(string Namespace, string Key)> TooltipEntries()
    {
        var entries = new HashSet<(string Namespace, string Key)>();

        foreach (var export in Export.Provider.LoadPackage(TooltipWidget).GetExports())
        {
            if (PropertyWalk.Get(export, TextField)?.GenericValue is not FText { TextHistory: FTextHistory.StringTableEntry entry })
            {
                continue;
            }

            entries.Add((entry.TableId.Text, entry.Key));
        }

        return entries;
    }

    [Fact]
    public void TheTooltipWidgetDrawsTheKeysTheReaderTakes()
    {
        var entries = TooltipEntries();

        Assert.Contains(Blessings.NameKey, entries);
        Assert.Contains(Blessings.TierLevelKey, entries);
    }

    [Fact]
    public void TheSettingsCarryTheWordsTheStringTablesHold()
    {
        _ = Export.Provider;
        var wording = Export.Loaded.Settings.Blessings;

        Assert.Equal(StringTables.Find(Blessings.NameKey.Namespace, Blessings.NameKey.Key), wording.Name);
        Assert.Equal(StringTables.Find(Blessings.TierLevelKey.Namespace, Blessings.TierLevelKey.Key), wording.TierLevel);
        Assert.Equal("Glyph", wording.Name);
        Assert.Equal(4, wording.Tiers);
    }
}
