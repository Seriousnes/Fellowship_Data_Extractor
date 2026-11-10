namespace Fellowship.Codex.Models;

public static class ToolPages
{
    public const string Label = "Tools";

    public const string Talents = "/tools/talents";

    public const string AttunementTrees = "/tools/attunement-trees";

    public const string PowerPotential = "/tools/power-potential";

    public const string Falloff = "/tools/multi-target-falloff";

    public const string ProcChance = "/tools/proc-chance";

    public static IReadOnlyList<(string Label, string Href)> All { get; } =
    [
        ("Talent Calculator", Talents),
        ("Attunement Trees", AttunementTrees),
        ("Power Potential", PowerPotential),
        ("Multi-Target Damage Falloff", Falloff),
        ("Proc Chance", ProcChance),
    ];
}
