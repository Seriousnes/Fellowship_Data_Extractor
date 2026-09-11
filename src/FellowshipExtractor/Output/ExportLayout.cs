using System.Text.Json;
using System.Text.Json.Serialization;

using Fellowship.SDK;

namespace FellowshipExtractor.Output;

public static class ExportLayout
{
    public const string DataFolder = "data";

    public const string UiFolder = TextureSizes.Folder;

    public const string EntitiesFile = "entities.jsonl";

    public const string ImageType = "image/webp";

    public static readonly JsonSerializerOptions SettingsJson = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Data(string build, string file) =>
        $"{DataFolder}/{ExportFolders.Name(build)}/{file}";
}
