namespace Fellowship.SDK;

/// <summary>One hero, as the three things an item row cannot say for itself plus a colour.</summary>
public sealed record HeroEntry(string Codename, string Name, string? ArmorType, string PrimaryStat)
{
    /// <summary>The hero's accent colour as six hex digits, from the palette row their asset names.</summary>
    public string? Color { get; init; }
}

/// <summary>
/// One rung of the rarity ladder, under the name the build files it and the word each vocabulary
/// prints for it. Item and gem are separate vocabularies rather than two guesses at one, so the two
/// display columns part company wherever <c>RarityNames.GemLabel</c> renames a tier the item
/// ladder renames differently: tier 4 is filed Champion, prints Heroic on an item and Flawless on a
/// gem. A gem's exported rarity is already the printed gem word rather than the filed one, so a
/// reader holding one can join it on this column and on no other.
/// </summary>
public sealed record RarityEntry(int Tier, string Name, string DisplayName, string GemDisplayName);
