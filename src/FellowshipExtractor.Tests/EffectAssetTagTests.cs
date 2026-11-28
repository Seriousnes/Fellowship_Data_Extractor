using System.Text.Json.Nodes;

using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class EffectAssetTagTests
{
    private const int ShatterstrikeWetDamage = 922;
    private const int ShatterstrikeNormalDamage = 924;
    private const int GlacialAssaultCleaveDamage = 2265;
    private const int TruesightDamageWithDuration = 2981;
    private const int PiercingHowlInterrupt = 3050;
    private const int PiercingHowlDamage = 3052;

    private const string Harmful = "EffectType.Harmful";
    private const string GeneratesThreat = "EffectType.GeneratesThreat";
    private const string Physical = "EffectType.Physical";
    private const string Frost = "EffectType.Magic.Frost";
    private const string Blood = "EffectType.Magic.Blood";

    private const string ShatterstrikeWetDamageClass =
        "/Game/Abilities/NPC/FrozenKingdom/BossFreezingWaterLord/" +
        "GE_FK_BossFreezingWaterLord_FreezingCone_WetDamage.GE_FK_BossFreezingWaterLord_FreezingCone_WetDamage_C";

    private const string TruesightDamageWithDurationClass =
        "/Game/Abilities/NPC/SandTomb/BossArbiter/GiveTruesight/" +
        "GE_ST_BossArbiter_GiveTruesight_Damage_Duration.GE_ST_BossArbiter_GiveTruesight_Damage_Duration_C";

    private const string Component = "AssetTagsGameplayEffectComponent";
    private const string ComponentContainer = "InheritableAssetTags";
    private const string CombinedTags = "CombinedTags";

    private static JsonObject Effect(int id) =>
        Export.Loaded.Documents
            .Where(document => document.Type == EntityType.Effect && document.Id == id)
            .Select(document => document.Body)
            .FirstOrDefault()
        ?? throw new InvalidOperationException($"Effect {id} is not in the export. Either the id moved or the effect is gone.");

    private static string[] Tags(int id) =>
        [.. (Effect(id)["effectTags"]?.AsArray() ?? []).Select(tag => (string)tag!)];

    [Theory]
    [InlineData(GlacialAssaultCleaveDamage, Frost)]
    [InlineData(TruesightDamageWithDuration, Blood)]
    public void A_child_class_has_the_school_tag_its_parent_class_states(int id, string school)
    {
        Assert.Contains(school, Tags(id));
    }

    [Fact]
    public void A_component_with_no_combined_tags_has_its_parent_components()
    {
        var provider = Export.Provider;
        var effect = ClassDefaults.Load(TruesightDamageWithDurationClass, provider);

        Assert.NotNull(effect);

        var component = Assert.Single(effect.Owner!.GetExports(), export => export.ExportType == Component);
        var container = PropertyWalk.GetStruct(component, ComponentContainer);

        Assert.NotNull(container);
        Assert.Null(PropertyWalk.GetTags(container, CombinedTags));
        Assert.Contains(Blood, EffectAssetTags.Of(effect, provider));
    }

    [Fact]
    public void An_interrupt_effect_has_its_native_classs_tags_beside_its_own()
    {
        Assert.Equal([GeneratesThreat, Harmful, Physical], Tags(PiercingHowlInterrupt));
    }

    [Fact]
    public void An_effect_with_no_asset_tags_component_has_no_tags()
    {
        var provider = Export.Provider;
        var effect = ClassDefaults.Load(ShatterstrikeWetDamageClass, provider);

        Assert.NotNull(effect);
        Assert.DoesNotContain(effect.Owner!.GetExports(), export => export.ExportType == Component);
        Assert.Empty(EffectAssetTags.Of(effect, provider));
        Assert.Empty(Tags(ShatterstrikeWetDamage));
    }

    [Theory]
    [InlineData(ShatterstrikeNormalDamage, new[] { Harmful, Frost })]
    [InlineData(PiercingHowlDamage, new[] { Harmful, Physical })]
    public void An_effect_stating_its_tags_has_those_tags(int id, string[] tags)
    {
        Assert.Equal(tags, Tags(id));
    }
}
