namespace Fellowship.Codex.Tests;

public class PatchNoteDiffTests
{
    private static IReadOnlyList<PatchNote> Between(string[] older, string[] newer) =>
        PatchNoteDiff.Between(ExportText.Group(older), ExportText.Group(newer));

    private static string Reads(PatchNoteField field) =>
        $"{field.Label}: {field.Older ?? "(none)"} -> {field.Newer ?? "(none)"}";

    private static string[] Fields(string older, string newer) =>
        [.. Assert.Single(Between([older], [newer])).Fields.Select(Reads)];

    [Fact]
    public void A_value_changed_from_one_value_to_another_is_noted()
    {
        Assert.Equal(
            ["Cooldown: 10 -> 12"],
            Fields("""{"$type":"ability","id":1,"cooldown":10}""", """{"$type":"ability","id":1,"cooldown":12}"""));
    }

    [Fact]
    public void A_value_the_older_build_has_as_null_is_not_noted()
    {
        Assert.Empty(Between(
            ["""{"$type":"ability","id":1,"cooldown":null}"""],
            ["""{"$type":"ability","id":1,"cooldown":12}"""]));
    }

    [Fact]
    public void A_property_the_older_build_lacks_is_not_noted()
    {
        Assert.Empty(Between(
            ["""{"$type":"ability","id":1}"""],
            ["""{"$type":"ability","id":1,"cooldown":12}"""]));
    }

    [Fact]
    public void A_value_gone_from_one_document_of_a_type_others_still_have_is_noted()
    {
        var notes = Between(
            ["""{"$type":"ability","id":1,"cooldown":10}"""],
            ["""{"$type":"ability","id":1}""", """{"$type":"ability","id":2,"cooldown":5}"""]);

        var changed = Assert.Single(notes, note => !note.Added);

        Assert.Equal(["Cooldown: 10 -> (none)"], changed.Fields.Select(Reads));
    }

    [Fact]
    public void A_property_no_document_of_the_type_has_in_the_newer_build_is_not_noted()
    {
        Assert.Empty(Between(
            ["""{"$type":"ability","id":1,"scalesWithHaste":true}"""],
            ["""{"$type":"ability","id":1}"""]));
    }

    [Fact]
    public void A_property_whose_value_changed_kind_is_not_noted()
    {
        Assert.Empty(Between(
            ["""{"$type":"ability","id":1,"cost":10}"""],
            ["""{"$type":"ability","id":1,"cost":{"amount":10}}"""]));
    }

    [Fact]
    public void An_entity_the_older_build_lacks_is_new()
    {
        var note = Assert.Single(Between([], ["""{"$type":"talent","id":14,"name":"Martial Command","icon":"T_Icon"}"""]));

        Assert.True(note.Added);
        Assert.Equal("Martial Command", note.Name);
        Assert.Equal("T_Icon", note.Icon);
        Assert.Empty(note.Fields);
    }

    [Fact]
    public void An_entity_the_newer_build_lacks_is_not_noted()
    {
        Assert.Empty(Between(["""{"$type":"talent","id":14}"""], []));
    }

    [Fact]
    public void An_unchanged_entity_is_not_noted()
    {
        Assert.Empty(Between(["""{"$type":"talent","id":14,"name":"A"}"""], ["""{"$type":"talent","id":14,"name":"A"}"""]));
    }

    [Fact]
    public void Each_variant_reads_against_the_older_variant_for_the_same_wearer()
    {
        var notes = Between(
            [
                """{"$type":"item","id":5,"armorType":"Cloth","slot":"Head"}""",
                """{"$type":"item","id":5,"armorType":"Leather","slot":"Head"}""",
            ],
            [
                """{"$type":"item","id":5,"armorType":"Cloth","slot":"Head"}""",
                """{"$type":"item","id":5,"armorType":"Leather","slot":"Back"}""",
                """{"$type":"item","id":5,"armorType":"Plate","slot":"Head"}""",
            ]);

        Assert.Equal(["Slot: Head -> Back"], Assert.Single(notes, note => !note.Added).Fields.Select(Reads));
        Assert.Single(notes, note => note.Added);
    }

    [Fact]
    public void Variants_noting_the_same_change_read_once()
    {
        var notes = Between(
            [
                """{"$type":"item","id":5,"armorType":"Cloth","level":1}""",
                """{"$type":"item","id":5,"armorType":"Leather","level":1}""",
            ],
            [
                """{"$type":"item","id":5,"armorType":"Cloth","level":2}""",
                """{"$type":"item","id":5,"armorType":"Leather","level":2}""",
            ]);

        Assert.Equal(["Level: 1 -> 2"], Assert.Single(notes).Fields.Select(Reads));
    }

    [Fact]
    public void An_element_inserted_ahead_of_others_reads_as_that_element_alone()
    {
        Assert.Equal(
            ["Changes › Field: (none) -> Z", "Changes › Value: (none) -> 9"],
            Fields(
                """{"$type":"talent","id":1,"changes":[{"field":"A","value":1},{"field":"B","value":2}]}""",
                """{"$type":"talent","id":1,"changes":[{"field":"Z","value":9},{"field":"A","value":1},{"field":"B","value":2}]}"""));
    }

    [Fact]
    public void A_reference_added_to_a_list_reads_as_added()
    {
        Assert.Equal(
            ["Grants: (none) -> Martial Command"],
            Fields(
                """{"$type":"talent","id":1,"grants":[{"type":"effect","id":1,"name":"A"}]}""",
                """{"$type":"talent","id":1,"grants":[{"type":"effect","id":1,"name":"A"},{"type":"effect","id":2,"name":"Martial Command"}]}"""));
    }

    [Fact]
    public void A_reference_list_pairs_its_elements_by_type_and_id()
    {
        Assert.Equal(
            ["Changed By: Shield Throw -> (none)", "Changed By › B › Name: B -> C", "Changed By: (none) -> New"],
            Fields(
                """{"$type":"effect","id":1,"changedBy":[{"type":"ability","id":985,"name":"Shield Throw"},{"type":"talent","id":2,"name":"B"}]}""",
                """{"$type":"effect","id":1,"changedBy":[{"type":"ability","id":7,"name":"New"},{"type":"talent","id":2,"name":"C"}]}"""));
    }

    [Fact]
    public void A_list_of_values_reads_as_one_field()
    {
        Assert.Equal(
            ["Scales Off: Agility -> Agility, Haste"],
            Fields(
                """{"$type":"ability","id":1,"scalesOff":["Agility"]}""",
                """{"$type":"ability","id":1,"scalesOff":["Agility","Haste"]}"""));
    }

    [Fact]
    public void A_list_of_values_gone_from_one_document_reads_as_its_values()
    {
        var notes = Between(
            ["""{"$type":"ability","id":1,"scalesOff":["Agility","Haste"]}"""],
            ["""{"$type":"ability","id":1}""", """{"$type":"ability","id":2,"scalesOff":["Haste"]}"""]);

        Assert.Equal(["Scales Off: Agility, Haste -> (none)"], Assert.Single(notes, note => !note.Added).Fields.Select(Reads));
    }

    [Fact]
    public void A_named_element_gone_from_a_list_reads_as_its_name()
    {
        Assert.Equal(
            ["Sources 2: A Champion's Fit -> (none)"],
            Fields(
                """{"$type":"item","id":1,"sources":[{"kind":"Drop","name":"A"},{"kind":"Challenge","name":"A Champion's Fit"}]}""",
                """{"$type":"item","id":1,"sources":[{"kind":"Drop","name":"A"}]}"""));
    }

    [Fact]
    public void An_unnamed_element_gone_from_a_list_reads_as_one_row_per_member()
    {
        Assert.Equal(
            ["Scaling 1 › Curve: Health -> (none)", "Scaling 1 › Steps: 1, 2 -> (none)"],
            Fields(
                """{"$type":"creature","id":1,"scaling":[{"curve":"Health","steps":[1,2]},{"curve":"Armor"}]}""",
                """{"$type":"creature","id":1,"scaling":[{"curve":"Armor"}]}"""));
    }

    [Fact]
    public void An_unnamed_reference_reads_as_nothing()
    {
        Assert.Empty(Between(
            ["""{"$type":"effect","id":1,"changedBy":[{"type":"ability","id":985}]}"""],
            ["""{"$type":"effect","id":1,"changedBy":[{"type":"ability","id":7}]}"""]));
    }

    [Fact]
    public void A_value_added_to_an_empty_list_reads_as_added()
    {
        Assert.Equal(
            ["Scales Off: (none) -> Haste"],
            Fields(
                """{"$type":"ability","id":1,"scalesOff":[]}""",
                """{"$type":"ability","id":1,"scalesOff":["Haste"]}"""));
    }

    [Fact]
    public void A_changed_field_source_is_not_noted()
    {
        Assert.Equal(
            ["Cast Time: 2 -> 3"],
            Fields(
                """{"$type":"ability","id":1,"castTime":2,"fieldSources":{"castTime":{"kind":"ConstantsRow","rows":["A.CastTime"],"tuning":["CastTime"]}}}""",
                """{"$type":"ability","id":1,"castTime":3,"fieldSources":{"castTime":{"kind":"RowProduct","rows":["B.CastTime","B.Scale"]}}}"""));
    }

    [Fact]
    public void A_field_source_gone_from_the_newer_build_is_not_noted()
    {
        Assert.Empty(Between(
            [
                """{"$type":"ability","id":1,"castTime":2,"fieldSources":{"castTime":{"kind":"ConstantsRow","rows":["A.CastTime"]}}}""",
                """{"$type":"ability","id":2,"fieldSources":{"range":{"kind":"ConstantsRow","rows":["A.Range"]}}}""",
            ],
            [
                """{"$type":"ability","id":1,"castTime":2}""",
                """{"$type":"ability","id":2,"fieldSources":{"range":{"kind":"ConstantsRow","rows":["A.Range"]}}}""",
            ]));
    }

    [Fact]
    public void A_nested_value_reads_under_its_parents()
    {
        Assert.Equal(
            ["Constants › Warmaster.Talent.AddedSpiritPassive: 0.05 -> 0.1"],
            Fields(
                """{"$type":"talent","id":1,"constants":{"Warmaster.Talent.AddedSpiritPassive":0.05}}""",
                """{"$type":"talent","id":1,"constants":{"Warmaster.Talent.AddedSpiritPassive":0.1}}"""));
    }
}
