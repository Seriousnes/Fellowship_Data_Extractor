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
        var mappings = options.ResolvedMappingsFile;

        if (mappings is not null && !File.Exists(mappings))
        {
            throw new FileNotFoundException(
                $"FELLOWSHIP_USMAP names {mappings}, and that file does not exist. Write the mappings for " +
                "this build with 'dotnet run --project src/FellowshipExtractor -- usmap'.",
                mappings);
        }

        await EnsureOodleAsync();
        await EnsureDetexAsync();

        var versions = new VersionContainer(options.Game);

        var provider = new DefaultFileProvider(
            options.PaksDir, SearchOption.TopDirectoryOnly, versions, StringComparer.OrdinalIgnoreCase);
        provider.Initialize();
        await provider.MountAsync();

        provider.LoadVirtualPaths();

        if (mappings is not null)
        {
            provider.MappingsContainer = new FileUsmapTypeMappingsProvider(mappings);
            EnumOrdinals.Read(provider.MappingsContainer.MappingsForGame);
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
