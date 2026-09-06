using System.Globalization;

namespace Fellowship.SDK;

public enum ModifierKind
{
    Trait,
    Blessing,
    Essence,
}

/// <summary>A modifier an item carries, written as <c>kind:id:amount</c>.</summary>
public readonly record struct ItemModifier(ModifierKind Kind, int Id, int Amount)
{
    private static readonly IReadOnlyDictionary<string, ModifierKind> Tokens =
        Enum.GetValues<ModifierKind>().ToDictionary(
            kind => kind.ToString(), kind => kind, StringComparer.OrdinalIgnoreCase);

    public static bool TryParse(string? value, IFormatProvider? provider, out ItemModifier result)
    {
        result = default;

        if (value is null) return false;

        var parts = value.Split(':');
        if (parts.Length != 3) return false;

        if (!Tokens.TryGetValue(parts[0], out var kind)) return false;

        if (!int.TryParse(parts[1], NumberStyles.Integer, provider, out var id)) return false;
        if (!int.TryParse(parts[2], NumberStyles.Integer, provider, out var amount)) return false;

        result = new ItemModifier(kind, id, amount);
        return true;
    }

    public override string ToString() =>
        $"{Kind.ToString().ToLowerInvariant()}:{Id.ToString(CultureInfo.InvariantCulture)}:" +
        Amount.ToString(CultureInfo.InvariantCulture);
}

/// <summary>A trait imbued into an item, written as <c>id:rank</c>.</summary>
public readonly record struct ImbuedTrait(int Id, int Rank)
{
    public static bool TryParse(string? value, IFormatProvider? provider, out ImbuedTrait result)
    {
        result = default;

        var separator = value?.IndexOf(':') ?? -1;
        if (value is null) return false;

        var netId = separator < 0 ? value : value[..separator];

        if (!int.TryParse(netId, NumberStyles.Integer, provider, out var trait)) return false;

        var rank = 1;
        if (separator >= 0 &&
            !int.TryParse(value.AsSpan((separator + 1)..), NumberStyles.Integer, provider, out rank))
        {
            return false;
        }

        result = new ImbuedTrait(trait, rank);
        return true;
    }

    public override string ToString() =>
        $"{Id.ToString(CultureInfo.InvariantCulture)}:{Rank.ToString(CultureInfo.InvariantCulture)}";
}
