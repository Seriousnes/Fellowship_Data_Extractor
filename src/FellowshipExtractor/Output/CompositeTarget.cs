using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Output;

public sealed class CompositeTarget(IReadOnlyList<IExportTarget> targets) : IExportTarget
{
    public async Task WriteDocumentAsync(EntityDocument document, CancellationToken cancellationToken)
    {
        foreach (var target in targets) await target.WriteDocumentAsync(document, cancellationToken);
    }

    public async Task WriteSettingsAsync(SettingsFile settings, CancellationToken cancellationToken)
    {
        foreach (var target in targets) await target.WriteSettingsAsync(settings, cancellationToken);
    }

    public async Task WriteImageAsync(string name, byte[] content, CancellationToken cancellationToken)
    {
        foreach (var target in targets) await target.WriteImageAsync(name, content, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        List<Exception> failures = [];

        foreach (var target in targets)
        {
            try
            {
                await target.DisposeAsync();
            }
            catch (Exception failure)
            {
                failures.Add(failure);
            }
        }

        if (failures.Count > 0) throw new AggregateException(failures);
    }
}
