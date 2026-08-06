using FellowshipExtractor.Tooltip;
using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Output;

public interface IOutputFormat : IAsyncDisposable
{
    Task WriteAsync(EntityDocument entity, CancellationToken cancellationToken);

    Task WriteVocabularyAsync(Vocabulary vocabulary, CancellationToken cancellationToken);
}
