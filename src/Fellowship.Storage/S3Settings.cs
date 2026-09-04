using Microsoft.Extensions.Configuration;

namespace Fellowship.Storage;

public sealed record S3Settings(string Bucket, string? PublicUrl, string? ServiceUrl)
{
    public const string Section = "S3";

    public const string BucketKey = $"{Section}:Bucket";

    public static S3Settings From(IConfiguration configuration, string bucket)
    {
        var section = configuration.GetSection(Section);

        return new S3Settings(
            bucket, StorageDefaults.Named(section["PublicUrl"]), StorageDefaults.Named(section["ServiceUrl"]));
    }
}
