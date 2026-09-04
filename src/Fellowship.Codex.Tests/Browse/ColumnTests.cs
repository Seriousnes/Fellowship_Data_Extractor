using Fellowship.Codex.Components.Browse;

namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class ColumnTests : BunitContext
{
    private readonly IEntityStoreService Store;

    private readonly BrowseView Viewing;

    public ColumnTests(Export export)
    {
        Store = export.Store;
        Services.AddSingleton(export.View);

        Viewing = new BrowseView(export.View.Codex, new CodexQuery(), Nothing);
    }

    private static CodexResults Nothing =>
        new(0, 1, 1, [], [], new Dictionary<EntityType, int>(), EntityType.Item, [], []);

    private IReadOnlyList<EntityDocument> Some(EntityType type) =>
        [.. Store.Entities.Where(document => EntityTypes.TypeOf(document) == type).Take(5)];

    private string[] Dispatched(EntityType type) =>
        [.. Render<Rows>(p => p
                .Add(x => x.Type, type)
                .Add(x => x.Documents, Some(type))
                .Add(x => x.View, Viewing))
            .FindAll("thead th")
            .Select(th => th.TextContent.Trim())];

    [Fact]
    public void The_items_list_heads_the_columns_the_plan_names_in_order()
    {
        Assert.Equal(
            ["", "Name", "ILvl", "Slot", "Source", "Type", "Set"],
            Dispatched(EntityType.Item));
    }

    [Fact]
    public void The_abilities_list_heads_its_source_and_school_columns()
    {
        Assert.Equal(["", "Name", "Source", "Type"], Dispatched(EntityType.Ability));
    }

    [Fact]
    public void The_effects_list_heads_the_applier_and_school_columns()
    {
        Assert.Equal(["", "Name", "Applied by", "Type"], Dispatched(EntityType.Effect));
    }

    [Fact]
    public void An_effects_row_names_the_ability_that_applies_it()
    {
        var effect = Store.Entities.OfType<EffectDocument>().First(row => row.AppliedBy is not null);

        var applier = Store.All(effect.AppliedBy!.Type, effect.AppliedBy.Id);

        var cells = Render<Effects>(p => p
                .Add(x => x.Rows, (IReadOnlyList<EntityDocument>)[effect])
                .Add(x => x.View, Viewing))
            .FindAll("tbody tr:first-child td")
            .Select(td => td.TextContent.Trim())
            .ToArray();

        Assert.Equal(PublicTypes.NameOf(applier[0]), cells[2]);
    }

    [Fact]
    public void An_effect_draws_the_schools_it_borrows_from_its_applier()
    {
        var effect = Store.Entities.OfType<EffectDocument>().First(row => row.Schools is { Count: > 0 });

        var chips = Render<Effects>(p => p
                .Add(x => x.Rows, (IReadOnlyList<EntityDocument>)[effect])
                .Add(x => x.View, Viewing))
            .FindAll("tbody tr:first-child td")[3]
            .QuerySelectorAll(".cx-chip")
            .Select(chip => chip.TextContent.Trim());

        Assert.Equal(effect.Schools!, chips);
    }

    [Fact]
    public void The_talents_list_heads_the_hero_cost_and_tier_its_slot_states()
    {
        Assert.Equal(["", "Name", "Hero", "Cost", "Tier"], Dispatched(EntityType.Talent));
    }

    [Fact]
    public void The_gem_traits_list_heads_the_power_it_needs_and_the_gem_it_belongs_to()
    {
        Assert.Equal(["", "Name", "Power Required", "Type"], Dispatched(EntityType.GemTrait));
    }

    [Fact]
    public void The_set_bonuses_list_heads_the_pieces_attribute_and_role_that_separate_its_rows()
    {
        Assert.Equal(["", "Name", "Required", "Attribute", "Role"], Dispatched(EntityType.SetBonus));
    }

    [Fact]
    public void The_creatures_list_heads_the_rank_health_and_location_columns() =>
        Assert.Equal(["", "Name", "Rank", "Health", "Location"], Dispatched(EntityType.Creature));

    [Fact]
    public void The_dungeons_list_heads_the_qualifier_score_and_creature_count_columns() =>
        Assert.Equal(
            ["", "Name", "Qualifier", "Required score", "Creatures"], Dispatched(EntityType.Dungeon));

    [Fact]
    public void Every_listed_type_draws_a_table_that_opens_with_its_art_and_name()
    {
        foreach (var type in PublicTypes.All.Where(type => type.Listed))
        {
            var heads = Dispatched(type.Type);

            Assert.True(heads.Length >= 2, $"{type.Label} draws {heads.Length} columns.");
            Assert.Equal(string.Empty, heads[0]);
            Assert.Equal("Name", heads[1]);
        }
    }

    [Fact]
    public void Every_listed_type_draws_one_row_per_document_it_is_given()
    {
        foreach (var type in PublicTypes.All.Where(type => type.Listed))
        {
            var given = Some(type.Type);

            var drawn = Render<Rows>(p => p
                    .Add(x => x.Type, type.Type)
                    .Add(x => x.Documents, given)
                    .Add(x => x.View, Viewing))
                .FindAll("tbody tr");

            Assert.Equal(given.Count, drawn.Count);
        }
    }

    [Fact]
    public void An_unlisted_type_names_itself_rather_than_drawing_an_empty_table()
    {
        var thrown = Assert.Throws<InvalidOperationException>(() => Dispatched(EntityType.TalentSlot));

        Assert.Contains(PublicTypes.Label(EntityType.TalentSlot), thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_items_row_reads_its_level_source_and_slot_off_the_document()
    {
        var item = Store.Entities.OfType<ItemDocument>()
            .First(row => row is { ItemLevel: not null, Sources.Count: > 0, Slot: not null });

        var cells = Render<Items>(p => p
                .Add(x => x.Rows, (IReadOnlyList<EntityDocument>)[item])
                .Add(x => x.View, Viewing))
            .FindAll("tbody tr:first-child td")
            .Select(td => td.TextContent.Trim())
            .ToArray();

        Assert.Equal(item.ItemLevel!.Value.ToString(), cells[2]);
        Assert.Equal(item.Slot, cells[3]);
        Assert.Contains(item.Sources![0].Name, cells[4], StringComparison.Ordinal);
    }

    [Fact]
    public void A_weapon_reads_its_hero_where_an_armour_slot_reads_its_armour_class()
    {
        var weapon = Store.Entities.OfType<ItemDocument>()
            .First(row => row.Slot == "Weapon" && row.Heroes is { Count: > 0 });

        var armour = Store.Entities.OfType<ItemDocument>()
            .First(row => row.Slot == "Chest" && row.ArmorType is not null);

        Assert.Contains(weapon.Heroes![0].ToString(), Kind(weapon), StringComparison.Ordinal);
        Assert.Equal(armour.ArmorType!.Value.ToString(), Kind(armour));
    }

    [Fact]
    public void A_slot_the_build_gives_no_armour_weight_reads_its_own_word()
    {
        var ring = Store.Entities.OfType<ItemDocument>().First(row => row.Slot == "Ring");

        Assert.Equal("Ring", Kind(ring));
    }

    private string Kind(ItemDocument item) =>
        Render<Items>(p => p
                .Add(x => x.Rows, (IReadOnlyList<EntityDocument>)[item])
                .Add(x => x.View, Viewing))
            .FindAll("tbody tr:first-child td")[5]
            .TextContent
            .Trim();
}
