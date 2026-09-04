using Fellowship.Codex.Services;

namespace Fellowship.Codex.Mcp;

public static class ExportFolder
{
    public const string Option = "--export";

    public static string Locate(IReadOnlyList<string> args)
    {
        if (Given(args) is { } asked) return Carried(asked, $"The {Option} argument names");

        if (Environment.GetEnvironmentVariable(FolderExportSource.Variable) is { Length: > 0 } named)
        {
            return Carried(named, $"{FolderExportSource.Variable} names");
        }

        return FolderExportSource.Above(Directory.GetCurrentDirectory())
            ?? FolderExportSource.Above(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException(
                $"No output/{ExportFolders.Pattern} carrying {ExportReader.EntitiesName} " +
                $"and {ExportReader.SettingsName} above " +
                $"{Directory.GetCurrentDirectory()} or {AppContext.BaseDirectory}, and neither {Option} nor " +
                $"{FolderExportSource.Variable} names one. Write it with " +
                "'dotnet run --project src/FellowshipExtractor'.");
    }

    private static string? Given(IReadOnlyList<string> args)
    {
        for (var at = 0; at < args.Count - 1; at++)
        {
            if (string.Equals(args[at], Option, StringComparison.Ordinal)) return args[at + 1];
        }

        return null;
    }

    private static string Carried(string folder, string source)
    {
        var full = Path.GetFullPath(folder);

        return FolderExportSource.Carries(full)
            ? full
            : throw new InvalidOperationException(
                $"{source} {full}, which carries no {ExportReader.EntitiesName}.");
    }
}
