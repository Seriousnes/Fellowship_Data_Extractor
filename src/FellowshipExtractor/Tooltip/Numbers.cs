namespace FellowshipExtractor.Tooltip;

public sealed record SplitNumbers(
    Dictionary<string, double> Named,
    Dictionary<string, object?> Rest,
    HashSet<string> Rows,
    HashSet<string> Own,
    Dictionary<string, double> Promoted);

public static class Numbers
{
    private const string RefSuffix = "ref";
    private const string CostSuffix = "cost";

    private static readonly Dictionary<string, string> Fields = new(StringComparer.Ordinal)
    {
        ["castingdata.castingduration"] = "castTime",
        ["targetedcastingdata.castingduration"] = "castTime",
        ["casttime"] = "castTime",
        ["channelingdata.channelingduration"] = "channelTime",
        ["channeltime"] = "channelTime",
        ["channelingdata.channelingtickinterval"] = "channelTick",
        ["cooldownduration"] = "cooldown",
        ["cooldown"] = "cooldown",
        ["baselinechargecount"] = "charges",
        ["maxcharges"] = "charges",
        ["maxnumcharges"] = "charges",
        ["numcharges"] = "charges",
        ["numofcharges"] = "charges",
        ["maxrange"] = "range",
        ["rangehorizontal"] = "range",
        ["range"] = "range",
        ["radius"] = "radius",
        ["aoeradius"] = "radius",
        ["maxradius"] = "radius",
        ["duration"] = "duration",
        ["durationmagnitude.scalablefloatmagnitude"] = "duration",
        ["period"] = "period",
        ["tickinterval"] = "period",
    };

    public const string Charges = "charges";
    public const string Period = "period";
    public const string Duration = "duration";
    public const string ChannelTick = "channelTick";

    public static double? Own(SplitNumbers split, string field)
    {
        var readings = Spellings(field)
            .Select(name => split.Named.TryGetValue(name, out var value) ? value : (double?)null)
            .OfType<double>()
            .Distinct()
            .ToArray();

        return readings.Length == 1 ? readings[0] : null;
    }

    private static string[] Spellings(string field) =>
        field == Period ? [Period, ChannelTick] : [field];

    public static SplitNumbers Split(
        IEnumerable<CurveBinding> bindings,
        string? tuningNamespace,
        bool usesCharges = true,
        Func<string, bool>? chargeFromItem = null)
    {
        var named = new Dictionary<string, double>(StringComparer.Ordinal);
        var rest = new Dictionary<string, object?>(StringComparer.Ordinal);
        var rows = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var own = new HashSet<string>(StringComparer.Ordinal);
        var promoted = new Dictionary<string, double>(StringComparer.Ordinal);

        var all = bindings as IReadOnlyCollection<CurveBinding> ?? [.. bindings];
        var claimed = new Dictionary<string, CurveBinding>(StringComparer.Ordinal);

        foreach (var binding in all)
        {
            if (binding.Values is not null || binding.Value is null) continue;

            var relative = TuningNamespace.Relative(binding.RowName, tuningNamespace);
            var under = relative.Length < binding.RowName.Length;
            var shallow = under && !relative.Contains('.');

            var field = Field(binding.Property) ?? (shallow ? Field(relative) : null);

            if (field is null || (field == Charges && !usesCharges)) continue;
            if (!claimed.TryAdd(field, binding)) continue;

            named[field] = binding.Value.Value;

            if (under)
            {
                promoted[relative] = binding.Value.Value;
                own.Add(relative);
            }
        }

        if (usesCharges && !named.ContainsKey(Charges))
        {
            foreach (var binding in all)
            {
                if (binding.Values is not null || binding.Value is null) continue;

                var relative = TuningNamespace.Relative(binding.RowName, tuningNamespace);
                if (relative.Length >= binding.RowName.Length) continue;

                var leaf = relative[(relative.LastIndexOf('.') + 1)..];
                if (Field(leaf) != Charges) continue;
                if (chargeFromItem?.Invoke(binding.RowName) == true) continue;

                claimed[Charges] = binding;
                named[Charges] = binding.Value.Value;
                break;
            }
        }

        var claimedRows = new HashSet<string>(
            claimed.Values.Select(b => b.RowName), StringComparer.OrdinalIgnoreCase);

        foreach (var binding in all)
        {
            rows.Add(binding.RowName);
            if (claimedRows.Contains(binding.RowName)) continue;

            Place(rest, own, binding.RowName, Value(binding), tuningNamespace);
        }

        return new SplitNumbers(named, rest, rows, own, promoted);
    }

    public static void Add(Dictionary<string, object?> constants, CurveBinding binding, string? tuningNamespace) =>
        constants[TuningNamespace.Relative(binding.RowName, tuningNamespace)] = Value(binding);

    public static void Add(SplitNumbers split, CurveBinding binding, string? tuningNamespace)
    {
        split.Rows.Add(binding.RowName);
        Place(split.Rest, split.Own, binding.RowName, Value(binding), tuningNamespace);
    }

    private static void Place(
        Dictionary<string, object?> rest,
        HashSet<string> own,
        string rowName,
        object? value,
        string? tuningNamespace)
    {
        var relative = TuningNamespace.Relative(rowName, tuningNamespace);
        rest[relative] = value;

        if (relative.Length < rowName.Length) own.Add(relative);
    }

    private static readonly string[] TalentSegments = ["Talent", "Talents"];

    public static (Dictionary<string, object?> Baseline, Dictionary<string, Dictionary<string, object?>> Talents)
        SplitTalents(Dictionary<string, object?> constants)
    {
        var baseline = new Dictionary<string, object?>(StringComparer.Ordinal);
        var talents = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);

        foreach (var (key, value) in constants)
        {
            var segments = key.Split('.');
            var at = Array.FindIndex(segments, s => TalentSegments.Contains(s, StringComparer.Ordinal));

            if (at < 0 || at + 2 >= segments.Length)
            {
                baseline[key] = value;
                continue;
            }

            var name = segments[at + 1];
            var field = string.Join('.', segments[(at + 2)..]);

            if (!talents.TryGetValue(name, out var fields)) talents[name] = fields = new(StringComparer.Ordinal);

            if (!fields.TryAdd(field, value)) baseline[key] = value;
        }

        return (baseline, talents);
    }

    private static object? Value(CurveBinding binding) =>
        binding.Values is { Length: > 1 } ranks ? ranks : binding.Value;

    public static (string Key, double Value)? SoleField(
        IReadOnlyDictionary<string, object?> constants,
        IReadOnlySet<string> own,
        IReadOnlySet<string> accountedFor,
        string field)
    {
        (string Key, double Value)? found = null;

        foreach (var (key, value) in constants)
        {
            if (value is not double number) continue;
            if (!own.Contains(key) || accountedFor.Contains(key)) continue;
            if (Field(key[(key.LastIndexOf('.') + 1)..]) != field) continue;

            if (found is not null) return null;
            found = (key, number);
        }

        return found;
    }

    public static string? Field(string name)
    {
        var key = Normalize(name);
        if (Fields.TryGetValue(key, out var field)) return field;

        return key.EndsWith(CostSuffix, StringComparison.Ordinal) ? "cost" : null;
    }

    private static string Normalize(string name)
    {
        var compact = name.Replace(" ", "").ToLowerInvariant();
        return compact.EndsWith(RefSuffix, StringComparison.Ordinal) && compact.Length > RefSuffix.Length
            ? compact[..^RefSuffix.Length]
            : compact;
    }
}
