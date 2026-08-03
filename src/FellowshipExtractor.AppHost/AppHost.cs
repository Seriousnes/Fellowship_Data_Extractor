using System.Globalization;
using System.Security.Cryptography;
using System.Text;

// The codex web app, the tooltip API it serves alongside it, and the blob store their icons come
// from.
//
// The icons are a build output rather than user data: the extractor regenerates all of them from
// the cooked game files, and a regenerated icon has to reach the container. The emulator keeps a
// data volume so a restart does not re-upload 145 MB, and the seeder that fills it compares content
// hashes, so an icon the extractor has rewritten is replaced and one it has not is skipped.

// ---- one stack, and one set of ports, per worktree ------------------------------------------
//
// Several worktrees of this repo are open at once and each runs its own AppHost. Every address an
// AppHost claims is global to the machine or to the Docker daemon, so two of them starting at the
// same time collide on all of it: the dashboard, its OTLP and resource-service endpoints, the
// emulator's blob port, the web app's own port, and the emulator's container name. The ports in
// launchSettings.json are literals shared by every worktree, so the collision is the default rather
// than the exception - measured, not assumed: starting a second AppHost fails on
// "Failed to bind to address https://127.0.0.1:22186" before any resource starts.
//
// The discriminator is where this AppHost was built from, hashed. It is stable across restarts of
// the same worktree - which matters because a blob URL is baked into every tooltip fragment already
// handed out, and because this project restarts the host constantly - and different for every other
// worktree. A Guid per run would give isolation and lose both of those.
//
// Set FELLOWSHIP_PORT_OFFSET to pin the offset by hand; the ports are then the base ports plus it,
// which is how a reader who wants to write an address down takes control of it.
var stack = Stack(AppContext.BaseDirectory);

// The dashboard's own three addresses are read from configuration before any of this file runs, so
// they are set here rather than passed to the builder. launchSettings supplies the literals; this
// overwrites them, because a per-worktree port is the whole point and a profile cannot compute one.
// Both launch paths apply the profile and both were checked against a second AppHost already
// running: `dotnet run --project` and `aspire run` each land on the same derived dashboard port. An
// address a profile does not set is left to Aspire's own choice rather than invented here.
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

// The export the extractor writes. The web app reads the entity lines from here at startup and seeds
// the container from the icons beside them, so both halves always describe the same export run.
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

/// <summary>
/// What this worktree's containers are grouped and named under, and how far its ports sit from the
/// base ones. Derived from the build output directory, which is inside the worktree.
/// </summary>
static (string Name, string Project, int Offset) Stack(string from)
{
    var digest = SHA256.HashData(Encoding.UTF8.GetBytes(from.ToLowerInvariant()));
    var name = Convert.ToHexString(digest)[..8].ToLowerInvariant();

    var offset = Environment.GetEnvironmentVariable("FELLOWSHIP_PORT_OFFSET") is { Length: > 0 } chosen &&
        int.TryParse(chosen, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pinned)
        ? pinned
        // Ten to 890: a multiple of 10 so a worktree's ports are recognisable as a set once one of
        // them is known, under 900 so no derived port can reach the next base port above it, and
        // never zero, because zero is the literal in launchSettings that every worktree shares and
        // landing on it is the one outcome this whole calculation exists to avoid. The base ports
        // stay reachable by pinning FELLOWSHIP_PORT_OFFSET=0.
        : (int.Parse(name[..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture) % 89 + 1) * 10;

    return (name, $"fellowship-{name}", offset);
}

/// <summary>
/// Moves an address list this worktree's ports along. The variable's own scheme and host are kept:
/// the dashboard is http or https depending on which profile was launched, and rewriting that would
/// change what the reader asked for rather than only where it listens.
/// </summary>
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

/// <summary>
/// A host port for one of this AppHost's own resources: the configured one if the reader set it,
/// otherwise the base port moved along by this worktree's offset.
/// </summary>
static int Port(IDistributedApplicationBuilder builder, string key, int offset, int basePort) =>
    int.TryParse(builder.Configuration[key], NumberStyles.Integer, CultureInfo.InvariantCulture, out var chosen)
        ? chosen
        : basePort + offset;

/// <summary>
/// Files a container under one Docker Compose project, so a worktree's containers show up as one
/// stack that can be seen, stopped and removed together.
///
/// The container name carries the stack as well, because a container name is global to the daemon
/// while the compose labels only group: naming two worktrees' emulators "azurite" makes the second
/// one fail to create however they are labelled.
/// </summary>
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
