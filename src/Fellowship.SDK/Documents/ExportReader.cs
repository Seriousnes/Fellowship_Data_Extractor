using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Fellowship.SDK.Documents;

/// <summary>Reads an export's settings.json and entities.jsonl.</summary>
public static class ExportReader
{
    public const string DataFolder = "data";

    public const string EntitiesName = "entities.jsonl";

    public const string SettingsName = "settings.json";

    private const string TrimWarning =
        "A document is read into a type named at run time, so trimming can take that type away.";

    public static string Data(string name) => $"{DataFolder}/{name}";

    public static string Data(string build, string name) => Data($"{ExportFolders.Name(build)}/{name}");

    public static InvalidOperationException Missing(string name) =>
        new($"The export carries no {name}. Run 'dotnet run --project src/FellowshipExtractor' to " +
            "write it: with no destination it writes the output/export-<build> folder the codex and the " +
            "MCP server read, '--bucket <name>' fills an S3 bucket, and '--connection-string' fills an " +
            "Azure Blob container. The FellowshipExtractor.Publisher app host runs that one. The " +
            "codex reads whichever of the two its own configuration names.");

    [RequiresUnreferencedCode(TrimWarning)]
    public static SettingsFile ReadSettings(string? json)
    {
        if (json is null) throw Missing(SettingsName);

        return JsonSerializer.Deserialize<SettingsFile>(json, EntityDocuments.Options)
            ?? throw new InvalidOperationException($"{SettingsName} deserialised to nothing.");
    }

    [RequiresUnreferencedCode(TrimWarning)]
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

    [RequiresUnreferencedCode(TrimWarning)]
    private static EntityDocument Document(string line, int number)
    {
        try
        {
            return JsonSerializer.Deserialize<EntityDocument>(line, EntityDocuments.Options)
                ?? throw new InvalidOperationException(
                    $"Line {number} of {EntitiesName} deserialised to nothing.");
        }
        catch (Exception failure) when (failure is JsonException or NotSupportedException)
        {
            throw new InvalidOperationException(
                $"Line {number} of {EntitiesName} does not read as a document: {failure.Message} An " +
                $"export written before a document had the '{EntityDocuments.Discriminator}' it is read " +
                "back as reads this way. Write it again with 'dotnet run --project " +
                "src/FellowshipExtractor'.",
                failure);
        }
    }
}
