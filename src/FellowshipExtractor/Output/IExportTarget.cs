using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Output;

public interface IExportTarget : IAsyncDisposable
{
    Task WriteDocumentAsync(EntityDocument document, CancellationToken cancellationToken);

    Task WriteSettingsAsync(SettingsFile settings, CancellationToken cancellationToken);

    Task WriteImageAsync(string name, byte[] content, CancellationToken cancellationToken);
}
