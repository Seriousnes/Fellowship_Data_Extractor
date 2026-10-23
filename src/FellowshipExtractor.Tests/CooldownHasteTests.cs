using System.Text.Json.Nodes;

namespace FellowshipExtractor.Tests;

public sealed class CooldownHasteTests
{
    private const int ColdSnap = 1020;
    private const int PricklyVine = 1057;
    private const int PowerStrike = 982;
    private const int Stagger = 1116;
    private const int TwilightSkybolt = 156;
    private const int IronleafWard = 116;
    private const int TheMonarch = 705;

    private const string HasteGrantedBy = "cooldownHasteGrantedBy";
    private const string AccelerationGrantedBy = "cooldownAccelerationGrantedBy";
    private const string Glyph = "glyph";
    private const string Major = "Major";

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
        Assert.True(Names(ability, HasteGrantedBy, type, augmentation));
        Assert.DoesNotContain(Haste, TimeRate(ability) ?? []);
    }

    [Theory]
    [InlineData(973)]
    [InlineData(1233)]
    [InlineData(1235)]
    [InlineData(1001)]
    [InlineData(111)]
    public void TheMonarchAcceleratesMajorCooldowns(int ability)
    {
        Assert.Equal(Major, Ability(ability)["category"]?.GetValue<string>());
        Assert.True(Names(ability, AccelerationGrantedBy, Glyph, TheMonarch));
    }

    [Theory]
    [InlineData(981)]
    [InlineData(1465)]
    [InlineData(1857)]
    [InlineData(1917)]
    public void OtherCategoriesHaveNoCooldownAcceleration(int ability)
    {
        Assert.NotEqual(Major, Ability(ability)["category"]?.GetValue<string>());
        Assert.Null(Ability(ability)[AccelerationGrantedBy]);
    }

    [Fact]
    public void IronleafWardNamesBothItsLegendaryPowerAndTheMonarch()
    {
        Assert.True(Names(IronleafWard, HasteGrantedBy, "legendary-power", 609));
        Assert.True(Names(IronleafWard, AccelerationGrantedBy, Glyph, TheMonarch));
    }

    [Fact]
    public void OnlyMajorAbilitiesNameTheMonarch()
    {
        var accelerated = Export.Loaded.Documents
            .Where(document => document.Type == EntityType.Ability && document.Body[AccelerationGrantedBy] is not null)
            .Select(document => document.Body["category"]?.GetValue<string>())
            .ToArray();

        Assert.NotEmpty(accelerated);
        Assert.All(accelerated, category => Assert.Equal(Major, category));
    }

    private static bool Names(int ability, string field, string type, int id) =>
        (Ability(ability)[field]?.AsArray() ?? [])
        .OfType<JsonObject>()
        .Any(reference => reference["type"]?.GetValue<string>() == type && reference["id"]?.GetValue<int>() == id);
}
