using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using CUE4Parse.FileProvider;
using CUE4Parse.FileProvider.Objects;
using CUE4Parse.UE4.Assets;

namespace FellowshipExtractor.Extraction;

/// <summary>
/// Packages loaded once per game file, for readers that open the same packages. A package's
/// exports deserialize on first access, so a cached package costs only its header until a reader
/// asks for an export.
/// </summary>
public sealed class PackageLoads(AbstractFileProvider provider)
{
    private readonly ConcurrentDictionary<GameFile, IPackage?> _loaded = new(ReferenceEqualityComparer.Instance);

    public bool TryLoad(GameFile file, [MaybeNullWhen(false)] out IPackage package)
    {
        package = _loaded.GetOrAdd(file, Load);
        return package is not null;
    }

    public bool TryLoad(string path, [MaybeNullWhen(false)] out IPackage package)
    {
        if (provider.TryGetGameFile(path, out var file)) return TryLoad(file, out package);

        package = null;
        return false;
    }

    private IPackage? Load(GameFile file) => provider.TryLoadPackage(file, out var package) ? package : null;
}
