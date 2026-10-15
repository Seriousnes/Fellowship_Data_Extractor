using Fellowship.Codex.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fellowship.Codex.Mcp;

public static class Entry
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

        builder.AddCodexMcp(ExportFolder.Locate(args)).WithStdioServerTransport();

        var host = builder.Build();

        await host.Services.LoadExportAsync(
            host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping);

        await host.RunAsync();
    }
}
