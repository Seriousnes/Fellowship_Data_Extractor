namespace FellowshipExtractor;

public static class RepositoryRoot
{
    public const string Marker = "src/FellowshipExtractor.slnx";

    public static string Folder { get; } =
        Above(AppContext.BaseDirectory) ?? Above(Directory.GetCurrentDirectory()) ?? Directory.GetCurrentDirectory();

    public static string? Above(string start)
    {
        for (var here = new DirectoryInfo(start); here is not null; here = here.Parent)
        {
            if (File.Exists(Path.Combine(here.FullName, Marker))) return here.FullName;
        }

        return null;
    }
}
