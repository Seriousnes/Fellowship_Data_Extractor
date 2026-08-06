using System.Buffers.Binary;

namespace FellowshipExtractor.Usmap;

public static class UsmapDebugCommand
{
    public static Task RunAsync(ExtractorOptions options, string propertyName)
    {
        var image = new PeImage(UsmapScanCommand.ShippingExe(options));
        var records = new ReflectionScanner(image).ScanProperties();

        var targets = records.Where(r => r.Name == propertyName).Take(3).ToArray();
        Console.WriteLine($"records named '{propertyName}': {records.Count(r => r.Name == propertyName)}");

        foreach (var record in targets)
        {
            Console.WriteLine($"\n=== record {record.Name}:{record.Type} va=0x{record.Va:X} off=0x{record.Offset:X}");

            foreach (var slot in FindPointers(image, record.Va).Take(3))
            {
                var slotVa = image.VirtualAddress(slot);
                Console.WriteLine($"  slot @0x{slot:X} va=0x{slotVa:X} [{SectionOf(image, slot)}]");
                DumpQwords(image, slot - 16, 6, "    slot ctx");

                foreach (var owner in FindPointers(image, slotVa).Take(2))
                {
                    Console.WriteLine($"    array-ref @0x{owner:X} [{SectionOf(image, owner)}]  (window around it)");
                    DumpFields(image, owner - 0x40, 20, owner);
                }
            }
        }

        return Task.CompletedTask;
    }

    private static List<int> FindPointers(PeImage image, ulong value)
    {
        var hits = new List<int>();
        var data = image.Data;
        for (var offset = 0; offset + 8 <= data.Length; offset += 8)
        {
            if (BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(offset)) == value) hits.Add(offset);
        }
        return hits;
    }

    private static string SectionOf(PeImage image, int offset)
    {
        foreach (var s in image.Sections)
        {
            if (offset >= s.RawOffset && offset < s.RawOffset + s.RawSize) return s.Name;
        }
        return "?";
    }

    private static void DumpFields(PeImage image, int start, int count, int anchor)
    {
        for (var i = 0; i < count; i++)
        {
            var offset = start + i * 8;
            var value = image.ReadPointer(offset);
            var lo = (uint)value;
            var hi = (uint)(value >> 32);
            var text = image.ReadUtf8(value, 48) ?? image.ReadUtf16(value, 48);
            var note = text is not null ? $"  \"{text}\"" :
                image.InSection(value, ".text") ? "  .text" :
                image.InSection(value, ".rdata") ? "  .rdata" :
                image.InSection(value, ".data") ? "  .data" : "";
            var marker = offset == anchor ? " <== array" : "";
            Console.WriteLine($"      {offset - anchor,+5}  {value:X16}   u32[{lo,10} {hi,10}]{note}{marker}");
        }
    }

    private static void DumpQwords(PeImage image, int start, int count, string prefix)
    {
        for (var i = 0; i < count; i++)
        {
            var offset = start + i * 8;
            if (offset < 0) continue;
            var value = image.ReadPointer(offset);
            var delta = offset - (start + 0x30 / 8 * 8);
            var text = image.ReadUtf8(value, 48);
            var note = text is not null ? $"  -> \"{text}\"" :
                image.InSection(value, ".text") ? "  -> .text" :
                image.InSection(value, ".rdata") ? "  -> .rdata" : "";
            Console.WriteLine($"{prefix}{delta,+4}  0x{value:X16}  ({(uint)value:X8} {(uint)(value >> 32):X8}){note}");
        }
    }
}
