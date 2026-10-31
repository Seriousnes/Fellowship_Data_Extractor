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
        Assert.Null(talent.Tuning!["Ink.BouncyProjectile.Talent.DamagePerUniqueTarget.Scaler"].Op);
    }

    [Fact]
    public void The_Motherload_lists_no_row_its_modifier_multiplies_by_one()
    {
        var talent = Assert.Single(export.Store.Entities.OfType<TalentDocument>(), talent => talent.Name == "The Motherload");
        var rows = talent.Constants!.ToDictionary(row => row.Key, row => (object?)row.Value);

        Assert.Equal(["0.5"], export.View.Tuning(talent, rows).Select(row => row.Value));
        Assert.DoesNotContain(talent.Changes ?? [], change => change.Value == 1);
    }

    [Fact]
    public void Chain_Lightning_lists_no_row_The_Motherload_multiplies_by_one()
    {
        var effect = Assert.Single(export.Store.Entities.OfType<EffectDocument>(), effect => effect.Id == 1774);
        var rows = effect.Talents!.SelectMany(group => group.Value.Select(leaf =>
            KeyValuePair.Create($"{group.Key}.{leaf.Key}", leaf.Value))).ToDictionary();

        Assert.Equal("MultiplyCompound", effect.Tuning!["IncreasedDamage.DamageMultiplier"].Op);
        Assert.Empty(export.View.Tuning(effect, rows));
    }

    [Fact]
    public void Crack_the_Sky_names_the_attribute_and_op_its_row_applies()
    {
        var talent = Assert.Single(export.Store.Entities.OfType<TalentDocument>(), talent => talent.Name == "Crack the Sky");
        var rows = talent.Constants!.ToDictionary(row => row.Key, row => (object?)row.Value);

        var row = Assert.Single(export.View.Tuning(talent, rows));

        Assert.Equal(("Crit Chance", "20% (additive)"), (row.Label, row.Value));
    }

    [Fact]
    public void Hammer_Storm_reads_its_strength_rows_as_coefficients()
    {
        var ability = Assert.Single(export.Store.Entities.OfType<AbilityDocument>(), ability => ability.Id == 1242);

        Assert.All(
            ability.Tuning!.Where(entry => entry.Value.Name.EndsWith("StrengthCoefficient", StringComparison.Ordinal)),
            entry => Assert.Null(entry.Value.Op));
    }

    [Theory]
    [InlineData("MultiplyCompound", 1.2, false, "20% (multiplicative)")]
    [InlineData("Multiplicitive", 0.8, false, "-20% (multiplicative)")]
    [InlineData("Multiplicitive", 0.2, false, "-80% (multiplicative)")]
    [InlineData("Division", 0.8, false, "25% (multiplicative)")]
    [InlineData("Additive", 0.2, true, "20% (additive)")]
    [InlineData("Additive", 0.07, true, "7% (additive)")]
    [InlineData("Additive", 15, false, "15 (additive)")]
    [InlineData("Subtract", 0.5, false, "-0.5 (additive)")]
    public void A_modifier_value_reads_as_its_effect_on_the_attribute(string op, double value, bool percent, string expected) =>
        Assert.Equal(expected, CodexView.Modifier(op, value, percent));

    [Fact]
    public void Every_tuning_label_on_a_page_names_one_row()
    {
        var shared = export.Store.Entities
            .Where(document => document.Tuning is not null)
            .SelectMany(document =>
            {
                var references = document.Tuning!.GroupBy(entry => entry.Value.Name).ToArray();
                var rows = references
                    .SelectMany((group, at) => group.Select(entry => KeyValuePair.Create(entry.Key, (object?)(at + 2.0))))
                    .ToDictionary();
                return export.View.Tuning(document, rows)
                    .GroupBy(row => row.Label)
                    .Where(group => group.Count() > 1)
                    .Select(group => $"{EntityTypes.TypeOf(document)} {document.Id}: {group.Key}");
            })
            .ToArray();

        Assert.True(shared.Length == 0, string.Join('\n', shared));
    }
}
