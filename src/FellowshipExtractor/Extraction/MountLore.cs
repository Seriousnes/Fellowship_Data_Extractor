namespace FellowshipExtractor.Extraction;

public sealed class MountLore(LocalizedText text)
{
    public const string TagPrefix = "ItemID.Mount.";

    private const string KeyPrefix = "Mount_";
    private const string LongSuffix = "_DescriptionLong";
    private const string ShortSuffix = "_Description";

    private readonly SortedDictionary<string, string> _keyByTag = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, string> KeyByTag => _keyByTag;

    public int Count => _keyByTag.Count;

    public static string KeyFor(string tagName, string suffix) =>
        KeyPrefix + tagName[TagPrefix.Length..].Replace('.', '_') + suffix;

    public string? For(string tagName)
    {
        if (!tagName.StartsWith(TagPrefix, StringComparison.OrdinalIgnoreCase)) return null;

        foreach (var suffix in (string[])[LongSuffix, ShortSuffix])
        {
            var key = KeyFor(tagName, suffix);
            if (text.Find(key) is not { } lore) continue;

            _keyByTag[tagName] = key;
            return lore;
        }

        return null;
    }
}
