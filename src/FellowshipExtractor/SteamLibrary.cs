using System.Text.RegularExpressions;

using Microsoft.Win32;

namespace FellowshipExtractor;

public static partial class SteamLibrary
{
    public const string InstallDirName = "Fellowship";

    public static string? FindGame()
    {
        foreach (var library in Libraries())
        {
            if (Game(library) is { } game) return game;
        }

        return null;
    }

    public static string? Game(string library)
    {
        var steamapps = new DirectoryInfo(Path.Combine(library, "steamapps"));

        if (!steamapps.Exists) return null;

        foreach (var manifest in steamapps.EnumerateFiles("appmanifest_*.acf"))
        {
            if (InstallDirEntry().Match(File.ReadAllText(manifest.FullName)) is not { Success: true } named) continue;
            if (!named.Groups[1].Value.Equals(InstallDirName, StringComparison.OrdinalIgnoreCase)) continue;

            var game = Path.Combine(steamapps.FullName, "common", named.Groups[1].Value);

            if (Directory.Exists(game)) return game;
        }

        return null;
    }

    public static IEnumerable<string> Libraries()
    {
        var seen = new HashSet<string>(OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal);

        foreach (var root in Roots())
        {
            if (!Directory.Exists(root)) continue;

            if (seen.Add(Path.GetFullPath(root))) yield return Path.GetFullPath(root);

            var folders = Path.Combine(root, "steamapps", "libraryfolders.vdf");

            if (!File.Exists(folders)) continue;

            foreach (var library in LibraryPaths(File.ReadAllText(folders)))
            {
                if (Directory.Exists(library) && seen.Add(Path.GetFullPath(library)))
                {
                    yield return Path.GetFullPath(library);
                }
            }
        }
    }

    public static IEnumerable<string> LibraryPaths(string libraryFolders) =>
        PathEntry().Matches(libraryFolders).Select(match => match.Groups[1].Value.Replace(@"\\", @"\"));

    private static IEnumerable<string> Roots()
    {
        if (OperatingSystem.IsWindows())
        {
            if (Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) is string user)
            {
                yield return user;
            }

            if (Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) is string machine)
            {
                yield return machine;
            }

            foreach (var programs in new[] { Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.ProgramFiles })
            {
                if (Environment.GetFolderPath(programs) is { Length: > 0 } folder) yield return Path.Combine(folder, "Steam");
            }

            yield break;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (home.Length == 0) yield break;

        if (OperatingSystem.IsMacOS())
        {
            yield return Path.Combine(home, "Library", "Application Support", "Steam");
            yield break;
        }

        yield return Path.Combine(home, ".steam", "steam");
        yield return Path.Combine(home, ".local", "share", "Steam");
        yield return Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam");
    }

    [GeneratedRegex("\"installdir\"\\s+\"([^\"]*)\"", RegexOptions.IgnoreCase)]
    internal static partial Regex InstallDirEntry();

    [GeneratedRegex("\"path\"\\s+\"([^\"]*)\"", RegexOptions.IgnoreCase)]
    private static partial Regex PathEntry();
}
