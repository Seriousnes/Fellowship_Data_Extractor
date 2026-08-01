namespace FellowshipExtractor.Usmap;

/// <summary>UECodeGen_Private::EPropertyGenFlags, the type tag UHT writes into each record.</summary>
public enum EPropertyGenFlags
{
    Byte = 0, Int8 = 1, Int16 = 2, Int = 3, Int64 = 4,
    UInt16 = 5, UInt32 = 6, UInt64 = 7, UnsizedInt = 8, UnsizedUInt = 9,
    Float = 10, Double = 11, Bool = 12,
    SoftClass = 13, WeakObject = 14, LazyObject = 15, SoftObject = 16,
    Class = 17, Object = 18, Interface = 19,
    Name = 20, Str = 21,
    Array = 22, Map = 23, Set = 24, Struct = 25,
    Delegate = 26, InlineMulticastDelegate = 27, SparseMulticastDelegate = 28,
    Text = 29, Enum = 30, FieldPath = 31, LargeWorldCoordinatesReal = 32,
    Optional = 33, VerseValue = 34,
}

/// <summary>
/// EPropertyGenFlags packs the type in the low bits and modifiers above it, so a bitfield bool
/// reads as 0x4C (Bool | NativeBool) rather than 12. Masking is required or every modified
/// property is dropped, which silently truncates the class that owns it.
/// </summary>
public static class PropertyGenFlags
{
    public const uint TypeMask = 0x3F;
    public const uint NativeBool = 0x40;
    public const uint ObjectPtr = 0x80;

    /// <summary>Everything the type and known modifiers can account for.</summary>
    public const uint KnownMask = TypeMask | NativeBool | ObjectPtr | 0x100 | 0x200;

    public static EPropertyGenFlags Type(uint raw) => (EPropertyGenFlags)(raw & TypeMask);

    public static bool IsValid(uint raw) =>
        (raw & ~KnownMask) == 0 && Enum.IsDefined(Type(raw));

    /// <summary>Types that carry a type-resolution function pointer after the base record.</summary>
    public static bool HasTypeFunc(EPropertyGenFlags type) => type switch
    {
        EPropertyGenFlags.Byte or EPropertyGenFlags.Enum
            or EPropertyGenFlags.Struct or EPropertyGenFlags.Object or EPropertyGenFlags.Class
            or EPropertyGenFlags.SoftClass or EPropertyGenFlags.SoftObject
            or EPropertyGenFlags.WeakObject or EPropertyGenFlags.LazyObject
            or EPropertyGenFlags.Interface or EPropertyGenFlags.Delegate
            or EPropertyGenFlags.InlineMulticastDelegate or EPropertyGenFlags.SparseMulticastDelegate => true,
        _ => false,
    };
}

public sealed record PropertyRecord(
    int Offset,
    ulong Va,
    string Name,
    EPropertyGenFlags Type,
    ulong PropertyFlags,
    ushort ArrayDim,
    ulong TypeFuncVa);

/// <summary>An array of property pointers, i.e. one struct or class's property list.</summary>
public sealed record PropertyArray(ulong Va, IReadOnlyList<PropertyRecord> Properties);

/// <summary>
/// Recovers UnrealHeaderTool's generated reflection tables from a shipping binary.
///
/// Every native property is emitted as a FPropertyParamsBase record in .rdata. The layout was
/// established empirically against this build: name pointer at +0x00, a mostly-null RepNotify
/// pointer at +0x08, the CPF_ flag mask at +0x10, the EPropertyGenFlags type tag at +0x18, and
/// EObjectFlags at +0x1C which is always RF_Public|RF_Transient|RF_MarkAsNative for generated
/// properties. Variants append a type-resolution function pointer at +0x38.
/// </summary>
public sealed class ReflectionScanner(PeImage image)
{
    private const int NameOffset = 0x00;
    private const int RepNotifyOffset = 0x08;
    private const int PropertyFlagsOffset = 0x10;
    private const int GenFlagsOffset = 0x18;
    private const int ObjectFlagsOffset = 0x1C;
    private const int ArrayDimOffset = 0x30;
    private const int TypeFuncOffset = 0x38;

    private const uint GeneratedPropertyObjectFlags = 0x45; // RF_Public | RF_Transient | RF_MarkAsNative

    public PeImage Image { get; } = image;

    public List<PropertyRecord> ScanProperties()
    {
        var rdata = Image.Section(".rdata");
        var records = new List<PropertyRecord>(1 << 16);
        var end = rdata.RawOffset + Math.Min(rdata.RawSize, rdata.VirtualSize);

        for (var offset = (int)rdata.RawOffset; offset + 0x38 <= end; offset += 8)
        {
            if (Image.ReadUInt32(offset + ObjectFlagsOffset) != GeneratedPropertyObjectFlags) continue;

            var gen = Image.ReadUInt32(offset + GenFlagsOffset);
            if (!PropertyGenFlags.IsValid(gen)) continue;

            var repNotify = Image.ReadPointer(offset + RepNotifyOffset);
            if (repNotify != 0 && Image.ReadUtf8(repNotify) is null) continue;

            var name = Image.ReadUtf8(Image.ReadPointer(offset + NameOffset));
            if (string.IsNullOrEmpty(name)) continue;

            // Only variants that declare one have a type function; for the rest +0x38 is the
            // next record's first field.
            var type = PropertyGenFlags.Type(gen);
            var typeFunc = PropertyGenFlags.HasTypeFunc(type) ? Image.ReadPointer(offset + TypeFuncOffset) : 0;

            records.Add(new PropertyRecord(
                offset,
                Image.VirtualAddress(offset),
                name,
                type,
                Image.ReadUInt64(offset + PropertyFlagsOffset),
                Image.ReadUInt16(offset + ArrayDimOffset),
                Image.InSection(typeFunc, ".text") ? typeFunc : 0));
        }

        return records;
    }

    /// <summary>
    /// Contiguous runs of pointers into the property-record set. Each run is one owner's
    /// PropPointers[] array. Container inners appear here too, so the run is not the schema.
    /// </summary>
    public List<PropertyArray> ScanPropertyArrays(IReadOnlyList<PropertyRecord> records)
    {
        var byVa = records.ToDictionary(r => r.Va);
        var rdata = Image.Section(".rdata");
        var end = rdata.RawOffset + Math.Min(rdata.RawSize, rdata.VirtualSize);
        var arrays = new List<PropertyArray>();

        var offset = (int)rdata.RawOffset;
        while (offset + 8 <= end)
        {
            if (!byVa.TryGetValue(Image.ReadPointer(offset), out var first)) { offset += 8; continue; }

            var run = new List<PropertyRecord> { first };
            var runStart = offset;
            offset += 8;

            while (offset + 8 <= end && byVa.TryGetValue(Image.ReadPointer(offset), out var next))
            {
                run.Add(next);
                offset += 8;
            }

            arrays.Add(new PropertyArray(Image.VirtualAddress(runStart), run));
        }

        return arrays;
    }
}
