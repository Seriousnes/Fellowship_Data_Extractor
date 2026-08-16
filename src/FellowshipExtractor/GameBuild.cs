using System.Text.RegularExpressions;

namespace FellowshipExtractor;

public static partial class GameBuild
{
    public const string OverrideVariable = "FELLOWSHIP_BUILD";

    public static string Read(ExtractorOptions options)
    {
        if (Environment.GetEnvironmentVariable(OverrideVariable) is { Length: > 0 } stated) return stated;

        var install = new DirectoryInfo(Path.GetFullPath(options.InstallDir));
        var library = install.Parent?.Parent;

        if (library is null || !library.Exists)
        {
            throw new DirectoryNotFoundException(
                $"No Steam library above {install.FullName}. The build number names the export folder and " +
                $"is read from the appmanifest whose installdir is {install.Name}. Set {OverrideVariable} " +
                "to name the build instead.");
        }

        foreach (var manifest in library.EnumerateFiles("appmanifest_*.acf"))
        {
            var text = File.ReadAllText(manifest.FullName);

            if (InstallDirEntry().Match(text) is not { Success: true } named) continue;
            if (!named.Groups[1].Value.Equals(install.Name, StringComparison.OrdinalIgnoreCase)) continue;

            return BuildIdEntry().Match(text) is { Success: true } build
                ? build.Groups[1].Value
                : throw new InvalidDataException(
                    $"{manifest.FullName} names {install.Name} but declares no buildid.");
        }

        throw new FileNotFoundException(
            $"No appmanifest in {library.FullName} declares installdir {install.Name}, so the build number " +
            $"that names the export folder cannot be read. Set {OverrideVariable} to name the build instead.");
    }

    [GeneratedRegex("\"installdir\"\\s+\"([^\"]*)\"", RegexOptions.IgnoreCase)]
    private static partial Regex InstallDirEntry();

    [GeneratedRegex("\"buildid\"\\s+\"([^\"]*)\"", RegexOptions.IgnoreCase)]
    private static partial Regex BuildIdEntry();
}
