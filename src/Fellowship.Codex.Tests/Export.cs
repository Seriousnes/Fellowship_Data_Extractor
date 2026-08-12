using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fellowship.Codex.Tests;

public sealed class ApiWebRoot : IWebHostEnvironment
{
    public ApiWebRoot()
    {
        WebRootPath = Path.Combine(Project.Folder, "wwwroot");
        WebRootFileProvider = new PhysicalFileProvider(WebRootPath);
        ContentRootPath = Project.Folder;
        ContentRootFileProvider = new PhysicalFileProvider(ContentRootPath);
    }

    public string ApplicationName { get; set; } = typeof(TooltipView).Assembly.GetName().Name!;

    public string EnvironmentName { get; set; } = "Test";

    public string WebRootPath { get; set; }

    public IFileProvider WebRootFileProvider { get; set; }

    public string ContentRootPath { get; set; }

    public IFileProvider ContentRootFileProvider { get; set; }
}

public sealed class Export
{
    public ICodexViewService View { get; }

    public ITooltipViewService Tooltip { get; }

    public IEntityStoreService Store { get; }

    public IChromeAssetService Chrome { get; }

    public string Folder { get; }

    public Export()
    {
        Folder = Environment.GetEnvironmentVariable("FELLOWSHIP_EXPORT") ?? Found();

        var snapshot = new FolderExportSource(Folder)
            .LoadAsync(CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        Store = new EntityStoreService(snapshot, NullLogger<EntityStoreService>.Instance);

        var tokens = new DesignTokenService(Store.Settings);
        var richText = new RichTextService(Store.Settings.TextStyles);

        var substitutions = new PlaceholderService(Store, richText);

        var icons = new BlobContainerClient(new Uri("http://localhost/icons"));
        var chrome = Chrome = new ChromeAssetService(icons, Store, tokens);

        View = new CodexViewService(Store, new CodexService(Store, tokens), tokens, chrome, richText, substitutions);

        Tooltip = new TooltipViewService(Store, tokens, richText, substitutions, chrome, new ApiWebRoot());
    }

    public static string Found()
    {
        for (var here = new DirectoryInfo(AppContext.BaseDirectory); here is not null; here = here.Parent)
        {
            var export = Path.Combine(here.FullName, "output", "export");

            if (File.Exists(Path.Combine(export, ExportReader.Data, ExportReader.EntitiesName))) return export;
        }

        throw new InvalidOperationException(
            "No output/export above the test binary and FELLOWSHIP_EXPORT is not set. It names the " +
            "folder the extractor's export-all command wrote, which is the same layout the container holds.");
    }
}
