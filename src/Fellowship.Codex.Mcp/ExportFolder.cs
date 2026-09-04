using Fellowship.Codex.Services;

namespace Fellowship.Codex.Mcp;

public static class ExportFolder
{
    public const string Option = "--export";

    public static string Locate(IReadOnlyList<string> args) =>
        Given(args) is { } asked
            ? FolderExportSource.Carried(asked, $"The {Option} argument names")
            : FolderExportSource.Located(Option);

    private static string? Given(IReadOnlyList<string> args)
    {
        for (var at = 0; at < args.Count - 1; at++)
        {
            if (string.Equals(args[at], Option, StringComparison.Ordinal)) return args[at + 1];
        }

        return null;
    }
}
