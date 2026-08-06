using CUE4Parse.MappingsProvider.Usmap;

namespace FellowshipExtractor.Usmap;

public static class UsmapDiffCommand
{
    public static Task RunAsync(ExtractorOptions options, string referencePath)
    {
        var image = new PeImage(UsmapScanCommand.ShippingExe(options));
        var records = new ReflectionScanner(image).ScanProperties();
        var structs = new OwnerScanner(image, records).ScanStructs();

        var recovered = structs
            .GroupBy(s => s.Name)
            .ToDictionary(g => g.Key, g => SchemaBuilder.Build(g.Key, g.First().Properties));

        var reference = new UsmapParser(referencePath).Mappings
            ?? throw new InvalidDataException($"No mappings in {referencePath}.");
        Console.WriteLine($"reference: {Path.GetFileName(referencePath)}  schemas={reference.Types.Count}");
        Console.WriteLine($"recovered: {recovered.Count} structs");

        int compared = 0, exact = 0, countMismatch = 0, nameMismatch = 0;
        var failures = new List<string>();

        foreach (var (name, mine) in recovered.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (!reference.Types.TryGetValue(name, out var theirs)) continue;
            compared++;

            var theirProps = Enumerable.Range(0, theirs.PropertyCount)
                .Select(i => theirs.TryGetValue(i, out var p) ? p : null)
                .Where(p => p is not null)
                .Select(p => p!.Name)
                .ToArray();
            var myProps = mine.Properties.Select(p => p.Name).ToArray();

            if (myProps.Length != theirProps.Length)
            {
                countMismatch++;
                if (failures.Count < 15)
                    failures.Add($"  {name}: {myProps.Length} vs {theirProps.Length}\n      mine  [{string.Join(", ", myProps)}]\n      ref   [{string.Join(", ", theirProps)}]");
                continue;
            }

            if (!myProps.SequenceEqual(theirProps, StringComparer.Ordinal))
            {
                nameMismatch++;
                if (failures.Count < 15)
                    failures.Add($"  {name}: order/name differs\n      mine  [{string.Join(", ", myProps)}]\n      ref   [{string.Join(", ", theirProps)}]");
                continue;
            }

            exact++;
        }

        Console.WriteLine($"\ncompared {compared} structs present in both");
        Console.WriteLine($"  exact match:     {exact} ({100.0 * exact / Math.Max(1, compared):F1}%)");
        Console.WriteLine($"  count mismatch:  {countMismatch}");
        Console.WriteLine($"  name/order diff: {nameMismatch}");

        if (failures.Count > 0)
        {
            Console.WriteLine("\n--- sample failures ---");
            foreach (var f in failures) Console.WriteLine(f);
        }

        return Task.CompletedTask;
    }
}
