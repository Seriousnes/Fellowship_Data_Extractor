using System.Text.Json;
using FellowshipExtractor.Extraction;
using FellowshipExtractor.Output;
using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;
using Fellowship.Storage;

namespace FellowshipExtractor.Tests;

public sealed class OutputFormatTests
{
    private const string NoStorage = "nothing here parses as a connection string";

    private const string Build = "19240315";

    private const string DevelopmentStorage =
        "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;" +
        "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
        "BlobEndpoint=http://localhost:10510/devstoreaccount1;" +
        "QueueEndpoint=http://localhost:10511/devstoreaccount1;" +
        "TableEndpoint=http://localhost:10512/devstoreaccount1";

    private sealed class ClosingTarget(bool fails) : IExportTarget
    {
        public bool Disposed { get; private set; }

        public Task WriteDocumentAsync(EntityDocument document, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task WriteSettingsAsync(SettingsFile settings, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task WriteImageAsync(string name, byte[] png, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            Disposed = true;

            return fails
                ? ValueTask.FromException(new InvalidOperationException("this target failed to close"))
                : ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task AJsonlLineIsTheDocumentAndItsType()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;

        await using (var target = new FolderTarget(folder, Build))
        {
            await target.WriteDocumentAsync(new AbilityDocument { Id = 1122, Name = "Stone Shield" }, default);
        }

        var line = (await File.ReadAllLinesAsync(
            Path.Combine(folder, ExportFolders.Name(Build), ExportLayout.EntitiesFile)))[0];
        using var document = JsonDocument.Parse(line);

        Assert.Equal("ability", document.RootElement.GetProperty(EntityDocuments.Discriminator).GetString());
        Assert.Equal(1122, document.RootElement.GetProperty("id").GetInt32());
        Assert.Equal("Stone Shield", document.RootElement.GetProperty("name").GetString());
        Assert.False(document.RootElement.TryGetProperty("table", out _));
    }

    [Fact]
    public async Task AReferenceCarriesTheTypesSlug()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;

        await using (var target = new FolderTarget(folder, Build))
        {
            await target.WriteDocumentAsync(
                new EffectDocument
                {
                    Id = 7788,
                    Name = "Searing Blaze",
                    NameBorrowedFrom = new EntityReference(EntityType.GemAbility, 4321, null),
                },
                default);
        }

        var line = (await File.ReadAllLinesAsync(
            Path.Combine(folder, ExportFolders.Name(Build), ExportLayout.EntitiesFile)))[0];
        using var document = JsonDocument.Parse(line);

        Assert.Equal("effect", document.RootElement.GetProperty(EntityDocuments.Discriminator).GetString());

        var reference = document.RootElement.GetProperty("nameBorrowedFrom");

        Assert.Equal("gem-ability", reference.GetProperty("type").GetString());
        Assert.Equal(4321, reference.GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task AnImageLandsUnderUiAndTheSettingsBesideTheDocuments()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;

        await using (var target = new FolderTarget(folder, Build))
        {
            await target.WriteImageAsync("Firemage_Bolt_full.webp", [1, 2, 3], default);
            await target.WriteSettingsAsync(new SettingsFile { GemTypes = ["Ruby"] }, default);
        }

        Assert.Equal(
            [1, 2, 3],
            await File.ReadAllBytesAsync(
                Path.Combine(folder, ExportLayout.UiFolder, "Firemage_Bolt_full.webp")));

        var settings = JsonSerializer.Deserialize<SettingsFile>(
            await File.ReadAllTextAsync(
                Path.Combine(folder, ExportFolders.Name(Build), Extraction.Settings.FileName)),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.Equal(["Ruby"], settings!.GemTypes);
    }

    [Fact]
    public async Task AnImageWrittenTwiceWithTheSameBytesIsWrittenOnce()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;

        await using var target = new FolderTarget(folder, Build);

        await target.WriteImageAsync("T_GoldDivider_full.webp", [7, 7, 7], default);
        await target.WriteImageAsync("T_GoldDivider_full.webp", [7, 7, 7], default);

        Assert.Single(Directory.EnumerateFiles(Path.Combine(folder, ExportLayout.UiFolder)));
    }

    [Fact]
    public async Task AnImageWrittenTwiceWithDifferentBytesNamesTheNameTwoSourcesClaim()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;

        await using var target = new FolderTarget(folder, Build);

        await target.WriteImageAsync("T_GoldDivider_full.webp", [7, 7, 7], default);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => target.WriteImageAsync("T_GoldDivider_full.webp", [8, 8, 8], default));

        Assert.Contains("T_GoldDivider_full.webp", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APassThatEndedWithoutItsSettingsPublishesNoDocumentsEither()
    {
        var target = new StorageTarget(AzureStorage.FromConnectionString(NoStorage), Build);

        await target.WriteDocumentAsync(new AbilityDocument { Id = 1122, Name = "Stone Shield" }, default);

        Assert.Null(await Record.ExceptionAsync(async () => await target.DisposeAsync()));
    }

    [Fact]
    public async Task APassHoldingBothHalvesPublishesThem()
    {
        var target = new StorageTarget(AzureStorage.FromConnectionString(NoStorage), Build);

        await target.WriteDocumentAsync(new AbilityDocument { Id = 1122, Name = "Stone Shield" }, default);
        await target.WriteSettingsAsync(new SettingsFile { GemTypes = ["Ruby"] }, default);

        await Assert.ThrowsAsync<FormatException>(async () => await target.DisposeAsync());
    }

    [Fact]
    public void AStorageRunFilesTheBuildItExportedUnderData()
    {
        Assert.Equal(
            $"{ExportLayout.DataFolder}/{ExportFolders.Name(Build)}/{ExportLayout.EntitiesFile}",
            ExportLayout.Data(Build, ExportLayout.EntitiesFile));

        Assert.Equal("data/export-19240315/settings.json", ExportLayout.Data(Build, Settings.FileName));
    }

    [Fact]
    public void AConnectionStringNamingItsContainerNamesThatContainer()
    {
        var storage = AzureStorage.FromConnectionString($"{DevelopmentStorage};ContainerName=snapshots");

        Assert.Equal("snapshots", storage.Name);
        Assert.Equal(
            "http://localhost:10510/devstoreaccount1/snapshots/ui/T_GoldDivider_full.webp",
            storage.Address("ui/T_GoldDivider_full.webp").AbsoluteUri);
    }

    [Fact]
    public void AConnectionStringNamingNoContainerNamesIcons()
    {
        var storage = AzureStorage.FromConnectionString(DevelopmentStorage);

        Assert.Equal("icons", storage.Name);
        Assert.Equal(
            "http://localhost:10510/devstoreaccount1/icons/ui/T_GoldDivider_full.webp",
            storage.Address("ui/T_GoldDivider_full.webp").AbsoluteUri);
    }

    [Fact]
    public async Task ATargetThatFailsToCloseLeavesEveryOtherTargetClosed()
    {
        ClosingTarget failing = new(fails: true), second = new(fails: false);

        var failure = await Assert.ThrowsAsync<AggregateException>(
            async () => await new CompositeTarget([failing, second]).DisposeAsync());

        Assert.True(failing.Disposed);
        Assert.True(second.Disposed);
        Assert.Single(failure.InnerExceptions);
    }

    [Fact]
    public void AReferencesTypeReadsBackAsTheMemberItWasWrittenFrom() =>
        Assert.All(Enum.GetValues<EntityType>(), type =>
        {
            var json = JsonSerializer.Serialize(new EntityReference(type, 1, null));

            Assert.Contains($"\"{EntityTypes.Slug(type)}\"", json);
            Assert.Equal(type, JsonSerializer.Deserialize<EntityReference>(json)!.Type);
        });
}
