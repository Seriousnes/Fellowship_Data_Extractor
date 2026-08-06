using System.Text.Json;
using System.Text.Json.Serialization;

using FellowshipExtractor.Tooltip;

using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Output;

public sealed class JsonlOutputFormat(string folder) : IOutputFormat
{
    public const string FileName = "entities.jsonl";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private StreamWriter? _writer;

    private StreamWriter Writer() =>
        _writer ??= new StreamWriter(Path.Combine(Folder(), FileName), append: false);

    private string Folder()
    {
        Directory.CreateDirectory(folder);
        return folder;
    }

    public async Task WriteAsync(EntityDocument entity, CancellationToken cancellationToken)
    {
        var type = EntityTypes.Slug(EntityTypes.TypeOf(entity));
        var body = JsonSerializer.SerializeToNode(entity, entity.GetType(), Options)!.AsObject();

        body.Insert(0, "type", type);

        await Writer().WriteLineAsync(body.ToJsonString(Options));
    }

    public async Task WriteVocabularyAsync(Vocabulary vocabulary, CancellationToken cancellationToken) =>
        await vocabulary.WriteAsync(Folder());

    public async ValueTask DisposeAsync()
    {
        if (_writer is null) return;

        await _writer.FlushAsync();
        await _writer.DisposeAsync();
    }
}
