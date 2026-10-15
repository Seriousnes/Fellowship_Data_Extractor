using Amazon;
using Amazon.Runtime;
using Amazon.S3;

using Fellowship.Storage;

namespace FellowshipExtractor.Tests;

public sealed class S3StorageTests
{
    private const string Texture = "ui/T_GoldDivider_full.webp";

    private static IAmazonS3 Client(string region) =>
        new AmazonS3Client(new BasicAWSCredentials("test", "test"), RegionEndpoint.GetBySystemName(region));

    [Fact]
    public void A_bucket_with_no_public_or_service_url_addresses_art_at_its_virtual_hosted_url()
    {
        var storage = new S3Storage(Client("eu-west-1"), new S3Settings("icons", null, null));

        Assert.Equal(
            "https://icons.s3.eu-west-1.amazonaws.com/ui/T_GoldDivider_full.webp",
            storage.Address(Texture).AbsoluteUri);
    }

    [Fact]
    public void A_public_url_addresses_art_under_it_rather_than_the_bucket()
    {
        var storage = new S3Storage(
            Client("us-east-1"), new S3Settings("icons", "https://texture.example.com/", null));

        Assert.Equal(
            "https://texture.example.com/ui/T_GoldDivider_full.webp",
            storage.Address(Texture).AbsoluteUri);
    }

    [Fact]
    public void A_service_url_addresses_art_path_style_under_the_bucket()
    {
        var storage = new S3Storage(
            Client("us-east-1"), new S3Settings("icons", null, "http://127.0.0.1:9000"));

        Assert.Equal(
            "http://127.0.0.1:9000/icons/ui/T_GoldDivider_full.webp",
            storage.Address(Texture).AbsoluteUri);
    }

    [Fact]
    public void A_public_url_wins_over_a_service_url()
    {
        var storage = new S3Storage(
            Client("us-east-1"), new S3Settings("icons", "https://texture.example.com", "http://127.0.0.1:9000"));

        Assert.Equal(
            "https://texture.example.com/ui/T_GoldDivider_full.webp",
            storage.Address(Texture).AbsoluteUri);
    }

    [Fact]
    public void A_plain_md5_etag_decodes_to_its_raw_bytes()
    {
        const string Md5 = "d41d8cd98f00b204e9800998ecf8427e";

        Assert.Equal(Convert.FromHexString(Md5), S3Storage.Hash($"\"{Md5}\""));
    }

    [Fact]
    public void A_multipart_etag_carries_no_content_hash()
    {
        Assert.Null(S3Storage.Hash("\"9bb58f26192e4ba00f01e2e7b136bbd8-5\""));
    }

    [Fact]
    public void An_etag_that_is_not_a_32_digit_hex_md5_carries_no_content_hash()
    {
        Assert.Null(S3Storage.Hash(null));
        Assert.Null(S3Storage.Hash("\"\""));
        Assert.Null(S3Storage.Hash("\"d41d8cd98f00b204e9800998ecf8427\""));
        Assert.Null(S3Storage.Hash("\"g41d8cd98f00b204e9800998ecf8427e\""));
    }

    [Fact]
    public async Task An_unreachable_bucket_is_reported_by_a_message_that_names_it()
    {
        var config = new AmazonS3Config
        {
            ServiceURL = "http://127.0.0.1:1",
            ForcePathStyle = true,
            MaxErrorRetry = 0,
        };

        var storage = new S3Storage(
            new AmazonS3Client(new BasicAWSCredentials("test", "test"), config),
            new S3Settings("codex-texture", null, "http://127.0.0.1:1"));

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => storage.PrepareAsync(CancellationToken.None));

        Assert.Contains("codex-texture", failure.Message, StringComparison.Ordinal);
        Assert.Contains("list and put objects", failure.Message, StringComparison.Ordinal);
    }
}
