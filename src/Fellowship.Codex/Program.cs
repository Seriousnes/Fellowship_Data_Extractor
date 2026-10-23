using Fellowship.Codex;
using Fellowship.Codex.Components;
using Fellowship.Codex.Endpoints;
using Fellowship.Codex.Services;
using Fellowship.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi(ApiDocument.Name, options =>
{
    options.AddDocumentTransformer<ApiInfo>();
    options.AddDocumentTransformer<QueryOperations>();
    options.AddOperationTransformer<StatsNote>();
    options.AddOperationTransformer<ListParameters>();
    options.AddOperationTransformer<BrowsingParameters>();
    options.AddSchemaTransformer<TypeSlugs>();
    options.AddSchemaTransformer<ParameterNumbers>();
});
builder.Services.AddProblemDetails();
builder.Services.AddValidation();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;

    options.SerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter());
});

FolderExportSource? folder = null;

if (StorageExtensions.Configured(builder.Configuration))
{
    builder.AddStorage();
    builder.Services.AddSingleton<IExportSource, StorageExportSource>();
    builder.Services.AddSingleton<ITextureSource, StorageTextureSource>();
    builder.Services.AddHttpClient();
    builder.Services.AddSingleton<ITextureReader, StorageTextureReader>();
}
else
{
    folder = new FolderExportSource(FolderExportSource.Located());
    builder.Services.AddSingleton<IExportSource>(folder);
    builder.Services.AddSingleton<ITextureSource, FolderTextureSource>();
    builder.Services.AddSingleton<ITextureReader>(new FolderTextureReader(folder.Root));
}

builder.Services.AddCodexServices();
builder.Services.AddTooltipServices();
builder.Services.AddCardServices();

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

if (folder is not null) app.UseStaticFiles(Textures.From(folder.Root));

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint(ApiDocument.Route, ApiDocument.Title);
    options.RoutePrefix = ApiDocument.Ui;
    options.DocumentTitle = ApiDocument.Title;
});

app.UseAntiforgery();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode().ServePages();

app.MapCards();

app.MapGroup(string.Empty).Serve().RequireCors(Serving.Origins).MapApiEndpoints();

app.Run();
