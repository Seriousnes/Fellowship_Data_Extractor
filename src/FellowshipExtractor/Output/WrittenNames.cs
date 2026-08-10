using System.Security.Cryptography;

namespace FellowshipExtractor.Output;

public sealed class WrittenNames
{
    private readonly Dictionary<string, byte[]> _hashes = new(StringComparer.Ordinal);

    public bool Repeats(string name, byte[] content, out byte[] hash)
    {
        hash = MD5.HashData(content);

        if (!_hashes.TryGetValue(name, out var written))
        {
            _hashes[name] = hash;
            return false;
        }

        if (written.AsSpan().SequenceEqual(hash)) return true;

        throw new InvalidOperationException(
            $"{name} was written twice with different bytes. One name names one image, so two sources " +
            "cannot both claim it.");
    }
}
