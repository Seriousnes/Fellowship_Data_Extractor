using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;

using FellowshipExtractor.Extraction;

namespace FellowshipExtractor;

public static class DumpAll
{
    private const string AbilityTable = "GameplayAbility";
    private const string EffectTable = "GameplayEffect";
    private const string ClassSuffix = "_C";
    private const string DefaultPrefix = "Default__";
    private const string OpEnum = "EGameplayModOp";
    private const string CalculationEnum = "EGameplayEffectMagnitudeCalculation";
    private const int MaxFileStem = 110;

    private sealed record Resolved(
        string Table,
        int Id,
        string Tag,
        FAssetData? Asset,
        string? MatchedBy,
        string? Reason,
        string[] Candidates)
    {
        public string? ObjectPath => Asset?.ObjectPath;

        public string? PackagePath =>
            Asset is null ? null : AbilityPresentationIndex.PackagePath(Asset.ObjectPath);
    }

    private sealed class Counts
    {
        public readonly SortedDictionary<string, int> Values = new(StringComparer.Ordinal);

        public void Add(string key, int many = 1) => Values[key] = Values.GetValueOrDefault(key) + many;
    }

    public static async Task<int> RunAsync(ExtractorOptions options, string root)
    {
        var clock = Stopwatch.StartNew();
        Directory.CreateDirectory(root);

        var counts = new Counts();
        var failures = ReadFailures.Collect();
        var provider = await ProviderFactory.CreateAsync(options);

        if (provider.MappingsContainer?.MappingsForGame is not { } mappings)
        {
            Console.Error.WriteLine(
                "No mappings are loaded, so every cooked package reads back as nothing. Write " +
                $"mappings/{ExtractorOptions.MappingsPattern} with 'dump-all' aborted.");
            return 1;
        }

        Console.WriteLine($"mounted {provider.Files.Count} files, mappings {options.ResolvedMappingsFile}");

        var assets = AssetRegistry.ReadAssets(provider);
        var tags = CsvTables.ReadTags(provider).ToList();
        var constants = new ConstantsTable(provider);
        var presentation = new AbilityPresentationIndex(provider, assets);

        counts.Add("mountedFiles", provider.Files.Count);
        counts.Add("registryAssets", assets.Length);
        counts.Add("tagRecords", tags.Count);
        counts.Add("constantsKeys", constants.Count);
        counts.Add("presentationRows", presentation.RowCount);
        counts.Add("presentationTables", presentation.TableCount);

        Console.WriteLine(
            $"registry {assets.Length} assets, tags {tags.Count} records, constants {constants.Count} keys, " +
            $"presentation {presentation.RowCount} rows across {presentation.TableCount} tables");

        var byPrimary = new Dictionary<string, FAssetData>(StringComparer.OrdinalIgnoreCase);
        var byName = new Dictionary<string, List<FAssetData>>(StringComparer.OrdinalIgnoreCase);

        foreach (var asset in assets)
        {
            if (AssetRegistry.TagValue(asset, "PrimaryAssetName") is { } primary) byPrimary.TryAdd(primary, asset);

            var name = asset.AssetName.Text;
            if (!byName.TryGetValue(name, out var same)) byName[name] = same = [];
            same.Add(asset);
        }

        var abilities = Resolve(tags, AbilityTable, byPrimary, byName);
        var effects = Resolve(tags, EffectTable, byPrimary, byName);

        counts.Add("abilityTags", abilities.Count);
        counts.Add("effectTags", effects.Count);
        counts.Add("abilitiesResolved", abilities.Count(a => a.Asset is not null));
        counts.Add("effectsResolved", effects.Count(e => e.Asset is not null));

        WriteTagRows(root, tags, counts);
        WriteConstantRows(root, provider, counts);
        WriteIndex(root, abilities, effects, null, null);

        var effectNames = new HashSet<string>(
            tags.Where(t => t.Table.Equals(EffectTable, StringComparison.OrdinalIgnoreCase)).Select(t => t.Name),
            StringComparer.OrdinalIgnoreCase);

        var idByEffectName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var effect in effects) idByEffectName.TryAdd(effect.Tag, effect.Id);

        var applied = new SortedDictionary<int, SortedSet<int>>();
        var calculationClasses = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var packages = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var priorityAbilities = new[] { 1889, 1027, 1207 };
        var priorityEffects = new[]
        {
            1295, 1325, 1335, 1353, 1358, 1362, 1364, 1734, 1745, 1812, 1813, 2424, 2734,
            1224, 1352, 1357, 1361, 1616,
        };

        foreach (var entry in abilities.Where(a => priorityAbilities.Contains(a.Id)))
        {
            WriteAbility(root, provider, constants, mappings, presentation, effectNames, idByEffectName,
                entry, applied, calculationClasses, packages, written, counts);
        }

        foreach (var entry in effects.Where(e => priorityEffects.Contains(e.Id)))
        {
            WriteEffect(root, provider, constants, mappings, effectNames, entry,
                calculationClasses, packages, written, counts);
        }

        Console.WriteLine($"priority specimens written at {clock.Elapsed}");

        WriteCurves(root, provider, assets, counts);
        WriteLocres(root, provider, counts);
        WriteConfig(root, provider, counts);
        WriteHeroes(root, provider, assets, counts);

        Console.WriteLine($"curves, locres and config written at {clock.Elapsed}");

        var scanned = 0;
        foreach (var entry in abilities)
        {
            WriteAbility(root, provider, constants, mappings, presentation, effectNames, idByEffectName,
                entry, applied, calculationClasses, packages, written, counts);

            if (++scanned % 250 == 0) Console.WriteLine($"  abilities {scanned}/{abilities.Count} {clock.Elapsed}");
        }

        scanned = 0;
        foreach (var entry in effects)
        {
            WriteEffect(root, provider, constants, mappings, effectNames, entry,
                calculationClasses, packages, written, counts);

            if (++scanned % 250 == 0) Console.WriteLine($"  effects {scanned}/{effects.Count} {clock.Elapsed}");
        }

        WriteIndex(root, abilities, effects, applied, calculationClasses);

        Console.WriteLine($"entity files written at {clock.Elapsed}");

        var routes = new Counts();
        foreach (var (path, why) in packages) routes.Add(why);

        foreach (var (path, why) in Related(assets, calculationClasses))
        {
            if (packages.TryAdd(path, why)) routes.Add(why);
        }

        WriteBytecode(root, provider, packages, abilities, effects, counts);

        foreach (var (route, many) in routes.Values) counts.Add($"bytecodeRoute.{route}", many);

        failures.Write(Path.Combine(root, "read-failures.json"));
        counts.Add("readFailureEvents", failures.Count);

        WriteManifest(root, options, counts, clock.Elapsed);

        Console.WriteLine($"dump complete in {clock.Elapsed} under {root}");
        return 0;
    }

    private static List<Resolved> Resolve(
        IEnumerable<TagRecord> tags,
        string table,
        IReadOnlyDictionary<string, FAssetData> byPrimary,
        IReadOnlyDictionary<string, List<FAssetData>> byName)
    {
        var found = new List<Resolved>();

        foreach (var tag in tags)
        {
            if (!tag.Table.Equals(table, StringComparison.OrdinalIgnoreCase)) continue;

            var name = tag.Name;

            if (byPrimary.TryGetValue(name, out var primary))
            {
                found.Add(new Resolved(table, tag.NetId, name, primary, "PrimaryAssetName", null, []));
                continue;
            }

            string[] spellings =
            [
                name,
                $"{name}{ClassSuffix}",
                name.EndsWith(ClassSuffix, StringComparison.Ordinal) ? name[..^ClassSuffix.Length] : name,
            ];

            Resolved? matched = null;
            string[] ambiguous = [];

            foreach (var spelling in spellings.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!byName.TryGetValue(spelling, out var same)) continue;

                if (same.Count == 1)
                {
                    matched = new Resolved(table, tag.NetId, name, same[0], $"AssetName:{spelling}", null, []);
                    break;
                }

                var candidates = same
                    .Select(a => a.ObjectPath)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                matched = new Resolved(
                    table,
                    tag.NetId,
                    name,
                    same.OrderBy(a => a.ObjectPath, StringComparer.OrdinalIgnoreCase).First(),
                    $"AssetName:{spelling} (ambiguous, first of {same.Count})",
                    null,
                    candidates);

                ambiguous = candidates;
                break;
            }

            found.Add(matched ?? new Resolved(
                table, tag.NetId, name, null, null, "no cooked asset carries this name", ambiguous));
        }

        return found;
    }

    private static void WriteTagRows(string root, IReadOnlyList<TagRecord> tags, Counts counts)
    {
        var folder = Path.Combine(root, "tags");
        Directory.CreateDirectory(folder);

        using var writer = new StreamWriter(Path.Combine(folder, "all-rows.csv"), false, new UTF8Encoding(false));
        writer.WriteLine("Table,Name,NetId,Flag");

        foreach (var tag in tags)
        {
            writer.WriteLine($"{Csv(tag.Table)},{Csv(tag.Name)},{tag.NetId},{tag.Flag}");
        }

        counts.Add("tagRowsWritten", tags.Count);
    }

    private static void WriteConstantRows(string root, AbstractFileProvider provider, Counts counts)
    {
        var folder = Path.Combine(root, "constants");
        Directory.CreateDirectory(folder);

        using var writer = new StreamWriter(Path.Combine(folder, "all-rows.csv"), false, new UTF8Encoding(false));
        writer.WriteLine("Table,Key,Value,Columns,Raws,Comment");

        var many = 0;

        foreach (var record in CsvTables.ReadConstants(provider))
        {
            var value = record.Value is { } number ? number.ToString("R", CultureInfo.InvariantCulture) : "";

            writer.WriteLine(
                $"{Csv(record.Table)},{Csv(record.Key)},{Csv(value)}," +
                $"{Csv(string.Join('|', record.Columns))},{Csv(string.Join('|', record.Raws))}," +
                $"{Csv(record.Comment ?? "")}");

            many++;
        }

        counts.Add("constantRowsWritten", many);
    }

    private static string Csv(string field) =>
        field.Contains(',') || field.Contains('"') || field.Contains('\n')
            ? $"\"{field.Replace("\"", "\"\"")}\""
            : field;

    private static void WriteIndex(
        string root,
        IReadOnlyList<Resolved> abilities,
        IReadOnlyList<Resolved> effects,
        IReadOnlyDictionary<int, SortedSet<int>>? applied,
        IReadOnlyCollection<string>? calculationClasses)
    {
        using var stream = File.Create(Path.Combine(root, "index.json"));
        using var writer = new Utf8JsonWriter(stream, UeJson.WriterOptions);

        writer.WriteStartObject();

        WriteSide(writer, "abilities", abilities);
        WriteSide(writer, "effects", effects);

        writer.WritePropertyName("abilityAppliesEffects");
        writer.WriteStartObject();
        foreach (var (ability, reached) in applied ?? new SortedDictionary<int, SortedSet<int>>())
        {
            writer.WritePropertyName(ability.ToString(CultureInfo.InvariantCulture));
            writer.WriteStartArray();
            foreach (var effect in reached) writer.WriteNumberValue(effect);
            writer.WriteEndArray();
        }
        writer.WriteEndObject();

        writer.WritePropertyName("effectAppliedByAbilities");
        writer.WriteStartObject();
        var inverse = new SortedDictionary<int, SortedSet<int>>();
        foreach (var (ability, reached) in applied ?? new SortedDictionary<int, SortedSet<int>>())
        {
            foreach (var effect in reached)
            {
                if (!inverse.TryGetValue(effect, out var appliers)) inverse[effect] = appliers = [];
                appliers.Add(ability);
            }
        }
        foreach (var (effect, appliers) in inverse)
        {
            writer.WritePropertyName(effect.ToString(CultureInfo.InvariantCulture));
            writer.WriteStartArray();
            foreach (var ability in appliers) writer.WriteNumberValue(ability);
            writer.WriteEndArray();
        }
        writer.WriteEndObject();

        writer.WritePropertyName("magnitudeCalculationClasses");
        writer.WriteStartArray();
        foreach (var path in calculationClasses ?? []) writer.WriteStringValue(path);
        writer.WriteEndArray();

        writer.WriteEndObject();
    }

    private static void WriteSide(Utf8JsonWriter writer, string side, IReadOnlyList<Resolved> entries)
    {
        writer.WritePropertyName($"{side}ById");
        writer.WriteStartObject();
        foreach (var entry in entries.OrderBy(e => e.Id))
        {
            writer.WritePropertyName(entry.Id.ToString(CultureInfo.InvariantCulture));
            writer.WriteStartObject();
            writer.WriteString("tag", entry.Tag);
            writer.WriteString("assetPath", entry.ObjectPath);
            writer.WriteString("matchedBy", entry.MatchedBy);
            writer.WriteString("unresolved", entry.Reason);
            writer.WriteString("file", $"{side}/{FileName(entry)}");
            writer.WriteEndObject();
        }
        writer.WriteEndObject();

        writer.WritePropertyName($"{side}ByAssetPath");
        writer.WriteStartObject();
        foreach (var entry in entries.Where(e => e.ObjectPath is not null)
                     .OrderBy(e => e.ObjectPath, StringComparer.OrdinalIgnoreCase))
        {
            writer.WriteNumber(entry.ObjectPath!, entry.Id);
        }
        writer.WriteEndObject();
    }

    private static string FileName(Resolved entry) => $"{entry.Id}-{Safe(entry.Tag)}.json";

    private static string Safe(string name)
    {
        var cleaned = new StringBuilder(name.Length);

        foreach (var glyph in name)
        {
            cleaned.Append(Path.GetInvalidFileNameChars().Contains(glyph) ? '_' : glyph);
        }

        return cleaned.ToString();
    }

    private static void WriteAbility(
        string root,
        AbstractFileProvider provider,
        ConstantsTable constants,
        TypeMappings mappings,
        AbilityPresentationIndex presentation,
        IReadOnlySet<string> effectNames,
        IReadOnlyDictionary<string, int> idByEffectName,
        Resolved entry,
        IDictionary<int, SortedSet<int>> applied,
        ISet<string> calculationClasses,
        IDictionary<string, string> packages,
        ISet<string> written,
        Counts counts)
    {
        var folder = Path.Combine(root, "abilities");
        Directory.CreateDirectory(folder);

        var target = Path.Combine(folder, FileName(entry));
        if (!written.Add(target)) return;

        var (package, cdo, failure) = Load(provider, entry);

        if (package is not null && entry.PackagePath is { } path) packages.TryAdd(path, "ability");

        using var stream = File.Create(target);
        using var writer = new Utf8JsonWriter(stream, UeJson.WriterOptions);

        writer.WriteStartObject();
        WriteHead(writer, entry, cdo, failure);

        if (cdo is not null)
        {
            WriteTuning(writer, cdo, constants);

            var reached = new SortedSet<int>();
            writer.WritePropertyName("appliesEffects");
            writer.WriteStartArray();
            foreach (var (name, effectPath, widened) in Referenced(cdo, effectNames))
            {
                writer.WriteStartObject();
                writer.WriteString("name", name);
                writer.WriteString("path", effectPath);
                writer.WriteBoolean("reachedByPackageScan", widened);
                if (idByEffectName.TryGetValue(name, out var id))
                {
                    writer.WriteNumber("id", id);
                    reached.Add(id);
                }
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            if (reached.Count > 0) applied[entry.Id] = reached;

            foreach (var found in CalculationClassPaths(cdo)) calculationClasses.Add(found);

            var row = presentation.Find(entry.Tag);
            writer.WritePropertyName("presentationRow");
            if (row is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteStartObject();
                writer.WriteString("table", row.Table);
                writer.WriteString("rowName", row.RowName);
                writer.WriteString("matchedBy", row.MatchedBy);
                writer.WritePropertyName("row");
                UeJson.WriteObject(writer, row.Row);
                writer.WriteEndObject();
            }

            counts.Add("abilityFilesWithCdo");
        }

        WriteFunctions(writer, package, counts);
        WriteExports(writer, package, cdo);

        writer.WriteEndObject();
        writer.Flush();

        counts.Add("abilityFiles");
    }

    private static void WriteEffect(
        string root,
        AbstractFileProvider provider,
        ConstantsTable constants,
        TypeMappings mappings,
        IReadOnlySet<string> effectNames,
        Resolved entry,
        ISet<string> calculationClasses,
        IDictionary<string, string> packages,
        ISet<string> written,
        Counts counts)
    {
        var folder = Path.Combine(root, "effects");
        Directory.CreateDirectory(folder);

        var target = Path.Combine(folder, FileName(entry));
        if (!written.Add(target)) return;

        var (package, cdo, failure) = Load(provider, entry);

        if (package is not null && entry.PackagePath is { } path) packages.TryAdd(path, "effect");

        using var stream = File.Create(target);
        using var writer = new Utf8JsonWriter(stream, UeJson.WriterOptions);

        writer.WriteStartObject();
        WriteHead(writer, entry, cdo, failure);

        if (cdo is not null)
        {
            WriteTuning(writer, cdo, constants);
            WriteModifiers(writer, cdo, constants, mappings, counts);
            WriteExecutions(writer, cdo, constants, mappings, counts);

            writer.WritePropertyName("durationMagnitude");
            if (PropertyWalk.GetStruct(cdo, "DurationMagnitude") is { } duration)
            {
                WriteMagnitude(writer, duration, constants, mappings);
            }
            else
            {
                writer.WriteNullValue();
            }

            writer.WritePropertyName("referencesEffects");
            writer.WriteStartArray();
            foreach (var (name, effectPath, widened) in Referenced(cdo, effectNames))
            {
                writer.WriteStartObject();
                writer.WriteString("name", name);
                writer.WriteString("path", effectPath);
                writer.WriteBoolean("reachedByPackageScan", widened);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            foreach (var found in CalculationClassPaths(cdo)) calculationClasses.Add(found);

            counts.Add("effectFilesWithCdo");
        }

        WriteFunctions(writer, package, counts);
        WriteExports(writer, package, cdo);

        writer.WriteEndObject();
        writer.Flush();

        counts.Add("effectFiles");
    }

    private static void WriteHead(Utf8JsonWriter writer, Resolved entry, UObject? cdo, string? failure)
    {
        writer.WriteNumber("id", entry.Id);
        writer.WriteString("tagTable", entry.Table);
        writer.WriteString("tag", entry.Tag);
        writer.WriteString("assetPath", entry.ObjectPath);
        writer.WriteString("packagePath", entry.PackagePath);
        writer.WriteString("assetClass", entry.Asset?.AssetClass.Text);
        writer.WriteString("matchedBy", entry.MatchedBy);
        writer.WriteString("unresolved", entry.Reason);
        writer.WriteString("readFailure", failure);

        writer.WritePropertyName("ambiguousCandidates");
        writer.WriteStartArray();
        foreach (var candidate in entry.Candidates) writer.WriteStringValue(candidate);
        writer.WriteEndArray();

        writer.WriteString("className", cdo?.ExportType);
        writer.WriteString("defaultObject", cdo?.Name);

        writer.WritePropertyName("registryTags");
        writer.WriteStartObject();
        if (entry.Asset is { } carrying)
        {
            foreach (var pair in carrying.TagsAndValues)
            {
                writer.WriteString(pair.Key.Text, pair.Value);
            }
        }
        writer.WriteEndObject();

        writer.WritePropertyName("cdo");
        if (cdo is null) writer.WriteNullValue();
        else UeJson.WriteObject(writer, cdo);
    }

    private static (IPackage? Package, UObject? Cdo, string? Failure) Load(
        AbstractFileProvider provider, Resolved entry)
    {
        if (entry.PackagePath is not { } path) return (null, null, null);

        try
        {
            if (!provider.TryLoadPackage(path, out var package)) return (null, null, "package would not load");

            var exports = package.GetExports().ToArray();

            var cdo = exports.FirstOrDefault(e => e.Name.StartsWith(DefaultPrefix, StringComparison.Ordinal))
                ?? exports.FirstOrDefault(e => e.Name.Equals(entry.Tag, StringComparison.OrdinalIgnoreCase))
                ?? exports.FirstOrDefault(e => e is not UStruct);

            return (package, cdo, null);
        }
        catch (Exception failure)
        {
            return (null, null, $"{failure.GetType().Name}: {failure.Message}");
        }
    }

    private static IEnumerable<(string Name, string Path, bool Widened)> Referenced(
        UObject cdo, IReadOnlySet<string> effectNames)
    {
        try
        {
            return GameplayEffects.Reached(cdo, effectNames).ToArray();
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static IEnumerable<string> CalculationClassPaths(UObject cdo)
    {
        List<string> found = [];

        try
        {
            foreach (var property in PropertyWalk.Walk(cdo))
            {
                if (property.Name is not ("CalculationClassMagnitude" or "CalculationClass")) continue;
                if (PropertyWalk.AsString(property.Value) is not { } path) continue;
                if (path.StartsWith("/Script/", StringComparison.OrdinalIgnoreCase)) continue;

                found.Add(path);
            }
        }
        catch (Exception)
        {
            return found;
        }

        return found;
    }

    private static void WriteTuning(Utf8JsonWriter writer, UObject cdo, ConstantsTable constants)
    {
        List<CurveBinding> bindings;
        try
        {
            bindings = CurveBindings.Collect(cdo, constants);
        }
        catch (Exception)
        {
            bindings = [];
        }

        var fromName = TuningNamespace.For(cdo.Owner?.Name is { } package
            ? AbilityPresentationIndex.AssetName(package)
            : cdo.Name, constants);

        var fromClass = TuningNamespace.For(cdo.ExportType, constants);
        var fromBindings = TuningNamespace.FromBindings(bindings, constants);

        var prefixes = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var binding in bindings)
        {
            var separator = binding.RowName.LastIndexOf('.');
            if (separator <= 0) continue;

            var prefix = binding.RowName[..separator];
            if (constants.IsNamespace(prefix)) prefixes.Add(prefix);
        }

        writer.WritePropertyName("tuningNamespace");
        writer.WriteStartObject();
        writer.WriteString("fromAssetName", fromName);
        writer.WriteString("fromClassName", fromClass);
        writer.WriteString("fromBindings", fromBindings);
        writer.WritePropertyName("boundRowPrefixes");
        writer.WriteStartArray();
        foreach (var prefix in prefixes) writer.WriteStringValue(prefix);
        writer.WriteEndArray();
        writer.WriteEndObject();

        writer.WritePropertyName("curveBindings");
        writer.WriteStartArray();
        foreach (var binding in bindings)
        {
            writer.WriteStartObject();
            writer.WriteString("property", binding.Property);
            writer.WriteString("rowName", binding.RowName);
            writer.WriteString("curveTable", binding.CurveTable);
            writer.WriteString("registryType", binding.RegistryType);
            if (binding.Value is { } value) writer.WriteNumber("rowValue", value);
            writer.WriteString("constantsTable", binding.ConstantsTable);
            writer.WriteString("comment", binding.Comment);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        var scopes = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in new[] { fromName, fromClass, fromBindings })
        {
            if (candidate is { Length: > 0 }) scopes.Add(candidate);
        }
        foreach (var prefix in prefixes) scopes.Add(prefix);

        writer.WritePropertyName("constantsDirectlyUnder");
        writer.WriteStartObject();
        foreach (var scope in scopes)
        {
            writer.WritePropertyName(scope);
            writer.WriteStartArray();
            foreach (var record in constants.DirectlyUnder(scope))
            {
                WriteConstant(writer, record);
            }
            writer.WriteEndArray();
        }
        writer.WriteEndObject();

        writer.WritePropertyName("constantsAllUnder");
        writer.WriteStartObject();
        foreach (var scope in scopes)
        {
            writer.WritePropertyName(scope);
            writer.WriteStartArray();
            foreach (var record in constants.AllUnder(scope).OrderBy(r => r.Key, StringComparer.OrdinalIgnoreCase))
            {
                WriteConstant(writer, record);
            }
            writer.WriteEndArray();
        }
        writer.WriteEndObject();
    }

    private static void WriteConstant(Utf8JsonWriter writer, ConstantRecord record)
    {
        writer.WriteStartObject();
        writer.WriteString("key", record.Key);
        writer.WriteString("table", record.Table);
        if (record.Value is { } value) writer.WriteNumber("value", value);
        writer.WritePropertyName("raws");
        writer.WriteStartArray();
        foreach (var raw in record.Raws) writer.WriteStringValue(raw);
        writer.WriteEndArray();
        writer.WritePropertyName("columns");
        writer.WriteStartArray();
        foreach (var column in record.Columns) writer.WriteStringValue(column);
        writer.WriteEndArray();
        writer.WriteString("comment", record.Comment);
        writer.WriteEndObject();
    }

    private static void WriteModifiers(
        Utf8JsonWriter writer, UObject cdo, ConstantsTable constants, TypeMappings mappings, Counts counts)
    {
        writer.WritePropertyName("modifiers");
        writer.WriteStartArray();

        if (PropertyWalk.Get(cdo, "Modifiers")?.GenericValue is UScriptArray declared)
        {
            for (var at = 0; at < declared.Properties.Count; at++)
            {
                if (declared.Properties[at]?.GenericValue is not FScriptStruct { StructType: IPropertyHolder modifier })
                {
                    continue;
                }

                writer.WriteStartObject();
                writer.WriteNumber("index", at);

                var attribute = PropertyWalk.GetStruct(modifier, "Attribute");
                writer.WriteString("attribute", attribute is null ? null : PropertyWalk.GetString(attribute, "AttributeName"));
                writer.WriteString(
                    "attributeOwner",
                    attribute is null ? null : PropertyWalk.GetString(attribute, "AttributeOwner"));

                var op = PropertyWalk.GetString(modifier, "ModifierOp");
                writer.WriteString("modifierOpWritten", op);
                writer.WriteString("modifierOpMember", EnumOrdinals.Member(mappings, OpEnum, op));
                var ordinal = PropertyWalk.AsOrdinal(PropertyWalk.Get(modifier, "ModifierOp")?.GenericValue);
                if (ordinal is { } known) writer.WriteNumber("modifierOpOrdinal", known);

                writer.WritePropertyName("modifierMagnitude");
                if (PropertyWalk.GetStruct(modifier, "ModifierMagnitude") is { } magnitude)
                {
                    WriteMagnitude(writer, magnitude, constants, mappings);
                }
                else
                {
                    writer.WriteNullValue();
                }

                writer.WritePropertyName("raw");
                UeJson.WriteObject(writer, modifier);

                writer.WriteEndObject();

                counts.Add("modifiersExpanded");
            }
        }

        writer.WriteEndArray();
    }

    private static void WriteExecutions(
        Utf8JsonWriter writer, UObject cdo, ConstantsTable constants, TypeMappings mappings, Counts counts)
    {
        writer.WritePropertyName("executions");
        writer.WriteStartArray();

        if (PropertyWalk.Get(cdo, "Executions")?.GenericValue is UScriptArray executions)
        {
            for (var at = 0; at < executions.Properties.Count; at++)
            {
                if (executions.Properties[at]?.GenericValue is not FScriptStruct { StructType: IPropertyHolder execution })
                {
                    continue;
                }

                writer.WriteStartObject();
                writer.WriteNumber("index", at);
                writer.WriteString("calculationClass", PropertyWalk.GetString(execution, "CalculationClass"));

                writer.WritePropertyName("calculationModifiers");
                writer.WriteStartArray();

                if (PropertyWalk.Get(execution, "CalculationModifiers")?.GenericValue is UScriptArray scoped)
                {
                    foreach (var entry in scoped.Properties)
                    {
                        if (entry?.GenericValue is not FScriptStruct { StructType: IPropertyHolder scope }) continue;

                        writer.WriteStartObject();

                        var captured = PropertyWalk.GetStruct(scope, "CapturedAttribute");
                        writer.WriteString(
                            "capturedAttribute",
                            captured is null ? null : PropertyWalk.GetString(captured, "AttributeName"));

                        var identifier = PropertyWalk.GetStruct(scope, "TransientAggregatorIdentifier");
                        writer.WriteString(
                            "transientAggregatorIdentifier",
                            identifier is null ? null : PropertyWalk.GetString(identifier, "TagName"));

                        var op = PropertyWalk.GetString(scope, "ModifierOp");
                        writer.WriteString("modifierOpWritten", op);
                        writer.WriteString("modifierOpMember", EnumOrdinals.Member(mappings, OpEnum, op));

                        writer.WritePropertyName("modifierMagnitude");
                        if (PropertyWalk.GetStruct(scope, "ModifierMagnitude") is { } magnitude)
                        {
                            WriteMagnitude(writer, magnitude, constants, mappings);
                        }
                        else
                        {
                            writer.WriteNullValue();
                        }

                        writer.WritePropertyName("raw");
                        UeJson.WriteObject(writer, scope);

                        writer.WriteEndObject();

                        counts.Add("calculationModifiersExpanded");
                    }
                }

                writer.WriteEndArray();

                writer.WritePropertyName("raw");
                UeJson.WriteObject(writer, execution);

                writer.WriteEndObject();

                counts.Add("executionsExpanded");
            }
        }

        writer.WriteEndArray();
    }

    private static void WriteMagnitude(
        Utf8JsonWriter writer, IPropertyHolder magnitude, ConstantsTable constants, TypeMappings mappings)
    {
        writer.WriteStartObject();

        var written = PropertyWalk.GetString(magnitude, "MagnitudeCalculationType");
        writer.WriteString("calculationTypeWritten", written);
        writer.WriteString("calculationTypeMember", EnumOrdinals.Member(mappings, CalculationEnum, written));

        writer.WritePropertyName("scalableFloatMagnitude");
        WriteScalable(writer, PropertyWalk.GetStruct(magnitude, "ScalableFloatMagnitude"), constants);

        writer.WritePropertyName("setByCallerMagnitude");
        if (PropertyWalk.GetStruct(magnitude, "SetByCallerMagnitude") is { } byCaller)
        {
            writer.WriteStartObject();
            writer.WriteString("dataName", PropertyWalk.GetString(byCaller, "DataName"));
            writer.WriteString(
                "dataTag",
                PropertyWalk.GetStruct(byCaller, "DataTag") is { } tag
                    ? PropertyWalk.GetString(tag, "TagName")
                    : PropertyWalk.GetString(byCaller, "DataTag"));
            writer.WritePropertyName("raw");
            UeJson.WriteObject(writer, byCaller);
            writer.WriteEndObject();
        }
        else
        {
            writer.WriteNullValue();
        }

        writer.WritePropertyName("customCalculationClassMagnitude");
        if (PropertyWalk.GetStruct(magnitude, "CustomMagnitude") is { } custom)
        {
            writer.WriteStartObject();
            writer.WriteString("calculationClass", PropertyWalk.GetString(custom, "CalculationClassMagnitude"));
            writer.WritePropertyName("coefficient");
            WriteScalable(writer, PropertyWalk.GetStruct(custom, "Coefficient"), constants);
            writer.WritePropertyName("preMultiplyAdditiveValue");
            WriteScalable(writer, PropertyWalk.GetStruct(custom, "PreMultiplyAdditiveValue"), constants);
            writer.WritePropertyName("postMultiplyAdditiveValue");
            WriteScalable(writer, PropertyWalk.GetStruct(custom, "PostMultiplyAdditiveValue"), constants);
            writer.WritePropertyName("raw");
            UeJson.WriteObject(writer, custom);
            writer.WriteEndObject();
        }
        else
        {
            writer.WriteNullValue();
        }

        writer.WritePropertyName("attributeBasedMagnitude");
        if (PropertyWalk.GetStruct(magnitude, "AttributeBasedMagnitude") is { } based)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("coefficient");
            WriteScalable(writer, PropertyWalk.GetStruct(based, "Coefficient"), constants);
            writer.WritePropertyName("preMultiplyAdditiveValue");
            WriteScalable(writer, PropertyWalk.GetStruct(based, "PreMultiplyAdditiveValue"), constants);
            writer.WritePropertyName("postMultiplyAdditiveValue");
            WriteScalable(writer, PropertyWalk.GetStruct(based, "PostMultiplyAdditiveValue"), constants);

            var backing = PropertyWalk.GetStruct(based, "BackingAttribute");
            writer.WriteString(
                "backingAttribute",
                backing is null || PropertyWalk.GetStruct(backing, "AttributeToCapture") is not { } captured
                    ? null
                    : PropertyWalk.GetString(captured, "AttributeName"));
            writer.WriteString(
                "attributeSource",
                backing is null ? null : PropertyWalk.GetString(backing, "AttributeSource"));
            writer.WriteString("attributeCalculationType", PropertyWalk.GetString(based, "AttributeCalculationType"));
            writer.WritePropertyName("raw");
            UeJson.WriteObject(writer, based);
            writer.WriteEndObject();
        }
        else
        {
            writer.WriteNullValue();
        }

        writer.WritePropertyName("raw");
        UeJson.WriteObject(writer, magnitude);

        writer.WriteEndObject();
    }

    private static void WriteScalable(Utf8JsonWriter writer, IPropertyHolder? scalable, ConstantsTable constants)
    {
        if (scalable is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();

        var value = PropertyWalk.AsNumber(PropertyWalk.Get(scalable, "Value")?.GenericValue);
        if (value is { } literal) writer.WriteNumber("value", literal);

        string? rowName = null;
        if (PropertyWalk.GetStruct(scalable, "Curve") is { } curve)
        {
            rowName = PropertyWalk.GetString(curve, "RowName");
            writer.WriteString("rowName", rowName);
            writer.WriteString("curveTable", PropertyWalk.GetString(curve, "CurveTable"));
        }

        if (rowName is { Length: > 0 } && !rowName.Equals("None", StringComparison.Ordinal))
        {
            var (record, tables) = constants.Lookup(rowName);
            if (record?.Value is { } declared)
            {
                writer.WriteNumber("rowValue", declared);
                writer.WriteNumber("resolved", (value ?? 1) * declared);
            }
            writer.WriteString("constantsTable", record?.Table);
            writer.WriteString("comment", record?.Comment);
            writer.WritePropertyName("rowTables");
            writer.WriteStartArray();
            foreach (var table in tables) writer.WriteStringValue(table);
            writer.WriteEndArray();
        }
        else if (value is { } bare)
        {
            writer.WriteNumber("resolved", bare);
        }

        writer.WritePropertyName("raw");
        UeJson.WriteObject(writer, scalable);

        writer.WriteEndObject();
    }

    private static void WriteFunctions(Utf8JsonWriter writer, IPackage? package, Counts counts)
    {
        writer.WritePropertyName("blueprintFunctions");
        writer.WriteStartArray();

        foreach (var export in Exports(package))
        {
            if (export is not UStruct body) continue;

            writer.WriteStartObject();
            writer.WriteString("name", export.Name);
            writer.WriteString("type", export.ExportType);
            writer.WriteNumber("statements", body.ScriptBytecode?.Length ?? 0);

            writer.WritePropertyName("childProperties");
            writer.WriteStartArray();
            foreach (var child in body.ChildProperties ?? [])
            {
                if (child is not FProperty property) continue;

                writer.WriteStartObject();
                writer.WriteString("name", property.Name.Text);
                writer.WriteString("flags", property.PropertyFlags.ToString());
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            writer.WriteEndObject();

            counts.Add("blueprintFunctionsListed");
        }

        writer.WriteEndArray();
    }

    private static void WriteExports(Utf8JsonWriter writer, IPackage? package, UObject? cdo)
    {
        writer.WritePropertyName("otherExports");
        writer.WriteStartArray();

        foreach (var export in Exports(package))
        {
            if (ReferenceEquals(export, cdo)) continue;
            if (export is UStruct) continue;

            writer.WriteStartObject();
            writer.WriteString("name", export.Name);
            writer.WriteString("type", export.ExportType);
            writer.WritePropertyName("properties");
            UeJson.WriteObject(writer, export);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static UObject[] Exports(IPackage? package)
    {
        if (package is null) return [];

        try
        {
            return package.GetExports().ToArray();
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static void WriteCurves(
        string root, AbstractFileProvider provider, IReadOnlyList<FAssetData> assets, Counts counts)
    {
        var folder = Path.Combine(root, "curves");
        Directory.CreateDirectory(folder);

        var paths = assets
            .Where(a => a.AssetClass.Text.Equals("CurveTable", StringComparison.OrdinalIgnoreCase))
            .Select(a => a.ObjectPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var rows = 0;
        var keys = 0;

        foreach (var objectPath in paths)
        {
            var packagePath = AbilityPresentationIndex.PackagePath(objectPath);
            var name = AbilityPresentationIndex.AssetName(objectPath);

            UCurveTable? table = null;
            string? failure = null;

            try
            {
                if (provider.TryLoadPackage(packagePath, out var package))
                {
                    table = package.GetExports().OfType<UCurveTable>().FirstOrDefault();
                }
            }
            catch (Exception broken)
            {
                failure = $"{broken.GetType().Name}: {broken.Message}";
            }

            using var stream = File.Create(Path.Combine(folder, $"{Safe(name)}.json"));
            using var writer = new Utf8JsonWriter(stream, UeJson.WriterOptions);

            writer.WriteStartObject();
            writer.WriteString("assetPath", objectPath);
            writer.WriteString("table", name);
            writer.WriteString("readFailure", failure);
            writer.WriteNumber("rowCount", table?.RowMap.Count ?? 0);

            writer.WritePropertyName("rows");
            writer.WriteStartObject();

            foreach (var (rowName, row) in table?.RowMap ?? [])
            {
                writer.WritePropertyName(rowName.Text);
                writer.WriteStartArray();
                foreach (var key in CurveRows.Keys(row))
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("time", key.Time);
                    writer.WriteNumber("value", key.Value);
                    writer.WriteEndObject();
                    keys++;
                }
                writer.WriteEndArray();
                rows++;
            }

            writer.WriteEndObject();

            writer.WritePropertyName("raw");
            if (table is null) writer.WriteNullValue();
            else UeJson.WriteObject(writer, table);

            writer.WriteEndObject();
            writer.Flush();
        }

        counts.Add("curveTables", paths.Length);
        counts.Add("curveRows", rows);
        counts.Add("curveKeys", keys);

        Console.WriteLine($"curves: {paths.Length} tables, {rows} rows, {keys} keys");
    }

    private static void WriteLocres(string root, AbstractFileProvider provider, Counts counts)
    {
        var folder = Path.Combine(root, "text");
        Directory.CreateDirectory(folder);

        var cultures = provider.Files.Keys
            .Where(p => p.Contains("/Localization/", StringComparison.OrdinalIgnoreCase) &&
                        p.EndsWith(".locres", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        using var stream = File.Create(Path.Combine(folder, "locres.json"));
        using var writer = new Utf8JsonWriter(stream, UeJson.WriterOptions);

        writer.WriteStartObject();
        writer.WriteString("culture", LocalizedText.DefaultCulture);
        writer.WriteString("path", LocalizedText.PathFor(LocalizedText.DefaultCulture));

        writer.WritePropertyName("otherLocresFiles");
        writer.WriteStartArray();
        foreach (var culture in cultures) writer.WriteStringValue(culture);
        writer.WriteEndArray();

        writer.WritePropertyName("strings");
        writer.WriteStartArray();

        var many = 0;
        foreach (var entry in LocalizedText.Read(provider, LocalizedText.DefaultCulture))
        {
            writer.WriteStartObject();
            writer.WriteString("namespace", entry.Namespace);
            writer.WriteString("key", entry.Key);
            writer.WriteString("text", entry.Text);
            writer.WriteEndObject();
            many++;
        }

        writer.WriteEndArray();
        writer.WriteNumber("count", many);
        writer.WriteEndObject();
        writer.Flush();

        counts.Add("locresStrings", many);
        counts.Add("locresFilesPresent", cultures.Length);

        Console.WriteLine($"locres: {many} strings, {cultures.Length} .locres files in the containers");
    }

    private static void WriteHeroes(
        string root, AbstractFileProvider provider, IReadOnlyList<FAssetData> assets, Counts counts)
    {
        var folder = Path.Combine(root, "heroes");
        Directory.CreateDirectory(folder);

        using var stream = File.Create(Path.Combine(folder, "metadata.json"));
        using var writer = new Utf8JsonWriter(stream, UeJson.WriterOptions);

        writer.WriteStartArray();

        var written = 0;

        foreach (var asset in assets.OrderBy(a => a.ObjectPath, StringComparer.OrdinalIgnoreCase))
        {
            if (!HeroMetadataClasses.Contains(asset.AssetClass.Text)) continue;
            if (!provider.TryLoadPackage(AbilityPresentationIndex.PackagePath(asset.ObjectPath), out var package))
            {
                continue;
            }

            foreach (var export in Exports(package))
            {
                writer.WriteStartObject();
                writer.WriteString("objectPath", asset.ObjectPath);
                writer.WriteString("assetClass", asset.AssetClass.Text);
                writer.WriteString("export", export.Name);
                writer.WriteString("exportType", export.ExportType);
                writer.WritePropertyName("properties");
                UeJson.WriteObject(writer, export);
                writer.WriteEndObject();

                written++;
            }
        }

        writer.WriteEndArray();

        counts.Add("heroMetadataExports", written);
        Console.WriteLine($"heroes: {written} exports across the hero metadata assets");
    }

    private static readonly HashSet<string> HeroMetadataClasses =
        new(StringComparer.Ordinal) { "BP_HeroMetaDataAsset_C", "CRHeroMetaDataAsset", "CRHeroResourceDataAsset" };

    private static void WriteConfig(string root, AbstractFileProvider provider, Counts counts)
    {
        var folder = Path.Combine(root, "config");
        Directory.CreateDirectory(folder);

        var files = provider.Files.Values
            .Where(f => f.Path.EndsWith(".ini", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        using var stream = File.Create(Path.Combine(folder, "ini.json"));
        using var writer = new Utf8JsonWriter(stream, UeJson.WriterOptions);

        writer.WriteStartObject();

        var sections = 0;
        var keys = 0;

        foreach (var file in files)
        {
            var read = new List<(string Section, string Key, string Written)>();
            string? failure = null;

            try
            {
                string? section = null;

                foreach (var raw in Encoding.UTF8.GetString(file.Read()).Split('\n'))
                {
                    var line = raw.Trim().TrimEnd('\r');
                    if (line.Length == 0 || line[0] is ';' or '#') continue;

                    if (line[0] == '[' && line[^1] == ']')
                    {
                        section = line[1..^1];
                        continue;
                    }

                    if (section is null) continue;

                    var assigned = line.IndexOf('=');
                    if (assigned < 0) continue;

                    read.Add((section, line[..assigned].Trim(), line[(assigned + 1)..].Trim()));
                }
            }
            catch (Exception broken)
            {
                failure = $"{broken.GetType().Name}: {broken.Message}";
            }

            writer.WritePropertyName(file.Path);
            writer.WriteStartObject();

            if (failure is not null) writer.WriteString("$readFailure", failure);

            foreach (var section in read.Select(entry => entry.Section).Distinct(StringComparer.Ordinal))
            {
                writer.WritePropertyName(section);
                writer.WriteStartObject();
                sections++;

                var inside = read.Where(entry => entry.Section.Equals(section, StringComparison.Ordinal)).ToArray();

                foreach (var key in inside.Select(entry => entry.Key).Distinct(StringComparer.Ordinal))
                {
                    writer.WritePropertyName(key);
                    writer.WriteStartArray();

                    foreach (var entry in inside)
                    {
                        if (!entry.Key.Equals(key, StringComparison.Ordinal)) continue;

                        writer.WriteStringValue(entry.Written);
                        keys++;
                    }

                    writer.WriteEndArray();
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        writer.WriteEndObject();
        writer.Flush();

        counts.Add("iniFiles", files.Length);
        counts.Add("iniSections", sections);
        counts.Add("iniKeys", keys);

        Console.WriteLine($"config: {files.Length} ini files, {sections} sections, {keys} keys");
    }

    private static IEnumerable<(string Path, string Route)> Related(
        IReadOnlyList<FAssetData> assets, IReadOnlyCollection<string> calculationClasses)
    {
        foreach (var path in calculationClasses)
        {
            yield return (AbilityPresentationIndex.PackagePath(path), "magnitudeCalculation");
        }

        string[] parents =
        [
            "GameplayCue", "AttributeSet", "BlueprintFunctionLibrary", "GameplayModMagnitudeCalculation",
            "GameplayEffectExecutionCalculation",
        ];

        foreach (var asset in assets)
        {
            var declared = string.Join(
                ' ',
                asset.TagsAndValues
                    .Where(pair => pair.Key.Text is "NativeParentClass" or "ParentClass" or "NativeClass")
                    .Select(pair => pair.Value ?? string.Empty));

            var route = Array.Find(parents, parent => declared.Contains(parent, StringComparison.OrdinalIgnoreCase));

            if (route is not null)
            {
                yield return (AbilityPresentationIndex.PackagePath(asset.ObjectPath), $"parentClass.{route}");
                continue;
            }

            var name = asset.AssetName.Text;

            if (name.StartsWith("GC_", StringComparison.OrdinalIgnoreCase))
            {
                yield return (AbilityPresentationIndex.PackagePath(asset.ObjectPath), "name.GC_");
            }
            else if (name.Contains("AttributeSet", StringComparison.OrdinalIgnoreCase))
            {
                yield return (AbilityPresentationIndex.PackagePath(asset.ObjectPath), "name.AttributeSet");
            }
            else if (name.Contains("BlueprintLibrary", StringComparison.OrdinalIgnoreCase) ||
                     name.StartsWith("BPFL_", StringComparison.OrdinalIgnoreCase) ||
                     name.StartsWith("BPL_", StringComparison.OrdinalIgnoreCase))
            {
                yield return (AbilityPresentationIndex.PackagePath(asset.ObjectPath), "name.BlueprintLibrary");
            }
            else if (name.StartsWith("GMMC_", StringComparison.OrdinalIgnoreCase) ||
                     name.StartsWith("GEC_", StringComparison.OrdinalIgnoreCase))
            {
                yield return (AbilityPresentationIndex.PackagePath(asset.ObjectPath), "name.GMMC_");
            }
            else if (name.Contains("HeroItemStatCalculation", StringComparison.OrdinalIgnoreCase))
            {
                yield return (AbilityPresentationIndex.PackagePath(asset.ObjectPath), "name.StatCalculation");
            }
        }
    }

    private static void WriteBytecode(
        string root,
        AbstractFileProvider provider,
        IReadOnlyDictionary<string, string> packages,
        IReadOnlyList<Resolved> abilities,
        IReadOnlyList<Resolved> effects,
        Counts counts)
    {
        var folder = Path.Combine(root, "bytecode");
        Directory.CreateDirectory(folder);

        var idByPackage = new Dictionary<string, (string Table, int Id)>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in abilities.Concat(effects))
        {
            if (entry.PackagePath is { } path) idByPackage.TryAdd(path, (entry.Table, entry.Id));
        }

        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = 0;
        var functions = 0;
        var statements = 0;
        var read = 0;

        using var indexStream = File.Create(Path.Combine(folder, "index.json"));
        using var index = new Utf8JsonWriter(indexStream, UeJson.WriterOptions);

        index.WriteStartObject();

        foreach (var (packagePath, route) in packages.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            UObject[] exports;

            try
            {
                if (!provider.TryLoadPackage(packagePath, out var package)) continue;
                exports = package.GetExports().ToArray();
            }
            catch (Exception)
            {
                continue;
            }

            read++;

            foreach (var export in exports)
            {
                if (export is not UStruct body) continue;
                if (body.ScriptBytecode is not { Length: > 0 } bytecode) continue;

                var stem = Trimmed($"{packagePath[(packagePath.LastIndexOf('/') + 1)..]}.{export.Name}");
                var name = $"{stem}.txt";

                for (var attempt = 2; !taken.Add(name); attempt++)
                {
                    name = $"{stem}~{attempt}.txt";
                }

                var target = Path.Combine(folder, name);

                using (var writer = new StreamWriter(target, false, new UTF8Encoding(false)))
                {
                    writer.WriteLine($"# package {packagePath}");
                    writer.WriteLine($"# route {route}");
                    writer.WriteLine($"# class {export.Owner?.Name ?? export.Name}");
                    writer.WriteLine($"# function {export.Name}");
                    writer.WriteLine($"# statements {bytecode.Length}");

                    if (idByPackage.TryGetValue(packagePath, out var identified))
                    {
                        writer.WriteLine($"# exportId {identified.Table} {identified.Id}");
                    }

                    foreach (var child in body.ChildProperties ?? [])
                    {
                        if (child is FProperty property)
                        {
                            writer.WriteLine($"# param {property.Name.Text} {property.PropertyFlags}");
                        }
                    }

                    foreach (var statement in bytecode)
                    {
                        Print(writer, statement, statement.StatementIndex, 0, string.Empty);
                        statements++;
                    }
                }

                index.WritePropertyName(name);
                index.WriteStartObject();
                index.WriteString("package", packagePath);
                index.WriteString("function", export.Name);
                index.WriteString("route", route);
                index.WriteNumber("statements", bytecode.Length);
                if (idByPackage.TryGetValue(packagePath, out var known))
                {
                    index.WriteString("table", known.Table);
                    index.WriteNumber("id", known.Id);
                }
                index.WriteEndObject();

                files++;
                functions++;

                if (functions % 1000 == 0) Console.WriteLine($"  bytecode {functions} functions");
            }
        }

        index.WriteEndObject();
        index.Flush();

        counts.Add("bytecodePackagesRead", read);
        counts.Add("bytecodePackagesRequested", packages.Count);
        counts.Add("bytecodeFiles", files);
        counts.Add("bytecodeFunctions", functions);
        counts.Add("bytecodeStatements", statements);

        Console.WriteLine(
            $"bytecode: {files} files from {read} of {packages.Count} packages, {statements} statements");
    }

    private static string Trimmed(string stem)
    {
        var safe = Safe(stem);
        if (safe.Length <= MaxFileStem) return safe;

        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(safe)))[..8];

        return $"{safe[..(MaxFileStem - 9)]}~{digest}";
    }

    private const int MaxPrintDepth = 24;

    private static void Print(TextWriter writer, object? value, int index, int depth, string label)
    {
        if (depth > MaxPrintDepth) return;

        switch (value)
        {
            case null:
                return;

            case KismetExpression expression:
                PrintExpression(writer, expression, index, depth, label);
                return;

            case string or FName:
                writer.WriteLine($"{index,6}  {new string(' ', depth * 2)}{label}= {Scalar(value)}");
                return;

            case IEnumerable list:
            {
                var at = 0;
                foreach (var element in list)
                {
                    Print(writer, element, index, depth, $"{label}[{at++}]");
                }
                return;
            }

            default:
                if (value.GetType().Namespace?.StartsWith("CUE4Parse.UE4.Kismet", StringComparison.Ordinal) == true)
                {
                    writer.WriteLine(
                        $"{index,6}  {new string(' ', depth * 2)}{label}: {value.GetType().Name}");

                    foreach (var (name, held) in Members(value.GetType()))
                    {
                        Print(writer, Read(held, value), index, depth + 1, name);
                    }

                    return;
                }

                writer.WriteLine($"{index,6}  {new string(' ', depth * 2)}{label}= {Scalar(value)}");
                return;
        }
    }

    private static void PrintExpression(
        TextWriter writer, KismetExpression expression, int index, int depth, string label)
    {
        var scalars = new List<string>();
        var children = new List<(string Name, object? Value)>();

        foreach (var (name, member) in Members(expression.GetType()))
        {
            var value = Read(member, expression);
            if (value is null) continue;

            if (Nested(value))
            {
                children.Add((name, value));
                continue;
            }

            if (Scalar(value) is { Length: > 0 } written) scalars.Add($"{name}={written}");
        }

        var head = $"{index,6}  {new string(' ', depth * 2)}{(label.Length > 0 ? label + ": " : string.Empty)}" +
                   $"{expression.Token}";

        writer.WriteLine(scalars.Count > 0 ? $"{head} {string.Join(' ', scalars)}" : head);

        foreach (var (name, value) in children)
        {
            Print(writer, value, index, depth + 1, name);
        }
    }

    private static bool Nested(object value)
    {
        if (value is KismetExpression) return true;
        if (value is string or FName or FKismetPropertyPointer or FPackageIndex) return false;

        if (value is IEnumerable list)
        {
            foreach (var element in list) return element is not null && Nested(element);
            return false;
        }

        return value.GetType().Namespace?.StartsWith("CUE4Parse.UE4.Kismet", StringComparison.Ordinal) == true;
    }

    private static readonly Dictionary<Type, (string Name, MemberInfo Member)[]> Reflected = [];

    private static (string Name, MemberInfo Member)[] Members(Type type)
    {
        if (Reflected.TryGetValue(type, out var known)) return known;

        var found = new List<(string, MemberInfo)>();

        foreach (System.Reflection.PropertyInfo property in
                 type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0) continue;
            if (property.Name is "Token" or "StatementIndex") continue;

            found.Add((property.Name, property));
        }

        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (field.Name is "Token" or "StatementIndex") continue;

            found.Add((field.Name, field));
        }

        return Reflected[type] = [.. found];
    }

    private static object? Read(MemberInfo member, object holder)
    {
        try
        {
            return member switch
            {
                System.Reflection.PropertyInfo property => property.GetValue(holder),
                FieldInfo field => field.GetValue(holder),
                _ => null,
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? Scalar(object? value) => value switch
    {
        null => null,
        string text => text,
        FName name => name.Text,
        FPackageIndex index => index.ResolvedObject?.GetPathName() ?? index.Name,
        FKismetPropertyPointer pointer => Pointer(pointer),
        FSoftObjectPath soft => soft.ToString(),
        bool truth => truth ? "true" : "false",
        float number => number.ToString("R", CultureInfo.InvariantCulture),
        double number => number.ToString("R", CultureInfo.InvariantCulture),
        Enum member => member.ToString(),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        IEnumerable list => string.Join('|', list.Cast<object?>().Select(Scalar)),
        _ => value.ToString(),
    };

    private static string Pointer(FKismetPropertyPointer pointer)
    {
        if (pointer.New?.Path is { Length: > 0 } path)
        {
            return $"{pointer.New.ResolvedOwner?.Name ?? string.Empty}.{string.Join('.', path.Select(p => p.Text))}";
        }

        return pointer.Old?.Name ?? "unnamed";
    }

    private static void WriteManifest(string root, ExtractorOptions options, Counts counts, TimeSpan took)
    {
        var folders = new SortedDictionary<string, int>(StringComparer.Ordinal);

        foreach (var folder in Directory.EnumerateDirectories(root))
        {
            folders[Path.GetFileName(folder)] = Directory.GetFiles(folder).Length;
        }

        using var stream = File.Create(Path.Combine(root, "manifest.json"));
        using var writer = new Utf8JsonWriter(stream, UeJson.WriterOptions);

        writer.WriteStartObject();
        writer.WriteString("root", root);
        writer.WriteString("mappings", options.ResolvedMappingsFile);
        writer.WriteString("paks", options.PaksDir);
        writer.WriteString("took", took.ToString());

        writer.WritePropertyName("files");
        writer.WriteStartObject();
        foreach (var (folder, many) in folders) writer.WriteNumber(folder, many);
        writer.WriteEndObject();

        writer.WritePropertyName("counts");
        writer.WriteStartObject();
        foreach (var (key, value) in counts.Values) writer.WriteNumber(key, value);
        writer.WriteEndObject();

        writer.WriteEndObject();
        writer.Flush();
    }
}
