using Fellowship.SDK.Client;

namespace Fellowship.Codex.Tests;

public class CodexAddressTests
{
    [Fact]
    public void A_tooltip_is_addressed_by_its_type_and_id_alone()
    {
        Assert.Equal("api/item/5218/tooltip", CodexAddresses.Tooltip(EntityType.Item, 5218));
    }

    [Fact]
    public void A_modifier_and_a_augmentation_are_written_as_the_tooltip_reads_them_back()
    {
        Assert.True(ItemModifiers.TryParse("abilityrank:12:3", out var modifiers));
        Assert.Equal("abilityrank:12:3", modifiers.Values.Single().ToString());

        Assert.True(Attunements.TryParse("456:2", out var augmentations));
        Assert.Equal("456:2", augmentations.Values.Single().ToString());
    }

    [Fact]
    public void The_export_routes_name_the_files_the_extractor_writes()
    {
        Assert.Equal($"{CodexAddresses.Export}/{ExportReader.EntitiesName}", CodexAddresses.ExportEntities);
        Assert.Equal($"{CodexAddresses.Export}/{ExportReader.SettingsName}", CodexAddresses.ExportSettings);
    }

    [Fact]
    public void A_page_and_a_record_address_the_same_entity()
    {
        Assert.Equal("item/5218?hero=Gunde", CodexAddresses.Page(EntityType.Item, 5218, "Gunde"));
        Assert.Equal("api/item/5218?hero=Gunde", CodexAddresses.Record(EntityType.Item, 5218, "Gunde"));
        Assert.Equal("ability/1964", CodexAddresses.Page(EntityType.Ability, 1964));
    }

    [Fact]
    public void A_record_reading_is_written_as_query_parameters()
    {
        Assert.Equal(
            "api/item/5218?hero=Gunde&itemLevel=300&upgrades=2&rarity=Epic",
            CodexAddresses.Record(
                EntityType.Item, 5218, new RecordReading { Hero = "Gunde", ItemLevel = 300, Upgrades = 2, Rarity = "Epic" }));

        Assert.Equal(
            "api/enchant/67?rankSources=Head%2CChest",
            CodexAddresses.Record(EntityType.Enchant, 67, new RecordReading { RankSources = ["Head", "Chest"] }));
    }

    [Fact]
    public void A_record_list_is_addressed_by_its_type_with_its_filter()
    {
        Assert.Equal("api/item", CodexAddresses.Records(EntityType.Item));

        Assert.Equal(
            "api/creature?sort=health&page=2&difficulty=30&rank=Boss",
            CodexAddresses.Records(EntityType.Creature, new RecordFilter
            {
                Sort = "health",
                Page = 2,
                Reading = new RecordReading { Difficulty = 30 },
                Fields = new Dictionary<string, string> { ["rank"] = "Boss" },
            }));
    }
}
