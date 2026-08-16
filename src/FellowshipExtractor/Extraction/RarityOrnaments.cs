using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Kismet;

namespace FellowshipExtractor.Extraction;

public sealed class RarityOrnaments
{
    public const string Widget = "UW_TooltipRarityFrame";
    public const string Function = "SetRarityFrame";

    private const string Absence =
        "Its select over the rarity ladder is the only thing that says which header ornament a rung draws.";

    public RarityOrnaments(AbstractFileProvider provider, RarityNames rarities) =>
        ByRarity = RaritySelect.Read(provider, rarities, Widget, Function, Texture, Absence);

    public IReadOnlyDictionary<string, string> ByRarity { get; }

    private static string? Texture(KismetExpression term) =>
        term is EX_ObjectConst drawn && PropertyWalk.AsString(drawn.Value) is { } path
            ? AbilityPresentationIndex.AssetName(path)
            : null;
}
