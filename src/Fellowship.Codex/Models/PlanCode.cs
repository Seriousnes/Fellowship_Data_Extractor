using System.Buffers.Text;

namespace Fellowship.Codex.Models;

public static class PlanCode
{
    private const int BitsPerByte = 8;
    private const int NumberBits = 7;
    private const byte NumberMore = 0x80;
    private const byte NumberValue = 0x7F;

    public static string Encode(IReadOnlyList<byte> bytes)
    {
        var length = bytes.Count;
        while (length > 0 && bytes[length - 1] == 0) length--;

        return length == 0 ? string.Empty : Base64Url.EncodeToString([.. bytes.Take(length)]);
    }

    public static byte[] Decode(string? code)
    {
        if (code is not { Length: > 0 } || !Base64Url.IsValid(code)) return [];

        var bytes = new byte[Base64Url.GetMaxDecodedLength(code.Length)];

        return Base64Url.TryDecodeFromChars(code, bytes, out var written) ? bytes[..written] : [];
    }

    public static int MaskLength(int count) => (count + BitsPerByte - 1) / BitsPerByte;

    public static byte[] Mask(IReadOnlyList<bool> set)
    {
        var mask = new byte[MaskLength(set.Count)];

        for (var at = 0; at < set.Count; at++)
        {
            if (set[at]) mask[at / BitsPerByte] |= (byte)(1 << (at % BitsPerByte));
        }

        return mask;
    }

    public static bool Bit(IReadOnlyList<byte> bytes, int at) =>
        at / BitsPerByte < bytes.Count && (bytes[at / BitsPerByte] & (1 << (at % BitsPerByte))) != 0;

    public static IEnumerable<byte> Numbers(IEnumerable<int> numbers)
    {
        foreach (var number in numbers)
        {
            var left = (uint)number;

            while (left > NumberValue)
            {
                yield return (byte)(left & NumberValue | NumberMore);
                left >>= NumberBits;
            }

            yield return (byte)left;
        }
    }

    public static IReadOnlyList<int> Numbers(IReadOnlyList<byte> bytes, int start)
    {
        var numbers = new List<int>();
        var number = 0;
        var shift = 0;

        for (var at = start; at < bytes.Count; at++)
        {
            if (shift > 28) return numbers;

            number |= (bytes[at] & NumberValue) << shift;

            if ((bytes[at] & NumberMore) != 0)
            {
                shift += NumberBits;
                continue;
            }

            numbers.Add(number);
            number = 0;
            shift = 0;
        }

        return numbers;
    }
}
