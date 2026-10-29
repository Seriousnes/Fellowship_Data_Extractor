using System.Diagnostics;
using System.Text;
using System.Text.Json;

using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;

using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Blueprints;

public static class BlueprintsCommand
{
    public const string GeneratedClassTag = "GeneratedClass";
    public const string GeneratedClassSuffix = "GeneratedClass";
    public const string UberGraphFunction = "UberGraphFunction";
    public const string DefaultsFile = "defaults.json";

    public sealed record Written(int Packages, int Files, long Bytes, int Failures);

    public static async Task<int> RunAsync(ExtractorOptions options, string? filter)
    {
        var clock = Stopwatch.StartNew();
        var provider = await ProviderFactory.CreateAsync(options);

        if (provider.MappingsContainer?.MappingsForGame is null)
        {
            Console.Error.WriteLine(
                $"No mappings are loaded, so every cooked package reads back as nothing. Write " +
                $"mappings/{ExtractorOptions.MappingsPattern} with 'usmap' first.");
            return 1;
        }

        var root = options.ResolvedBlueprintsDir;
        if (filter is null && Directory.Exists(root)) Directory.Delete(root, recursive: true);
        Directory.CreateDirectory(root);

        var packages = Packages(provider)
            .Where(path => filter is null || path.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Console.WriteLine($"{packages.Length} blueprint packages in the asset registry");

        var written = Write(provider, packages, root, (done, failed) =>
        {
            if (done % 500 == 0) Console.WriteLine($"  {done} of {packages.Length} packages, {failed} unread");
        });

        Console.WriteLine(
            $"wrote {written.Files} files ({written.Bytes / (1024.0 * 1024.0):F1} MiB) for {written.Packages} " +
            $"packages under {root} in {clock.Elapsed}, {written.Failures} packages unread");

        return 0;
    }

    public static IEnumerable<string> Packages(AbstractFileProvider provider) =>
        AssetRegistry.ReadAssets(provider)
            .Where(asset => AssetRegistry.TagValue(asset, GeneratedClassTag) is not null ||
                            asset.AssetClass.Text.EndsWith(GeneratedClassSuffix, StringComparison.Ordinal))
            .Select(asset => AbilityPresentationIndex.PackagePath(asset.ObjectPath))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase);

    public static Written Write(
        AbstractFileProvider provider, IReadOnlyList<string> packages, string root, Action<int, int>? progress = null)
    {
        var files = 0;
        var bytes = 0L;
        var failures = 0;
        var done = 0;

        foreach (var packagePath in packages)
        {
            UObject[] exports;

            try
            {
                exports = provider.TryLoadPackage(packagePath, out var package) ? [.. package.GetExports()] : [];
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"  {packagePath}: {exception.GetType().Name}: {exception.Message}");
                exports = [];
            }

            if (exports.Length == 0) failures++;
            else
            {
                var folder = Path.Combine(root, Path.Combine(packagePath.Split('/', StringSplitOptions.RemoveEmptyEntries)));
                foreach (var file in Write(packagePath, exports, folder))
                {
                    files++;
                    bytes += new FileInfo(file).Length;
                }
            }

            progress?.Invoke(++done, failures);
        }

        return new Written(packages.Count - failures, files, bytes, failures);
    }

    public static IEnumerable<string> Write(string packagePath, IReadOnlyList<UObject> exports, string folder)
    {
        Directory.CreateDirectory(folder);

        var classes = exports.OfType<UClass>().ToArray();
        var functions = exports
            .OfType<UStruct>()
            .Where(body => body is not UClass && body.ScriptBytecode is { Length: > 0 })
            .ToArray();

        var defaults = Path.Combine(folder, DefaultsFile);
        WriteDefaults(defaults, classes, exports.Where(export => Defaults(export, classes)).ToArray());
        yield return defaults;

        var entries = Entries(classes, functions);
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var function in functions)
        {
            var stem = DumpAll.Trimmed(function.Name);
            if (!taken.Add(stem)) stem = DumpAll.Trimmed($"{function.Outer?.Name}.{function.Name}");

            var target = Path.Combine(folder, $"{stem}.txt");
            File.WriteAllText(target, Listing(packagePath, classes, function, entries), new UTF8Encoding(false));
            yield return target;
        }
    }

    public static string Listing(
        string packagePath, IReadOnlyList<UClass> classes, UStruct function, IReadOnlyDictionary<string, Dictionary<long, List<string>>> entries)
    {
        var text = new StringBuilder();
        text.AppendLine($"// package {packagePath}");

        if (classes.FirstOrDefault(owner => owner.Name == function.Outer?.Name.Text) is { } owner)
        {
            text.AppendLine($"// class {owner.Name} : {KismetPrinter.Object(owner.SuperStruct)}");
        }

        text.AppendLine($"// function {function.Name}");

        if (function is UFunction flagged) text.AppendLine($"// flags {flagged.FunctionFlags}");
        if (function.SuperStruct is { IsNull: false } super) text.AppendLine($"// overrides {KismetPrinter.Object(super)}");

        foreach (var child in function.ChildProperties ?? [])
        {
            if (child is not FProperty property) continue;

            var kind = property.PropertyFlags.HasFlag(EPropertyFlags.ReturnParm) ? "return"
                : property.PropertyFlags.HasFlag(EPropertyFlags.OutParm) ? "out"
                : property.PropertyFlags.HasFlag(EPropertyFlags.Parm) ? "param"
                : "local";

            text.AppendLine($"// {kind} {property.Name.Text} : {property.GetType().Name[1..]}");
        }

        var bytecode = function.ScriptBytecode ?? [];

        foreach (var (ubergraph, offset) in Calls(bytecode, entries.Keys))
        {
            text.AppendLine($"// event {function.Name} enters {ubergraph} at {KismetPrinter.Label(offset)}");
        }

        text.AppendLine();

        var own = entries.TryGetValue(function.Name, out var found)
            ? found.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value)
            : null;

        foreach (var line in KismetListing.Structured(bytecode, own)) text.AppendLine(line);

        return text.ToString();
    }

    public static Dictionary<string, Dictionary<long, List<string>>> Entries(
        IReadOnlyList<UClass> classes, IReadOnlyList<UStruct> functions)
    {
        var ubergraphs = classes
            .Select(owner => owner.GetOrDefault<FPackageIndex>(UberGraphFunction))
            .Where(index => index is { IsNull: false })
            .Select(index => index.Name)
            .ToHashSet(StringComparer.Ordinal);

        var entries = ubergraphs.ToDictionary(
            name => name, _ => new Dictionary<long, List<string>>(), StringComparer.Ordinal);

        foreach (var function in functions)
        {
            var bytecode = function.ScriptBytecode ?? [];

            foreach (var (ubergraph, offset) in Calls(bytecode, ubergraphs))
            {
                Add(entries[ubergraph], offset, function.Name);
            }

            foreach (var structure in bytecode.SelectMany(s => BytecodeGates.Descendants(s, 0)).OfType<EX_StructConst>())
            {
                var properties = structure.Properties ?? [];
                if (properties.OfType<EX_NameConst>().FirstOrDefault(name => ubergraphs.Contains(name.Value.Text)) is not { } resumed) continue;
                if (properties.OfType<EX_SkipOffsetConst>().FirstOrDefault() is not { } linkage) continue;

                Add(entries[resumed.Value.Text], linkage.Value, $"{function.Name} latent resume");
            }
        }

        return entries;
    }

    private static IEnumerable<(string Ubergraph, long Offset)> Calls(KismetExpression[] bytecode, IEnumerable<string> ubergraphs)
    {
        var names = ubergraphs.ToHashSet(StringComparer.Ordinal);

        foreach (var call in bytecode.SelectMany(s => BytecodeGates.Descendants(s, 0)).OfType<EX_FinalFunction>())
        {
            if (call.StackNode is not { IsNull: false } node || !names.Contains(node.Name)) continue;
            if (call.Parameters is not [EX_IntConst entry]) continue;

            yield return (node.Name, entry.Value);
        }
    }

    private static void Add(Dictionary<long, List<string>> entries, long offset, string name)
    {
        if (!entries.TryGetValue(offset, out var names)) entries[offset] = names = [];
        if (!names.Contains(name)) names.Add(name);
    }

    private static bool Defaults(UObject export, IReadOnlyList<UClass> classes)
    {
        if (export is UStruct { ScriptBytecode.Length: > 0 } and not UClass) return false;
        if (classes.Count == 0) return true;

        var path = export.GetPathName();

        return classes
            .SelectMany(owner => new[] { owner.GetPathName(), owner.ClassDefaultObject?.ResolvedObject?.GetPathName() })
            .OfType<string>()
            .Any(anchor => path.Equals(anchor, StringComparison.OrdinalIgnoreCase) ||
                           path.StartsWith($"{anchor}:", StringComparison.OrdinalIgnoreCase) ||
                           path.StartsWith($"{anchor}.", StringComparison.OrdinalIgnoreCase));
    }

    private static void WriteDefaults(string target, IReadOnlyList<UClass> classes, IReadOnlyList<UObject> exports)
    {
        using var stream = File.Create(target);
        using var writer = new Utf8JsonWriter(stream, UeJson.WriterOptions);

        writer.WriteStartObject();

        writer.WritePropertyName("classes");
        writer.WriteStartArray();
        foreach (var owner in classes)
        {
            writer.WriteStartObject();
            writer.WriteString("class", owner.GetPathName());
            writer.WriteString("super", KismetPrinter.Object(owner.SuperStruct));
            writer.WriteString("defaultObject", KismetPrinter.Object(owner.ClassDefaultObject));
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WritePropertyName("exports");
        writer.WriteStartArray();
        foreach (var export in exports) UeJson.WriteObject(writer, export);
        writer.WriteEndArray();

        writer.WriteEndObject();
        writer.Flush();
    }
}
