using Fellowship.Codex.Services;

namespace Fellowship.Codex.Mcp;

public sealed record ExportLocation(string Folder, string? Output);

public static class ExportFolder
{
    public const string Option = "--export";

    public static ExportLocation Locate(IReadOnlyList<string> args)
    {
        if (Argument(args) is { } path) return Named(path, $"The {Option} argument names");

        if (Environment.GetEnvironmentVariable(FolderExportSource.Variable) is { Length: > 0 } named)
        {
            return Named(named, $"{FolderExportSource.Variable} names");
        }

        var folder = FolderExportSource.Located(Option);

        return new ExportLocation(folder, Path.GetDirectoryName(folder));
    }

    private static ExportLocation Named(string path, string source)
    {
        var full = Path.GetFullPath(path);

        return FolderExportSource.Carries(full)
            ? new ExportLocation(full, null)
            : new ExportLocation(FolderExportSource.Carried(full, source), full);
    }

    private static string? Argument(IReadOnlyList<string> args)
    {
        for (var index = 0; index < args.Count - 1; index++)
        {
            if (string.Equals(args[index], Option, StringComparison.Ordinal)) return args[index + 1];
        }

        return null;
    }
}
