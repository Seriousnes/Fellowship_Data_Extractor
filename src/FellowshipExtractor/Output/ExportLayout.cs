namespace FellowshipExtractor.Output;

public static class ExportLayout
{
    public const string DataFolder = "data";

    public const string UiFolder = "ui";

    public const string EntitiesFile = "entities.jsonl";

    public static string Variant(string image, string rarity) =>
        $"{Path.GetFileNameWithoutExtension(image)}-{rarity.ToLowerInvariant()}{Path.GetExtension(image)}";
}
