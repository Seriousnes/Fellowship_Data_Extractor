namespace FellowshipExtractor.Usmap;

public sealed record ClassParams(ulong Va, string Name, ulong NoRegisterFuncVa, IReadOnlyList<PropertyRecord> Properties)
{
    public ulong SuperFuncVa { get; init; }
}

public sealed class ClassScanner(PeImage image, IReadOnlyList<PropertyRecord> records)
{
    private const int NoRegisterFuncOffset = -0x28;
    private const int ConfigNameOffset = -0x20;
    private const int DependencySingletonsOffset = -0x10;

    private readonly Dictionary<ulong, PropertyRecord> _recordByVa = records.ToDictionary(r => r.Va);

    public Dictionary<ulong, string> ScanRegistrations()
    {
        var map = new Dictionary<ulong, string>();

        foreach (var section in image.Sections)
        {
            if (section.RawSize == 0 || section.Name is ".text" or ".pdata" or ".reloc" or ".rsrc") continue;
            var end = section.RawOffset + Math.Min(section.RawSize, section.VirtualSize);

            for (var offset = (int)section.RawOffset; offset + 24 <= end; offset += 8)
            {
                var outer = image.ReadPointer(offset);
                if (!image.InSection(outer, ".text")) continue;

                var second = image.ReadPointer(offset + 8);
                var name = image.ReadUtf16(image.ReadPointer(offset + 16), 128)
                           ?? image.ReadUtf16(second, 128);
                if (name is null || !IsCppTypeName(name)) continue;

                map.TryAdd(outer, name);
                if (image.InSection(second, ".text")) map.TryAdd(second, name);
            }
        }

        return map;
    }

    public List<ClassParams> ScanClasses(IReadOnlyDictionary<ulong, string> registrations, ISet<ulong> claimedArrays)
    {
        var (slots, referencedStarts) = IndexArrays();
        var classes = new List<ClassParams>();

        foreach (var section in image.Sections)
        {
            if (section.RawSize == 0 || section.Name is ".text" or ".pdata" or ".reloc" or ".rsrc") continue;
            var end = section.RawOffset + Math.Min(section.RawSize, section.VirtualSize);

            for (var offset = (int)section.RawOffset; offset + 8 <= end; offset += 8)
            {
                var arrayVa = image.ReadPointer(offset);
                if (!referencedStarts.Contains(arrayVa) || claimedArrays.Contains(arrayVa)) continue;

                var noRegister = image.ReadPointer(offset + NoRegisterFuncOffset);
                if (!registrations.TryGetValue(noRegister, out var cppName)) continue;

                var configVa = image.ReadPointer(offset + ConfigNameOffset);
                if (configVa != 0 && image.ReadUtf8(configVa, 64) is null) continue;

                if (!HasFunctionList(offset + DependencySingletonsOffset)) continue;

                var properties = ReadRun(arrayVa, slots, referencedStarts);
                if (properties.Count == 0) continue;

                classes.Add(new ClassParams(
                    image.VirtualAddress(offset), StripPrefix(cppName), noRegister, properties)
                {
                    SuperFuncVa = ReadSuperFunc(offset, registrations),
                });
            }
        }

        return classes;
    }

    private bool HasFunctionList(int pointerFieldOffset)
    {
        var listVa = image.ReadPointer(pointerFieldOffset);
        var listOffset = image.Offset(listVa);
        return listOffset >= 0 && image.InSection(image.ReadPointer(listOffset), ".text");
    }

    private ulong ReadSuperFunc(int classParamsArrayOffset, IReadOnlyDictionary<ulong, string> registrations)
    {
        var listVa = image.ReadPointer(classParamsArrayOffset + DependencySingletonsOffset);
        var listOffset = image.Offset(listVa);
        if (listOffset < 0) return 0;

        for (var i = 0; i < 4; i++)
        {
            var func = image.ReadPointer(listOffset + i * 8);
            if (!image.InSection(func, ".text")) break;
            if (registrations.ContainsKey(func)) return func;
        }

        return 0;
    }

    private (Dictionary<ulong, int> Slots, HashSet<ulong> Starts) IndexArrays()
    {
        var rdata = image.Section(".rdata");
        var end = rdata.RawOffset + Math.Min(rdata.RawSize, rdata.VirtualSize);
        var slots = new Dictionary<ulong, int>();

        for (var offset = (int)rdata.RawOffset; offset + 8 <= end; offset += 8)
        {
            if (_recordByVa.ContainsKey(image.ReadPointer(offset))) slots[image.VirtualAddress(offset)] = offset;
        }

        var starts = new HashSet<ulong>();
        foreach (var section in image.Sections)
        {
            if (section.RawSize == 0 || section.Name is ".text" or ".pdata" or ".reloc" or ".rsrc") continue;
            var sectionEnd = section.RawOffset + Math.Min(section.RawSize, section.VirtualSize);
            for (var offset = (int)section.RawOffset; offset + 8 <= sectionEnd; offset += 8)
            {
                var value = image.ReadPointer(offset);
                if (slots.ContainsKey(value) && !slots.ContainsKey(image.VirtualAddress(offset))) starts.Add(value);
            }
        }

        return (slots, starts);
    }

    private List<PropertyRecord> ReadRun(ulong startVa, Dictionary<ulong, int> slots, HashSet<ulong> starts)
    {
        var run = new List<PropertyRecord>();
        if (!slots.TryGetValue(startVa, out var offset)) return run;

        while (true)
        {
            var slotVa = image.VirtualAddress(offset);
            if (run.Count > 0 && starts.Contains(slotVa)) break;
            if (!slots.ContainsKey(slotVa)) break;
            if (!_recordByVa.TryGetValue(image.ReadPointer(offset), out var record)) break;

            run.Add(record);
            offset += 8;
        }

        return run;
    }

    private static bool IsCppTypeName(string s) =>
        s.Length is > 1 and <= 120
        && char.IsUpper(s[0])
        && s.All(c => char.IsLetterOrDigit(c) || c == '_');

    public static string StripPrefix(string cppName) =>
        cppName.Length > 1 && cppName[0] is 'U' or 'A' or 'F' or 'E' && char.IsUpper(cppName[1])
            ? cppName[1..]
            : cppName;
}
