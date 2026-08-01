using CUE4Parse.UE4.Versions;

namespace FellowshipExtractor;

/// <summary>
/// Single place where the game install, engine version and mappings knobs live.
/// Everything downstream reads the provider this produces, never these paths.
/// </summary>
public sealed class ExtractorOptions
{
    public const string DefaultInstallDir = @"C:\Program Files (x86)\Steam\steamapps\common\Fellowship";

    public string InstallDir { get; init; } = DefaultInstallDir;

    /// <summary>
    /// Provisional. The build reports an engine source fork (++depot+release-source-CL-112206),
    /// so there is no stock version tag to read; this can only be pinned once a .usmap lets a
    /// real asset round-trip. Override with FELLOWSHIP_UE_VERSION, e.g. "5.6" or "GAME_UE5_6".
    /// </summary>
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

    /// <summary>Optional .usmap; required once we start reading unversioned properties.</summary>
    public string? MappingsFile { get; init; }

    public string OutputDir { get; init; } =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "output");

    public string PaksDir => Path.Combine(InstallDir, "fellowship", "Content", "Paks");

    public string ResolvedOutputDir => Path.GetFullPath(OutputDir);
}
