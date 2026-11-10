using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;

namespace FellowshipExtractor.Extraction;

public static class DifficultyStars
{
    public const string Widget = "W_HeroSelect";
    public const string Function = "SetStar";

    private const string Absence =
        "Its Stars array is the only thing that says how many stars an OverallDifficulty is drawn across.";

    public static int Count(AbstractFileProvider provider)
    {
        var file = provider.Files.Values.FirstOrDefault(candidate =>
            candidate.Path.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase) &&
            Path.GetFileNameWithoutExtension(candidate.Path).Equals(Widget, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"No {Widget} package in the build. {Absence}");

        var function = provider.LoadPackage(file).GetExports().OfType<UStruct>()
            .FirstOrDefault(export => export.Name.Equals(Function, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"{Widget} compiles no {Function}. {Absence}");

        var made = (function.ScriptBytecode ?? []).OfType<EX_SetArray>().ToArray();

        return made.Length == 1 && made[0].Elements.Length > 0
            ? made[0].Elements.Length
            : throw new InvalidOperationException(
                $"{Widget}.{Function} makes {made.Length} arrays, and exactly one of star widgets is expected. {Absence}");
    }
}
