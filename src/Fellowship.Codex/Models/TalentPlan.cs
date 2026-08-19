using System.Globalization;
using System.Text;

using Fellowship.SDK.Documents;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Models;

public sealed record TalentNode(TalentSlotDocument Slot, TalentDocument Talent)
{
    public int Id => Talent.Id;

    public int Cost => Slot.Cost;
}

public sealed record TalentRow(int Number, string Numeral, IReadOnlyList<TalentNode> Nodes);

public static class TalentPlan
{
    public const int Points = 14;

    public static IReadOnlyList<TalentRow> Tree(IEntityStoreService store, Hero hero) =>
    [
        .. store.Tree(hero)
            .GroupBy(slot => slot.Row)
            .OrderBy(row => row.Key)
            .Select(row => new TalentRow(
                row.Key,
                Numeral(row.Key),
                [
                    .. row.OrderBy(slot => slot.Column)
                        .Select(slot => Node(store, slot))
                        .OfType<TalentNode>(),
                ]))
            .Where(row => row.Nodes.Count > 0),
    ];

    public static IReadOnlySet<int> Read(string? picks, IReadOnlyList<TalentRow> tree)
    {
        if (picks is not { Length: > 0 }) return new HashSet<int>();

        var offered = tree.SelectMany(row => row.Nodes).Select(node => node.Id).ToHashSet();
        var taken = new HashSet<int>();

        foreach (var part in picks.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) &&
                offered.Contains(id))
            {
                taken.Add(id);
            }
        }

        return taken;
    }

    public static string Written(IReadOnlyList<TalentRow> tree, IReadOnlySet<int> taken) =>
        string.Join(
            ',',
            tree.SelectMany(row => row.Nodes)
                .Where(node => taken.Contains(node.Id))
                .Select(node => node.Id.ToString(CultureInfo.InvariantCulture)));

    public static int Spent(IReadOnlyList<TalentRow> tree, IReadOnlySet<int> taken) =>
        tree.SelectMany(row => row.Nodes).Where(node => taken.Contains(node.Id)).Sum(node => node.Cost);

    public static string Numeral(int number)
    {
        if (number < 1) return number.ToString(CultureInfo.InvariantCulture);

        var written = new StringBuilder();
        var left = number;

        foreach (var (value, numeral) in Numerals)
        {
            while (left >= value)
            {
                written.Append(numeral);
                left -= value;
            }
        }

        return written.ToString();
    }

    private static readonly (int Value, string Numeral)[] Numerals =
    [
        (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"),
        (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I"),
    ];

    private static TalentNode? Node(IEntityStoreService store, TalentSlotDocument slot) =>
        store.All(EntityType.Talent, slot.Talent.Id).OfType<TalentDocument>().FirstOrDefault() is { } talent
            ? new TalentNode(slot, talent)
            : null;
}
