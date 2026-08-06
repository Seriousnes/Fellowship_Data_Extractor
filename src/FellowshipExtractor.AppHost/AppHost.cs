using System.Globalization;
using System.Security.Cryptography;
using System.Text;

var stack = Stack(AppContext.BaseDirectory);

Rebind("ASPNETCORE_URLS", stack.Offset, 17079, 15133);
Rebind("ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL", stack.Offset, 21156);
Rebind("ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL", stack.Offset, 22186);

var builder = DistributedApplication.CreateBuilder(args);

var codexPort = Port(builder, "Ports:Codex", stack.Offset, 5280);
var blobPort = Port(builder, "Ports:Blob", stack.Offset, 10000);

var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(emulator => emulator
        .WithDataVolume($"fellowship-azurite-{stack.Name}")
        .WithBlobPort(blobPort)
        .InComposeStack("azurite", stack.Project));

var icons = storage.AddBlobContainer("icons", "icons");

var exportPath = Path.GetFullPath(
    Path.Combine(builder.AppHostDirectory, "..", "..", "output", "export"));

builder.AddProject<Projects.FellowshipExtractor_Api>("codex")
    .WithReference(icons)
    .WaitFor(icons)
    .WithEnvironment("FELLOWSHIP_EXPORT", exportPath)
    .WithHttpEndpoint(port: codexPort, name: "codex-http", isProxied: false)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

builder.Build().Run();

static (string Name, string Project, int Offset) Stack(string from)
{
    var digest = SHA256.HashData(Encoding.UTF8.GetBytes(from.ToLowerInvariant()));
    var name = Convert.ToHexString(digest)[..8].ToLowerInvariant();

    var offset = Environment.GetEnvironmentVariable("FELLOWSHIP_PORT_OFFSET") is { Length: > 0 } chosen &&
        int.TryParse(chosen, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pinned)
        ? pinned
        : (int.Parse(name[..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture) % 89 + 1) * 10;

    return (name, $"fellowship-{name}", offset);
}

static void Rebind(string variable, int offset, params int[] basePorts)
{
    var current = Environment.GetEnvironmentVariable(variable);
    if (string.IsNullOrEmpty(current)) return;

    var moved = current.Split(';', StringSplitOptions.RemoveEmptyEntries)
        .Select(address =>
        {
            if (!Uri.TryCreate(address.Trim(), UriKind.Absolute, out var uri)) return address;
            if (!basePorts.Contains(uri.Port)) return address;

            return new UriBuilder(uri) { Port = uri.Port + offset }.Uri.ToString().TrimEnd('/');
        });

    Environment.SetEnvironmentVariable(variable, string.Join(';', moved));
}

static int Port(IDistributedApplicationBuilder builder, string key, int offset, int basePort) =>
    int.TryParse(builder.Configuration[key], NumberStyles.Integer, CultureInfo.InvariantCulture, out var chosen)
        ? chosen
        : basePort + offset;

static class ComposeStackExtensions
{
    public static IResourceBuilder<T> InComposeStack<T>(
        this IResourceBuilder<T> builder, string service, string project)
        where T : ContainerResource
        => builder
            .WithContainerName($"{project}-{service}")
            .WithContainerRuntimeArgs(
                "--label", $"com.docker.compose.project={project}",
                "--label", $"com.docker.compose.service={service}",
                "--label", "com.docker.compose.oneoff=False");
}
