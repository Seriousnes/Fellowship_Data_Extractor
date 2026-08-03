namespace FellowshipExtractor.Tooltip;

/// <summary>
/// The tooltip fields a tuning number can land in, and everything else it reaches. Rows records
/// every row already accounted for, whether it became a named field or a constant, so a later
/// pass over the same rows does not report one twice under two different names.
///
/// Own holds the keys that came from under the entity's own namespace. A reader asking which of an
/// entity's rows answers a question about the entity needs that distinction, because a row
/// belonging to something the entity merely waits on is reachable and is not an answer.
///
/// Promoted holds the entity's own rows a named field took, under the key Rest would have filed
/// them by. They leave Rest so the constants do not repeat what a field already says, but a field
/// and a token can name the same number and the row is the answer to both.
/// </summary>
public sealed record SplitNumbers(
    Dictionary<string, double> Named,
    Dictionary<string, object?> Rest,
    HashSet<string> Rows,
    HashSet<string> Own,
    Dictionary<string, double> Promoted);

/// <summary>
/// Sorting an entity's tuning numbers into the ones a tooltip shows in a fixed place - cast time,
/// cooldown, range - and the rest.
///
/// Designers spell the same field several ways. Range is MaxRange on 154 abilities, "Max Range"
/// on 401, MaxRangeRef on 117 and "Max Range Ref" on 25; cast time is CastingData.CastingDuration
/// on 216 and "Casting Data.CastingDuration" on 207. Matching the spellings one at a time would
/// leave most of the corpus behind, so a name is reduced to spaces removed, a trailing Ref
/// dropped and case folded before it is looked up.
/// </summary>
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
        // Vigor.SingleTargetAbsorb.BaselineChargeCount is the only row of its name in all 13,156,
        // and Luminous Barrier is the only ability declaring its baseline outright. Recognising the
        // spelling is all it takes for the baseline to win: an entity's own top-level row is read
        // before any row named for a talent, and the talent's count stays under its talent.
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

    /// <summary>
    /// An entity's own bindings, split into named tooltip fields and everything else. Only the
    /// entity's own bindings are offered here: an effect it applies may well carry a duration, but
    /// that is the effect's duration and belongs among the constants rather than in the field a
    /// reader takes for the ability's own.
    /// </summary>
    public const string Charges = "charges";
    public const string Period = "period";
    public const string Duration = "duration";

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

        // The entity's own top-level rows, which are the ones a field can be read from directly.
        foreach (var binding in all)
        {
            // A row that scales with rank is four numbers, which no single field can hold.
            if (binding.Values is not null || binding.Value is null) continue;

            var relative = TuningNamespace.Relative(binding.RowName, tuningNamespace);
            var under = relative.Length < binding.RowName.Length;
            var shallow = under && !relative.Contains('.');

            // A row leaf only names a field when the row is the entity's own. Read from any row it
            // would promote Hero.AbilityPaddingCooldown.Default.Duration, which belongs to the
            // shared padding cooldown rather than to the ability that waits on it.
            var field = Field(binding.Property) ?? (shallow ? Field(relative) : null);

            if (field is null || (field == Charges && !usesCharges)) continue;
            if (!claimed.TryAdd(field, binding)) continue;

            named[field] = binding.Value.Value;

            // The row keeps its place among the entity's own even though the field takes it out of
            // Rest. Only the entity's own: a field can be read off the property of a row belonging
            // to something the entity waits on, and that row answers nothing about the entity.
            if (under)
            {
                promoted[relative] = binding.Value.Value;
                own.Add(relative);
            }
        }

        // A charge count is the one field worth reading from a row named for a talent, and only
        // for an ability that runs the charge machinery. Fire Ball keeps its whole charge count in
        // Talent.PlacedProjectileAoeHasCharges.NumCharges and has two charges in play; Apocalypse
        // has an identically shaped Talent.AddedCharges.MaxCharges and has none. What separates
        // them is not the row and not a gate tag but the effect the ability charges through.
        //
        // A count that a named legendary is the only source of is not the ability's baseline, so it
        // stays where a row named for a talent otherwise goes, under that talent's grouping.
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

    /// <summary>Adds a binding to a plain constants map, under the reader's own namespace.</summary>
    public static void Add(Dictionary<string, object?> constants, CurveBinding binding, string? tuningNamespace) =>
        constants[TuningNamespace.Relative(binding.RowName, tuningNamespace)] = Value(binding);

    /// <summary>Folds an effect's binding into an entity that has already been split.</summary>
    public static void Add(SplitNumbers split, CurveBinding binding, string? tuningNamespace)
    {
        split.Rows.Add(binding.RowName);
        Place(split.Rest, split.Own, binding.RowName, Value(binding), tuningNamespace);
    }

    /// <summary>
    /// Files a row under the name the entity sees it by, recording whether the name shortened. A
    /// row that did not is not the entity's: Hero.AbilityPaddingCooldown.Default.Duration is
    /// reachable from almost every ability and belongs to none of them.
    /// </summary>
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

    /// <summary>
    /// Groups the tuning rows that name a talent under the talent they name. This is a fact about
    /// the row names and nothing more: it does not mean the value is conditional. Only 42 entities
    /// in the build declare an actual gate tag while 691 have rows named this way, so most of these
    /// names are what a talent folded into its ability leaves behind. Where a real gate exists the
    /// asset declares it, and that is reported separately.
    ///
    /// The grouping earns its place on findability alone. Ten abilities carry a charge count only
    /// in a row named for a talent, and in one flat list of seventeen there is nothing to mark
    /// Talent.PlacedProjectileAoeHasCharges.NumCharges as the answer to "how many charges".
    /// </summary>
    public static (Dictionary<string, object?> Baseline, Dictionary<string, Dictionary<string, object?>> Talents)
        SplitTalents(Dictionary<string, object?> constants)
    {
        var baseline = new Dictionary<string, object?>(StringComparer.Ordinal);
        var talents = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);

        foreach (var (key, value) in constants)
        {
            var segments = key.Split('.');
            var at = Array.FindIndex(segments, s => TalentSegments.Contains(s, StringComparer.Ordinal));

            // The talent's name follows the marker and at least one field has to follow that.
            if (at < 0 || at + 2 >= segments.Length)
            {
                baseline[key] = value;
                continue;
            }

            var name = segments[at + 1];
            var field = string.Join('.', segments[(at + 2)..]);

            if (!talents.TryGetValue(name, out var fields)) talents[name] = fields = new(StringComparer.Ordinal);

            // Two talents of the same name in different scopes would overwrite each other, so the
            // second one keeps its full key rather than being merged into the first.
            if (!fields.TryAdd(field, value)) baseline[key] = value;
        }

        return (baseline, talents);
    }

    /// <summary>
    /// One number, four for a row that scales with rank, or null when the asset names a row the
    /// shipped tables do not define.
    /// </summary>
    private static object? Value(CurveBinding binding) =>
        binding.Values is { Length: > 1 } ranks ? ranks : binding.Value;

    /// <summary>
    /// The one row of the entity's own naming a field, or null when none or several do. A token
    /// asking for a period or a duration is answered from here rather than from the whole reachable
    /// set, for the same reason a field is: Echoes of Ruin reaches two rows whose leaf reads
    /// Duration and only Dot.Duration is its own.
    ///
    /// The rows a named field took are among the candidates, so a field and a token can name the
    /// same number. Incinerate shows its channel interval as the tick beside the cast time and
    /// prints it again as {IncinerateChannelInterval}; with the field's row held back the token had
    /// nothing to print and the sentence read "every {IncinerateChannelInterval} seconds". Two
    /// candidates still means neither is reported.
    /// </summary>
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

        // Cost is spelled Cost, ManaCost, ResourceCost, SpiritCost and HpCost across the heroes,
        // while CostMultiplier, CostPerTick and CostReduction are not costs at all, so the suffix
        // is what identifies one.
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
