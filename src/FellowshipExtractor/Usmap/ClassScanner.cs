namespace FellowshipExtractor.Usmap;

public sealed record ClassParams(ulong Va, string Name, ulong NoRegisterFuncVa, IReadOnlyList<PropertyRecord> Properties)
{
    /// <summary>Z_Construct of the base class, taken from the dependency singleton list.</summary>
    public ulong SuperFuncVa { get; init; }
}

/// <summary>
/// Recovers FClassParams records and names them.
///
/// Unlike FStructParams, FClassParams carries no name: UE keeps class names in a separate
/// FClassRegisterCompiledInInfo table as TCHAR (UTF-16) holding the C++ identifier, prefix and
/// all. The link is the NoRegister function pointer, which appears in both records. UE strips
/// the U/A prefix when it registers the UClass, so we strip it too.
///
/// Array lengths come from reference points rather than a count field: every PropPointers run
/// is pointed at by its owner, so a run ends where the next referenced run begins.
/// </summary>
public sealed class ClassScanner(PeImage image, IReadOnlyList<PropertyRecord> records)
{
    private const int NoRegisterFuncOffset = -0x28; // relative to the PropertyArray field
    private const int ConfigNameOffset = -0x20;
    private const int DependencySingletonsOffset = -0x10;

    private readonly Dictionary<ulong, PropertyRecord> _recordByVa = records.ToDictionary(r => r.Va);

    /// <summary>C++ registration name for each Z_Construct/NoRegister function.</summary>
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

                // Classes register as {OuterRegister, InnerRegister, Name}; structs leave the
                // second function null when they have no cpp ops; enums put the name straight
                // after the single register function. Accept all three shapes.
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

                // A config-name slot (or null) sits between the two, which distinguishes
                // FClassParams from an unrelated pointer that happens to land here.
                var configVa = image.ReadPointer(offset + ConfigNameOffset);
                if (configVa != 0 && image.ReadUtf8(configVa, 64) is null) continue;

                // Every real FClassParams points at a dependency singleton list whose entries are
                // functions. Without this a stray pointer that happens to sit 0x28 after a
                // registered function's address is accepted, which is how a phantom UObject with
                // a borrowed property array got in.
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

    /// <summary>True when the pointer resolves to a list starting with a code address.</summary>
    private bool HasFunctionList(int pointerFieldOffset)
    {
        var listVa = image.ReadPointer(pointerFieldOffset);
        var listOffset = image.Offset(listVa);
        return listOffset >= 0 && image.InSection(image.ReadPointer(listOffset), ".text");
    }

    /// <summary>
    /// UHT lists the base class first in the dependency singletons, followed by the module's
    /// package singleton. Take the first entry that names a registered type.
    /// </summary>
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

    /// <summary>Record-pointer slots, and the set of run starts that something points at.</summary>
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
            if (run.Count > 0 && starts.Contains(slotVa)) break;      // next owner's run begins here
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

    /// <summary>UE registers UFoo as "Foo" and AActor as "Actor".</summary>
    public static string StripPrefix(string cppName) =>
        cppName.Length > 1 && cppName[0] is 'U' or 'A' or 'F' or 'E' && char.IsUpper(cppName[1])
            ? cppName[1..]
            : cppName;
}
