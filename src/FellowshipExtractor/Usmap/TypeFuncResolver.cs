namespace FellowshipExtractor.Usmap;

public static class TypeFuncResolver
{
    private const int ScanBytes = 320;

    public static Dictionary<ulong, string> Resolve(
        PeImage image,
        IEnumerable<ulong> typeFuncs,
        IReadOnlyDictionary<ulong, string> paramsByVa)
    {
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

    private static ulong FunctionLength(
        IReadOnlyList<(ulong Start, ulong End)> bounds, ulong[] starts, ulong va)
    {
        var index = Array.BinarySearch(starts, va);
        if (index < 0) index = ~index - 1;
        if (index < 0) return 0;

        var (start, end) = bounds[index];
        return va >= start && va < end ? end - va : 0;
    }

    private static IEnumerable<ulong> RipTargets(PeImage image, int start, int length)
    {
        var data = image.Data;
        var end = Math.Min(start + length, data.Length - 7);

        for (var i = start; i < end; i++)
        {
            if ((data[i] & 0xF8) != 0x48) continue;
            if (data[i + 1] is not (0x8D or 0x8B)) continue;
            if ((data[i + 2] & 0xC7) != 0x05) continue;

            var displacement = BitConverter.ToInt32(data, i + 3);
            var next = image.VirtualAddress(i + 7);
            if (next == 0) continue;

            yield return (ulong)((long)next + displacement);
        }
    }
}
