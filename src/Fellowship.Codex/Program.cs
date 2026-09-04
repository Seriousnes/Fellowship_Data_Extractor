using Fellowship.SDK.Documents;

using Fellowship.Codex;
using Fellowship.Codex.Components;
using Fellowship.Codex.Endpoints;
using Fellowship.Codex.Models;
using Fellowship.Codex.Services;
using Fellowship.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi(ApiDocument.Name, options =>
{
    options.AddDocumentTransformer<ApiInfo>();
    options.AddOperationTransformer<StatsNote>();
    options.AddOperationTransformer<ListParameters>();
    options.AddOperationTransformer<SwitchParameters>();
    options.AddOperationTransformer<BrowsingParameters>();
});
builder.Services.AddProblemDetails();
builder.Services.AddValidation();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.DefaultIgnoreCondition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull);

builder.AddStorage();
builder.Services.AddSingleton<IExportSource, StorageExportSource>();
builder.Services.AddSingleton<IArtSource, StorageArtSource>();

builder.Services.AddCodexServices();
builder.Services.AddTooltipServices();

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddResponseCompression(options => options.EnableForHttps = true);

var app = builder.Build();

await app.Services.LoadExportAsync(app.Lifetime.ApplicationStopping);

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
