using System.Text.Json.Nodes;

namespace FellowshipExtractor.Tests;

public sealed class CooldownHasteTests
{
    private const int ColdSnap = 1020;
    private const int PricklyVine = 1057;
    private const int PowerStrike = 982;
    private const int Stagger = 1116;
    private const int TwilightSkybolt = 156;

    private const string Haste = "Haste";
    private const string CooldownRecovery = "CooldownRecovery";
    private const string CooldownReduction = "CooldownReduction";

    private static JsonObject Ability(int id) =>
        Export.Loaded.Documents
            .Where(document => document.Type == EntityType.Ability && document.Id == id)
            .Select(document => document.Body)
            .FirstOrDefault()
        ?? throw new InvalidOperationException(
            $"Ability {id} is not in the export. Either the id moved or the ability is gone.");

    private static string[]? Attributes(JsonObject ability, string field) =>
        ability[field]?.AsArray().Select(attribute => attribute!.GetValue<string>()).ToArray();

    private static string[]? TimeRate(int id) => Attributes(Ability(id), "cooldownTimeRateScalesOff");

    private static string[]? Duration(int id) => Attributes(Ability(id), "cooldownDurationScalesOff");

    [Theory]
    [InlineData(ColdSnap)]
    [InlineData(1313)]
    [InlineData(1072)]
    [InlineData(1207)]
    [InlineData(1130)]
    [InlineData(1122)]
    [InlineData(977)]
    [InlineData(984)]
    [InlineData(TwilightSkybolt)]
    public void TextNamingHasteAccelerationMatchesTheTimeRate(int id) => Assert.Contains(Haste, TimeRate(id) ?? []);

    [Fact]
    public void ColdSnapRechargesOnTheChargeEffect()
    {
        Assert.Equal([CooldownRecovery, Haste], TimeRate(ColdSnap));
        Assert.Null(Duration(ColdSnap));
    }

    [Fact]
    public void PricklyVineRechargesWithoutHaste()
    {
        Assert.Equal([CooldownRecovery], TimeRate(PricklyVine));
        Assert.Null(Duration(PricklyVine));
    }

    [Fact]
    public void TwilightSkyboltRechargesWithoutCooldownRecovery() =>
        Assert.Equal([Haste], TimeRate(TwilightSkybolt));

    [Fact]
    public void PowerStrikeDividesItsDurationByHaste()
    {
        Assert.Equal([CooldownReduction, Haste], Duration(PowerStrike));
        Assert.Equal([CooldownRecovery], TimeRate(PowerStrike));
    }

    [Fact]
    public void StaggerReadsNoCooldownAttribute()
    {
        Assert.NotNull(Ability(Stagger)["cooldown"]);
        Assert.Null(Duration(Stagger));
        Assert.Null(TimeRate(Stagger));
    }

    [Theory]
    [InlineData(1075, "talent", 31)]
    [InlineData(996, "talent", 566)]
    [InlineData(116, "legendary-power", 609)]
    [InlineData(975, "legendary-power", 625)]
    public void ChargeGrantingAugmentationsBringHaste(int ability, string type, int augmentation)
    {
        var granted = Ability(ability)["cooldownHasteGrantedBy"]?.AsArray() ?? [];

        Assert.Contains(
            granted.OfType<JsonObject>(),
            reference => reference["type"]?.GetValue<string>() == type &&
                         reference["id"]?.GetValue<int>() == augmentation);

        Assert.DoesNotContain(Haste, TimeRate(ability) ?? []);
    }
}
