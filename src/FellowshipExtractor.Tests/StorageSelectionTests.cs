using Fellowship.Storage;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FellowshipExtractor.Tests;

public sealed class StorageSelectionTests
{
    private const string Azurite = "UseDevelopmentStorage=true";

    private static IServiceProvider Configured(Dictionary<string, string?> settings)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.Configuration.AddInMemoryCollection(settings);
        builder.AddStorage();

        return builder.Services.BuildServiceProvider();
    }

    [Fact]
    public void A_configuration_naming_neither_store_is_refused_by_both_keys_it_reads()
    {
        var failure = Assert.Throws<InvalidOperationException>(() => Configured([]));

        Assert.Contains(StorageExtensions.ConnectionKey, failure.Message, StringComparison.Ordinal);
        Assert.Contains(S3Settings.BucketKey, failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_connection_string_serves_the_store_from_the_blob_container_it_names()
    {
        var storage = Configured(new Dictionary<string, string?>
        {
            [StorageExtensions.ConnectionKey] = $"{Azurite};ContainerName=art",
        }).GetRequiredService<IStorage>();

        Assert.IsType<AzureStorage>(storage);
        Assert.Equal("art", storage.Name);
    }

    [Fact]
    public void A_connection_string_naming_no_container_serves_the_default_one()
    {
        var storage = Configured(new Dictionary<string, string?>
        {
            [StorageExtensions.ConnectionKey] = Azurite,
        }).GetRequiredService<IStorage>();

        Assert.Equal(StorageDefaults.Name, storage.Name);
    }

    [Fact]
    public void A_bucket_serves_the_store_from_S3()
    {
        var storage = Configured(new Dictionary<string, string?>
        {
            [S3Settings.BucketKey] = "fellowship-codex",
            ["AWS:Region"] = "ap-southeast-2",
        }).GetRequiredService<IStorage>();

        Assert.IsType<S3Storage>(storage);
        Assert.Equal("fellowship-codex", storage.Name);
    }

    [Fact]
    public void A_bucket_reads_its_public_and_service_urls()
    {
        var settings = Configured(new Dictionary<string, string?>
        {
            [S3Settings.BucketKey] = "icons",
            ["S3:PublicUrl"] = "https://art.example.com",
            ["S3:ServiceUrl"] = "",
        }).GetRequiredService<S3Settings>();

        Assert.Equal("icons", settings.Bucket);
        Assert.Equal("https://art.example.com", settings.PublicUrl);
        Assert.Null(settings.ServiceUrl);
    }

    [Fact]
    public void A_connection_string_alongside_a_bucket_serves_the_store_from_the_blob_container()
    {
        var storage = Configured(new Dictionary<string, string?>
        {
            [StorageExtensions.ConnectionKey] = Azurite,
            [S3Settings.BucketKey] = "fellowship-codex",
            ["AWS:Region"] = "ap-southeast-2",
        }).GetRequiredService<IStorage>();

        Assert.IsType<AzureStorage>(storage);
    }
}
