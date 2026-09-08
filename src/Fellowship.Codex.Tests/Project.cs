namespace Fellowship.Codex.Tests;

public static class Project
{
    public static string Folder { get; } = Find();

    private static string Find()
    {
        for (var here = new DirectoryInfo(AppContext.BaseDirectory); here is not null; here = here.Parent)
        {
            var project = Path.Combine(here.FullName, "src", "Fellowship.Codex");

            if (File.Exists(Path.Combine(project, "Fellowship.Codex.csproj"))) return project;
        }

        throw new InvalidOperationException(
            "No src/Fellowship.Codex above the test binary. The web root the tooltip view reads " +
            "its art through sits in that folder, under wwwroot.");
    }
}
