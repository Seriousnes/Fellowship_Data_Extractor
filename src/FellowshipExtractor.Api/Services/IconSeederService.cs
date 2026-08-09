using System.Diagnostics;
using System.Security.Cryptography;

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;

namespace FellowshipExtractor.Api.Services;

public sealed class IconSeederService(
    BlobContainerClient container,
    IExportPathService paths,
    IChromeAssetService chrome,
    ILogger<IconSeederService> logger) : IHostedService
{
    private const string ContentType = "image/png";
    private const string Revalidate = "no-cache";
    private const int Concurrency = 16;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var started = Stopwatch.StartNew();

        await container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);
        await container.SetAccessPolicyAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);

        var present = new Dictionary<string, string>(StringComparer.Ordinal);
        await foreach (var blob in container.GetBlobsAsync(cancellationToken: cancellationToken))
        {
            if (blob.Properties.ContentHash is not { } hash) continue;
            if (blob.Properties.CacheControl != Revalidate) continue;

            present[blob.Name] = Convert.ToBase64String(hash);
        }

        var local = Directory.GetFiles(paths.Export, "*.png");
        var uploaded = 0;
        var bytes = 0L;

        await Parallel.ForEachAsync(
            local,
            new ParallelOptions { MaxDegreeOfParallelism = Concurrency, CancellationToken = cancellationToken },
            async (file, token) =>
            {
                var content = await File.ReadAllBytesAsync(file, token);

                if (await UploadAsync(Path.GetFileName(file), content, present, token))
                {
                    Interlocked.Increment(ref uploaded);
                    Interlocked.Add(ref bytes, content.Length);
                }
            });

        logger.LogInformation(
            "Icons seeded from {Export}: {Uploaded} of {Total} uploaded ({Megabytes:F1} MB), {Skipped} already current, in {Elapsed:F1}s",
            paths.Export, uploaded, local.Length, bytes / 1048576d, local.Length - uploaded, started.Elapsed.TotalSeconds);

        var chromeStarted = Stopwatch.StartNew();
        var payloads = chrome.Payloads();
        var chromeUploaded = 0;
        var chromeBytes = 0L;

        await Parallel.ForEachAsync(
            payloads,
            new ParallelOptions { MaxDegreeOfParallelism = Concurrency, CancellationToken = cancellationToken },
            async (payload, token) =>
            {
                if (await UploadAsync(payload.Name, payload.Content, present, token))
                {
                    Interlocked.Increment(ref chromeUploaded);
                    Interlocked.Add(ref chromeBytes, payload.Content.Length);
                }
            });

        logger.LogInformation(
            "Chrome seeded from {Ui} under {Prefix}/: {Uploaded} of {Total} uploaded ({Kilobytes:F0} kB), {Skipped} already current, in {Elapsed:F1}s",
            paths.Ui, ChromeAssetService.Prefix, chromeUploaded, payloads.Count, chromeBytes / 1024d,
            payloads.Count - chromeUploaded, chromeStarted.Elapsed.TotalSeconds);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task<bool> UploadAsync(
        string name, byte[] content, IReadOnlyDictionary<string, string> present, CancellationToken token)
    {
        var hash = MD5.HashData(content);

        if (present.TryGetValue(name, out var existing) && existing == Convert.ToBase64String(hash))
        {
            return false;
        }

        await container.GetBlobClient(name).UploadAsync(
            new BinaryData(content),
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = ContentType,
                    ContentHash = hash,
                    CacheControl = Revalidate,
                },
            },
            token);

        return true;
    }
}
