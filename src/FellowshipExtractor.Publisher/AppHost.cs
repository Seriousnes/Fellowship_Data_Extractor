using static StackIdentity;

var stack = Stack(Directory.GetCurrentDirectory());

Rebind("ASPNETCORE_URLS", stack.Offset, 18079, 16133);
Rebind("ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL", stack.Offset, 23256);
Rebind("ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL", stack.Offset, 24286);

var builder = DistributedApplication.CreateBuilder(args);

var blobPort = Port(builder, "Ports:Blob", stack.Offset, 10000);

var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(emulator => emulator
        .WithDataVolume($"fellowship-azurite-{stack.Name}")
        .WithBlobPort(blobPort)
        .WithLifetime(ContainerLifetime.Persistent));

var icons = storage.AddBlobContainer("icons", "icons");

builder.AddProject<Projects.FellowshipExtractor>("extractor")
    .WithReference(icons)
    .WaitFor(icons)
    .WithArgs("export-all");

builder.InComposeStack(stack.Project, stack.Root).Build().Run();
