using Fellowship.Codex.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fellowship.Codex.Mcp;

public sealed record CodexReaders(
    string Folder,
    IEntityStoreService Store,
    ICodexService Codex,
    ISuggestionService Suggestions,
    ICodexViewService View);

public sealed class LiveExport(ILoggerFactory loggers) : IDisposable
{
    private ServiceProvider? provider;
    private CodexReaders? current;

    public CodexReaders Current => Volatile.Read(ref current) ?? throw new InvalidOperationException(
        "The export is not loaded yet. Program.cs awaits LiveExport.LoadAsync before host.RunAsync().");

    public async Task LoadAsync(string folder, CancellationToken cancellationToken)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton(loggers);
        services.AddSingleton<IExportSource>(new FolderExportSource(folder));
        services.AddSingleton<ITextureSource, NamedTextureSource>();
        services.AddCodexServices();

        var loaded = services.BuildServiceProvider();

        try
        {
            await loaded.LoadExportAsync(cancellationToken);

            var readers = new CodexReaders(
                folder,
                loaded.GetRequiredService<IEntityStoreService>(),
                loaded.GetRequiredService<ICodexService>(),
                loaded.GetRequiredService<ISuggestionService>(),
                loaded.GetRequiredService<ICodexViewService>());

            var replaced = provider;

            provider = loaded;
            Volatile.Write(ref current, readers);

            if (replaced is not null) await replaced.DisposeAsync();
        }
        catch
        {
            await loaded.DisposeAsync();
            throw;
        }
    }

    public void Dispose() => provider?.Dispose();
}
