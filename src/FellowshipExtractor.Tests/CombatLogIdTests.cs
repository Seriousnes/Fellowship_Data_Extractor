using System.Text.Json.Nodes;

using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class CombatLogIdTests
{
    private const int FinalBlow = 2909;
    private const int ArcaneVolleyHit = 909;
    private const int ArcaneVolleyDot = 908;
    private const int ArcaneVolley = 749;
    private const int MaraAutoAttackProc = 1726;

    private const int FireBallDamageMonitor = 1565;
    private const int FireBall = 1260;

    private const int KingOfTheHill = 105;
    private const int ReducedDamageFromTarget = 2476;
    private const int SurgingPower = 208;

    private const string EffectClass = "EffectClass";
    private const string AbilityClass = "AbilityClass";
    private const string Instant = "Instant";

    private static IEnumerable<(int Id, JsonObject Body)> Of(EntityType type) =>
        Export.Loaded.Documents
            .Where(document => document.Type == type)
            .Select(document => (document.Id, document.Body));

    private static JsonObject Effect(int id) =>
        Of(EntityType.Effect).Where(effect => effect.Id == id).Select(effect => effect.Body).FirstOrDefault()
        ?? throw new InvalidOperationException($"Effect {id} is not in the export. Either the id moved or the effect is gone.");

    private static JsonObject Ability(int id) =>
        Of(EntityType.Ability).Where(ability => ability.Id == id).Select(ability => ability.Body).FirstOrDefault()
        ?? throw new InvalidOperationException($"Ability {id} is not in the export. Either the id moved or the ability is gone.");

    private static string? IdClass(JsonObject effect) => effect["logIdClass"]?.GetValue<string>();

    private static (string Type, int Id)? Reference(JsonNode? reference) =>
        reference is null ? null : (reference["type"]!.GetValue<string>(), reference["id"]!.GetValue<int>());

    private static bool LogsEvents(JsonObject effect) => effect["logsEffectEvents"]?.GetValue<bool>() == true;

    [Theory]
    [InlineData(KingOfTheHill, true)]
    [InlineData(ArcaneVolleyDot, true)]
    [InlineData(ReducedDamageFromTarget, false)]
    [InlineData(ArcaneVolleyHit, false)]
    public void An_effect_logs_its_events_with_a_duration_a_tag_the_logger_asks_for_and_a_name(int id, bool logged) =>
        Assert.Equal(logged, LogsEvents(Effect(id)));

    [Fact]
    public void An_effect_logs_its_events_through_a_tag_outside_the_exported_tag_families()
    {
        var effect = Effect(SurgingPower);
        var tags = effect["effectTags"]!.AsArray().Select(tag => tag!.GetValue<string>()).ToArray();

        Assert.DoesNotContain(tags, tag => CombatLogIds.EffectEventTags.Contains(tag));
        Assert.True(LogsEvents(effect));
    }

    [Fact]
    public void Every_effect_logging_its_events_has_a_duration()
    {
        var logged = Of(EntityType.Effect).Where(effect => LogsEvents(effect.Body)).ToArray();

        Assert.NotEmpty(logged);
        Assert.All(logged, effect => Assert.NotEqual(Instant, effect.Body["durationPolicy"]?.GetValue<string>()));
    }

    [Fact]
    public void An_instant_effect_always_using_its_visual_display_name_is_logged_under_its_own_id()
    {
        var effect = Effect(FinalBlow);

        Assert.True(effect["alwaysUseVisualDisplayName"]!.GetValue<bool>());
        Assert.Equal("Final Blow", effect["visualDisplayName"]!.GetValue<string>());
        Assert.Equal(EffectClass, IdClass(effect));
        Assert.Equal(("effect", FinalBlow), Reference(effect["loggedUnder"]));
    }

    [Fact]
    public void An_instant_effect_of_a_named_ability_is_logged_under_the_ability()
    {
        var effect = Effect(ArcaneVolleyHit);

        Assert.Null(effect["alwaysUseVisualDisplayName"]);
        Assert.Equal(AbilityClass, IdClass(effect));
        Assert.Equal(("ability", ArcaneVolley), Reference(effect["loggedUnder"]));
    }

    [Fact]
    public void An_effect_with_a_duration_is_logged_under_its_own_id()
    {
        var effect = Effect(ArcaneVolleyDot);

        Assert.Equal(EffectClass, IdClass(effect));
        Assert.Equal(("effect", ArcaneVolleyDot), Reference(effect["loggedUnder"]));
    }

    [Fact]
    public void A_visual_display_name_does_not_take_the_line_from_an_auto_attack_ability()
    {
        var effect = Effect(MaraAutoAttackProc);

        Assert.True(effect["alwaysUseVisualDisplayName"]!.GetValue<bool>());
        Assert.Equal(EffectClass, IdClass(effect));
        Assert.Equal("ability", Reference(effect["loggedUnder"])?.Type);
    }

    [Fact]
    public void An_ability_logged_as_a_different_ability_names_that_ability()
    {
        Assert.Equal(("ability", FireBall), Reference(Ability(FireBallDamageMonitor)["logAsADifferentAbility"]));
    }

    [Fact]
    public void The_log_id_class_is_on_effects_with_a_damage_or_healing_execution_alone()
    {
        var logged = Of(EntityType.Effect).Where(effect => IdClass(effect.Body) is not null).ToArray();

        Assert.NotEmpty(logged);
        Assert.All(logged, effect => Assert.NotNull(effect.Body["executions"]));
    }

    [Fact]
    public void An_effect_class_effect_with_a_duration_is_logged_under_its_own_id()
    {
        var own = Of(EntityType.Effect)
            .Where(effect => IdClass(effect.Body) == EffectClass)
            .Where(effect => effect.Body["durationPolicy"]?.GetValue<string>() != Instant)
            .ToArray();

        Assert.NotEmpty(own);
        Assert.All(own, effect => Assert.Equal(("effect", effect.Id), Reference(effect.Body["loggedUnder"])));
    }

    [Fact]
    public void An_effect_logged_under_an_ability_has_the_ability_applying_it_or_the_ability_that_is_logged_in_its_place()
    {
        var abilities = Of(EntityType.Ability).ToDictionary(ability => ability.Id, ability => ability.Body);

        var underAbility = Of(EntityType.Effect)
            .Where(effect => Reference(effect.Body["loggedUnder"]) is { Type: "ability" })
            .Where(effect => Reference(effect.Body["appliedBy"]) is { Type: "ability" })
            .ToArray();

        Assert.NotEmpty(underAbility);
        Assert.All(underAbility, effect =>
        {
            var applier = Reference(effect.Body["appliedBy"])!.Value.Id;
            var expected = Reference(abilities[applier]["logAsADifferentAbility"])?.Id ?? applier;

            Assert.Equal(expected, Reference(effect.Body["loggedUnder"])!.Value.Id);
        });
    }
}
