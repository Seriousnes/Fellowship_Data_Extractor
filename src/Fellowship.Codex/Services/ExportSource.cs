using System.Text.Json;

using Fellowship.SDK.Documents;

namespace Fellowship.Codex.Services;

public sealed record ExportSnapshot(SettingsFile Settings, IReadOnlyList<EntityDocument> Entities);

public interface IExportSource
{
    Task<ExportSnapshot> LoadAsync(CancellationToken cancellationToken);
}

public sealed class ExportSnapshotHolder
{
    private ExportSnapshot? loaded;

    public ExportSnapshot Snapshot
    {
        get => loaded ?? throw new InvalidOperationException(
            "The export snapshot is not loaded yet. Program.cs awaits IExportSource.LoadAsync after " +
            "builder.Build() and before app.Run(), so everything the store is built from reads a loaded one.");
        set => loaded = value;
    }
}

public static class ExportReader
{
    public const string Data = "data";

    public const string EntitiesName = "entities.jsonl";

    public const string SettingsName = "settings.json";

    private static readonly JsonSerializerOptions Options = EntityDocuments.Options;

    public static string Blob(string name) => $"{Data}/{name}";

    public static InvalidOperationException Missing(string name) =>
        new($"The export carries no {Blob(name)}. Run the FellowshipExtractor.Publisher app host, which " +
            "runs the extractor into the icons container, or run 'dotnet run --project " +
            "src/FellowshipExtractor -- --output <folder>' to write the folder it reads instead.");

    public static SettingsFile ReadSettings(string? json)
    {
        if (json is null) throw Missing(SettingsName);

        return JsonSerializer.Deserialize<SettingsFile>(json, Options)
            ?? throw new InvalidOperationException($"{Blob(SettingsName)} deserialised to nothing.");
    }

    public static IReadOnlyList<EntityDocument> ReadEntities(IEnumerable<string> lines)
    {
        var documents = new List<EntityDocument>();
        var number = 0;

        foreach (var line in lines)
        {
            number++;
            if (line.Length == 0) continue;

            documents.Add(Document(line, number));
        }

        return documents;
    }

    public static IEnumerable<string> Lines(string text)
    {
        using var reading = new StringReader(text);

        while (reading.ReadLine() is { } line) yield return line;
    }

    private static EntityDocument Document(string line, int number)
    {
        try
        {
            return JsonSerializer.Deserialize<EntityDocument>(line, Options)
                ?? throw new InvalidOperationException(
                    $"Line {number} of {Blob(EntitiesName)} deserialised to nothing.");
        }
        catch (Exception failure) when (failure is JsonException or NotSupportedException)
        {
            throw new InvalidOperationException(
                $"Line {number} of {Blob(EntitiesName)} does not read as a document: {failure.Message} An " +
                $"export written before a document carried the '{EntityDocuments.Discriminator}' it is read " +
                "back as reads this way. Write it again with 'dotnet run --project " +
                "src/FellowshipExtractor'.",
                failure);
        }
    }
}
