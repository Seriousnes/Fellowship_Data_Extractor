using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Kismet;

namespace FellowshipExtractor.Extraction;

public sealed class RarityColorRows
{
    public const string Library = "BPL_DS_ColorsFunctions";
    public const string Function = "GetNamedColorFromItemRarity";

    private const string NamedColorStruct = "CRNamedColor";
    private const string RowHandleStruct = "DataTableRowHandle";

    private const string Absence =
        "Its select over the rarity ladder is the only thing that says which named colour a rung prints in.";

    public RarityColorRows(AbstractFileProvider provider, RarityNames rarities)
    {
        ByRarity = RaritySelect.Read(provider, rarities, Library, Function, Row, Absence);

        var silent = rarities.Tiers
            .Select(tier => rarities.Label(tier))
            .OfType<string>()
            .Where(name => !ByRarity.ContainsKey(name))
            .ToArray();

        if (silent.Length > 0)
        {
            throw new InvalidOperationException(
                $"{Source} names no colour for {string.Join(" or ", silent)}, so those rungs would print in no " +
                "colour at all.");
        }
    }

    public IReadOnlyDictionary<string, string> ByRarity { get; }

    public string Source => $"{Library}.{Function}";

    private static string? Row(KismetExpression term) =>
        term is EX_StructConst { Struct.Name: NamedColorStruct } colour && Handle(colour) is { } handle
            ? handle.Properties.OfType<EX_NameConst>().FirstOrDefault()?.Value.Text
            : null;

    private static EX_StructConst? Handle(EX_StructConst colour) =>
        colour.Properties
            .OfType<EX_StructConst>()
            .FirstOrDefault(member => member.Struct.Name.Equals(RowHandleStruct, StringComparison.Ordinal));
}
