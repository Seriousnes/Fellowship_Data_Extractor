using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Versions;

using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class MissingMappingsTests
{
    private static DefaultFileProvider Empty(string folder) => new(
        folder, SearchOption.TopDirectoryOnly, new VersionContainer(EGame.GAME_UE5_5), StringComparer.OrdinalIgnoreCase);

    private static void InTempFolder(Action<string> test)
    {
        var folder = Directory.CreateTempSubdirectory("fellowship-mappings").FullName;

        try
        {
            test(folder);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void AResolverBuiltWithoutMappingsNamesTheUsmapRatherThanTheTableItCouldNotRead() => InTempFolder(folder =>
    {
        var failure = Assert.Throws<InvalidOperationException>(() => new TooltipResolver(Empty(folder)));

        Assert.Contains("FELLOWSHIP_USMAP", failure.Message, StringComparison.Ordinal);
        Assert.Contains(ExtractorOptions.MappingsName, failure.Message, StringComparison.Ordinal);
        Assert.Contains("usmap", failure.Message, StringComparison.Ordinal);
    });

    [Fact]
    public void AnEnvironmentNamingNoUsmapReadsTheOneTheUsmapCommandWrites() => InTempFolder(folder =>
    {
        var generated = Path.Combine(folder, ExtractorOptions.MappingsName);
        File.WriteAllBytes(generated, []);

        var options = new ExtractorOptions { MappingsDir = folder };

        Assert.Equal(generated, options.ResolvedMappingsFile);
    });

    [Fact]
    public void AMappingsFolderWithNoUsmapInItResolvesToNone() => InTempFolder(folder =>
    {
        var options = new ExtractorOptions { MappingsDir = folder };

        Assert.Null(options.ResolvedMappingsFile);
    });

    [Fact]
    public void AUsmapNamedByTheEnvironmentWinsOverTheGeneratedOne() => InTempFolder(folder =>
    {
        File.WriteAllBytes(Path.Combine(folder, ExtractorOptions.MappingsName), []);

        var named = Path.Combine(folder, "another.usmap");
        File.WriteAllBytes(named, []);

        var options = new ExtractorOptions { MappingsDir = folder, MappingsFile = named };

        Assert.Equal(named, options.ResolvedMappingsFile);
    });

    [Fact]
    public async Task AUsmapNamedButNeverWrittenFailsByNameBeforeAnythingIsMounted() => await InTempFolderAsync(async folder =>
    {
        var named = Path.Combine(folder, "absent.usmap");

        var failure = await Assert.ThrowsAsync<FileNotFoundException>(() =>
            ProviderFactory.CreateAsync(new ExtractorOptions { MappingsDir = folder, MappingsFile = named }));

        Assert.Contains(named, failure.Message, StringComparison.Ordinal);
        Assert.Contains("FELLOWSHIP_USMAP", failure.Message, StringComparison.Ordinal);
    });

    private static async Task InTempFolderAsync(Func<string, Task> test)
    {
        var folder = Directory.CreateTempSubdirectory("fellowship-mappings").FullName;

        try
        {
            await test(folder);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
