namespace Fellowship.Codex.Services;

public static class CodexServices
{
    public static IServiceCollection AddCodexServices(this IServiceCollection services)
    {
        services.AddSingleton<ExportHistoryHolder>();

        services.AddSingleton<IEntityStoreService>(s => new EntityStoreService(
            s.GetRequiredService<ExportHistoryHolder>().History.Snapshot,
            s.GetRequiredService<ILogger<EntityStoreService>>()));
        services.AddSingleton<IExportHistoryService>(s =>
            new ExportHistoryService(s.GetRequiredService<ExportHistoryHolder>().History));
        services.AddSingleton<IDesignTokenService>(s =>
            new DesignTokenService(s.GetRequiredService<IEntityStoreService>().Settings));
        services.AddSingleton<IRichTextService>(s =>
            new RichTextService(s.GetRequiredService<IEntityStoreService>().Settings.TextStyles));

        services.AddSingleton<IChromeAssetService, ChromeAssetService>();
        services.AddSingleton<IPlaceholderService, PlaceholderService>();

        services.AddSingleton<ICodexService, CodexService>();
        services.AddSingleton<IMenuService, MenuService>();
        services.AddSingleton<ISearchIndexService, SearchIndexService>();
        services.AddSingleton<ICodexSearchService, CodexSearchService>();
        services.AddSingleton<ICodexViewService, CodexViewService>();
        services.AddSingleton<ISuggestionService, SuggestionService>();

        return services;
    }

    public static IServiceCollection AddTooltipServices(this IServiceCollection services)
    {
        services.AddSingleton<ITooltipViewService, TooltipViewService>();
        services.AddSingleton<IFragmentService, FragmentService>();
        services.AddSingleton<ITooltipService, TooltipService>();

        return services;
    }

    public static async Task LoadExportAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        services.GetRequiredService<ExportHistoryHolder>().History =
            await services.GetRequiredService<IExportSource>().LoadAsync(cancellationToken);
    }
}
