using System.Net;
using System.Runtime.CompilerServices;

using Amazon.S3;
using Amazon.S3.Model;

namespace Fellowship.Storage;

public sealed class S3Storage(IAmazonS3 client, S3Settings settings) : IStorage
{
    public string Name => settings.Bucket;

    public static S3Storage FromBucket(string bucket) =>
        new(new AmazonS3Client(), new S3Settings(bucket, null, null));

    public Uri Address(string name) => new(Url(name));

    private string Url(string name)
    {
        if (settings.PublicUrl is { } publicUrl) return $"{publicUrl.TrimEnd('/')}/{name}";

        if (settings.ServiceUrl is { } serviceUrl)
            return $"{serviceUrl.TrimEnd('/')}/{settings.Bucket}/{name}";

        return $"https://{settings.Bucket}.s3.{client.Config.RegionEndpoint.SystemName}.amazonaws.com/{name}";
    }

    public static byte[]? Hash(string? etag)
    {
        if (etag is null) return null;

        var value = etag.AsSpan().Trim('"');

        if (value.Length != 32) return null;

        foreach (var digit in value)
        {
            if (!Uri.IsHexDigit(digit)) return null;
        }

        return Convert.FromHexString(value);
    }

    public async IAsyncEnumerable<StoredObject> ListAsync(
        string? prefix, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var request = new ListObjectsV2Request { BucketName = settings.Bucket, Prefix = prefix };

        while (true)
        {
            var response = await client.ListObjectsV2Async(request, cancellationToken);

            foreach (var stored in response.S3Objects ?? [])
            {
                yield return new StoredObject(stored.Key, Hash(stored.ETag));
            }

            if (response.IsTruncated != true) yield break;

            request.ContinuationToken = response.NextContinuationToken;
        }
    }

    public async Task<string?> ReadTextAsync(string name, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetObjectAsync(settings.Bucket, name, cancellationToken);
            using var reader = new StreamReader(response.ResponseStream);

            return await reader.ReadToEndAsync(cancellationToken);
        }
        catch (AmazonS3Exception notFound)
            when (notFound.StatusCode == HttpStatusCode.NotFound || notFound.ErrorCode == "NoSuchKey")
        {
            return null;
        }
    }

    public async Task PrepareAsync(CancellationToken cancellationToken)
    {
        try
        {
            await client.HeadBucketAsync(
                new HeadBucketRequest { BucketName = settings.Bucket }, cancellationToken);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                $"The S3 bucket '{settings.Bucket}' cannot be reached. It must exist in the configured " +
                "region, and the account's credentials must be allowed to list and put objects in it.",
                failure);
        }
    }

    public Task WriteAsync(
        string name,
        ReadOnlyMemory<byte> content,
        string contentType,
        string cacheControl,
        byte[] md5,
        CancellationToken cancellationToken)
    {
        var request = new PutObjectRequest
        {
            BucketName = settings.Bucket,
            Key = name,
            InputStream = new MemoryStream(content.ToArray()),
            ContentType = contentType,
            MD5Digest = Convert.ToBase64String(md5),
        };

        request.Headers.CacheControl = cacheControl;

        return client.PutObjectAsync(request, cancellationToken);
    }
}
