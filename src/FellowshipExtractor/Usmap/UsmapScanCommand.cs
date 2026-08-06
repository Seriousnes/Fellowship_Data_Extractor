namespace FellowshipExtractor.Usmap;

public static class UsmapScanCommand
{
    public static string ShippingExe(ExtractorOptions options) =>
        Path.Combine(options.InstallDir, "fellowship", "Binaries", "Win64", "fellowship-Win64-Shipping.exe");

    public static Task RunAsync(ExtractorOptions options)
    {
        var image = new PeImage(ShippingExe(options));
        Console.WriteLine($"ImageBase 0x{image.ImageBase:X}, {image.Sections.Count} sections");

        var scanner = new ReflectionScanner(image);
        var records = scanner.ScanProperties();
        Console.WriteLine($"property records: {records.Count}");

        var withTypeFunc = records.Count(r => r.TypeFuncVa != 0);
        Console.WriteLine($"  with a .text type function at +0x38: {withTypeFunc}");

        foreach (var group in new[]
                 {
                     EPropertyGenFlags.Struct, EPropertyGenFlags.Object, EPropertyGenFlags.Enum,
                     EPropertyGenFlags.Byte, EPropertyGenFlags.Class, EPropertyGenFlags.Int,
                     EPropertyGenFlags.Float, EPropertyGenFlags.Array,
                 })
        {
            var of = records.Where(r => r.Type == group).ToArray();
            if (of.Length == 0) continue;
            var resolved = of.Count(r => r.TypeFuncVa != 0);
            Console.WriteLine($"    {group,-10} {of.Length,6}  typeFunc {resolved,6} ({100.0 * resolved / of.Length:F0}%)");
        }

        var arrays = scanner.ScanPropertyArrays(records);
        var covered = arrays.Sum(a => a.Properties.Count);
        Console.WriteLine($"property arrays:  {arrays.Count}");
        Console.WriteLine($"  entries covered: {covered} of {records.Count} records ({100.0 * covered / records.Count:F1}%)");

        var sizes = arrays.GroupBy(a => a.Properties.Count switch
        {
            1 => "1", 2 => "2", <= 4 => "3-4", <= 8 => "5-8", <= 16 => "9-16", <= 32 => "17-32", _ => "33+",
        }).OrderByDescending(g => g.Count());
        Console.WriteLine("  array length distribution: " + string.Join("  ", sizes.Select(g => $"{g.Key}:{g.Count()}")));

        var structs = new OwnerScanner(image, records).ScanStructs();
        var distinct = structs.Select(s => s.Name).Distinct().Count();
        var withSuper = structs.Count(s => s.SuperFuncVa != 0);
        Console.WriteLine($"struct params: {structs.Count} ({distinct} distinct names)");
        Console.WriteLine($"  covered properties: {structs.Sum(s => s.Properties.Count)} of {records.Count}");
        Console.WriteLine($"  with a super func:  {withSuper}");

        Console.WriteLine("--- known structs ---");
        foreach (var want in new[] { "GameplayTag", "GameplayTagContainer", "ScalableFloat", "Vector", "DataTableRowHandle", "GameplayEffectSpec" })
        {
            var match = structs.FirstOrDefault(s => s.Name == want);
            if (match is null) { Console.WriteLine($"  {want,-22} not found"); continue; }
            var props = string.Join(", ", match.Properties.Take(8).Select(p => $"{p.Name}:{p.Type}"));
            Console.WriteLine($"  {want,-22} [{match.Properties.Count}] {props}");
        }

        var classScanner = new ClassScanner(image, records);
        var registrations = classScanner.ScanRegistrations();
        Console.WriteLine($"class registrations (func -> C++ name): {registrations.Count}");

        var claimed = structs.Select(s => s.Properties.Count > 0 ? s.Properties[0].Va : 0).ToHashSet();
        var classes = classScanner.ScanClasses(registrations, claimed);
        Console.WriteLine($"class params: {classes.Count} ({classes.Select(c => c.Name).Distinct().Count()} distinct)");

        Console.WriteLine("--- gameplay classes ---");
        foreach (var want in new[]
                 {
                     "CRDynamicHeroItemDefinition", "CRVendorItemDataAsset", "CRTalentData",
                     "CRMountData", "CRGameplayAbility", "DataTable",
                 })
        {
            var match = classes.FirstOrDefault(c => c.Name == want);
            if (match is null) { Console.WriteLine($"  {want,-30} not found"); continue; }
            var props = string.Join(", ", match.Properties.Take(6).Select(p => $"{p.Name}:{p.Type}"));
            Console.WriteLine($"  {want,-30} [{match.Properties.Count}] {props}");
        }

        return Task.CompletedTask;
    }
}
