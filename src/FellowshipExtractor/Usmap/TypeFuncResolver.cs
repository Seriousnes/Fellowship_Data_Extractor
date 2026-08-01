namespace FellowshipExtractor.Usmap;

/// <summary>
/// Maps a Z_Construct_* function to the type it builds.
///
/// Struct and enum registration tables do not share the class table's shape, so the name is
/// recovered from the function body instead: every generated constructor holds a RIP-relative
/// reference to its own params record, and those records carry the name. Only the functions
/// actually referenced by a property are decoded, which keeps this to a few thousand short reads
/// rather than a sweep of 128 MB of .text.
/// </summary>
public static class TypeFuncResolver
{
    /// <summary>Generated constructors are short; the params reference is near the top.</summary>
    private const int ScanBytes = 320;

    public static Dictionary<ulong, string> Resolve(
        PeImage image,
        IEnumerable<ulong> typeFuncs,
        IReadOnlyDictionary<ulong, string> paramsByVa)
    {
        // The scan must stop at the end of the function. A constructor whose own params record
        // was not recovered - a struct declaring no properties of its own, for one - otherwise
        // runs on into the next function and adopts its type, which reads as a confident wrong
        // answer rather than a missing one.
        var bounds = image.FunctionRanges().OrderBy(r => r.Start).ToArray();
        var starts = bounds.Select(r => r.Start).ToArray();

        var resolved = new Dictionary<ulong, string>();

        foreach (var funcVa in typeFuncs.Distinct())
        {
            if (funcVa == 0) continue;
            var start = image.Offset(funcVa);
            if (start < 0) continue;

            var length = Math.Min(ScanBytes, (int)Math.Min(int.MaxValue, FunctionLength(bounds, starts, funcVa)));
            if (length <= 0) continue;

            foreach (var target in RipTargets(image, start, length))
            {
                if (!paramsByVa.TryGetValue(target, out var name)) continue;
                resolved[funcVa] = name;
                break;
            }
        }

        return resolved;
    }

    /// <summary>Bytes from this address to the end of the .pdata range containing it.</summary>
    private static ulong FunctionLength(
        IReadOnlyList<(ulong Start, ulong End)> bounds, ulong[] starts, ulong va)
    {
        var index = Array.BinarySearch(starts, va);
        if (index < 0) index = ~index - 1;
        if (index < 0) return 0;

        var (start, end) = bounds[index];
        return va >= start && va < end ? end - va : 0;
    }

    /// <summary>
    /// Targets of RIP-relative lea/mov instructions. Matches REX.W + 8D/8B + ModRM with mod=00
    /// and rm=101, which is the encoding MSVC emits for taking the address of static data.
    /// </summary>
    private static IEnumerable<ulong> RipTargets(PeImage image, int start, int length)
    {
        var data = image.Data;
        var end = Math.Min(start + length, data.Length - 7);

        for (var i = start; i < end; i++)
        {
            if ((data[i] & 0xF8) != 0x48) continue;              // REX.W
            if (data[i + 1] is not (0x8D or 0x8B)) continue;     // lea / mov r64, m
            if ((data[i + 2] & 0xC7) != 0x05) continue;          // mod=00, rm=101 -> RIP-relative

            var displacement = BitConverter.ToInt32(data, i + 3);
            var next = image.VirtualAddress(i + 7);
            if (next == 0) continue;

            yield return (ulong)((long)next + displacement);
        }
    }
}
