using CUE4Parse.MappingsProvider.Usmap;

namespace FellowshipExtractor.Usmap;

public static class UsmapCountProbeCommand
{
    public static Task RunAsync(ExtractorOptions options, string referencePath)
    {
        var image = new PeImage(UsmapScanCommand.ShippingExe(options));
        var records = new ReflectionScanner(image).ScanProperties();
        var structs = new OwnerScanner(image, records).ScanStructs();
        var classScanner = new ClassScanner(image, records);
        var registrations = classScanner.ScanRegistrations();
        var claimed = structs.Where(s => s.Properties.Count > 0).Select(s => s.Properties[0].Va).ToHashSet();
        var classes = classScanner.ScanClasses(registrations, claimed);

        var reference = new UsmapParser(referencePath).Mappings
            ?? throw new InvalidDataException($"No mappings in {referencePath}.");
        Console.WriteLine($"classes {classes.Count}, reference schemas {reference.Types.Count}");

        var votes16 = new Dictionary<int, int>();
        var votes32 = new Dictionary<int, int>();
        var tested = 0;

        foreach (var c in classes)
        {
            if (!reference.Types.TryGetValue(c.Name, out var truth)) continue;
            var expected = truth.PropertyCount;
            if (expected == 0) continue;

            var arrayOffset = image.Offset(c.Va);
            if (arrayOffset < 0) continue;
            tested++;

            for (var delta = -0x40; delta <= 0x40; delta += 2)
            {
                if (image.ReadUInt16(arrayOffset + delta) == expected)
                    votes16[delta] = votes16.GetValueOrDefault(delta) + 1;
            }
            for (var delta = -0x40; delta <= 0x40; delta += 4)
            {
                if (image.ReadUInt32(arrayOffset + delta) == (uint)expected)
                    votes32[delta] = votes32.GetValueOrDefault(delta) + 1;
            }
        }

        Console.WriteLine($"classes present in the reference: {tested}");
        Console.WriteLine("top uint16 offsets:");
        foreach (var (delta, count) in votes16.OrderByDescending(kv => kv.Value).Take(6))
            Console.WriteLine($"  {delta,+5}  {count,5} ({100.0 * count / Math.Max(1, tested):F1}%)");
        Console.WriteLine("top uint32 offsets:");
        foreach (var (delta, count) in votes32.OrderByDescending(kv => kv.Value).Take(6))
            Console.WriteLine($"  {delta,+5}  {count,5} ({100.0 * count / Math.Max(1, tested):F1}%)");

        return Task.CompletedTask;
    }
}
