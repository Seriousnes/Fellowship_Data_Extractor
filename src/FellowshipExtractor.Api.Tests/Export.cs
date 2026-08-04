using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace FellowshipExtractor.Api.Tests;

/// <summary>
/// A CodexView over the export the app itself reads, built once for the whole run. Chrome and the
/// blob client are left null: they are reached only by CodexView.Icon and CodexView.Chrome, no
/// section draws art, and constructing ChromeAssets measures the PNG corpus in the export's ui
/// folder.
/// </summary>
public sealed class Export
{
    public CodexView View { get; }

    public EntityStore Store { get; }

    public Export()
    {
        var folder = Environment.GetEnvironmentVariable("FELLOWSHIP_EXPORT") ?? Found();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["FELLOWSHIP_EXPORT"] = folder })
            .Build();

        var paths = new ExportPaths(configuration);

        Store = new EntityStore(paths, NullLogger<EntityStore>.Instance);

        var tokens = new DesignTokens(Store.Vocabulary);
        var richText = new RichTextRenderer(Store.Vocabulary.TextStyles);

        View = new CodexView(Store, new Codex(Store, tokens), tokens, chrome: null!, richText,
            new Placeholders(Store, richText), icons: null!);
    }

    /// <summary>
    /// The repo's own export, found by walking up from the test binary. The same folder the AppHost
    /// hands the API, so a section is tested against the corpus it renders in the browser rather
    /// than against a second copy of the shapes that could drift from it.
    /// </summary>
    private static string Found()
    {
        for (var here = new DirectoryInfo(AppContext.BaseDirectory); here is not null; here = here.Parent)
        {
            var export = Path.Combine(here.FullName, "output", "export");

            if (File.Exists(Path.Combine(export, "entities.jsonl"))) return export;
        }

        throw new InvalidOperationException(
            "No output/export above the test binary and FELLOWSHIP_EXPORT is not set. It names the " +
            "folder the extractor's export-all command wrote, which is the same folder the app reads.");
    }
}
