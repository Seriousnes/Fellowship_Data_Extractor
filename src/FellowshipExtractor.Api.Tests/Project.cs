namespace FellowshipExtractor.Api.Tests;

public static class Project
{
    public static string Folder { get; } = Find();

    public static string Bundle { get; } =
        Path.Combine(Folder, "obj", "Debug", "net10.0", "scopedcss", "bundle",
            "FellowshipExtractor.Api.styles.css");

    private static string Find()
    {
        for (var here = new DirectoryInfo(AppContext.BaseDirectory); here is not null; here = here.Parent)
        {
            var project = Path.Combine(here.FullName, "src", "FellowshipExtractor.Api");

            if (File.Exists(Path.Combine(project, "FellowshipExtractor.Api.csproj"))) return project;
        }

        throw new InvalidOperationException(
            "No src/FellowshipExtractor.Api above the test binary. The generated stylesheets sit in " +
            "that folder, beside their .scss sources and under wwwroot.");
    }
}
