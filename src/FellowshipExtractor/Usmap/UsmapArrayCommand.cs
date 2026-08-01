namespace FellowshipExtractor.Usmap;

/// <summary>
/// Walks one class's PropPointers[] array slot by slot, annotating why the run ends. Used to
/// diagnose truncated class property lists.
/// </summary>
public static class UsmapArrayCommand
{
    public static Task RunAsync(ExtractorOptions options, string className)
    {
        var image = new PeImage(UsmapScanCommand.ShippingExe(options));
        var records = new ReflectionScanner(image).ScanProperties();
        var recordByVa = records.ToDictionary(r => r.Va);

        var structs = new OwnerScanner(image, records).ScanStructs();
        var scanner = new ClassScanner(image, records);
        var registrations = scanner.ScanRegistrations();
        var claimed = structs.Where(s => s.Properties.Count > 0).Select(s => s.Properties[0].Va).ToHashSet();
        var classes = scanner.ScanClasses(registrations, claimed);

        // FClassParams stores the property array at its own address; FStructParams at +0x20.
        var owners = classes
            .Where(c => c.Name == className)
            .Select(c => (Kind: "class", c.Va, c.Properties))
            .Concat(structs
                .Where(s => s.Name == className)
                .Select(s => (Kind: "struct", Va: s.Va + 0x20, s.Properties)))
            .ToArray();

        Console.WriteLine($"'{className}': {owners.Length} params record(s)");

        foreach (var c in owners.Take(3))
        {
            Console.WriteLine($"\n=== {c.Kind}");
            var arrayFieldOffset = image.Offset(c.Va);
            var arrayVa = image.ReadPointer(arrayFieldOffset);
            Console.WriteLine($"\n  params@0x{arrayFieldOffset:X} array=0x{arrayVa:X} recovered={c.Properties.Count}");

            var slotOffset = image.Offset(arrayVa);
            for (var i = 0; i < 24 && slotOffset >= 0; i++)
            {
                var thisSlot = slotOffset + i * 8;
                var slotVa = image.VirtualAddress(thisSlot);
                var target = image.ReadPointer(thisSlot);

                if (recordByVa.TryGetValue(target, out var record))
                {
                    var referrers = CountReferrers(image, slotVa);
                    Console.WriteLine($"    [{i,2}] slot=0x{slotVa:X} -> {record.Name}:{record.Type}   referrers={referrers}");
                }
                else
                {
                    var text = image.ReadUtf8(target, 40);
                    var where = image.InSection(target, ".text") ? ".text"
                        : image.InSection(target, ".rdata") ? ".rdata"
                        : image.InSection(target, ".data") ? ".data" : "-";
                    Console.WriteLine($"    [{i,2}] slot=0x{slotVa:X} -> NOT A RECORD  0x{target:X} {where} {(text is null ? "" : $"\"{text}\"")}");

                    // Show why the scanner rejected it.
                    var recOffset = image.Offset(target);
                    if (recOffset < 0) continue;
                    var name = image.ReadUtf8(image.ReadPointer(recOffset));
                    Console.WriteLine($"         name={(name ?? "<none>")}  gen={image.ReadUInt32(recOffset + 0x18)}  " +
                                      $"objFlags=0x{image.ReadUInt32(recOffset + 0x1C):X}  " +
                                      $"cpf=0x{image.ReadUInt64(recOffset + 0x10):X}  repNotify=0x{image.ReadPointer(recOffset + 8):X}");
                }
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>How many qwords in the image point at this slot. A run start has at least one.</summary>
    private static int CountReferrers(PeImage image, ulong slotVa)
    {
        var count = 0;
        foreach (var section in image.Sections)
        {
            if (section.RawSize == 0 || section.Name is ".text" or ".pdata" or ".reloc" or ".rsrc") continue;
            var end = section.RawOffset + Math.Min(section.RawSize, section.VirtualSize);
            for (var offset = (int)section.RawOffset; offset + 8 <= end; offset += 8)
            {
                if (image.ReadPointer(offset) == slotVa) count++;
            }
        }
        return count;
    }
}
