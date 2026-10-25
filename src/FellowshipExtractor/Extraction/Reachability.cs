using System.Text.Json;
using System.Text.Json.Nodes;

using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

public static class Reachability
{
    private const string TypeField = "type";
    private const string IdField = "id";

    private static readonly IReadOnlySet<EntityType> Derived =
        new HashSet<EntityType> { EntityType.Creature, EntityType.Ability, EntityType.Effect };

    private static readonly IReadOnlySet<string> ReachedFrom =
        new HashSet<string>(StringComparer.Ordinal) { "owner", "appliedBy", "partOf", "foundIn" };

    private static readonly IReadOnlySet<string> Reaching =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "placements", "abilities", "passives", "grants", "spiritAbilityReferences", "legendaryStats", "talent",
            "targetCountFalloff",
        };

    public static IReadOnlySet<(EntityType Type, int Id)> Unreachable(IReadOnlyList<EntityDocument> documents)
    {
        var reachers = new Dictionary<(EntityType, int), HashSet<(EntityType, int)>>();
        var disabled = new Dictionary<(EntityType, int), bool>();

        foreach (var document in documents)
        {
            var key = (EntityTypes.TypeOf(document), document.Id);
            disabled[key] = disabled.GetValueOrDefault(key, true) && document.EnabledInGame is false;

            var body = JsonSerializer.SerializeToNode<EntityDocument>(document, EntityDocuments.Options)!.AsObject();

            foreach (var (field, value) in body)
            {
                if (ReachedFrom.Contains(field))
                {
                    foreach (var source in References(value)) Link(source, key);
                }
                else if (Reaching.Contains(field))
                {
                    foreach (var target in References(value)) Link(key, target);
                }
            }
        }

        var enabled = disabled.ToDictionary(pair => pair.Key, pair => !pair.Value);
        var changed = true;

        while (changed)
        {
            changed = false;

            foreach (var (key, sources) in reachers)
            {
                if (!Derived.Contains(key.Item1) || !enabled.GetValueOrDefault(key)) continue;
                if (sources.Any(source => enabled.GetValueOrDefault(source, true))) continue;

                enabled[key] = false;
                changed = true;
            }
        }

        return enabled
            .Where(pair => !pair.Value && !disabled[pair.Key])
            .Select(pair => pair.Key)
            .ToHashSet();

        void Link((EntityType, int) source, (EntityType, int) target)
        {
            if (source == target) return;
            if (!reachers.TryGetValue(target, out var sources)) reachers[target] = sources = [];
            sources.Add(source);
        }
    }

    private static IEnumerable<(EntityType, int)> References(JsonNode? node)
    {
        switch (node)
        {
            case JsonArray array:
                foreach (var item in array)
                {
                    foreach (var found in References(item)) yield return found;
                }

                break;

            case JsonObject reference when
                reference[TypeField]?.GetValueKind() is JsonValueKind.String &&
                reference[IdField]?.GetValueKind() is JsonValueKind.Number &&
                EntityTypes.TryParse(reference[TypeField]!.GetValue<string>(), out var type):
                yield return (type, reference[IdField]!.GetValue<int>());

                break;

            case JsonObject holder:
                foreach (var (_, value) in holder)
                {
                    foreach (var found in References(value)) yield return found;
                }

                break;
        }
    }
}
