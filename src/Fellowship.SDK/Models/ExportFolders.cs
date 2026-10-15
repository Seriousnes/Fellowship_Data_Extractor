namespace Fellowship.SDK;

public static class ExportFolders
{
    public const string Prefix = "export-";

    public const string Pattern = $"{Prefix}*";

    public static string Name(string build) => $"{Prefix}{build}";

    public static string Id(string folder) =>
        folder.StartsWith(Prefix, StringComparison.Ordinal) ? folder[Prefix.Length..] : folder;

    public static long Number(string build) => long.TryParse(build, out var number) ? number : -1;

    public static long Build(string folder) =>
        folder.StartsWith(Prefix, StringComparison.Ordinal) ? Number(Id(folder)) : -1;
}
