namespace FellowshipExtractor.Usmap;

public sealed record EnumMember(string Name, long Value);

public sealed record EnumParams(ulong Va, string Name, string CppType, IReadOnlyList<EnumMember> Members);

public sealed class EnumScanner(PeImage image)
{
    private const int NameOffset = 0x10;
    private const int CppTypeOffset = 0x18;
    private const int MemberArrayOffset = 0x20;
    private const int ObjectFlagsOffset = 0x28;
    private const int MemberCountOffset = 0x2C;

    private const int MemberStride = 16;
    private const int MemberValueOffset = 8;

    private const uint GeneratedObjectFlags = 0x45;
    private const int MaxMembers = 4096;
    private const string Scope = "::";
    private const string MaxSuffix = "_MAX";

    public const long HighestOrdinal = byte.MaxValue - 1;

    public List<EnumParams> Scan()
    {
        var found = new Dictionary<ulong, EnumParams>();

        foreach (var section in image.Sections)
        {
            if (section.RawSize == 0 || section.Name is ".text" or ".pdata" or ".reloc" or ".rsrc") continue;

            var end = section.RawOffset + Math.Min(section.RawSize, section.VirtualSize);
            for (var offset = (int)section.RawOffset; offset + 0x30 <= end; offset += 8)
            {
                if (image.ReadUInt32(offset + ObjectFlagsOffset) != GeneratedObjectFlags) continue;

                var count = image.ReadUInt16(offset + MemberCountOffset);
                if (count is 0 or > MaxMembers) continue;

                var name = image.ReadUtf8(image.ReadPointer(offset + NameOffset), 128);
                if (name is null || !IsIdentifier(name)) continue;

                if (image.ReadUtf8(image.ReadPointer(offset + CppTypeOffset), 128) is not { } cppType) continue;

                var members = ReadMembers(image.ReadPointer(offset + MemberArrayOffset), (int)count, name);
                if (members is null) continue;

                var va = image.VirtualAddress(offset);
                found[va] = new EnumParams(va, name, cppType, members);
            }
        }

        return [.. found.Values];
    }

    private List<EnumMember>? ReadMembers(ulong arrayVa, int count, string owner)
    {
        var start = image.Offset(arrayVa);
        if (start < 0) return null;

        var members = new List<EnumMember>(count);

        for (var i = 0; i < count; i++)
        {
            var name = image.ReadUtf8(image.ReadPointer(start + i * MemberStride), 256);
            if (name is null) return null;

            var scoped = name.IndexOf(Scope, StringComparison.Ordinal);
            if (scoped >= 0)
            {
                if (!string.Equals(name[..scoped], owner, StringComparison.Ordinal)) return null;
                name = name[(scoped + Scope.Length)..];
            }

            if (!IsIdentifier(name)) return null;

            members.Add(new EnumMember(name, (long)image.ReadUInt64(start + i * MemberStride + MemberValueOffset)));
        }

        return members;
    }

    public static IReadOnlyList<EnumMember> Declared(EnumParams declaring) =>
        [.. declaring.Members.Where(member => member.Name != declaring.Name + MaxSuffix)];

    public static bool Fits(EnumMember member) => member.Value >= 0 && member.Value <= HighestOrdinal;

    public static UsmapEnum? ByOrdinal(EnumParams declaring)
    {
        var members = Declared(declaring).Where(Fits).ToArray();
        if (members.Length == 0) return null;

        var byOrdinal = new string[members.Max(member => member.Value) + 1];
        for (var ordinal = 0; ordinal < byOrdinal.Length; ordinal++) byOrdinal[ordinal] = ordinal.ToString();
        foreach (var member in members) byOrdinal[member.Value] = member.Name;

        return new UsmapEnum(declaring.Name, byOrdinal);
    }

    private static bool IsIdentifier(string value) =>
        value.Length is > 0 and <= 120
        && (char.IsLetter(value[0]) || value[0] == '_')
        && value.All(c => char.IsLetterOrDigit(c) || c == '_');
}
