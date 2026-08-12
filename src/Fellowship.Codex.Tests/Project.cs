namespace Fellowship.Codex.Tests;

public static class Project
{
    public static string Folder { get; } = Find();

    public static string Bundle { get; } =
        Path.Combine(Folder, "obj", "Debug", "net10.0", "scopedcss", "bundle",
            "Fellowship.Codex.styles.css");

    private static string Find()
    {
        for (var here = new DirectoryInfo(AppContext.BaseDirectory); here is not null; here = here.Parent)
        {
            var project = Path.Combine(here.FullName, "src", "Fellowship.Codex");

            if (File.Exists(Path.Combine(project, "Fellowship.Codex.csproj"))) return project;
        }

        throw new InvalidOperationException(
            "No src/Fellowship.Codex above the test binary. The generated stylesheets sit in " +
            "that folder, beside their .scss sources and under wwwroot.");
    }
}
