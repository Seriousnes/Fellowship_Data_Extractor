using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FellowshipExtractor.Tooltip;
using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Output;

/// <summary>
/// One line per row, in the folder the project writes to today. The line is the document with its
/// type beside it, which is what a reader needs to know which shape they are holding.
/// </summary>
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

    // Opened by the first line rather than by the constructor, because opening it truncates the
    // file that is already there. A run that gets as far as being constructed and then stops -
    // an unknown table, no mappings - leaves the last good export where it was.
    private StreamWriter? _writer;

    private StreamWriter Writer() =>
        _writer ??= new StreamWriter(Path.Combine(Folder(), FileName), append: false);

    /// <summary>The folder written to, made if this is the first thing the run puts in it.</summary>
    private string Folder()
    {
        Directory.CreateDirectory(folder);
        return folder;
    }

    public async Task WriteAsync(EntityDocument entity, CancellationToken cancellationToken)
    {
        var type = EntityTypes.Slug(DocumentProjection.TypeOf(entity));
        var body = JsonSerializer.SerializeToNode(entity, entity.GetType(), Options)!.AsObject();

        // The type leads the line, because it is what tells a reader which of the twenty-one shapes
        // the rest of the line is.
        body.Insert(0, "type", type);

        await Writer().WriteLineAsync(body.ToJsonString(Options));
    }

    /// <summary>
    /// The sidecar, written by the vocabulary itself. It builds from the cooked tables and knows the
    /// shape it writes, so this format asks rather than restating it.
    /// </summary>
    public async Task WriteVocabularyAsync(Vocabulary vocabulary, CancellationToken cancellationToken) =>
        await vocabulary.WriteAsync(Folder());

    public async ValueTask DisposeAsync()
    {
        if (_writer is null) return;

        await _writer.FlushAsync();
        await _writer.DisposeAsync();
    }
}
