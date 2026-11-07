namespace FellowshipExtractor.Tests;

public sealed class SteamLibraryTests
{
    private static void InTempFolder(Action<string> test)
    {
        var folder = Directory.CreateTempSubdirectory("fellowship-steam").FullName;

        try
        {
            test(folder);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void LibraryFoldersYieldsEveryLibraryPathUnescaped()
    {
        const string folders = """
            "libraryfolders"
            {
                "0"
                {
                    "path"		"C:\\Program Files (x86)\\Steam"
                    "apps" { "228980" "0" }
                }
                "1"
                {
                    "path"		"/mnt/games/SteamLibrary"
                }
            }
            """;

        Assert.Equal(
            [@"C:\Program Files (x86)\Steam", "/mnt/games/SteamLibrary"],
            SteamLibrary.LibraryPaths(folders));
    }

    [Fact]
    public void ALibraryWhoseManifestNamesTheGameYieldsItsInstallFolder() => InTempFolder(library =>
    {
        var steamapps = Path.Combine(library, "steamapps");
        var game = Directory.CreateDirectory(Path.Combine(steamapps, "common", SteamLibrary.InstallDirName)).FullName;

        File.WriteAllText(Path.Combine(steamapps, "appmanifest_1.acf"), "\"AppState\" { \"installdir\" \"Other\" }");
        File.WriteAllText(
            Path.Combine(steamapps, "appmanifest_2.acf"),
            $"\"AppState\" {{ \"installdir\" \"{SteamLibrary.InstallDirName}\" \"buildid\" \"1\" }}");

        Assert.Equal(game, SteamLibrary.Game(library));
    });

    [Fact]
    public void ALibraryWithoutTheGameYieldsNothing() => InTempFolder(library =>
    {
        var steamapps = Directory.CreateDirectory(Path.Combine(library, "steamapps")).FullName;

        File.WriteAllText(Path.Combine(steamapps, "appmanifest_1.acf"), "\"AppState\" { \"installdir\" \"Other\" }");

        Assert.Null(SteamLibrary.Game(library));
    });

    [Fact]
    public void ANamedInstallFolderIsUsedWithoutSearchingSteam()
    {
        var options = new ExtractorOptions { InstallDir = "/games/Fellowship" };

        Assert.Equal("/games/Fellowship", options.InstallDir);
    }

    [Fact]
    public void TheRepositoryRootHoldsTheSolution() =>
        Assert.True(File.Exists(Path.Combine(RepositoryRoot.Folder, RepositoryRoot.Marker)));
}
