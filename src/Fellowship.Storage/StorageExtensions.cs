using Amazon.Extensions.NETCore.Setup;
using Amazon.S3;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Fellowship.Storage;

public static class StorageExtensions
{
    public const string ConnectionName = StorageDefaults.Name;

    public const string ConnectionKey = $"ConnectionStrings:{ConnectionName}";

    public static void AddStorage(this IHostApplicationBuilder builder)
    {
        var configuration = builder.Configuration;

        if (StorageDefaults.Named(configuration.GetConnectionString(ConnectionName)) is { } connectionString)
        {
            builder.Services.AddSingleton<IStorage>(AzureStorage.FromConnectionString(connectionString));
            return;
        }

        if (StorageDefaults.Named(configuration[S3Settings.BucketKey]) is not { } bucket) throw Unconfigured();

        var settings = S3Settings.From(configuration, bucket);
        var options = configuration.GetAWSOptions();

        builder.Services.AddSingleton(settings);
        builder.Services.AddSingleton(_ => Client(options, settings));
        builder.Services.AddSingleton<IStorage, S3Storage>();
    }

    private static InvalidOperationException Unconfigured() =>
        new($"No store is configured. Set {ConnectionKey} to an Azure Blob container, " +
            $"'{StorageDefaults.Name}' unless the string names another, or set {S3Settings.BucketKey} to the " +
            "S3 bucket the codex reads and the extractor writes, with credentials allowed to list and put " +
            "objects in it reachable through the AWS SDK's own chain: environment variables, a shared " +
            "credentials file or an instance role.");

    private static IAmazonS3 Client(AWSOptions options, S3Settings settings)
    {
        var config = new AmazonS3Config();

        if (settings.ServiceUrl is { } serviceUrl)
        {
            config.ServiceURL = serviceUrl;
            config.ForcePathStyle = true;
        }
        else if (options.Region is { } region)
        {
            config.RegionEndpoint = region;
        }

        return options.Credentials is { } credentials
            ? new AmazonS3Client(credentials, config)
            : new AmazonS3Client(config);
    }
}
