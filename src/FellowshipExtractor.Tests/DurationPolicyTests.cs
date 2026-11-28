using System.Text.Json.Nodes;

namespace FellowshipExtractor.Tests;

public sealed class DurationPolicyTests
{
    private const int SanguineRitualTickDamage = 3037;
    private const int ArcaneVolleyDot = 908;
    private const int Anchored = 718;

    private const string HasDuration = "HasDuration";
    private const string Infinite = "Infinite";

    private static IEnumerable<(int Id, JsonObject Body)> Effects =>
        Export.Loaded.Documents
            .Where(document => document.Type == EntityType.Effect)
            .Select(document => (document.Id, document.Body));

    private static JsonObject Effect(int id) =>
        Effects.Where(effect => effect.Id == id).Select(effect => effect.Body).FirstOrDefault()
        ?? throw new InvalidOperationException($"Effect {id} is not in the export. Either the id moved or the effect is gone.");

    private static string? Policy(JsonObject effect) => effect["durationPolicy"]?.GetValue<string>();

    [Fact]
    public void An_effect_stating_no_policy_has_the_policy_its_native_class_constructor_stores()
    {
        var effect = Effect(SanguineRitualTickDamage);

        Assert.Equal("Instant", Policy(effect));
        Assert.Null(effect["ticks"]);
    }

    [Theory]
    [InlineData(ArcaneVolleyDot, HasDuration)]
    [InlineData(Anchored, Infinite)]
    public void An_effect_stating_a_policy_has_that_policy(int id, string policy) =>
        Assert.Equal(policy, Policy(Effect(id)));

    [Fact]
    public void Ticks_are_on_effects_of_the_duration_policy_alone()
    {
        var ticking = Effects.Where(effect => effect.Body["ticks"] is not null).ToArray();

        Assert.NotEmpty(ticking);
        Assert.All(ticking, effect => Assert.Equal(HasDuration, Policy(effect.Body)));
    }

    [Fact]
    public void Infinite_is_true_for_the_infinite_policy_alone()
    {
        var stated = Effects.Where(effect => Policy(effect.Body) is not null).ToArray();

        Assert.NotEmpty(stated);
        Assert.All(
            stated,
            effect => Assert.Equal(Policy(effect.Body) == Infinite, effect.Body["infinite"]?.GetValue<bool>() == true));
    }
}
