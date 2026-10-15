using static StackIdentity;

var stack = Stack(AppContext.BaseDirectory);

Rebind("ASPNETCORE_URLS", stack.Offset, 17079, 15133);
Rebind("ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL", stack.Offset, 21156);
Rebind("ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL", stack.Offset, 22186);

var builder = DistributedApplication.CreateBuilder(args);

var codexPort = Port(builder, "Ports:Codex", stack.Offset, 5280);
var blobPort = Port(builder, "Ports:Blob", 0, SharedBlobPort);

var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(emulator => emulator
        .WithDataVolume(SharedVolume)
        .WithBlobPort(blobPort)
        .WithLifetime(ContainerLifetime.Persistent)
        .InSharedStack(SharedContainer, SharedProject));

var icons = storage.AddBlobContainer("icons", "icons");

builder.AddProject<Projects.Fellowship_Codex>("codex")
    .WithReference(icons)
    .WaitFor(icons)
    .WithHttpEndpoint(port: codexPort, name: "codex-http", isProxied: false)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

builder.InComposeStack(stack.Project, stack.Root).Build().Run();
