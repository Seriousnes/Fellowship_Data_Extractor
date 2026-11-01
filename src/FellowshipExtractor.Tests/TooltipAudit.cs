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

    /// <summary>
    /// Only a linked document has the number, so the entity's own page does not show it. The evidence names the
    /// document.
    /// </summary>
    Elsewhere,

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
         "textureBorrowedFrom", "grants", "applies", "parentClass", "row", "column", "icon", "color", "heroes", "effectTags",
         "ticks", "result", "atMaxStacks", "setByCallerScaling"],
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
        var family = new Dictionary<(EntityType, int), List<(EntityType, int)>>();
        var appliedBy = new Dictionary<(EntityType, int), List<(EntityType, int)>>();

        foreach (var (type, id, _, body) in documents)
        {
            byKey.TryAdd((type, id), body);

            foreach (var field in FamilyFields)
            {
                if (Reference(body[field]) is not { } kin) continue;
                Index(family, kin, (type, id));
                Index(family, (type, id), kin);
            }

            if (Reference(body["appliedBy"]) is { } applier) Index(appliedBy, applier, (type, id));
        }

        var found = new List<TooltipNumber>();

        foreach (var (type, id, name, body) in documents)
        {
            if (Unaudited.Contains(type)) continue;
            if (body["description"]?.GetValue<string>() is not { Length: > 0 } description) continue;

            var plain = Tokens().Replace(Markup().Replace(description, ""), "{}").Replace('\r', ' ').Replace('\n', ' ');
            var shown = new List<Shown>();
            var constants = new HashSet<double>();
            var elsewhere = new List<(Shown? Typed, double? Constant, string Source)>();
            var derived = Derived(body);
            var derivedElsewhere = new List<(Shown Derived, string Source)>();

            Walk(body, shown, constants);

            foreach (var linked in Linked((type, id), byKey, family, appliedBy))
            {
                if (ReferenceEquals(linked, body)) continue;

                var linkedShown = new List<Shown>();
                var linkedConstants = new HashSet<double>();
                var source = $"{linked["$type"]?.GetValue<string>()} {linked["id"]}";

                Walk(linked, linkedShown, linkedConstants);
                elsewhere.AddRange(linkedShown.Select(item => ((Shown?)item, (double?)null, source)));
                elsewhere.AddRange(linkedConstants.Select(value => ((Shown?)null, (double?)value, source)));
                derivedElsewhere.AddRange(Derived(linked).Select(item => (item, source)));
            }

            var ordinal = 0;

            foreach (Match match in Numbers().Matches(plain))
            {
                var number = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                if (number is 0 or 1) continue;

                var percent = match.Groups[2].Value == "%";
                var unit = percent ? "%" : Unit(plain, match.Index + match.Length);
                var window = plain[Math.Max(0, match.Index - 32)..Math.Min(plain.Length, match.Index + match.Length + 32)];

                bool Reads(double value) => Readings(value, percent).Any(reading => Near(reading, number));

                var typed = shown.Where(item => Near(item.Value, number)).ToList();
                var contradicting = shown
                    .Concat(elsewhere.Select(entry => entry.Typed).OfType<Shown>())
                    .Where(item => !Near(item.Value, number) && Mentions(window, item.Label) &&
                                   Measures(unit, item.Label, item.Op) && SameClause(plain, match.Index, item.Label))
                    .ToList();
                var linkedTyped = elsewhere.Where(entry => entry.Typed is { } item && Near(item.Value, number)).ToList();
                var linkedConstant = elsewhere.Where(entry => entry.Constant is { } value && Reads(value)).ToList();

                var constant = constants.Any(Reads);

                var status = typed.Count > 0 ? TooltipNumberStatus.Typed
                    : linkedTyped.Count > 0 ? constant ? TooltipNumberStatus.Constant : TooltipNumberStatus.Elsewhere
                    : contradicting.Count > 0 ? TooltipNumberStatus.Contradicted
                    : constant ? TooltipNumberStatus.Constant
                    : linkedConstant.Count > 0 ? TooltipNumberStatus.Elsewhere
                    : TooltipNumberStatus.Missing;

                bool Explains(Shown item) =>
                    Near(item.Value, number) &&
                    (status == TooltipNumberStatus.Missing || SameClause(plain, match.Index, item.Label));

                var computed = status is TooltipNumberStatus.Missing or TooltipNumberStatus.Contradicted
                    ? derived.Where(Explains).ToList()
                    : [];
                var computedElsewhere = status is TooltipNumberStatus.Missing or TooltipNumberStatus.Contradicted && computed.Count == 0
                    ? derivedElsewhere.Where(entry => Explains(entry.Derived)).ToList()
                    : [];

                if (computed.Count > 0) status = TooltipNumberStatus.Typed;
                else if (computedElsewhere.Count > 0) status = TooltipNumberStatus.Elsewhere;

                var evidence = status switch
                {
                    TooltipNumberStatus.Contradicted => contradicting.Select(item => $"{item.Label} {item.Op} {Format(item.Raw)}"),
                    TooltipNumberStatus.Elsewhere when computedElsewhere.Count > 0 =>
                        computedElsewhere.Select(entry => $"{entry.Source}: {entry.Derived.Label} {entry.Derived.Op} {Format(entry.Derived.Raw)}"),
                    TooltipNumberStatus.Elsewhere => linkedTyped.Count > 0
                        ? linkedTyped.Select(entry => $"{entry.Source}: {entry.Typed!.Label} {entry.Typed.Op} {Format(entry.Typed.Raw)}")
                        : linkedConstant.Select(entry => $"{entry.Source}: {Format(entry.Constant!.Value)}"),
                    _ when computed.Count > 0 => computed.Select(item => $"{item.Label} {item.Op} {Format(item.Raw)}"),
                    _ => typed.Select(item => $"{item.Label} {item.Op} {Format(item.Raw)}"),
                };

                evidence = evidence.Distinct().Take(6).ToArray();

                var start = Math.Max(0, match.Index - 48);
                var context = plain[start..Math.Min(plain.Length, match.Index + match.Length + 48)].Trim();

                found.Add(new TooltipNumber(EntityTypes.Slug(type), id, name, match.Value, status, context, string.Join("; ", evidence.ToArray()))
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

    private static IEnumerable<(EntityType, int)> References(JsonNode? node) =>
        node is JsonArray items ? items.Select(Reference).OfType<(EntityType, int)>() : [];

    private static readonly string[] FamilyFields = ["partOf", "namedAfter"];

    private static void Index<TKey>(Dictionary<TKey, List<(EntityType, int)>> index, TKey key, (EntityType, int) value)
        where TKey : notnull
    {
        if (!index.TryGetValue(key, out var held)) index[key] = held = [];
        if (!held.Contains(value)) held.Add(value);
    }

    /// <summary>
    /// The entity's family (every document joined to it through partOf or namedAfter, in either direction), the
    /// abilities and effects a family member grants, applies, derives its class from or is applied by, the effects a
    /// granted ability applies, the family of each ability reached, every effect any of those apply, and the targets
    /// of the entity's own changes.
    /// </summary>
    private static IEnumerable<JsonObject> Linked(
        (EntityType, int) self,
        Dictionary<(EntityType, int), JsonObject> byKey,
        Dictionary<(EntityType, int), List<(EntityType, int)>> family,
        Dictionary<(EntityType, int), List<(EntityType, int)>> appliedBy)
    {
        var members = Family(self, family);

        var reached = new HashSet<(EntityType, int)>(members);

        foreach (var member in members)
        {
            if (byKey.GetValueOrDefault(member) is not { } body) continue;

            reached.UnionWith(References(body["grants"]));
            reached.UnionWith(References(body["applies"]));

            if (Reference(body["appliedBy"]) is { } applier) reached.Add(applier);
            if (Reference(body["parentClass"]) is { } parent) reached.Add(parent);
        }

        foreach (var holder in reached.ToArray())
        {
            if (members.Contains(holder) || holder.Item1 != EntityType.Ability) continue;

            reached.UnionWith(Family(holder, family));
            reached.UnionWith(References(byKey.GetValueOrDefault(holder)?["applies"]));
        }

        foreach (var holder in reached.ToArray()) reached.UnionWith(appliedBy.GetValueOrDefault(holder) ?? []);

        foreach (var change in byKey.GetValueOrDefault(self)?["changes"]?.AsArray() ?? [])
        {
            if (Reference(change?["target"]) is { } target) reached.Add(target);
        }

        return reached.Select(key => byKey.GetValueOrDefault(key)).OfType<JsonObject>();
    }

    /// <summary>Every document joined to <paramref name="self"/> through partOf or namedAfter, in either direction.</summary>
    private static HashSet<(EntityType, int)> Family(
        (EntityType, int) self, Dictionary<(EntityType, int), List<(EntityType, int)>> family)
    {
        var members = new HashSet<(EntityType, int)> { self };
        var pending = new Queue<(EntityType, int)>(members);

        while (pending.TryDequeue(out var member))
        {
            foreach (var kin in family.GetValueOrDefault(member) ?? [])
            {
                if (members.Add(kin)) pending.Enqueue(kin);
            }
        }

        return members;
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

    /// <summary>
    /// Numbers the game computes from one document's own fields: a modifier at the effect's maximum stacks, a rank
    /// run through its transform, a change's resulting value, a resource gain, a set-by-caller magnitude at each
    /// count, and a per-tick constant over its group's duration. A product reads only fields of the one document,
    /// because products across documents match almost any number. A computed number can explain a number nothing
    /// else does, and never contradicts one.
    /// </summary>
    private static List<Shown> Derived(JsonObject body)
    {
        var derived = new List<Shown>();

        void Add(double value, string label, string op, double raw)
        {
            foreach (var reading in new[] { value, value * 100, (value - 1) * 100 }.Select(Math.Abs).Distinct())
            {
                derived.Add(new Shown(reading, label, op, raw));
            }
        }

        if (Number(body["maxStacks"]) is { } stacks && stacks > 1)
        {
            foreach (var modifier in body["modifiers"]?.AsArray() ?? [])
            {
                if (modifier?["op"]?.GetValue<string>() is not { } op || Number(modifier["value"]) is not { } value) continue;

                var label = modifier["attribute"]?.GetValue<string>() ?? "";
                var stacked = op switch
                {
                    "Additive" => value * stacks,
                    "Multiplicitive" => 1 + (value - 1) * stacks,
                    "MultiplyCompound" or "Multiply" => Math.Pow(value, stacks),
                    _ => (double?)null,
                };

                if (stacked is not { } total || !double.IsFinite(total)) continue;

                foreach (var reading in ShownBy(op, total, false)) derived.Add(new Shown(reading, label, $"{op} x{Format(stacks)} stacks", total));
            }
        }

        foreach (var run in body["rankRuns"]?.AsArray() ?? [])
        {
            if (run?["transform"]?.GetValue<string>() is not { } transform) continue;

            var ticks = Number(run["ticks"]) ?? 1;
            var label = run["key"]?.GetValue<string>() ?? "";

            foreach (var value in Values(run["values"]))
            {
                var printed = transform switch
                {
                    "raw" => value * 100,
                    "delta" => (value - 1) * 100,
                    "reduction" => (1 - value) * 100,
                    "ticks" => value * ticks * 100,
                    _ => value,
                };

                derived.Add(new Shown(Math.Abs(printed), label, transform, value));
            }
        }

        foreach (var change in body["changes"]?.AsArray() ?? [])
        {
            if (Number(change?["result"]) is not { } result) continue;

            derived.Add(new Shown(Math.Abs(result), change!["field"]?.GetValue<string>() ?? "", "result", result));
        }

        foreach (var gain in body["generates"]?.AsArray() ?? [])
        {
            var label = (gain?["resourceName"] ?? gain?["resource"])?.GetValue<string>() ?? "";

            foreach (var amount in new[] { Number(gain?["amount"]), Number(gain?["amountOnCritical"]) }.OfType<double>())
            {
                derived.Add(new Shown(Math.Abs(amount), label, "generates", amount));
            }
        }

        foreach (var scaling in body["setByCallerScaling"]?.AsArray() ?? [])
        {
            var label = scaling?["magnitude"]?.GetValue<string>() is { } magnitude ? magnitude[(magnitude.LastIndexOf('.') + 1)..] : "";

            foreach (var key in new[] { "base", "per", "every" })
            {
                if (Number(scaling?[key]) is { } value) derived.Add(new Shown(Math.Abs(value), label, key, value));
            }

            var count = 0;

            foreach (var value in Values(scaling?["values"]))
            {
                derived.Add(new Shown(++count, label, "count", count));
                Add(value, label, $"at {count}", value);
            }
        }

        if (body["constants"] is JsonObject held)
        {
            var rows = held
                .Select(entry => (Key: entry.Key, Value: Number(entry.Value)))
                .Where(entry => entry.Value is not null)
                .GroupBy(entry => entry.Key[..Math.Max(0, entry.Key.LastIndexOf('.'))], StringComparer.Ordinal);

            foreach (var group in rows)
            {
                double? Leaf(params string[] names) => group
                    .FirstOrDefault(entry => names.Any(name => entry.Key.EndsWith("." + name, StringComparison.Ordinal)))
                    .Value;

                if (Leaf("Duration") is not { } duration || Leaf("TickInterval", "TickPeriod", "Period") is not { } period ||
                    period <= 0)
                {
                    continue;
                }

                var ticks = Math.Floor(duration / period + 1e-9);

                foreach (var (key, value) in group.Where(entry => entry.Key.EndsWith("Coefficient", StringComparison.Ordinal)))
                {
                    derived.Add(new Shown(Math.Abs(value!.Value * ticks * 100), key, $"per tick x{Format(ticks)} ticks", value.Value));
                }
            }
        }

        if (Number(body["ticks"]) is { } effectTicks)
        {
            foreach (var placeholder in body["placeholders"]?.AsArray() ?? [])
            {
                if (Number(placeholder?["coefficient"]) is not { } coefficient) continue;

                var label = placeholder!["attribute"]?.GetValue<string>() ?? "";
                derived.Add(new Shown(Math.Abs(coefficient * effectTicks * 100), label, $"per tick x{Format(effectTicks)} ticks", coefficient));
            }
        }

        return derived;
    }

    private static double? Number(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<double>(out var number) ? number : null;

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

    private static readonly HashSet<string> Timed =
        new(["duration", "cooldown", "castTime", "channelTime", "channelTick", "period"], StringComparer.Ordinal);

    /// <summary>
    /// True when a number printed in <paramref name="unit"/> can be the value of <paramref name="label"/>. A bare
    /// number can be any label's, and a percent any label's but a timed field a Set op gives in seconds. A number in
    /// seconds is a timed field's, and a number in any other unit word is the label's only where the label names that
    /// word, so "1.2 PPM" beside a Haste rating is no Haste rating.
    /// </summary>
    private static bool Measures(string unit, string label, string op) => unit switch
    {
        "" => true,
        "%" => op != "Set" || !Timed.Contains(label),
        "second" => Timed.Contains(label),
        _ => CamelBreak().Replace(label, " ").Split(' ').Any(word => word.Equals(unit, StringComparison.OrdinalIgnoreCase)),
    };

    [GeneratedRegex(@"[,.;:()]|\s(?:and|but|or)\s")]
    private static partial Regex ClauseBreak();

    /// <summary>
    /// True when <paramref name="label"/> is in the clause holding the number at <paramref name="at"/>, so "+20% Spirit
    /// and 2% max mana" gives Spirit to the 20% and not the 2%. A comma, period, semicolon, colon, parenthesis, "and",
    /// "but" or "or" ends a clause; a decimal point inside a number does not.
    /// </summary>
    private static bool SameClause(string text, int at, string label)
    {
        var start = 0;
        var end = text.Length;

        foreach (Match boundary in ClauseBreak().Matches(text))
        {
            if (boundary.Value == "." && boundary.Index + 1 < text.Length && char.IsDigit(text[boundary.Index + 1])) continue;
            if (boundary.Index < at) start = boundary.Index + boundary.Length;
            else { end = boundary.Index; break; }
        }

        return text[start..end].Contains(CamelBreak().Replace(label, " "), StringComparison.OrdinalIgnoreCase);
    }

    private static bool Mentions(string window, string label)
    {
        var words = CamelBreak().Replace(label, " ").ToLowerInvariant();
        return words.Length > 4 && window.Contains(words, StringComparison.OrdinalIgnoreCase);
    }

    private static string Format(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}
