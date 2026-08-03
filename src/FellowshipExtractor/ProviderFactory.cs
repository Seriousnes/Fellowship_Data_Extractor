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

        // The asset registry and the containers disagree on case - "Items/Mounts" against
        // "Items/mounts" - so a case-sensitive lookup loses every asset under those paths.
        var provider = new DefaultFileProvider(
            options.PaksDir, SearchOption.TopDirectoryOnly, versions, StringComparer.OrdinalIgnoreCase);
        provider.Initialize();
        await provider.MountAsync();

        // GameFeature plugins are referenced by their own root, "/ForestMeadow/...", but mounted
        // under the plugin's Content directory. Without this the registry's paths do not resolve.
        provider.LoadVirtualPaths();

        if (options.MappingsFile is { Length: > 0 } mappings)
        {
            provider.MappingsContainer = new FileUsmapTypeMappingsProvider(mappings);
        }

        // Blueprint bodies are skipped by default. Two of the numbers a tooltip needs - the stat
        // pool an item level is worth - are not a data table anywhere in the build; they are the
        // compiled arithmetic of HeroItemStatCalculation, so the bytecode has to survive loading.
        provider.ReadScriptData = true;

        return provider;
    }

    /// <summary>
    /// Fellowship's paks are Oodle-compressed and the game links Oodle statically, so no
    /// oo2core DLL ships on disk. Pull one next to the extractor once and reuse it.
    /// </summary>
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

    /// <summary>
    /// BC6H and BC7 textures decode through a native library that ships embedded in
    /// CUE4Parse-Conversion. Every other format decodes in managed code, so a failure here only
    /// costs those two and is reported rather than thrown.
    /// </summary>
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
