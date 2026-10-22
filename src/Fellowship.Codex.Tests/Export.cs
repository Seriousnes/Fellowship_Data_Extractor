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

    public Export()
    {
        var folder = Environment.GetEnvironmentVariable(FolderExportSource.Variable) ?? Found();

        var history = new FolderExportSource(folder)
            .LoadAsync(CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        Store = new EntityStoreService(history.Snapshot, NullLogger<EntityStoreService>.Instance);

        var tokens = new DesignTokenService(Store.Settings);
        var richText = new RichTextService(Store.Settings.TextStyles);

        var substitutions = new PlaceholderService(Store, richText);

        var chrome = Chrome = new ChromeAssetService(new StorageTextureSource(new MemoryStorage()), Store, tokens);

        var codex = new CodexService(Store, tokens);

        View = new CodexViewService(Store, codex, tokens, chrome, richText, substitutions);

        Tooltip = new TooltipViewService(Store, tokens, richText, substitutions, chrome, new ApiWebRoot());
    }

    private static string Found() =>
        FolderExportSource.Above(AppContext.BaseDirectory)
        ?? throw new InvalidOperationException(
            $"No output/{ExportFolders.Pattern} carrying both data files above the test binary, and " +
            $"{FolderExportSource.Variable} is not set. It names the folder the extractor wrote, which " +
            "is the same layout the container holds.");
}
