using System.Text.Json;

using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;
using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

/// <summary>
/// The channel flags and tag containers of an ability, and the granted tags and executions of an effect, against
/// Zeraleth's Hunger (<c>GA_Weapon_CastedHealAndDamageChannel</c>) and Raging Stomp's stun
/// (<c>GE_Gunde_AoeDamageAndStun_Stun</c>). <c>docs/casting-and-channeling.md</c> has the routines reading them.
/// </summary>
public sealed class CastingAndChannelingTests
{
    private const int ZeralethsHunger = 162;
    private const int RagingStompStun = 1802;
    private const string CancelExecution = "CRCancelAbilitiesBlockedByThisEffectsParentExecution";

    private static T Document<T>(EntityType type, int id) where T : EntityDocument =>
        Export.Loaded.Documents.Single(row => row.Type == type && row.Id == id).Body.Deserialize<T>(EntityDocuments.Options)!;

    [Fact]
    public void ZeralethsHungerTicksImmediatelyOverADurationWithNoTimeRate()
    {
        var hunger = Document<AbilityDocument>(EntityType.Ability, ZeralethsHunger);

        Assert.Equal(
            new ChannelingTaskData(
                ContinuousChannel: false,
                TickImmediately: true,
                ScaleDurationWithAbilityTimeRate: false,
                EnablePartialTicks: false,
                AllowSpellPushback: false),
            hunger.ChannelingData);
        Assert.True(hunger.CastTimeScalesWithHaste);
        Assert.Null(hunger.ScaleTimeWithAttackSpeed);
    }

    [Fact]
    public void ZeralethsHungerIsASkillBlockedByBlockAllSkills()
    {
        var hunger = Document<AbilityDocument>(EntityType.Ability, ZeralethsHunger);

        Assert.Contains("AbilityType.Skill.Offensive", hunger.AbilityTags!);
        Assert.Contains("AbilityType.Channeled", hunger.AbilityTags!);
        Assert.Contains("CharacterStatus.BlockAllSkills", hunger.ActivationBlockedTags!);
    }

    [Fact]
    public void RagingStompsStunGrantsTheFourStatusTagsAndAppliesTheCancelExecution()
    {
        var stun = Document<EffectDocument>(EntityType.Effect, RagingStompStun);

        Assert.Equal(
            [
                "CharacterStatus.BlockAllSkills",
                "CharacterStatus.IgnoreActionInput",
                "CharacterStatus.IgnoreMoveInput",
                "CharacterStatus.IgnoreRotationInput",
            ],
            stun.GrantedTags);

        Assert.Contains(
            stun.Applies!,
            applied => Document<EffectDocument>(EntityType.Effect, applied.Id).Executions?.Contains(CancelExecution) is true);
    }

    [Fact]
    public void TheDefaultsAreTheConstructorsOfCRChannelingTaskData()
    {
        Assert.True(AbilityTaskReads.Defaults.ScaleDurationWithAbilityTimeRate);
        Assert.False(AbilityTaskReads.Defaults.TickImmediately);
        Assert.False(AbilityTaskReads.Defaults.EnablePartialTicks);
        Assert.False(AbilityTaskReads.Defaults.ContinuousChannel);
        Assert.False(AbilityTaskReads.Defaults.AllowSpellPushback);
    }
}
