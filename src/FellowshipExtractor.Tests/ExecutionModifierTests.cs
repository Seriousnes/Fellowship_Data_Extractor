using System.Text.Json.Nodes;

namespace FellowshipExtractor.Tests;

public sealed class ExecutionModifierTests
{
    private const int FreezingTorrentTick = 1391;
    private const int TheSinisterPassive = 3182;

    private static JsonObject Document(EntityType type, int id) =>
        Export.Loaded.Documents
            .Where(document => document.Type == type && document.Id == id)
            .Select(document => document.Body)
            .FirstOrDefault()
        ?? throw new InvalidOperationException(
            $"{type} {id} is not in the export. Either the id moved or the entity is gone.");

    private static string[] Strings(JsonNode? node) =>
        node?.AsArray().Select(element => element!.GetValue<string>()).ToArray() ?? [];

    private static JsonObject[] Modifiers(int id, string field) =>
        Document(EntityType.Effect, id)[field]!.AsArray().Select(node => node!.AsObject()).ToArray();

    [Fact]
    public void FreezingTorrentTickAddsTheSoulfrostCritChanceToItsSnapshotSourceCapture()
    {
        var crit = Modifiers(FreezingTorrentTick, "executionModifiers")
            .Single(modifier => modifier["attribute"]?.GetValue<string>() == "CritChance");

        Assert.Equal("Additive", crit["op"]?.GetValue<string>());
        Assert.Equal("SetByCaller.CritChance", crit["setByCaller"]?.GetValue<string>());
        Assert.Equal(
            ["CustomGrantedTags.Rime.Talent.ChanneledBeamSingleDamage.BoostByCritProc.ProcActive"],
            Strings(crit["sourceRequires"]));
        Assert.Equal("Source", crit["captureSource"]?.GetValue<string>());
        Assert.True(crit["snapshot"]?.GetValue<bool>());
    }

    [Fact]
    public void TheSinisterRaisesCritChanceAndCritPowerOfCoreAbilitiesAlone()
    {
        var modifiers = Modifiers(TheSinisterPassive, "modifiers");

        Assert.All(modifiers, modifier => Assert.Equal(["AbilityType.Core"], Strings(modifier["sourceRequires"])));
        Assert.Equal(
            0.01,
            modifiers.Single(modifier => modifier["attribute"]?.GetValue<string>() == "CritChance")["value"]?
                .GetValue<double>());
    }
}
