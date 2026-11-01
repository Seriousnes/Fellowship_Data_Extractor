using System.Globalization;
using System.Text.RegularExpressions;

namespace FellowshipExtractor;

public static partial class GameBuild
{
    public const string OverrideVariable = "FELLOWSHIP_BUILD";

    public static string Read(ExtractorOptions options)
    {
        if (Environment.GetEnvironmentVariable(OverrideVariable) is { Length: > 0 } stated) return stated;

        var (manifest, text) = Manifest(options);

        return BuildIdEntry().Match(text) is { Success: true } build
            ? build.Groups[1].Value
            : throw new InvalidDataException($"{manifest.FullName} names {Install(options).Name} but declares no buildid.");
    }

    public static DateTimeOffset? Date(ExtractorOptions options)
    {
        if (Environment.GetEnvironmentVariable(OverrideVariable) is { Length: > 0 }) return null;

        var (_, text) = Manifest(options);

        return LastUpdatedEntry().Match(text) is { Success: true } updated &&
            long.TryParse(updated.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;
    }

    private static DirectoryInfo Install(ExtractorOptions options) => new(Path.GetFullPath(options.InstallDir));

    private static (FileInfo Manifest, string Text) Manifest(ExtractorOptions options)
    {
        var install = Install(options);
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

            return (manifest, text);
        }

        throw new FileNotFoundException(
            $"No appmanifest in {library.FullName} declares installdir {install.Name}, so the build number " +
            $"that names the export folder cannot be read. Set {OverrideVariable} to name the build instead.");
    }

    [GeneratedRegex("\"installdir\"\\s+\"([^\"]*)\"", RegexOptions.IgnoreCase)]
    private static partial Regex InstallDirEntry();

    [GeneratedRegex("\"buildid\"\\s+\"([^\"]*)\"", RegexOptions.IgnoreCase)]
    private static partial Regex BuildIdEntry();

    [GeneratedRegex("\"LastUpdated\"\\s+\"([0-9]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex LastUpdatedEntry();
}
