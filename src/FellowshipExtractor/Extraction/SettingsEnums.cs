using FellowshipExtractor.Output;

namespace FellowshipExtractor.Extraction;

public static class SettingsEnums
{
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Members(SettingsFile settings) =>
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["ArmorType"] = Sorted(settings.StatCalculation.ArmorWeights.Keys),
            ["PrimaryStat"] = Sorted(settings.Heroes.Select(hero => hero.PrimaryStat)),
            ["Hero"] = Sorted(settings.Heroes.Select(hero => hero.Name)),
            ["GemType"] = Sorted(settings.GemTypes),
        };

    public static IReadOnlyList<(string Name, int Tier)> Rarities(SettingsFile settings) =>
        [.. settings.Rarities.OrderBy(rarity => rarity.Tier).Select(rarity => (rarity.Name, rarity.Tier))];

    public static IReadOnlyList<(string Name, int Value)> Categories(SettingsFile settings) =>
        [.. settings.AbilityCategories
            .OrderBy(category => category.Value)
            .Select(category => (category.Name, category.Value))];

    private static IReadOnlyList<string> Sorted(IEnumerable<string> members) =>
        [.. members.Where(member => member.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    public static void Publish(string folder)
    {
        var exported = Path.Combine(folder, ExportLayout.DataFolder, Settings.FileName);
        var settings = SettingsReader.Read(exported);

        var committed = Path.Combine(RepositoryRoot(), "src", "Fellowship.SDK", Settings.FileName);
        File.Copy(exported, committed, overwrite: true);

        Console.WriteLine($"wrote {committed}, the settings the SDK enums are generated from");
        foreach (var (name, values) in Members(settings)) Console.WriteLine($"  {name}: {values.Count} members");
        Console.WriteLine($"  Rarity: {Rarities(settings).Count} members");
        Console.WriteLine($"  AbilityCategory: {Categories(settings).Count} members");
    }

    private static string RepositoryRoot()
    {
        for (var at = new DirectoryInfo(AppContext.BaseDirectory); at is not null; at = at.Parent)
        {
            if (File.Exists(Path.Combine(at.FullName, "src", "FellowshipExtractor.slnx"))) return at.FullName;
        }

        throw new DirectoryNotFoundException(
            "No repository root found above the running binary. Looked for a directory holding " +
            $"src/FellowshipExtractor.slnx, walking up from {AppContext.BaseDirectory}.");
    }
}
