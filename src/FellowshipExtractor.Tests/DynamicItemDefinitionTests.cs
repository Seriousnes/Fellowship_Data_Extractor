using CUE4Parse.UE4.Assets.Exports;

using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class DynamicItemDefinitionTests
{
    private const string ItemIdField = "ItemID";
    private const string TypeField = "HeroItemType";

    private static IReadOnlyList<UObject> Definitions() =>
    [
        .. AssetRegistry.ReadAssets(Export.Provider)
            .Where(asset => asset.AssetClass.Text == DynamicItemRuleSets.DefinitionClass)
            .Select(asset => Export.Provider.LoadPackageObject<UObject>(AbilityPresentationIndex.PackagePath(asset.ObjectPath))),
    ];

    private static IReadOnlyDictionary<string, int> ItemIds() =>
        CsvTables.Current([.. CsvTables.ReadTags(Export.Provider)])
            .Where(tag => tag.Table == "ItemID")
            .ToDictionary(tag => tag.Name, tag => tag.NetId, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void ADefinitionDisabledInGameIsNoItemDocument()
    {
        var ids = ItemIds();
        var items = Export.Loaded.Documents.Where(row => row.Type == EntityType.Item).Select(row => row.Id).ToHashSet();

        var disabled = Definitions()
            .Where(definition => PropertyWalk.Get(definition, DynamicItemRuleSets.EnabledField)?.GenericValue is false)
            .ToArray();

        Assert.NotEmpty(disabled);
        Assert.All(disabled, definition =>
            Assert.DoesNotContain(ids[PropertyWalk.GetString(definition, ItemIdField)!], items));
    }

    [Fact]
    public void AnItemStatesTheSlotItsDefinitionsItemTypeNames()
    {
        var ids = ItemIds();
        var slotNames = Export.Loaded.Settings.Items.SlotNames;
        var mappings = Export.Provider.MappingsContainer!.MappingsForGame!;
        var members = EnumOrdinals.Members(mappings, "ECRHeroItemType");

        var slotById = Export.Loaded.Documents
            .Where(row => row.Type == EntityType.Item)
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.First().Body["slot"]?.GetValue<string>());

        foreach (var definition in Definitions())
        {
            if (PropertyWalk.Get(definition, DynamicItemRuleSets.EnabledField)?.GenericValue is false) continue;

            var ordinal = PropertyWalk.AsOrdinal(PropertyWalk.Get(definition, TypeField)?.GenericValue) ?? 0;
            var id = ids[PropertyWalk.GetString(definition, ItemIdField)!];

            Assert.Equal(slotNames[members[ordinal]], slotById[id]);
        }
    }

    [Fact]
    public void ARelicPassiveItemSitsInTheRelicSlot()
    {
        var relics = Export.Loaded.Documents
            .Where(row => row.Type == EntityType.Item && row.Name?.StartsWith("Orb of", StringComparison.Ordinal) is true)
            .ToArray();

        Assert.NotEmpty(relics);
        Assert.All(relics, row => Assert.Equal("Relic", row.Body["slot"]?.GetValue<string>()));
    }
}
