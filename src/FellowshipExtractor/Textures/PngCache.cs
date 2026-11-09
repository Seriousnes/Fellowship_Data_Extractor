using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace FellowshipExtractor.Textures;

/// <summary>
/// PNG encodings on disk, keyed by the encoder's input and the encoder's identity. A texture's PNG
/// is the key <see cref="TextureVariants"/> files its variants under, so each run needs the same PNG
/// bytes, and encoding them is most of the cost of an icon.
/// </summary>
public static class PngCache
{
    public static string CacheFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FellowshipExtractor",
        "png");

    /// <summary>
    /// The PNG for one input: read from the cache when an earlier run encoded the same input with the
    /// same encoder, otherwise encoded and kept.
    /// </summary>
    public static byte[] Encoded(string encoder, int width, int height, ReadOnlySpan<byte> input, Func<byte[]> encode)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(encoder));
        Span<byte> size = stackalloc byte[8];
        BitConverter.TryWriteBytes(size, width);
        BitConverter.TryWriteBytes(size[4..], height);
        hash.AppendData(size);
        hash.AppendData(input);

        var key = Convert.ToHexStringLower(hash.GetHashAndReset());
        var file = Path.Combine(CacheFolder, key[..2], $"{key}.png");

        if (File.Exists(file)) return File.ReadAllBytes(file);

        var png = encode();

        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var staging = $"{file}.{Guid.NewGuid():N}";
        File.WriteAllBytes(staging, png);

        try
        {
            File.Move(staging, file);
        }
        catch (IOException) when (File.Exists(file))
        {
            File.Delete(staging);
        }

        return png;
    }

    /// <summary>The assembly's informational version, which names the package build exactly.</summary>
    public static string Version(Type type) =>
        type.Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? type.Assembly.GetName().Version!.ToString();
}
