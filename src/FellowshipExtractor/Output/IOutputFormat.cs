using FellowshipExtractor.Tooltip;
using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Output;

/// <summary>
/// Where a run is written. The document is the whole of what crosses and its runtime class is its
/// type, so a format added later is one class and no change here.
/// </summary>
public interface IOutputFormat : IAsyncDisposable
{
    Task WriteAsync(EntityDocument entity, CancellationToken cancellationToken);

    Task WriteVocabularyAsync(Vocabulary vocabulary, CancellationToken cancellationToken);
}
