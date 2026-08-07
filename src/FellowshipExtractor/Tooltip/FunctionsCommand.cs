using System.Text.Json;

using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;

using FellowshipExtractor.Commands;

namespace FellowshipExtractor.Tooltip;

public sealed record FunctionSignature(string Name, string Type, bool Out);

public sealed record FunctionRecord(
    string Name,
    int Classes,
    int Statements,
    IReadOnlyList<FunctionSignature> Parameters,
    IReadOnlyList<string> Calls,
    IReadOnlyList<string> DeclaredBy);

public static class FunctionsCommand
{
    private const string BlueprintClass = "BlueprintGeneratedClass";
    private const int Examples = 5;

    private static readonly JsonSerializerOptions Compact = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static async Task RunAsync(ExtractorOptions options)
    {
        var provider = await ProviderFactory.CreateAsync(options);
        var assets = RegistryCommand.ReadAssets(provider).ToArray();

        var packages = assets
            .Where(asset => asset.AssetClass.Text.Equals(BlueprintClass, StringComparison.Ordinal))
            .Select(asset => asset.PackageName.Text)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Console.WriteLine($"{packages.Length} Blueprint classes to walk of {assets.Length} assets");

        var byName = new Dictionary<string, Tally>(StringComparer.Ordinal);
        var read = 0;
        var unreadable = 0;

        foreach (var package in packages)
        {
            if (++read % 500 == 0) Console.WriteLine($"  {read} of {packages.Length}...");

            IEnumerable<UObject> exports;
            try
            {
                if (!provider.TryLoadPackage(package, out var loaded)) { unreadable++; continue; }
                exports = loaded.GetExports();
            }
            catch (Exception)
            {
                unreadable++;
                continue;
            }

            foreach (var export in exports)
            {
                if (export is not UStruct { ScriptBytecode: { Length: > 0 } bytecode } body) continue;

                var tally = Take(byName, body.Name);
                tally.Classes++;
                tally.Statements += bytecode.Length;

                if (tally.DeclaredBy.Count < Examples) tally.DeclaredBy.Add(package);

                if (tally.Parameters.Count == 0) tally.Parameters.AddRange(Parameters(body));

                foreach (var called in Calls(bytecode))
                {
                    if (called.StartsWith("ExecuteUbergraph_", StringComparison.Ordinal)) continue;
                    tally.Calls.Add(called);
                }
            }
        }

        var records = byName
            .Select(entry => new FunctionRecord(
                entry.Key,
                entry.Value.Classes,
                entry.Value.Statements,
                entry.Value.Parameters,
                [.. entry.Value.Calls.OrderBy(name => name, StringComparer.Ordinal)],
                entry.Value.DeclaredBy))
            .OrderByDescending(record => record.Classes)
            .ThenBy(record => record.Name, StringComparer.Ordinal)
            .ToArray();

        var path = Path.Combine(options.ResolvedOutputDir, "blueprint-functions.jsonl");
        Directory.CreateDirectory(options.ResolvedOutputDir);

        await using (var writer = new StreamWriter(path, false))
        {
            foreach (var record in records)
            {
                await writer.WriteLineAsync(JsonSerializer.Serialize(record, Compact));
            }
        }

        Console.WriteLine($"\nwrote {records.Length} distinct function names to {path}");
        Console.WriteLine($"  {read - unreadable} classes read, {unreadable} unreadable");
        Console.WriteLine($"  {records.Count(r => r.Classes > 1)} names declared by more than one class");

        Console.WriteLine("\nthe hooks many classes share:");
        foreach (var record in records.Where(r => r.Classes >= 20).Take(40))
        {
            Console.WriteLine($"  {record.Classes,6}  {record.Name}");
        }
    }

    private sealed class Tally
    {
        public int Classes;
        public int Statements;
        public readonly List<FunctionSignature> Parameters = [];
        public readonly SortedSet<string> Calls = new(StringComparer.Ordinal);
        public readonly List<string> DeclaredBy = [];
    }

    private static Tally Take(Dictionary<string, Tally> byName, string name)
    {
        if (!byName.TryGetValue(name, out var tally)) byName[name] = tally = new Tally();
        return tally;
    }

    private static IEnumerable<FunctionSignature> Parameters(UStruct body)
    {
        foreach (var child in body.ChildProperties ?? [])
        {
            if (child.Name.Text.StartsWith("CallFunc_", StringComparison.Ordinal)) continue;
            if (child.Name.Text.StartsWith("K2Node_", StringComparison.Ordinal)) continue;
            if (child.Name.Text.StartsWith("Temp_", StringComparison.Ordinal)) continue;

            yield return new FunctionSignature(
                child.Name.Text,
                child.GetType().Name,
                child.Name.Text is "ReturnValue" or "MinValue" or "MaxValue");
        }
    }

    private static IEnumerable<string> Calls(KismetExpression[] bytecode)
    {
        foreach (var statement in bytecode)
        {
            foreach (var called in Walk(statement, 0)) yield return called;
        }
    }

    private static IEnumerable<string> Walk(KismetExpression? expression, int depth)
    {
        if (expression is null || depth > 12) yield break;

        switch (expression)
        {
            case EX_CallMath call:
                yield return call.StackNode.Name;
                foreach (var parameter in call.Parameters)
                {
                    foreach (var nested in Walk(parameter, depth + 1)) yield return nested;
                }

                break;

            case EX_LocalVirtualFunction call:
                yield return call.VirtualFunctionName.Text;
                break;

            case EX_LocalFinalFunction call:
                yield return call.StackNode.Name;
                break;

            case EX_VirtualFunction call:
                yield return call.VirtualFunctionName.Text;
                break;

            case EX_FinalFunction call:
                yield return call.StackNode.Name;
                break;

            case EX_Context context:
                foreach (var nested in Walk(context.ContextExpression, depth + 1)) yield return nested;
                break;

            case EX_Let let:
                foreach (var nested in Walk(let.Assignment, depth + 1)) yield return nested;
                break;

            case EX_LetBool let:
                foreach (var nested in Walk(let.Assignment, depth + 1)) yield return nested;
                break;

            case EX_LetObj let:
                foreach (var nested in Walk(let.Assignment, depth + 1)) yield return nested;
                break;
        }
    }
}
