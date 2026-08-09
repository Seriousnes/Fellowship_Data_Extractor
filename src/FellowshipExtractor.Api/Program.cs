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
    options.AddOperationTransformer<ApiParameters>();
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

app.MapGet("/api/tooltip/{slug}/{id:int}", async (string slug, int id, HttpContext http, ITooltipService tooltips) =>
        EntityTypes.TryParse(slug, out var type)
            ? await tooltips.Get(type, id, http.Request.Query)
            : PublicRoutes.NoSuchType(slug, StatusCodes.Status404NotFound))
    .WithName("GetTooltip")
    .WithTags(ApiDocument.Tooltips)
    .WithSummary("Any entity's tooltip as a self-contained HTML fragment, addressed by type and id.")
    .WithQuery(ApiQuerySet.Tooltip)
    .Produces<string>(StatusCodes.Status200OK, "text/html")
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status404NotFound);

app.MapGet("/api/entity/{slug}/{id:int}", (string slug, int id, IEntityStoreService store, ICodexViewService view) =>
{
    if (!EntityTypes.TryParse(slug, out var type))
    {
        return PublicRoutes.NoSuchType(slug, StatusCodes.Status404NotFound);
    }

    var readings = store.All(type, id);

    return readings.Count == 0
        ? Results.Problem(
            title: "No such id.", detail: $"The build has no {PublicTypes.Noun(type)} {id}.",
            statusCode: StatusCodes.Status404NotFound)
        : Results.Ok(readings.Select(reading => PublicEntity.From(reading, view)));
})
    .WithName("GetEntity")
    .WithTags(ApiDocument.Records)
    .WithSummary("One entity's record, with every per-hero reading of its id.")
    .Produces<IReadOnlyList<PublicEntity>>()
    .ProducesProblem(StatusCodes.Status404NotFound);

app.Run();

public partial class Program;

public static class PublicRoutes
{
    public static IResult NoSuchType(string? asked, int status) => Results.Problem(
        title: "No such type.",
        detail: PublicTypes.NoSuchType(asked),
        statusCode: status);
}
