using Fellowship.SDK.Documents;

using FellowshipExtractor.Api;
using FellowshipExtractor.Api.Components;
using FellowshipExtractor.Api.Endpoints;
using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi(ApiDocument.Name, options =>
{
    options.AddDocumentTransformer<ApiInfo>();
    options.AddOperationTransformer<StatsNote>();
    options.AddOperationTransformer<ListParameters>();
    options.AddOperationTransformer<BrowsingParameters>();
});
builder.Services.AddProblemDetails();
builder.Services.AddValidation();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.DefaultIgnoreCondition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull);

builder.AddAzureBlobContainerClient("icons");
builder.Services.AddSingleton<IExportPathService, ExportPathService>();
builder.Services.AddHostedService<IconSeederService>();

builder.Services.AddSingleton<IEntityStoreService, EntityStoreService>();
builder.Services.AddSingleton<IDesignTokenService>(s =>
    new DesignTokenService(s.GetRequiredService<IEntityStoreService>().Vocabulary));
builder.Services.AddSingleton<IRichTextService>(s =>
    new RichTextService(s.GetRequiredService<IEntityStoreService>().Vocabulary.TextStyles));

builder.Services.AddSingleton<IChromeAssetService, ChromeAssetService>();
builder.Services.AddSingleton<IPlaceholderService, PlaceholderService>();
builder.Services.AddSingleton<ITooltipViewService, TooltipViewService>();
builder.Services.AddSingleton<IFragmentService, FragmentService>();
builder.Services.AddSingleton<ITooltipService, TooltipService>();

builder.Services.AddSingleton<ICodexService, CodexService>();
builder.Services.AddSingleton<ISearchIndexService, SearchIndexService>();
builder.Services.AddSingleton<ICodexSearchService, CodexSearchService>();
builder.Services.AddSingleton<ICodexViewService, CodexViewService>();
builder.Services.AddSingleton<ISuggestionService, SuggestionService>();

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddResponseCompression(options => options.EnableForHttps = true);

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapOpenApi();

app.UseResponseCompression();
app.MapStaticAssets();

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint(ApiDocument.Route, ApiDocument.Title);
    options.RoutePrefix = ApiDocument.Ui;
    options.DocumentTitle = ApiDocument.Title;
});

app.UseAntiforgery();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.MapApiEndpoints();

app.Run();

public partial class Program;
