namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public sealed class ArmorTypeTests(Export export)
{
    private const string WeaponSlot = "Weapon";

    private IEnumerable<ItemDocument> Items(string slot) =>
        export.Store.Entities.OfType<ItemDocument>().Where(item => item.Slot == slot);

    private static bool Tank(ItemDocument weapon) =>
        weapon.Stats?.Any(stat => stat.Stat.Equals("Stamina", StringComparison.OrdinalIgnoreCase)) is true &&
        weapon.RuleSet is not null;

    [Theory]
    [InlineData("Neck")]
    [InlineData("Ring")]
    public void An_unarmored_slot_has_no_armor_type(string slot)
    {
        Assert.NotEmpty(Items(slot));
        Assert.All(Items(slot), item => Assert.Null(item.ArmorType));
    }

    [Fact]
    public void A_weapon_without_a_stamina_slot_has_no_armor_line()
    {
        var weapons = Items(WeaponSlot).Where(weapon => !Tank(weapon)).ToArray();

        Assert.NotEmpty(weapons);
        Assert.All(weapons, weapon =>
            Assert.Null(StatMagnitudes.Armor(weapon, export.Store.Settings.StatCalculation, 300)));
    }

    [Fact]
    public void A_tank_weapon_wears_its_heros_armor()
    {
        var weapons = Items(WeaponSlot).Where(Tank).ToArray();

        Assert.NotEmpty(weapons);
        Assert.All(weapons, weapon =>
            Assert.Equal(export.Store.Hero(weapon.Hero!.Value.ToString())!.ArmorType, weapon.ArmorType?.ToString()));
    }
}
