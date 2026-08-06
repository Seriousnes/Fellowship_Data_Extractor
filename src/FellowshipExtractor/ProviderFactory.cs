using CUE4Parse.Compression;
using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider.Usmap;
using CUE4Parse.UE4.Versions;
using CUE4Parse_Conversion.Textures.BC;

namespace FellowshipExtractor;

public static class ProviderFactory
{
    public static async Task<DefaultFileProvider> CreateAsync(ExtractorOptions options)
    {
        await EnsureOodleAsync();
        await EnsureDetexAsync();

        var versions = new VersionContainer(options.Game);

        var provider = new DefaultFileProvider(
            options.PaksDir, SearchOption.TopDirectoryOnly, versions, StringComparer.OrdinalIgnoreCase);
        provider.Initialize();
        await provider.MountAsync();

        provider.LoadVirtualPaths();

        if (options.MappingsFile is { Length: > 0 } mappings)
        {
            provider.MappingsContainer = new FileUsmapTypeMappingsProvider(mappings);
        }

        provider.ReadScriptData = true;

        return provider;
    }

    private static async Task EnsureOodleAsync()
    {
        var dll = Path.Combine(AppContext.BaseDirectory, OodleHelper.OodleFileName);
        if (!File.Exists(dll))
        {
            Console.WriteLine($"Downloading {OodleHelper.OodleFileName}...");
            if (!await OodleHelper.DownloadOodleDllAsync(ref dll, CancellationToken.None))
            {
                throw new InvalidOperationException($"Could not obtain {OodleHelper.OodleFileName}.");
            }
        }

        OodleHelper.Initialize(dll);
    }

    private static async Task EnsureDetexAsync()
    {
        var dll = Path.Combine(AppContext.BaseDirectory, DetexHelper.DLL_NAME);
        if (!File.Exists(dll) && !await DetexHelper.LoadDllAsync(dll))
        {
            Console.Error.WriteLine($"Could not unpack {DetexHelper.DLL_NAME}; BC6H and BC7 textures will not decode.");
            return;
        }

        DetexHelper.Initialize(dll);
    }
}
