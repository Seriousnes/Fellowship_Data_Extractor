using System.Text.Json;
using System.Text.Json.Nodes;

using Fellowship.SDK;
using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Models;

public static class AppliedChanges
{
    private static readonly string Cooldown = Field(nameof(AbilityDocument.Cooldown));

    private const string CooldownPreAdditives = "CooldownPreAdditives";
    private const string CooldownMultipliers = "CooldownMultipliers";
    private const string CooldownPostAdditives = "CooldownPostAdditives";

    private const string Additive = "Additive";
    private const string Multiplicitive = "Multiplicitive";
    private const string Division = "Division";
    private const string Override = "Override";
    private const string MultiplyCompound = "MultiplyCompound";
    private const string AddFinal = "AddFinal";

    private const string Set = "Set";
    private const string Add = "Add";
    private const string Subtract = "Subtract";
    private const string Multiply = "Multiply";

    private static readonly HashSet<string> OutputScales = new(["DamageScale", "HealScale"], StringComparer.Ordinal);

    private static readonly HashSet<string> AttributeOps =
        new([Additive, Multiplicitive, Division, Override, MultiplyCompound, AddFinal], StringComparer.Ordinal);

    private static string Field(string property) => JsonNamingPolicy.CamelCase.ConvertName(property);

    public static IReadOnlyList<Change> Toward(EntityDocument source, EntityDocument target) =>
        [.. ChangesOf(source).Where(change => Aims(change, target))];

    public static IReadOnlyList<ChangeGate> Gates(EntityDocument source, EntityDocument target) =>
    [
        .. Toward(source, target)
            .Where(change => Needed(change).Any())
            .Select(change => new ChangeGate(
                [.. Needed(change).Order()],
                change.While is not { Count: > 0 }))
            .DistinctBy(gate => string.Join(',', gate.Effects)),
    ];

    private static IEnumerable<int> Needed(Change change) =>
        (change.While ?? []).Concat(change.WhileTarget ?? []).Select(held => held.Id).Distinct();

    public static IReadOnlyList<EntityDocument> Sources(EntityDocument target, IEntityStoreService store) =>
    [
        .. DocumentFields.References(target, nameof(AbilityDocument.ChangedBy))
            .SelectMany(source => store.All(source.Type, source.Id).Take(1)),
    ];

    public static bool Changes(EntityDocument target, EntityDocument source, IReadOnlyList<int>? held)
    {
        var changed = Apply(target, [(source, held)]);

        return !ReferenceEquals(changed, target) && Serialized(changed) != Serialized(target);
    }

    public static EntityDocument Apply(
        EntityDocument target, IReadOnlyList<ChangeSource> sources, IEntityStoreService store) =>
        sources.Count == 0
            ? target
            : Apply(target, [.. sources.SelectMany(source =>
                store.All(source.Type, source.Id).Take(1).Select(document => (document, source.While)))]);

    private static EntityDocument Apply(
        EntityDocument target, IReadOnlyList<(EntityDocument Source, IReadOnlyList<int>? Held)> ticked)
    {
        var changes = ticked
            .SelectMany(entry => Toward(entry.Source, target)
                .Where(change => Holds(change, entry.Held))
                .Select(change => (Source: (EntityTypes.TypeOf(entry.Source), entry.Source.Id), Change: change)))
            .Distinct()
            .Select(entry => entry.Change)
            .ToArray();

        if (changes.Length == 0) return target;

        var document = JsonSerializer.SerializeToNode(target, EntityDocuments.Options)!.AsObject();

        foreach (var field in changes.GroupBy(change => change.Field, StringComparer.Ordinal))
        {
            var applied = field.ToArray();

            if (field.Key == Cooldown)
            {
                Write(document, field.Key, current => Cooled(current, applied));
            }
            else if (applied.All(change => AttributeOps.Contains(change.Op)))
            {
                if (OutputScales.Contains(field.Key)) Scale(document, field.Key, Aggregated(1, applied));
            }
            else
            {
                Write(document, field.Key, current => Stepped(current, applied));
            }
        }

        return document.Deserialize<EntityDocument>(EntityDocuments.Options)!;
    }

    public static double Cooled(double current, IReadOnlyList<Change> changes) =>
        (current + Sum(changes, CooldownPreAdditives)) * Product(changes, CooldownMultipliers) +
        Sum(changes, CooldownPostAdditives);

    public static double Aggregated(double current, IReadOnlyList<Change> changes)
    {
        if (changes.FirstOrDefault(change => change.Op == Override) is { } overridden) return overridden.Value;

        var multiplied = 1 + changes.Where(change => change.Op == Multiplicitive).Sum(change => change.Value - 1);
        var divided = 1 + changes.Where(change => change.Op == Division).Sum(change => change.Value - 1);

        if (Math.Abs(divided) <= 1e-8) divided = 1;

        return (current + Sum(changes, Additive)) * multiplied / divided * Product(changes, MultiplyCompound) +
            Sum(changes, AddFinal);
    }

    public static double Stepped(double current, IReadOnlyList<Change> changes)
    {
        if (changes.LastOrDefault(change => change.Op == Set) is { } set) return set.Value;

        return (current + Sum(changes, Add) - Sum(changes, Subtract)) * Product(changes, Multiply);
    }

    private static double Sum(IEnumerable<Change> changes, string op) =>
        changes.Where(change => change.Op == op).Sum(change => change.Value);

    private static double Product(IEnumerable<Change> changes, string op) =>
        changes.Where(change => change.Op == op).Aggregate(1d, (product, change) => product * change.Value);

    private static void Scale(JsonObject document, string attribute, double factor)
    {
        var coefficient = Field(nameof(Placeholder.Coefficient));
        var scalesOff = Field(nameof(Placeholder.ScalesOff));

        bool Names(JsonNode? scaled) =>
            scaled is JsonArray names && names.Any(named => named?.GetValue<string>() == attribute);

        var whole = Names(document[scalesOff]);

        foreach (var placeholder in Entries(document, Field(nameof(AbilityDocument.Placeholders))))
        {
            if (placeholder[coefficient]?.GetValue<double>() is not { } current) continue;
            if (placeholder[scalesOff] is { } own ? !Names(own) : !whole) continue;

            placeholder[coefficient] = current * factor;
        }
    }

    private static void Write(JsonObject owner, string field, Func<double, double> next)
    {
        if (owner[field]?.GetValue<double>() is not { } current)
        {
            if (field == Cooldown) return;

            current = 0;
        }

        owner[field] = next(current);
    }

    private static IEnumerable<JsonObject> Entries(JsonObject document, string field) =>
        document[field] is JsonArray entries ? entries.OfType<JsonObject>() : [];

    private static bool Holds(Change change, IReadOnlyList<int>? held) =>
        Needed(change).All(effect => held?.Contains(effect) is true);

    private static bool Aims(Change change, EntityDocument target) =>
        change.Target.Type == EntityTypes.TypeOf(target) && change.Target.Id == target.Id;

    private static IReadOnlyList<Change> ChangesOf(EntityDocument source) => source switch
    {
        TalentDocument talent => talent.Changes ?? [],
        LegendaryPowerDocument power => power.Changes ?? [],
        _ => [],
    };

    private static string Serialized(EntityDocument document) =>
        JsonSerializer.Serialize(document, EntityDocuments.Options);
}
