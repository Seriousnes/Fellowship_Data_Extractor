using System.Text.Json;

using FellowshipExtractor.Tooltip;

using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Output;

public sealed class JsonlOutputFormat(string folder) : IOutputFormat
{
    public const string FileName = "entities.jsonl";

    private StreamWriter? _writer;

    private StreamWriter Writer() =>
        _writer ??= new StreamWriter(Path.Combine(Folder(), FileName), append: false);

    private string Folder()
    {
        Directory.CreateDirectory(folder);
        return folder;
    }

    public async Task WriteAsync(EntityDocument entity, CancellationToken cancellationToken) =>
        await Writer().WriteLineAsync(JsonSerializer.Serialize<EntityDocument>(entity, EntityDocuments.Options));

    public async Task WriteVocabularyAsync(Vocabulary vocabulary, CancellationToken cancellationToken) =>
        await vocabulary.WriteAsync(Folder());

    public async ValueTask DisposeAsync()
    {
        if (_writer is null) return;

        await _writer.FlushAsync();
        await _writer.DisposeAsync();
    }
}
