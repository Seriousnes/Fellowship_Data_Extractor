using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace FellowshipExtractor.Api.Tests;

public sealed class Export
{
    public CodexView View { get; }

    public TooltipView Tooltip { get; }

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

        var substitutions = new Placeholders(Store, richText);

        View = new CodexView(Store, new Codex(Store, tokens), tokens, chrome: null!, richText,
            substitutions, icons: null!);

        var icons = new BlobContainerClient(new Uri("http://localhost/icons"));

        Tooltip = new TooltipView(
            Store, tokens, richText, substitutions, new ChromeAssets(icons, paths, Store, tokens), icons);
    }

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
