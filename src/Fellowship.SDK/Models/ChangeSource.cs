using System.Globalization;
using System.Text.Json.Serialization;

using Fellowship.SDK.Models;

namespace Fellowship.SDK;

public readonly record struct ChangeSource(
    [property: JsonConverter(typeof(EntityTypeSlugConverter))] EntityType Type,
    int Id)
{
    private const char Gate = '~';

    private const string Effect = "effect";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<int>? While { get; init; }

    public static bool TryParse(string? value, IFormatProvider? provider, out ChangeSource result)
    {
        result = default;

        if (value is null) return false;

        var gates = value.Split(Gate);

        if (gates[0].Split(':') is not [var slug, var number]) return false;
        if (!EntityTypes.TryParse(slug, out var type)) return false;
        if (!int.TryParse(number, NumberStyles.None, provider, out var id)) return false;

        var held = new List<int>();

        foreach (var gate in gates[1..])
        {
            if (gate.Split(':') is not [Effect, var effect]) return false;
            if (!int.TryParse(effect, NumberStyles.None, provider, out var effectId)) return false;

            held.Add(effectId);
        }

        result = new ChangeSource(type, id) { While = held.Count > 0 ? [.. held.Order()] : null };
        return true;
    }

    public bool Equals(ChangeSource other) =>
        Type == other.Type && Id == other.Id && (While ?? []).SequenceEqual(other.While ?? []);

    public override int GetHashCode() => HashCode.Combine(Type, Id, Gated(While));

    public override string ToString() =>
        $"{EntityTypes.Slug(Type)}:{Id.ToString(CultureInfo.InvariantCulture)}{Gated(While)}";

    private static string Gated(IReadOnlyList<int>? held) =>
        string.Concat((held ?? []).Select(effect => $"{Gate}{Effect}:{effect.ToString(CultureInfo.InvariantCulture)}"));
}
