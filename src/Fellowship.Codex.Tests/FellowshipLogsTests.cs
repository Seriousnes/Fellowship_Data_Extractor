using System.Text.Json;

namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public sealed class FellowshipLogsTests(Export export)
{
    [Fact]
    public void A_gear_entry_the_codex_writes_reads_back_as_the_same_modifiers_and_attunements()
    {
        var store = export.Store;
        var settings = store.Settings;

        var weapon = store.Entities
            .OfType<ItemDocument>()
            .First(item => item.Slot == ItemPower.Printed(settings.Items, ItemPower.WeaponType) && item.TraitTree is not null);

        var tree = TraitTrees.Find(weapon.TraitTree, settings)!;
        var attuned = tree.Nodes.SelectMany(node => TraitTrees.Offered(node, store)).First();

        var enchant = store.Entities.OfType<TraitDocument>().First(Enchants.Is);
        var glyph = store.Entities.OfType<BlessingDocument>().First(blessing => blessing.AbilityRank is not null);
        var gem = store.Entities.OfType<GemTypeDocument>().First();

        var query = new TooltipQuery
        {
            ItemLevel = 300,
            Modifiers =
            [
                new ItemModifier(ModifierKind.Essence, gem.Id, 40),
                new ItemModifier(ModifierKind.Trait, enchant.Id, 1),
                new ItemModifier(ModifierKind.Blessing, glyph.Id, 1),
            ],
            ImbuedTraits = [new ImbuedTrait(attuned.Id, 1)],
        };

        using var entry = JsonDocument.Parse(FellowshipLogs.Write(weapon, query, store).ToJsonString());

        var read = FellowshipLogs.Read(entry.RootElement, store, out var id, out var unresolved);

        Assert.Empty(unresolved);
        Assert.Equal(weapon.Id, id);
        Assert.Equal(query.Modifiers.OrderBy(modifier => modifier.Kind), read.Modifiers.OrderBy(modifier => modifier.Kind));
        Assert.Equal(query.ImbuedTraits, read.ImbuedTraits);
    }
}
