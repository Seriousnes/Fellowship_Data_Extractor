namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class TuningLabelTests(Export export)
{
    [Fact]
    public void Every_talent_and_legendary_power_constant_names_what_it_tunes()
    {
        var unnamed = export.Store.Entities
            .Select(document => (Document: document, Constants: document switch
            {
                TalentDocument talent => talent.Constants,
                LegendaryPowerDocument legendary => legendary.Constants,
                _ => null,
            }))
            .SelectMany(entry => (entry.Constants?.Keys ?? [])
                .Where(row => entry.Document.Tuning?.ContainsKey(row) != true)
                .Select(row => $"{EntityTypes.TypeOf(entry.Document)} {entry.Document.Id}: {row}"))
            .ToArray();

        Assert.True(unnamed.Length == 0, string.Join('\n', unnamed));
    }

    [Fact]
    public void The_Motherload_reads_its_scaler_under_Chain_Lightning()
    {
        var talent = Assert.Single(export.Store.Entities.OfType<TalentDocument>(), talent => talent.Name == "The Motherload");
        var rows = talent.Constants!.ToDictionary(row => row.Key, row => (object?)row.Value);

        var row = Assert.Single(export.View.Tuning(talent, rows), row => row.Value == "0.5");

        Assert.Equal("Chain Lightning / Damage Per Unique Target Scaler", row.Label);
    }

    [Fact]
    public void Chain_Lightning_names_The_Motherload_for_its_damage_multiplier()
    {
        var effect = Assert.Single(export.Store.Entities.OfType<EffectDocument>(), effect => effect.Id == 1774);
        var rows = effect.Talents!.SelectMany(group => group.Value.Select(leaf =>
            KeyValuePair.Create($"{group.Key}.{leaf.Key}", leaf.Value))).ToDictionary();

        var row = Assert.Single(export.View.Tuning(effect, rows));

        Assert.Equal("The Motherload / Damage Multiplier", row.Label);
    }
}
