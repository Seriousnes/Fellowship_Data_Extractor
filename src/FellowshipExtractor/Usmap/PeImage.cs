using System.Buffers.Binary;
using System.Text;

namespace FellowshipExtractor.Usmap;

public readonly record struct PeSection(string Name, uint Rva, uint VirtualSize, uint RawOffset, uint RawSize);

/// <summary>
/// Minimal PE32+ reader: section table, virtual-address translation, and the .pdata function
/// ranges. Enough to walk the generated reflection tables in .rdata.
/// </summary>
public sealed class PeImage
{
    public byte[] Data { get; }
    public ulong ImageBase { get; }
    public IReadOnlyList<PeSection> Sections { get; }

    public PeImage(string path)
    {
        Data = File.ReadAllBytes(path);

        var pe = BinaryPrimitives.ReadInt32LittleEndian(Data.AsSpan(0x3C));
        if (BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan(pe)) != 0x00004550)
            throw new InvalidDataException("Not a PE image.");

        var optOffset = pe + 24;
        if (BinaryPrimitives.ReadUInt16LittleEndian(Data.AsSpan(optOffset)) != 0x20B)
            throw new InvalidDataException("Not a PE32+ image.");

        ImageBase = BinaryPrimitives.ReadUInt64LittleEndian(Data.AsSpan(optOffset + 24));
        var sectionCount = BinaryPrimitives.ReadUInt16LittleEndian(Data.AsSpan(pe + 6));
        var optSize = BinaryPrimitives.ReadUInt16LittleEndian(Data.AsSpan(pe + 20));

        var sections = new List<PeSection>(sectionCount);
        for (var i = 0; i < sectionCount; i++)
        {
            var s = optOffset + optSize + i * 40;
            sections.Add(new PeSection(
                Encoding.ASCII.GetString(Data, s, 8).TrimEnd('\0'),
                BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan(s + 12)),
                BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan(s + 8)),
                BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan(s + 20)),
                BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan(s + 16))));
        }
        Sections = sections;
    }

    public PeSection Section(string name)
    {
        foreach (var s in Sections)
        {
            if (s.Name == name) return s;
        }
        throw new InvalidDataException($"No {name} section.");
    }

    /// <summary>File offset for a virtual address, or -1 if it is not backed by raw data.</summary>
    public int Offset(ulong va)
    {
        if (va <= ImageBase) return -1;
        var rva = va - ImageBase;
        if (rva > uint.MaxValue) return -1;

        foreach (var s in Sections)
        {
            var mapped = Math.Min(s.VirtualSize, s.RawSize);
            if (rva >= s.Rva && rva < s.Rva + mapped) return (int)(s.RawOffset + ((uint)rva - s.Rva));
        }
        return -1;
    }

    public ulong VirtualAddress(int fileOffset)
    {
        foreach (var s in Sections)
        {
            if (fileOffset >= s.RawOffset && fileOffset < s.RawOffset + s.RawSize)
                return ImageBase + s.Rva + (uint)(fileOffset - (int)s.RawOffset);
        }
        return 0;
    }

    public bool InSection(ulong va, string name)
    {
        var s = Section(name);
        if (va <= ImageBase) return false;
        var rva = va - ImageBase;
        return rva >= s.Rva && rva < s.Rva + s.VirtualSize;
    }

    public ulong ReadPointer(int offset) =>
        offset >= 0 && offset + 8 <= Data.Length ? BinaryPrimitives.ReadUInt64LittleEndian(Data.AsSpan(offset)) : 0;

    public uint ReadUInt32(int offset) =>
        offset >= 0 && offset + 4 <= Data.Length ? BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan(offset)) : 0;

    public ushort ReadUInt16(int offset) =>
        offset >= 0 && offset + 2 <= Data.Length ? BinaryPrimitives.ReadUInt16LittleEndian(Data.AsSpan(offset)) : (ushort)0;

    public ulong ReadUInt64(int offset) => ReadPointer(offset);

    /// <summary>Null-terminated ASCII at a virtual address, or null if it is not clean text.</summary>
    public string? ReadUtf8(ulong va, int maxLength = 256)
    {
        var start = Offset(va);
        if (start < 0) return null;

        var end = start;
        while (end < Data.Length && end - start < maxLength && Data[end] != 0)
        {
            if (Data[end] is < 0x20 or >= 0x7F) return null;
            end++;
        }

        return end < Data.Length && Data[end] == 0 && end > start
            ? Encoding.ASCII.GetString(Data, start, end - start)
            : null;
    }

    /// <summary>Null-terminated UTF-16 at a virtual address. UE stores registration names as TCHAR.</summary>
    public string? ReadUtf16(ulong va, int maxLength = 256)
    {
        var start = Offset(va);
        if (start < 0) return null;

        var sb = new StringBuilder();
        for (var i = start; i + 2 <= Data.Length && sb.Length < maxLength; i += 2)
        {
            var c = BinaryPrimitives.ReadUInt16LittleEndian(Data.AsSpan(i));
            if (c == 0) return sb.Length > 0 ? sb.ToString() : null;
            if (c is < 0x20 or >= 0x7F) return null;
            sb.Append((char)c);
        }
        return null;
    }

    /// <summary>
    /// Function ranges from the exception directory. Generated Z_Construct_* bodies are tiny and
    /// this gives their exact bounds without disassembling.
    /// </summary>
    public IReadOnlyList<(ulong Start, ulong End)> FunctionRanges()
    {
        var pdata = Section(".pdata");
        var result = new List<(ulong, ulong)>((int)(pdata.RawSize / 12));

        for (var off = (int)pdata.RawOffset; off + 12 <= pdata.RawOffset + pdata.RawSize; off += 12)
        {
            var begin = ReadUInt32(off);
            var end = ReadUInt32(off + 4);
            if (begin == 0 || end <= begin) continue;
            result.Add((ImageBase + begin, ImageBase + end));
        }

        return result;
    }
}
