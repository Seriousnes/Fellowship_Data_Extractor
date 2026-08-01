using CUE4Parse.UE4.Exceptions;

namespace FellowshipExtractor;

/// <summary>
/// Measures how much of the build is readable with the mappings currently configured.
/// Answers "do we need a .usmap, and for what fraction of the content" with numbers.
/// </summary>
public static class ProbeCommand
{
    public static async Task RunAsync(ExtractorOptions options, int sampleSize)
    {
        var provider = await ProviderFactory.CreateAsync(options);

        var packages = provider.Files.Values
            .Where(f => f.Path.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var step = Math.Max(1, packages.Length / sampleSize);
        int ok = 0, needsMappings = 0, otherFailure = 0;
        var failures = new Dictionary<string, int>();

        for (var i = 0; i < packages.Length; i += step)
        {
            try
            {
                var package = provider.LoadPackage(packages[i]);
                _ = package.GetExports().ToArray();
                ok++;
            }
            catch (MappingException)
            {
                needsMappings++;
            }
            catch (Exception ex)
            {
                otherFailure++;
                var key = $"{ex.GetType().Name}: {ex.Message}";
                failures[key] = failures.GetValueOrDefault(key) + 1;
            }
        }

        var sampled = ok + needsMappings + otherFailure;
        Console.WriteLine($"Sampled {sampled} of {packages.Length} packages (every {step}th)");
        Console.WriteLine($"  parsed:          {ok}");
        Console.WriteLine($"  needs mappings:  {needsMappings}");
        Console.WriteLine($"  other failures:  {otherFailure}");

        foreach (var (message, count) in failures.OrderByDescending(kv => kv.Value).Take(10))
        {
            Console.WriteLine($"    {count,5}  {message}");
        }
    }
}
