using CUE4Parse.UE4.Versions;

namespace FellowshipExtractor;

public sealed class ExtractorOptions
{
    public const string DefaultInstallDir = @"C:\Program Files (x86)\Steam\steamapps\common\Fellowship";

    public string InstallDir { get; init; } = DefaultInstallDir;

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

    public string OutputDir { get; init; } =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "output");

    public string PaksDir => Path.Combine(InstallDir, "fellowship", "Content", "Paks");

    public string ResolvedOutputDir => Path.GetFullPath(OutputDir);
}
