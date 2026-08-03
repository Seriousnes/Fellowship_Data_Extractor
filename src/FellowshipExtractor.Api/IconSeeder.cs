using System.Diagnostics;
using System.Security.Cryptography;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace FellowshipExtractor.Api;

/// <summary>
/// Fills the icon container from the export the extractor wrote: the icon corpus at the root, and
/// the tooltip chrome under its own folder.
///
/// Both are a build output rather than user data. Re-running the extractor rewrites all of them, so
/// the container has to follow the export rather than being loaded once and left. The emulator
/// keeps a data volume, which makes an unconditional upload of the whole 145 MB corpus the wrong
/// default and an upload-if-absent the wrong one too: the first is slow on every start and the
/// second serves last week's art after a re-export.
///
/// What separates them is the content hash. Blob storage records the MD5 an upload declares and
/// returns it on the listing, so one list call tells the seeder exactly which files the export has
/// changed, and only those go over the wire. That covers the tinted masks too, which are generated
/// rather than read from disk: they hash the same on every start, so they upload once.
///
/// One service and two passes over one listing. Two hosted services would race each other on the
/// container create and the access policy below.
/// </summary>
public sealed class IconSeeder(
    BlobContainerClient container,
    ExportPaths paths,
    ChromeAssets chrome,
    ILogger<IconSeeder> logger) : IHostedService
{
    private const string ContentType = "image/png";
    private const int Concurrency = 16;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var started = Stopwatch.StartNew();

        // Blob-level anonymous read. A tooltip is an HTML fragment the caller embeds, so its icon
        // src has to be a URL the reader's browser can fetch on its own. Proxying the images back
        // through the API would work and would put every icon request through it for nothing.
        //
        // The access level is set rather than only passed to the create, because the create does
        // nothing at all to a container that already exists. The emulator keeps a data volume, so
        // after the first run the container always already exists and an access level given only
        // here would never be applied: every icon comes back 403 and the tooltip renders holes.
        await container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);
        await container.SetAccessPolicyAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);

        var present = new Dictionary<string, string>(StringComparer.Ordinal);
        await foreach (var blob in container.GetBlobsAsync(cancellationToken: cancellationToken))
        {
            if (blob.Properties.ContentHash is { } hash) present[blob.Name] = Convert.ToBase64String(hash);
        }

        // The icon corpus, which is every PNG at the top of the export. Not a recursive search: the
        // chrome lives one folder down and goes up under its own names in the pass below.
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
            paths.Ui, ChromeAssets.Prefix, chromeUploaded, payloads.Count, chromeBytes / 1024d,
            payloads.Count - chromeUploaded, chromeStarted.Elapsed.TotalSeconds);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// One blob, uploaded only if the container is not already holding these exact bytes. True when
    /// it went over the wire.
    /// </summary>
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
                HttpHeaders = new BlobHttpHeaders { ContentType = ContentType, ContentHash = hash },
            },
            token);

        return true;
    }
}

/// <summary>
/// Where the export the API serves lives. The AppHost passes it so the API and the icons it points
/// at always come from the same extractor run.
/// </summary>
public sealed class ExportPaths(IConfiguration configuration)
{
    private const string Variable = "FELLOWSHIP_EXPORT";

    public string Export { get; } = configuration[Variable]
        ?? throw new InvalidOperationException(
            $"{Variable} is not set. It names the folder the extractor's export-all command wrote, " +
            "and the AppHost passes it. Set it to run the API outside the AppHost.");

    public string Entities => Path.Combine(Export, "entities.jsonl");

    public string Vocabulary => Path.Combine(Export, "vocabulary.json");

    /// <summary>The tooltip chrome the extractor's ui command writes.</summary>
    public string Ui => Path.Combine(Export, "ui");
}
