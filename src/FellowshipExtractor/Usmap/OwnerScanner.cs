namespace FellowshipExtractor.Usmap;

/// <summary>A generated FStructParams record: one native struct and its property list.</summary>
public sealed record StructParams(
    ulong Va,
    string Name,
    ulong OuterFuncVa,
    ulong SuperFuncVa,
    IReadOnlyList<PropertyRecord> Properties)
{
    public ushort SizeOf { get; init; }
}

/// <summary>
/// Recovers FStructParams records. These sit in .data rather than .rdata, and the layout was
/// established by tracing FGameplayTag's TagName property back to its owner:
///
///   +0x00 OuterFunc  +0x08 SuperFunc  +0x10 StructOpsFunc  +0x18 NameUTF8
///   +0x20 PropertyArray  +0x30 ObjectFlags(0x45)  +0x34 NumProperties
///
/// The property count is cross-checked against the pointer run it describes, so a record only
/// survives if its declared length really is that many property records.
/// </summary>
public sealed class OwnerScanner(PeImage image, IReadOnlyList<PropertyRecord> records)
{
    private const int OuterFuncOffset = 0x00;
    private const int SuperFuncOffset = 0x08;
    private const int NameOffset = 0x18;
    private const int PropertyArrayOffset = 0x20;
    private const int PropertyCountOffset = 0x28;
    private const int SizeOfOffset = 0x2A;
    private const int ObjectFlagsOffset = 0x30;

    private const uint GeneratedObjectFlags = 0x45;
    private const int MaxProperties = 2048;

    private readonly Dictionary<ulong, PropertyRecord> _recordByVa = records.ToDictionary(r => r.Va);

    public List<StructParams> ScanStructs()
    {
        var found = new List<StructParams>();

        foreach (var section in image.Sections)
        {
            if (section.RawSize == 0 || section.Name is ".text" or ".pdata" or ".reloc" or ".rsrc") continue;

            var end = section.RawOffset + Math.Min(section.RawSize, section.VirtualSize);
            for (var offset = (int)section.RawOffset; offset + 0x38 <= end; offset += 8)
            {
                if (image.ReadUInt32(offset + ObjectFlagsOffset) != GeneratedObjectFlags) continue;

                var count = image.ReadUInt16(offset + PropertyCountOffset);
                if (count > MaxProperties) continue;

                var array = image.ReadPointer(offset + PropertyArrayOffset);

                // A struct that adds no properties of its own still has a params record, and it
                // is the only place its name is written. Recovering it is what keeps the type
                // resolver from mistaking such a struct for its neighbour. With no property run
                // to check against, require the null array UHT emits in that case.
                if (count == 0 && array != 0) continue;

                var name = image.ReadUtf8(image.ReadPointer(offset + NameOffset), 128);
                if (name is null || !IsIdentifier(name)) continue;

                var properties = count == 0 ? [] : ReadRun(array, count);
                if (properties is null) continue;

                found.Add(new StructParams(
                    image.VirtualAddress(offset),
                    name,
                    image.ReadPointer(offset + OuterFuncOffset),
                    image.ReadPointer(offset + SuperFuncOffset),
                    properties)
                { SizeOf = image.ReadUInt16(offset + SizeOfOffset) });
            }
        }

        return found;
    }

    /// <summary>The declared property run, or null if any entry is not a property record.</summary>
    private IReadOnlyList<PropertyRecord>? ReadRun(ulong arrayVa, int count)
    {
        var start = image.Offset(arrayVa);
        if (start < 0) return null;

        var run = new List<PropertyRecord>(count);
        for (var i = 0; i < count; i++)
        {
            if (!_recordByVa.TryGetValue(image.ReadPointer(start + i * 8), out var record)) return null;
            run.Add(record);
        }
        return run;
    }

    private static bool IsIdentifier(string s) =>
        s.Length is > 0 and <= 120
        && (char.IsLetter(s[0]) || s[0] == '_')
        && s.All(c => char.IsLetterOrDigit(c) || c == '_');
}
