using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;

namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class EffectFactTests(Export export)
{
    private const int CooldownRecoveryBonus = 106;
    private const int InvictusSelfBuff = 2719;

    private EffectDocument Effect(int id) => Assert.IsType<EffectDocument>(export.Store.All(EntityType.Effect, id)[0]);

    [Fact]
    public void An_attribute_based_duration_prints_its_share_at_each_rank()
    {
        var duration = Assert.Single(export.View.Facts(Effect(CooldownRecoveryBonus)), fact => fact.Label == "Duration");

        Assert.Equal("10% / 12% / 14% / 16% of WeaponAbilityCooldown", duration.Value);
    }

    [Fact]
    public void A_custom_calculation_prints_its_formula_on_the_modifier_line()
    {
        var modifier = Assert.Single(Effect(InvictusSelfBuff).Modifiers!);

        Assert.Equal("0.33334 × (Haste - 1) / Stacks", export.View.ModifierFormula(modifier));
    }
}
