using System.Net;
using System.Text;

using Azure.Core.Pipeline;
using Azure.Storage.Blobs;

namespace Fellowship.Codex.Tests;

public class BlobHistoryTests
{
    private const string Older = """{"$type":"item","id":5224,"name":"Sunforged Greatsword","slot":"Back"}""";

    private const string Newer = """{"$type":"item","id":5224,"name":"Sunforged Greatsword","slot":"Chest"}""";

    private const string Settings = """{"gemTypes":["Ruby"]}""";

    private sealed class Container(IReadOnlyDictionary<string, string> blobs) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath.TrimStart('/');
            var listing = request.RequestUri.Query.Contains("comp=list", StringComparison.Ordinal);

            var body = listing ? Listing() : blobs.GetValueOrDefault(path[(path.IndexOf('/') + 1)..]);

            if (body is null) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

            var content = new StringContent(body, Encoding.UTF8);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = request.Method == HttpMethod.Head ? new StringContent(string.Empty) : content,
            });
        }

        private string Listing()
        {
            var listed = new StringBuilder();

            listed.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>")
                .Append("<EnumerationResults ContainerName=\"http://localhost/icons\">")
                .Append("<Blobs>");

            foreach (var name in blobs.Keys)
            {
                listed.Append("<Blob><Name>").Append(name).Append("</Name><Properties /></Blob>");
            }

            return listed.Append("</Blobs><NextMarker /></EnumerationResults>").ToString();
        }
    }

    private static BlobExportSource Reading(IReadOnlyDictionary<string, string> blobs) =>
        new(new BlobContainerClient(
            new Uri("http://localhost/icons"),
            new BlobClientOptions
            {
                Transport = new HttpClientTransport(new HttpClient(new Container(blobs))),
            }));

    [Fact]
    public async Task Two_prefixes_in_one_container_load_as_the_newer_and_the_one_beneath_it()
    {
        var source = Reading(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ExportReader.Blob("1000", ExportReader.EntitiesName)] = Older,
            [ExportReader.Blob("1000", ExportReader.SettingsName)] = Settings,
            [ExportReader.Blob("1001", ExportReader.EntitiesName)] = Newer,
            [ExportReader.Blob("1001", ExportReader.SettingsName)] = Settings,
        });

        var history = await source.LoadAsync(Xunit.TestContext.Current.CancellationToken);

        Assert.Equal("1001", history.Latest);
        Assert.Equal("Chest", Assert.IsType<ItemDocument>(Assert.Single(history.Snapshot.Entities)).Slot);

        var beneath = Assert.Single(history.Older).Value;

        Assert.Equal("1000", beneath.Build);
        Assert.Equal([Older], beneath.Documents[new DocumentKey("item", 5224)]);
    }

    [Fact]
    public async Task A_prefix_carrying_documents_without_settings_names_the_file_it_lacks()
    {
        var source = Reading(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ExportReader.Blob("1001", ExportReader.EntitiesName)] = Newer,
        });

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => source.LoadAsync(Xunit.TestContext.Current.CancellationToken));

        Assert.Contains(
            ExportReader.Blob(ExportReader.SettingsName), failure.Message, StringComparison.Ordinal);
    }
}
