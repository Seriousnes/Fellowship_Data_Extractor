using CUE4Parse.MappingsProvider.Usmap;

namespace FellowshipExtractor.Usmap;

public static class UsmapGenerateCommand
{
    private static readonly string[] Required =
    [
        "GameplayTag", "ScalableFloat", "CRMountData", "CRTalentData", "DataTable", "Object",
        "PrimaryDataAsset",
    ];

    public static Task<bool> RunAsync(ExtractorOptions options)
    {
        var image = new PeImage(options.ShippingExe);
        var records = new ReflectionScanner(image).ScanProperties();
        var structs = new OwnerScanner(image, records).ScanStructs();
        var enums = new EnumScanner(image).Scan();

        var classScanner = new ClassScanner(image, records);
        var registrations = classScanner.ScanRegistrations();
        var claimed = structs.Where(s => s.Properties.Count > 0).Select(s => s.Properties[0].Va).ToHashSet();
        var classes = classScanner.ScanClasses(registrations, claimed);

        var paramsByVa = structs.ToDictionary(s => s.Va, s => s.Name);
        foreach (var declaring in enums) paramsByVa.TryAdd(declaring.Va, declaring.Name);
        var decoded = TypeFuncResolver.Resolve(
            image,
            records.Select(r => r.TypeFuncVa)
                .Concat(structs.Select(s => s.SuperFuncVa))
                .Concat(classes.Select(c => c.SuperFuncVa)),
            paramsByVa);
        Console.WriteLine($"type functions decoded from .text: {decoded.Count}");

        string? Resolve(ulong funcVa) =>
            decoded.TryGetValue(funcVa, out var direct) ? direct
            : registrations.TryGetValue(funcVa, out var cpp) ? ClassScanner.StripPrefix(cpp)
            : null;

        var schemas = new Dictionary<string, UsmapSchema>(StringComparer.Ordinal);

        foreach (var s in structs)
        {
            var built = SchemaBuilder.Build(s.Name, s.Properties, Resolve);
            Keep(schemas, new UsmapSchema(s.Name, Resolve(s.SuperFuncVa), built.Properties));
        }

        foreach (var c in classes)
        {
            var built = SchemaBuilder.Build(c.Name, c.Properties, Resolve);
            var super = Resolve(c.SuperFuncVa);
            Keep(schemas, new UsmapSchema(c.Name, super == c.Name ? null : super, built.Properties));
        }

        foreach (var super in schemas.Values.Select(s => s.SuperName).Where(n => n is not null).Distinct().ToArray())
        {
            if (!schemas.ContainsKey(super!)) schemas[super!] = new UsmapSchema(super!, null, []);
        }

        var ordered = schemas.Values.OrderBy(s => s.Name, StringComparer.Ordinal).ToArray();

        var tables = enums
            .GroupBy(declaring => declaring.Name, StringComparer.Ordinal)
            .Select(named => named.MaxBy(declaring => declaring.Members.Count)!)
            .Select(EnumScanner.ByOrdinal)
            .OfType<UsmapEnum>()
            .OrderBy(table => table.Name, StringComparer.Ordinal)
            .ToArray();

        var target = options.GeneratedMappingsFile(GameBuild.Read(options));
        Directory.CreateDirectory(options.ResolvedMappingsDir);
        new UsmapWriter().Write(target, ordered, tables);

        var size = new FileInfo(target).Length;
        Console.WriteLine($"structs {structs.Count}, classes {classes.Count} -> {ordered.Length} schemas");
        Console.WriteLine($"  with a super: {ordered.Count(s => s.SuperName is not null)}");

        var declared = enums.Sum(declaring => EnumScanner.Declared(declaring).Count);
        var written = enums.Sum(declaring => EnumScanner.Declared(declaring).Count(EnumScanner.Fits));
        Console.WriteLine($"enums {enums.Count} -> {tables.Length} tables, {declared} members");
        Console.WriteLine($"  outside the ordinals a usmap can carry: {declared - written}");
        Console.WriteLine($"  properties naming an enum: {ordered.Sum(s => s.Properties.Count(NamesAnEnum))}");
        Console.WriteLine($"wrote {target} ({size / 1024} KB)");

        try
        {
            var parsed = new UsmapParser(target);
            var mappings = parsed.Mappings ?? throw new InvalidDataException("Round-trip produced no mappings.");
            Console.WriteLine($"round-trip OK: version={parsed.Version} schemas={mappings.Types.Count} enums={mappings.Enums.Count}");

            List<string> missing = [];

            foreach (var want in Required)
            {
                if (mappings.Types.TryGetValue(want, out var schema))
                {
                    var names = Enumerable.Range(0, schema.PropertyCount)
                        .Select(i => schema.TryGetValue(i, out var p) ? p!.Name : "?");
                    Console.WriteLine($"  {want,-20} super={schema.SuperType ?? "(none)"} [{schema.PropertyCount}] {string.Join(", ", names)}");
                }
                else
                {
                    Console.WriteLine($"  {want,-20} missing");
                    missing.Add(want);
                }
            }

            if (missing.Count > 0)
            {
                Console.Error.WriteLine(
                    $"{target} resolves no schema for {string.Join(", ", missing)}, so the tables those " +
                    "types carry would read back empty. The mappings this run wrote are the ones on disk.");

                return Task.FromResult(false);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"round-trip FAILED: {ex.GetType().Name}: {ex.Message}");

            return Task.FromResult(false);
        }

        Console.WriteLine($"\nWrote {target}. The extractor reads it with no environment set.");
        return Task.FromResult(true);
    }

    private static bool NamesAnEnum(SchemaProperty property) =>
        (property.Type == EPropertyGenFlags.Enum && property.StructOrEnumName is not null)
        || property.Inners.Any(NamesAnEnum);

    private static void Keep(Dictionary<string, UsmapSchema> schemas, UsmapSchema candidate)
    {
        if (!schemas.TryGetValue(candidate.Name, out var existing)
            || candidate.Properties.Count > existing.Properties.Count
            || (candidate.Properties.Count == existing.Properties.Count && existing.SuperName is null && candidate.SuperName is not null))
        {
            schemas[candidate.Name] = candidate;
        }
    }
}
