using Fellowship.SDK.Models;
using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class ModifierIndexTests
{
    private const string GlacialBlast = "Rime.CastedProjectileDamageOrbSpender";
    private const string Granted = "Talents.Rime.CastedProjectileDamageOrbSpender.DamageIncreaseAndCastTimeIncrease";
    private const string AdditionalDamage = GlacialBlast + ".Talent.DamageIncreaseAndCastTimeIncrease.AdditionalDamage";
    private const string CastTime = GlacialBlast + ".Talent.DamageIncreaseAndCastTimeIncrease.CastTimeIncreaseDuration";
    private const string Coefficient = GlacialBlast + ".IntellectCoefficient";
    private const string Description = "Your Glacial Blast deals 40% more damage but its cast time is increased by 0.5 seconds.";

    private const string ExecutionSlot = "property:Executions[0].CalculationModifiers[3].ModifierMagnitude.ScalableFloatMagnitude";
    private const string CastingHook = "sink:return:ModifyCastingDuration|SelectFloat@1(0;_),Subtract_DoubleDouble@0(_),Add_DoubleDouble@0(_),SelectFloat@0(0;_)";

    private static Entity Ability(int id, string? tuning, params (string Row, double Value)[] rows) => new()
    {
        Type = EntityType.Ability,
        NetId = id,
        Name = $"GA_{id}",
        DisplayName = $"Ability {id}",
        Tuning = tuning,
        Rows = rows.ToDictionary(row => row.Row, row => row.Value),
        OwnRows = [.. rows.Select(row => row.Row)],
    };

    private static Entity Talent(int id, string description, params string[] tags) => new()
    {
        Type = EntityType.Talent,
        NetId = id,
        DisplayName = $"Talent {id}",
        Description = description,
        GrantedTags = tags,
    };

    private static (ModifierIndex Index, DocumentProjection.References References) Indexed(IReadOnlyList<Entity> entities)
    {
        var index = new ModifierIndex(entities);

        return (index, new DocumentProjection.References((_, _) => null, (_, id) => $"Entity {id}"));
    }

    private static IReadOnlyList<SourceChange> Records(IReadOnlyList<Entity> entities, Entity of)
    {
        var (index, references) = Indexed(entities);

        return index.Of(of, references) ?? [];
    }

    private static (Entity Blast, Entity Talent) GreaterGlacialBlast()
    {
        var blast = Ability(1028, GlacialBlast, (Coefficient, 10.24), (AdditionalDamage, 1.4), (CastTime, 0.5)) with
        {
            Coefficients = [new AbilityCoefficient("Intellect", 10.24)],
            GatedModifiers = new Dictionary<string, IReadOnlyList<EffectModifier>>
            {
                [Granted] = [new EffectModifier("DamageScale") { Op = "MultiplyCompound", Value = 1.4, Row = AdditionalDamage }],
            },
            RowUses = new Dictionary<string, IReadOnlyList<string>>
            {
                [AdditionalDamage] = [ExecutionSlot],
                [CastTime] = [CastingHook],
            },
        };

        var talent = Talent(190, Description, Granted) with
        {
            TagRows = new Dictionary<string, double> { [AdditionalDamage] = 1.4, [CastTime] = 0.5 },
        };

        return (blast, talent);
    }

    [Fact]
    public void ATalentRowTargetsTheAbilityReadingItWithItsType()
    {
        var (blast, talent) = GreaterGlacialBlast();

        var records = Records([blast, talent], talent);

        Assert.Equal(2, records.Count);
        Assert.All(records, record => Assert.Equal(1028, Assert.Single(record.Targets!).Id));
        Assert.Contains(records, record => record is { Type: ModifierType.DamageMultiplier, Value: 1.4, Attribute: null });
        Assert.Contains(records, record => record is { Type: ModifierType.CastTimeIncrease, Value: 0.5 });
        Assert.All(records, record => Assert.Null(record.Condition));
    }

    [Fact]
    public void AnAbilityStatesNoRecords()
    {
        var (blast, talent) = GreaterGlacialBlast();

        Assert.Empty(Records([blast, talent], blast));
    }

    [Fact]
    public void AnAbilityReadingTheOwnersTalentPropertyUnderTheTalentTagIsAlsoATarget()
    {
        var (blast, talent) = GreaterGlacialBlast();

        var swap = Ability(2223, "Rime.Talent.SpendersSwap.SingleTarget") with
        {
            Coefficients = [new AbilityCoefficient("Intellect", 4.096)],
            ForeignReads = [new ForeignRead(Granted, blast.Name!, "Talent DamageIncreaseAndCastTimeIncrease DamageMultiplier Ref")],
        };

        var records = Records([blast, swap, talent], talent);

        Assert.Equal(2, records.Count);
        Assert.All(records, record => Assert.Equal([1028, 2223], record.Targets!.Select(target => target.Id)));

        var (index, references) = Indexed([blast, swap, talent]);

        Assert.Contains(index.ModifiedBy(swap, references)!, modifier => modifier.Id == 190);
    }

    [Fact]
    public void AnAbilityThatReplacesAnotherIsATargetOfEveryTalentChangingIt()
    {
        var (blast, talent) = GreaterGlacialBlast();
        var swap = Ability(2223, "Rime.Talent.SpendersSwap.SingleTarget") with
        {
            Coefficients = [new AbilityCoefficient("Intellect", 4.096)],
        };

        var icy = Talent(658, "Your Glacial Blast is replaced with Talon Strike.", "Talents.Rime.SpenderSwap") with
        {
            Replacements = [$"{blast.Name}{AbilityReplacements.Separator}{swap.Name}"],
        };

        var (index, references) = Indexed([blast, swap, talent, icy]);

        Assert.All(index.Of(talent, references)!, record => Assert.Contains(record.Targets!, target => target.Id == 2223));
        Assert.Equal(1028, index.Replaces(swap, references)?.Id);
        Assert.Equal(658, Assert.Single(index.GrantedBy(swap, references)!).Id);
    }

    [Fact]
    public void ATalentRowItsDescriptionNeverPrintsIsLeftOut()
    {
        var (blast, talent) = GreaterGlacialBlast();

        var quiet = talent with { Description = "Your Glacial Blast is stronger." };

        Assert.Empty(Records([blast, quiet], quiet));
    }

    [Fact]
    public void ATalentRowAnEffectAppliesToItsHolderTargetsThatEffect()
    {
        const string granted = "Talents.Ink.ChargedBuff.RageIncrease";
        const string row = "Ink.ChargedBuff.Talent.RageIncrease.AddedHaste";

        var thunder = Ability(1235, "Ink.ChargedBuff");

        var buff = new Entity
        {
            Type = EntityType.Effect,
            NetId = 1745,
            Name = "GE_1745",
            Rows = new Dictionary<string, double> { [row] = 0.05 },
            OwnRows = [row],
            RowUses = new Dictionary<string, IReadOnlyList<string>>
            {
                [row] = ["attribute:Haste|Additive|holder", "property:Modifiers[0].ModifierMagnitude.ScalableFloatMagnitude"],
            },
        };

        var talent = Talent(87, "You gain +5% Haste.", granted) with
        {
            TagRows = new Dictionary<string, double> { [row] = 0.05 },
        };

        var record = Assert.Single(Records([thunder, buff, talent], talent));

        Assert.Equal(ModifierType.AttributeIncrease, record.Type);
        Assert.Equal("Haste", record.Attribute);
        Assert.Equal(1745, Assert.Single(record.Targets!).Id);
    }

    [Fact]
    public void AnEffectStatesTheAttributeModifiersItAppliesToItsHolder()
    {
        var buff = new Entity
        {
            Type = EntityType.Effect,
            NetId = 1314,
            Name = "GE_1314",
            Modifiers =
            [
                new EffectModifier("Resources") { Op = "Additive", Value = -1 },
                new EffectModifier("MoveSpeed") { Value = 1.3, Op = "MultiplyCompound" },
            ],
        };

        var records = Records([buff], buff);

        Assert.Equal(2, records.Count);
        Assert.Contains(records, record => record is { Type: ModifierType.AttributeIncrease, Attribute: "Resources", Value: -1 });
        Assert.Contains(records, record => record is { Type: ModifierType.AttributeMultiplier, Attribute: "MoveSpeed", Value: 1.3 });
        Assert.All(records, record => Assert.Null(record.Targets));
    }

    [Fact]
    public void ATalentRowGatedOnATagAnEffectAddsAppliesWhileThatEffectIsActive()
    {
        const string first = "Talents.Rime.ChanneledBeamSingleDamage.BoostByCritProc";
        const string second = "CustomGrantedTags.Rime.Talent.ChanneledBeamSingleDamage.BoostByCritProc.Proc";
        const string row = "Rime.ChanneledBeamSingleDamage.Talent.BoostByCritProc.CritChanceIncrease";

        var proc = new Entity
        {
            Type = EntityType.Effect,
            NetId = 3000,
            Name = "GE_3000",
            AddedTags = [second],
            GatedAdds = new Dictionary<string, IReadOnlyList<string>> { [first] = [second] },
        };

        var torrent = Ability(1027, "Rime.ChanneledBeamSingleDamage", (row, 0.3)) with
        {
            RowUses = new Dictionary<string, IReadOnlyList<string>>
            {
                [row] = [$"sink:AssignTagSetByCallerMagnitude#2[SetByCaller.CritChance]{RowSinks.GateMark}{second}"],
            },
        };

        var talent = Talent(195, "Your critical strikes grant 30% critical strike chance.", first);

        var record = Assert.Single(Records([proc, torrent, talent], talent));

        Assert.Equal(1027, Assert.Single(record.Targets!).Id);
        Assert.Equal(ModifierType.CritChanceIncrease, record.Type);
        Assert.Equal(3000, Assert.Single(record.Condition!.Effects!).Id);
    }

    [Fact]
    public void ATuningReferenceNamesTheOwnerAndTheTalentAndFoldsTheRest()
    {
        var (blast, talent) = GreaterGlacialBlast();
        var other = Ability(1012, null);

        var (index, references) = Indexed([blast, talent, other]);

        var reference = index.Reference(
            GlacialBlast + ".Talent.DamageIncreaseAndCastTimeIncrease.Buff.Duration", other, references);

        Assert.Equal("BuffDuration", reference.Name);
        Assert.Equal(1028, reference.Owner?.Id);
        Assert.Equal(190, reference.Talent?.Id);
    }
}
