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

FolderExportSource? folder = null;

if (StorageExtensions.Names(builder.Configuration))
{
    builder.AddStorage();
    builder.Services.AddSingleton<IExportSource, StorageExportSource>();
    builder.Services.AddSingleton<IArtSource, StorageArtSource>();
}
else
{
    folder = new FolderExportSource(FolderExportSource.Located());
    builder.Services.AddSingleton<IExportSource>(folder);
    builder.Services.AddSingleton<IArtSource, FolderArtSource>();
}

builder.Services.AddCodexServices();
builder.Services.AddTooltipServices();

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddResponseCompression(options => options.EnableForHttps = true);
builder.Services.AddServing();

var app = builder.Build();

await app.Services.LoadExportAsync(app.Lifetime.ApplicationStopping);

app.MapDefaultEndpoints();
app.MapOpenApi();

app.UseCors();
app.UseOutputCache();
app.UseResponseCompression();
app.UseRateLimiter();
app.UseRequestTimeouts();
app.MapStaticAssets();

if (folder is not null) app.UseStaticFiles(Art.From(folder.Root));

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint(ApiDocument.Route, ApiDocument.Title);
    options.RoutePrefix = ApiDocument.Ui;
    options.DocumentTitle = ApiDocument.Title;
});

app.UseAntiforgery();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode().ServePages();

app.MapGroup(string.Empty).Serve().RequireCors(Serving.Origins).MapApiEndpoints();

app.Run();

public partial class Program;
