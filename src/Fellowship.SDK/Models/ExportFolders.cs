namespace Fellowship.SDK;

public static class ExportFolders
{
    public const string Prefix = "export-";

    public const string Pattern = $"{Prefix}*";

    public static string Name(string build) => $"{Prefix}{build}";

    public static long Build(string folder) =>
        folder.StartsWith(Prefix, StringComparison.Ordinal) &&
        long.TryParse(folder[Prefix.Length..], out var build)
            ? build
            : -1;
}
