using System.Diagnostics.CodeAnalysis;

using CUE4Parse.UE4.Versions;

namespace FellowshipExtractor;

public sealed class ExtractorOptions
{
    public const string InstallVariable = "FELLOWSHIP_INSTALL";

    public const string InstallFlag = "--install";

    public const string MappingsPrefix = "fellowship-";

    public const string MappingsSuffix = ".usmap";

    public const string MappingsPattern = $"{MappingsPrefix}*{MappingsSuffix}";

    public static string MappingsName(string build) => $"{MappingsPrefix}{build}{MappingsSuffix}";

    private string? installDir;

    [AllowNull]
    public string InstallDir
    {
        get => installDir ??= LocateInstall();
        init => installDir = value is { Length: > 0 } ? value : null;
    }

    private static string LocateInstall() =>
        Environment.GetEnvironmentVariable(InstallVariable) is { Length: > 0 } named
            ? named
            : SteamLibrary.FindGame()
                ?? throw new InstallNotFoundException(
                    $"No Steam library has {SteamLibrary.InstallDirName} installed. Set {InstallVariable} " +
                    $"or pass {InstallFlag} with the game's install folder.");

    public EGame Game { get; init; } = EGame.GAME_UE5_5;

    public static EGame ParseGame(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return EGame.GAME_UE5_5;

        var name = value.StartsWith("GAME_", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"GAME_UE{value.Replace('.', '_')}";

        return Enum.TryParse<EGame>(name, ignoreCase: true, out var game)
            ? game
            : throw new ArgumentException($"Unknown engine version '{value}'.");
    }

    public string? MappingsFile { get; init; }

    public string MappingsDir { get; init; } =
        Path.Combine(RepositoryRoot.Folder, "mappings");

    public string ResolvedMappingsDir => Path.GetFullPath(MappingsDir);

    public string GeneratedMappingsFile(string build) => Path.Combine(ResolvedMappingsDir, MappingsName(build));

    public string? ResolvedMappingsFile =>
        MappingsFile is { Length: > 0 } named ? named : NewestGenerated();

    private string? NewestGenerated()
    {
        var here = new DirectoryInfo(ResolvedMappingsDir);

        return here.Exists
            ? here.EnumerateFiles(MappingsPattern)
                .OrderByDescending(file => Build(file.Name))
                .ThenByDescending(file => file.Name, StringComparer.Ordinal)
                .FirstOrDefault()
                ?.FullName
            : null;
    }

    private static long Build(string name) =>
        long.TryParse(name[MappingsPrefix.Length..^MappingsSuffix.Length], out var build) ? build : -1;

    public string OutputDir { get; init; } =
        Path.Combine(RepositoryRoot.Folder, "output");

    public string BlueprintsDir { get; init; } =
        Path.Combine(RepositoryRoot.Folder, "blueprints");

    public string ResolvedBlueprintsDir => Path.GetFullPath(BlueprintsDir);

    public string PaksDir => Path.Combine(InstallDir, "fellowship", "Content", "Paks");

    public string ShippingExe =>
        Path.Combine(InstallDir, "fellowship", "Binaries", "Win64", "fellowship-Win64-Shipping.exe");

    public string ResolvedOutputDir => Path.GetFullPath(OutputDir);
}

public sealed class InstallNotFoundException(string message) : DirectoryNotFoundException(message);
