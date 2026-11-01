using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Fellowship.SDK.Models;

namespace FellowshipExtractor.Tests;

/// <summary>Status of one literal number in a description, against the documents it is linked to.</summary>
public enum TooltipNumberStatus
{
    /// <summary>A modifier or change shows the number once its op converts the value.</summary>
    Typed,

    /// <summary>A constant, duration, count or other number without an op equals the number.</summary>
    Constant,

    /// <summary>A modifier labelled beside the number shows a different number.</summary>
    Contradicted,

    /// <summary>No linked document has the number.</summary>
    Missing,
}

public sealed record TooltipNumber(
    string Type, int Id, string? Name, string Number, TooltipNumberStatus Status, string Context, string Evidence)
{
    /// <summary>Key the baseline records the number under. The ordinal separates repeats in one description.</summary>
    public required int Ordinal { get; init; }

    public string Key => $"{Type}\t{Id}\t{Ordinal}\t{Number}";
}

/// <summary>
/// Every literal number in every description, against the numbers on the entity's document and the documents linked
/// to it: granted abilities, effects those apply, the ability it is part of and the ability applying it. A modifier
/// shows its value through its op, so an additive 1.6 printed as a percent reads 160 and never 60.
/// </summary>
public static partial class TooltipAudit
{
    private static readonly HashSet<string> FactorOps =
        new(["Multiplicitive", "MultiplyCompound", "Multiply", "CooldownMultipliers", "Division"], StringComparer.Ordinal);

    private static readonly HashSet<string> Skipped = new(
        ["id", "target", "while", "whileTarget", "owner", "talent", "appliedBy", "partOf", "changedBy", "nameBorrowedFrom",
         "textureBorrowedFrom", "grants", "row", "column", "icon", "color", "heroes", "effectTags"],
        StringComparer.Ordinal);

    private static readonly HashSet<EntityType> Unaudited =
        [EntityType.Item, EntityType.Hero, EntityType.Mount, EntityType.Currency, EntityType.Dungeon, EntityType.CompanionPet];

    [GeneratedRegex(@"(?<![\w.{])[+-]?(\d+(?:\.\d+)?)(%?)(?![\w}])")]
    private static partial Regex Numbers();

    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex Markup();

    [GeneratedRegex(@"\{[^}]*\}")]
    private static partial Regex Tokens();

    [GeneratedRegex(@"^\s*(?:/\s*[+-]?[\d.]+%?\s*)*([A-Za-z]+)")]
    private static partial Regex Following();

    [GeneratedRegex("(?<=[a-z])(?=[A-Z])")]
    private static partial Regex CamelBreak();

    private sealed record Shown(double Value, string Label, string Op, double Raw);

    public static IReadOnlyList<TooltipNumber> Run(IReadOnlyList<(EntityType Type, int Id, string? Name, JsonObject Body)> documents)
    {
        var byKey = new Dictionary<(EntityType, int), JsonObject>();
        var appliedBy = new Dictionary<(EntityType, int), List<JsonObject>>();

        foreach (var (type, id, _, body) in documents)
        {
            byKey.TryAdd((type, id), body);

            if (Reference(body["appliedBy"]) is { } applier)
            {
                if (!appliedBy.TryGetValue(applier, out var applied)) appliedBy[applier] = applied = [];
                applied.Add(body);
            }
        }

        var found = new List<TooltipNumber>();

        foreach (var (type, id, name, body) in documents)
        {
            if (Unaudited.Contains(type)) continue;
            if (body["description"]?.GetValue<string>() is not { Length: > 0 } description) continue;

            var plain = Tokens().Replace(Markup().Replace(description, ""), "{}").Replace('\r', ' ').Replace('\n', ' ');
            var shown = new List<Shown>();
            var constants = new HashSet<double>();

            foreach (var linked in Linked((type, id), body, byKey, appliedBy)) Walk(linked, shown, constants);

            var ordinal = 0;

            foreach (Match match in Numbers().Matches(plain))
            {
                var number = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                if (number is 0 or 1) continue;

                var percent = match.Groups[2].Value == "%";
                var unit = percent ? "%" : Unit(plain, match.Index + match.Length);
                var window = plain[Math.Max(0, match.Index - 32)..Math.Min(plain.Length, match.Index + match.Length + 32)];

                var typed = shown.Where(item => Near(item.Value, number)).ToList();
                var contradicting = shown
                    .Where(item => !Near(item.Value, number) && Mentions(window, item.Label) &&
                                   !(unit == "second" && item.Label != "duration"))
                    .ToList();

                var status = typed.Count > 0 ? TooltipNumberStatus.Typed
                    : contradicting.Count > 0 ? TooltipNumberStatus.Contradicted
                    : constants.Any(value => Readings(value, percent).Any(reading => Near(reading, number)))
                        ? TooltipNumberStatus.Constant
                        : TooltipNumberStatus.Missing;

                var evidence = (status == TooltipNumberStatus.Contradicted ? contradicting : typed)
                    .Select(item => $"{item.Label} {item.Op} {Format(item.Raw)}")
                    .Distinct()
                    .ToArray();

                var start = Math.Max(0, match.Index - 48);
                var context = plain[start..Math.Min(plain.Length, match.Index + match.Length + 48)].Trim();

                found.Add(new TooltipNumber(EntityTypes.Slug(type), id, name, match.Value, status, context, string.Join("; ", evidence))
                {
                    Ordinal = ordinal++,
                });
            }
        }

        return found;
    }

    private static (EntityType, int)? Reference(JsonNode? node) =>
        node is JsonObject reference && EntityTypes.TryParse(reference["type"]?.GetValue<string>() ?? "", out var type)
            ? (type, reference["id"]!.GetValue<int>())
            : null;

    private static IEnumerable<JsonObject> Linked(
        (EntityType, int) self, JsonObject body,
        Dictionary<(EntityType, int), JsonObject> byKey, Dictionary<(EntityType, int), List<JsonObject>> appliedBy)
    {
        var linked = new HashSet<JsonObject>(ReferenceEqualityComparer.Instance) { body };

        linked.UnionWith(appliedBy.GetValueOrDefault(self) ?? []);

        foreach (var granted in body["grants"]?.AsArray() ?? [])
        {
            if (Reference(granted) is not { } key) continue;
            if (byKey.GetValueOrDefault(key) is { } ability) linked.Add(ability);
            linked.UnionWith(appliedBy.GetValueOrDefault(key) ?? []);
        }

        foreach (var field in (string[])["partOf", "appliedBy"])
        {
            if (Reference(body[field]) is { } key && byKey.GetValueOrDefault(key) is { } held) linked.Add(held);
        }

        return linked;
    }

    private static void Walk(JsonNode? node, List<Shown> shown, HashSet<double> constants)
    {
        switch (node)
        {
            case JsonObject held:
                var op = held["op"]?.GetValue<string>();

                if (op is not null)
                {
                    var label = (held["field"] ?? held["attribute"])?.GetValue<string>() ?? "";
                    var percent = held["percent"]?.GetValue<bool>() == true;

                    foreach (var value in Values(held["value"]).Concat(Values(held["ranks"])))
                    {
                        foreach (var reading in ShownBy(op, value, percent)) shown.Add(new Shown(reading, label, op, value));
                        if (op == "Set") constants.Add(value);
                    }
                }

                foreach (var (key, child) in held)
                {
                    if (Skipped.Contains(key) || (op is not null && key is "value" or "ranks")) continue;
                    Walk(child, shown, constants);
                }

                break;

            case JsonArray items:
                foreach (var item in items) Walk(item, shown, constants);
                break;

            case JsonValue value when value.TryGetValue<double>(out var number):
                constants.Add(number);
                break;
        }
    }

    private static IEnumerable<double> Values(JsonNode? node) => node switch
    {
        JsonValue value when value.TryGetValue<double>(out var number) => [number],
        JsonArray items => items.SelectMany(Values),
        _ => [],
    };

    private static IEnumerable<double> ShownBy(string op, double value, bool percent)
    {
        if (FactorOps.Contains(op))
        {
            var factor = op == "Division" && value != 0 ? 1 / value : value;
            yield return Math.Abs((factor - 1) * 100);
            yield break;
        }

        if (op == "Set")
        {
            yield return value;
            yield break;
        }

        yield return Math.Abs(percent ? value * 100 : value);
        if (!percent) yield return Math.Abs(value * 100);
    }

    private static IEnumerable<double> Readings(double value, bool percent)
    {
        yield return Math.Abs(value);
        yield return Math.Abs(value * 100);

        if (!percent) yield break;

        yield return Math.Abs((value - 1) * 100);
        if (value != 0) yield return Math.Abs((1 / value - 1) * 100);
    }

    private static bool Near(double first, double second) =>
        Math.Abs(first - second) <= Math.Max(0.011, Math.Abs(second) * 0.005);

    private static string Unit(string text, int after)
    {
        var match = Following().Match(text[after..]);
        var word = match.Success ? match.Groups[1].Value.ToLowerInvariant() : "";

        return word.StartsWith("sec", StringComparison.Ordinal) ? "second" : word.TrimEnd('s');
    }

    private static bool Mentions(string window, string label)
    {
        var words = CamelBreak().Replace(label, " ").ToLowerInvariant();
        return words.Length > 4 && window.Contains(words, StringComparison.OrdinalIgnoreCase);
    }

    private static string Format(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}
