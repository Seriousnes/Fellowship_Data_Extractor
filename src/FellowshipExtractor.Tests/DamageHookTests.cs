using System.Text.Json.Nodes;

namespace FellowshipExtractor.Tests;

public sealed class DamageHookTests
{
    private const int AgainstAllOddsMonitor = 1951;
    private const int AgainstAllOddsCooldown = 2714;
    private const int AncestralInterventionMonitor = 1950;
    private const int LingeringConcussion = 2478;
    private const int LingeringConcussionSelfBuff = 2476;
    private const int Aftershock = 2428;
    private const int AftershockOwnerEffect = 3088;

    private const string OwningCharacter = "OwningCharacter";
    private const string RedirectDamage = "RedirectDamage";
    private const string DeathPrevention = "DeathPrevention";

    private const string InstigatorField = "effectAppliedToTheInstigatorOfTheMainEffect";
    private const string OwnerField = "effectAppliedToTheOwnerOfTheMainEffect";

    private static IEnumerable<JsonObject> Bodies(EntityType type) =>
        Export.Loaded.Documents.Where(document => document.Type == type).Select(document => document.Body);

    private static JsonObject Body(EntityType type, int id) =>
        Export.Loaded.Documents
            .Where(document => document.Type == type && document.Id == id)
            .Select(document => document.Body)
            .FirstOrDefault()
        ?? throw new InvalidOperationException($"{type} {id} is not in the export. Either the id moved or the entity is gone.");

    private static JsonObject Task(int ability) =>
        Assert.Single(Body(EntityType.Ability, ability)["damageModificationTasks"]?.AsArray() ?? [])!.AsObject();

    [Fact]
    public void Against_All_Odds_has_its_owners_incoming_hits_and_is_blocked_by_its_cooldown_tag()
    {
        var task = Task(AgainstAllOddsMonitor);
        var granted = Body(EntityType.Effect, AgainstAllOddsCooldown)["grantedTags"]!.AsArray().Select(tag => (string?)tag);

        Assert.Equal(OwningCharacter, (string?)task["incomingDamageCharacter"]);
        Assert.Null(task["outgoingDamageCharacter"]);
        Assert.Equal(RedirectDamage, (string?)task["prio"]);
        Assert.Contains((string?)task["blockOnTaskOwnerTag"], granted);
    }

    [Fact]
    public void Ancestral_Intervention_runs_at_the_death_prevention_level()
    {
        Assert.Equal(DeathPrevention, (string?)Task(AncestralInterventionMonitor)["prio"]);
    }

    [Fact]
    public void A_main_effect_names_the_effect_of_its_instigator_or_of_its_owner()
    {
        var concussion = Body(EntityType.Effect, LingeringConcussion);
        var aftershock = Body(EntityType.Effect, Aftershock);

        Assert.Equal(LingeringConcussionSelfBuff, (int?)concussion[InstigatorField]?["id"]);
        Assert.Null(concussion[OwnerField]);
        Assert.Equal(AftershockOwnerEffect, (int?)aftershock[OwnerField]?["id"]);
        Assert.Null(aftershock[InstigatorField]);
    }

    [Fact]
    public void Every_actor_dependency_effect_is_named_by_one_main_effect()
    {
        var effects = Bodies(EntityType.Effect).ToArray();

        var dependent = effects
            .Where(effect => (bool?)effect["actorDependency"] == true)
            .Select(effect => (int?)effect["id"])
            .Order()
            .ToArray();

        var named = effects
            .SelectMany(effect => new[] { effect[InstigatorField], effect[OwnerField] })
            .OfType<JsonNode>()
            .Select(reference => (int?)reference["id"])
            .Order()
            .ToArray();

        Assert.NotEmpty(dependent);
        Assert.Equal(dependent, named);
    }
}
