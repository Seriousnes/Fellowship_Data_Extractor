namespace Fellowship.Codex.Tests;

public static class CombatantInfo
{
    public const string FileName = "combatant-info-events.json";

    public static string Payload { get; } =
        Path.Combine(AppContext.BaseDirectory, "FellowshipLogs", FileName);

    public static bool Present { get; } = File.Exists(Payload);

    public static void Require() =>
        Assert.SkipUnless(
            Present,
            $"docs/{FileName} is not in the checkout, so nothing was copied to {Payload} and the " +
            "tests that read a combatant-info payload have no input.");
}
