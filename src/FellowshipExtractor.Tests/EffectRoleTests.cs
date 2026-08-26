using Fellowship.SDK;
using Fellowship.SDK.Models;

using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class EffectRoleTests
{
    private static readonly string[] Codenames = ["Warmaster", "Ink"];

    private static EffectRoles Roles(params string[] effects) => new(
        [
            new Entity { Type = EntityType.Ability, NetId = 977, Name = "GA_Warmaster_MeleeCleave" },
            .. effects.Select(name => new Entity { Type = EntityType.Effect, Name = name }),
        ],
        Codenames);

    [Fact]
    public void TheLongestAbilityPrefixNamesTheAbilityAndTheRestIsTheRole()
    {
        var roles = Roles();

        Assert.Equal(977, roles.PartOf("GE_Warmaster_MeleeCleave_AbsorbBuff_SelfBuff"));
        Assert.Equal("AbsorbBuffSelfBuff", roles.Role("GE_Warmaster_MeleeCleave_AbsorbBuff_SelfBuff"));
    }

    [Fact]
    public void ASingleWordTailIsWrittenUnchanged() =>
        Assert.Equal("Damage", Roles().Role("GE_Warmaster_MeleeCleave_Damage"));

    [Fact]
    public void ATailNamingAHeroIsWithheldWhileTheAbilityIsStillNamed()
    {
        var roles = Roles();

        Assert.Equal(977, roles.PartOf("GE_Warmaster_MeleeCleave_WarmasterShout"));
        Assert.Null(roles.Role("GE_Warmaster_MeleeCleave_WarmasterShout"));
    }

    [Fact]
    public void AHeroNameInsideALongerWordIsNotAHero() =>
        Assert.Equal("ShieldBuffLink", Roles().Role("GE_Warmaster_MeleeCleave_ShieldBuff_Link"));

    [Fact]
    public void AnEffectNoAbilityPrefixNamesCarriesNeither()
    {
        var roles = Roles();

        Assert.Null(roles.PartOf("GE_Warmaster_SomethingElse"));
        Assert.Null(roles.Role("GE_Warmaster_SomethingElse"));
    }

    [Fact]
    public void TheCountsSeparateWhatWasWrittenFromWhatWasWithheld()
    {
        var roles = Roles(
            "GE_Warmaster_MeleeCleave_Damage",
            "GE_Warmaster_MeleeCleave_WarmasterShout",
            "GE_Warmaster_SomethingElse");

        Assert.Equal(2, roles.Parts);
        Assert.Equal(1, roles.Roles);
        Assert.Equal(1, roles.Withheld);
    }
}
