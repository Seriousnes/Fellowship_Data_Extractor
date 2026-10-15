using System.Text.Json.Nodes;

namespace Fellowship.Codex.Tests;

public class HistoryServiceTests
{
    private static readonly SettingsFile Bare = new() { GemTypes = ["Ruby"] };

    private static ExportHistoryService Reading(params (string Build, string[] Lines)[] older) =>
        new(new ExportHistory(
            "1002",
            new ExportSnapshot(Bare, []),
            new ExportFiles(ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty),
            older.ToDictionary(
                version => version.Build,
                version => new ExportVersion(
                    version.Build, ExportReader.Group(version.Lines), "{}"),
                StringComparer.Ordinal)));

    [Fact]
    public void Every_build_reads_newest_first()
    {
        var history = Reading(("999", []), ("1001", []), ("1000", []));

        Assert.Equal(["1002", "1001", "1000", "999"], history.Builds);
    }

    [Fact]
    public void The_newest_entry_is_the_document_on_screen()
    {
        var history = Reading();

        var reading = Assert.Single(history.Of(new ItemDocument { Id = 5224, Slot = "Back" }));

        Assert.Equal("1002", reading.Build);
        Assert.Equal("item", reading.Value!["$type"]!.GetValue<string>());
        Assert.Equal("Back", reading.Value!["slot"]!.GetValue<string>());
    }

    [Fact]
    public void A_version_holding_one_line_under_the_key_reads_that_line()
    {
        var history = Reading(("1001", ["""{"$type":"item","id":5224,"slot":"Back"}"""]));

        var older = history.Of(new ItemDocument { Id = 5224, Slot = "Chest" })[1];

        Assert.Equal("1001", older.Build);
        Assert.Equal("Back", older.Value!["slot"]!.GetValue<string>());
    }

    [Fact]
    public void A_version_holding_no_line_under_the_key_reads_nothing()
    {
        var history = Reading(("1001", ["""{"$type":"item","id":5225,"slot":"Back"}"""]));

        Assert.Null(history.Of(new ItemDocument { Id = 5224 })[1].Value);
    }

    [Fact]
    public void A_version_holding_several_lines_under_the_key_reads_the_one_the_wearer_names()
    {
        var history = Reading(("1001",
        [
            """{"$type":"item","id":5224,"slot":"Back","armorType":"Leather","primaryStat":"Agility"}""",
            """{"$type":"item","id":5224,"slot":"Head","armorType":"Cloth","primaryStat":"Intellect"}""",
        ]));

        var reading = history.Of(new ItemDocument
        {
            Id = 5224,
            ArmorType = ArmorType.Cloth,
            PrimaryStat = PrimaryStat.Intellect,
        });

        Assert.Equal("Head", reading[1].Value!["slot"]!.GetValue<string>());
    }

    [Fact]
    public void A_version_whose_lines_all_read_for_another_wearer_reads_nothing()
    {
        var history = Reading(("1001",
        [
            """{"$type":"item","id":5224,"armorType":"Leather"}""",
            """{"$type":"item","id":5224,"armorType":"Plate"}""",
        ]));

        Assert.Null(history.Of(new ItemDocument { Id = 5224, ArmorType = ArmorType.Cloth })[1].Value);
    }

    [Fact]
    public void The_heroes_a_line_lists_take_part_in_the_match()
    {
        var history = Reading(("1001",
        [
            """{"$type":"item","id":5224,"slot":"Back","heroes":["Aeona"]}""",
            """{"$type":"item","id":5224,"slot":"Head","heroes":["Aeona","Mara"]}""",
        ]));

        var reading = history.Of(new ItemDocument { Id = 5224, Heroes = [Hero.Aeona, Hero.Mara] });

        Assert.Equal("Head", reading[1].Value!["slot"]!.GetValue<string>());
    }

    [Fact]
    public void Each_older_entry_answers_under_the_build_that_holds_it()
    {
        var history = Reading(
            ("1001", ["""{"$type":"item","id":5224,"slot":"Chest"}"""]),
            ("1000", ["""{"$type":"item","id":5224,"slot":"Back"}"""]));

        var readings = history.Of(new ItemDocument { Id = 5224, Slot = "Legs" });

        Assert.Equal(["1002", "1001", "1000"], readings.Select(reading => reading.Build));
        Assert.Equal(
            ["Legs", "Chest", "Back"],
            readings.Select(reading => (reading.Value as JsonObject)?["slot"]?.GetValue<string>()));
    }
}
