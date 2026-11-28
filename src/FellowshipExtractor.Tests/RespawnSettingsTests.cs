using Fellowship.SDK.Models;

namespace FellowshipExtractor.Tests;

public sealed class RespawnSettingsTests
{
    [Fact]
    public void TheSettingsStateTheRespawnCooldownOfTheDungeonGameMode()
    {
        var respawn = Export.Loaded.Settings.Respawn;

        Assert.NotNull(respawn);
        Assert.Equal("GE_Shared_RespawnCooldown", respawn.CooldownEffect);
        Assert.Equal(30, respawn.CooldownDuration);
        Assert.Equal("Shared.RespawnCooldown.Duration", respawn.CooldownDurationRow);
        Assert.Equal("Cooldown.Respawn", respawn.CooldownTag);
        Assert.Contains("respawn request", respawn.DisplayText);
    }

    [Fact]
    public void TheCooldownDurationIsTheRespawnCooldownEffectsDuration()
    {
        var respawn = Export.Loaded.Settings.Respawn;
        var effect = Assert.Single(
            Export.Loaded.Documents,
            document => document.Type == EntityType.Effect && document.Name == "Respawn Cooldown");

        Assert.NotNull(respawn);
        Assert.Equal(respawn.CooldownDuration, effect.Body["duration"]!.GetValue<double>());
    }
}
