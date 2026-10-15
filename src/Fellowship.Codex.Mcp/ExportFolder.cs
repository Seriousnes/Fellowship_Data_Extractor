using Fellowship.Codex.Services;

namespace Fellowship.Codex.Mcp;

public static class ExportFolder
{
    public const string Option = "--export";

    public static string Locate(IReadOnlyList<string> args) =>
        Argument(args) is { } path
            ? FolderExportSource.Carried(path, $"The {Option} argument names")
            : FolderExportSource.Located(Option);

    private static string? Argument(IReadOnlyList<string> args)
    {
        for (var index = 0; index < args.Count - 1; index++)
        {
            if (string.Equals(args[index], Option, StringComparison.Ordinal)) return args[index + 1];
        }

        return null;
    }
}
