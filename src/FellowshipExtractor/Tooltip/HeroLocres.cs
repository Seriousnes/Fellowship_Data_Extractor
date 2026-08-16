namespace FellowshipExtractor.Tooltip;

public sealed record HeroProse(string Codename)
{
    public string? Title { get; init; }

    public string? Biography { get; init; }

    public string? Playstyle { get; init; }

    public string? SpiritAbility { get; init; }
}

public sealed class HeroLocres
{
    public const string KeyPrefix = "Hero_";
    public const string TitleSuffix = "_Title";
    public const string BiographySuffix = "_Biography";
    public const string PlaystyleSuffix = "_Playstyle";
    public const string SpiritAbilitySuffix = "_SpiritAbilityDescription";

    private readonly Dictionary<string, HeroProse> _byCodename = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _byCodename.Count;

    public int Titled => _byCodename.Values.Count(prose => prose.Title is not null);

    public int WithBiography => _byCodename.Values.Count(prose => prose.Biography is not null);

    public int WithPlaystyle => _byCodename.Values.Count(prose => prose.Playstyle is not null);

    public int WithSpiritAbility => _byCodename.Values.Count(prose => prose.SpiritAbility is not null);

    public static string KeyFor(string codename, string suffix) => KeyPrefix + codename + suffix;

    public HeroLocres(LocalizedText text, HeroNames heroes)
    {
        foreach (var profile in heroes.Profiles)
        {
            _byCodename[profile.Codename] = new HeroProse(profile.Codename)
            {
                Title = text.Find(KeyFor(profile.Codename, TitleSuffix)),
                Biography = text.Find(KeyFor(profile.Codename, BiographySuffix)),
                Playstyle = text.Find(KeyFor(profile.Codename, PlaystyleSuffix)),
                SpiritAbility = text.Find(KeyFor(profile.Codename, SpiritAbilitySuffix)),
            };
        }

        if (_byCodename.Count > 0 && Titled == 0)
        {
            throw new InvalidOperationException(
                $"No hero on the roster has a {KeyPrefix}<codename>{TitleSuffix} string in the {text.Culture} " +
                "localization table, so every hero would export with no title at all.");
        }
    }

    public HeroProse? For(string codename) => _byCodename.GetValueOrDefault(codename);
}
