// The tooltip API and the blob store its icons come from.
//
// The icons are a build output rather than user data: the extractor regenerates all of them from
// the cooked game files, and a regenerated icon has to reach the container. The emulator keeps a
// data volume so a restart does not re-upload 145 MB, and the seeder that fills it compares content
// hashes, so an icon the extractor has rewritten is replaced and one it has not is skipped.

// Both ports are fixed rather than left to be allocated. A tooltip's icon src is a URL baked into
// the HTML the caller embeds, so a blob port that moves on every restart invalidates every fragment
// already handed out. The API's own port is fixed for the same reason a dev API usually is: so the
// address can be written down. Measured cost of not doing this: every restart made a reader hunt
// the port through the process list and then probe each listener to find which one speaks HTTP,
// three or four round trips, and this project restarts the host constantly.
const int ApiPort = 5280;
const int BlobPort = 10000;

var builder = DistributedApplication.CreateBuilder(args);

var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(emulator => emulator.WithDataVolume().WithBlobPort(BlobPort));

var icons = storage.AddBlobContainer("icons", "icons");

// The export the extractor writes. The API reads the entity lines from here at startup and seeds
// the container from the icons beside them, so both halves always describe the same export run.
var exportPath = Path.GetFullPath(
    Path.Combine(builder.AppHostDirectory, "..", "..", "output", "export"));

builder.AddProject<Projects.FellowshipExtractor_Api>("api")
    .WithReference(icons)
    .WaitFor(icons)
    .WithEnvironment("FELLOWSHIP_EXPORT", exportPath)
    .WithHttpEndpoint(port: ApiPort, name: "api-http", isProxied: false)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

builder.Build().Run();
