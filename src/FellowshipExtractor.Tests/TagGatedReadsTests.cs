using CUE4Parse.UE4.Assets.Exports;

using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class TagGatedReadsTests
{
    private const string IceStrike =
        "fellowship/Content/Abilities/Heroes/Rime/InstantSingleDamage/GA_Rime_InstantSingleDamage";

    private static IReadOnlyList<GatedRead> Reads()
    {
        Assert.True(Export.Provider.TryLoadPackage(IceStrike, out var package), IceStrike);

        return TagGatedReads.Of(
            package.GetExports().First(export => export.Name.StartsWith("Default__", StringComparison.Ordinal)));
    }

    [Fact]
    public void TheOrbSpenderTalentTagGatesTheReducesCastTimeProcEffect()
    {
        Assert.Contains(
            new GatedRead(
                "Talents.Rime.InstantSingleDamage.ReducesCooldownOnOrbSpender",
                null,
                "GE_Rime_Talent_InstantSingleDamage_ReducesCastTimeOnOrbSpender_Proc"),
            Reads());
    }

    [Fact]
    public void TheTorrentTalentTagGatesTheChanneledBeamCooldownReduction()
    {
        Assert.Contains(
            new GatedRead(
                "Talents.Rime.InstantSingleDamage.ReducesCooldownOnTorrent",
                "ChanneledBeamCooldownReduction Ref",
                null),
            Reads());
    }
}
