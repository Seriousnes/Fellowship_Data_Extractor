using static StackIdentity;

var stack = Stack(AppContext.BaseDirectory);

Rebind("ASPNETCORE_URLS", stack.Offset, 18079, 16133);
Rebind("ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL", stack.Offset, 23256);
Rebind("ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL", stack.Offset, 24286);

var builder = DistributedApplication.CreateBuilder(args);

var blobPort = Port(builder, "Ports:Blob", 0, SharedBlobPort);

var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(emulator => emulator
        .WithDataVolume(SharedVolume)
        .WithBlobPort(blobPort)
        .WithLifetime(ContainerLifetime.Persistent)
        .InSharedStack(SharedContainer, SharedProject));

var icons = storage.AddBlobContainer("icons", "icons");

var extractor = builder.AddProject<Projects.FellowshipExtractor>("extractor")
    .WithReference(icons)
    .WaitFor(icons);

if (Environment.GetEnvironmentVariable("FELLOWSHIP_USMAP") is { Length: > 0 } mappings)
{
    extractor.WithEnvironment("FELLOWSHIP_USMAP", Path.GetFullPath(mappings));
}

builder.InComposeStack(stack.Project, stack.Root).Build().Run();
