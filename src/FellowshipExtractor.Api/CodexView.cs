using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Azure.Storage.Blobs;
using FellowshipExtractor.Tooltip;
using Microsoft.AspNetCore.Components;

namespace FellowshipExtractor.Api;

public sealed class CodexView(
    EntityStore store,
    Codex codex,
    DesignTokens tokens,
    ChromeAssets chrome,
    RichTextRenderer richText,
    Placeholders placeholders,
    BlobContainerClient icons)
{
    public EntityStore Store => store;

    public Codex Codex => codex;

    public DesignTokens Tokens => tokens;

    public ChromeAssets Chrome => chrome;

    public string Icon(string? icon) => icon is null ? chrome.SlotPlate : $"{icons.Uri}/{icon}";

    public bool HasIcon(string? icon) => icon is not null;

    public string NameColour(Entity entity)
    {
        if (entity.Rarity is { } rarity && tokens.Rarity(rarity) is { } rung) return Readable(rung)!;
        if (entity.GemType is { } gem && tokens.Gem(gem) is { } stone) return Readable(stone)!;

        if (entity.Heroes is { Count: 1 } && store.Hero(entity.Heroes[0]) is { } hero &&
            tokens.Hero(hero.Codename) is { } colour)
        {
            return Readable(colour)!;
        }

        // The muted tone is what a row named only by its noun and id is drawn in, so it follows the
        // same reading of "named" the heading does: a row whose shipped name is an asset path prints
        // as "Effect 2812" and is drawn as the unnamed row it now is.
        return PublicTypes.DisplayNameOf(entity) is null
            ? tokens.Color("core.text-secondary") ?? "#909BA6"
            : tokens.Color("core.text-primary") ?? "#EAE3DB";
    }

    public string? HeroColour(string name) => Readable(tokens.Hero(store.Hero(name)?.Codename ?? name));

    public string? HeroSwatch(string name) => tokens.Hero(store.Hero(name)?.Codename ?? name);

    public static string? Readable(string? colour)
    {
        if (colour is null || colour.Length != 7 || colour[0] != '#') return colour;

        if (!int.TryParse(colour.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var packed))
        {
            return colour;
        }

        double r = (packed >> 16) & 0xFF, g = (packed >> 8) & 0xFF, b = packed & 0xFF;

        for (var mix = 0; mix <= 10; mix++)
        {
            var k = mix / 10d;
            double lr = r + (255 - r) * k, lg = g + (255 - g) * k, lb = b + (255 - b) * k;

            if (Luminance(lr, lg, lb) >= Floor || mix == 10)
            {
                return $"#{(int)Math.Round(lr):X2}{(int)Math.Round(lg):X2}{(int)Math.Round(lb):X2}";
            }
        }

        return colour;
    }

    private const double Floor = 0.16;

    private static double Luminance(double r, double g, double b) =>
        0.2126 * Channel(r) + 0.7152 * Channel(g) + 0.0722 * Channel(b);

    private static double Channel(double value)
    {
        var scaled = value / 255d;
        return scaled <= 0.03928 ? scaled / 12.92 : Math.Pow((scaled + 0.055) / 1.055, 2.4);
    }

    public string Name(Entity entity) => PublicTypes.NameOf(entity);

    /// <summary>
    /// The public name of a row known only by its address, for the references that carry a table,
    /// an id and the row's shipped tag: the tag is the one thing about the reference that must not
    /// be drawn, so the row itself is read and named as it is named everywhere else.
    /// </summary>
    public string Name(string? table, int netId) =>
        store.All(table ?? string.Empty, netId) is { Count: > 0 } readings
            ? PublicTypes.NameOf(readings[0])
            : PublicTypes.NameOf(new Entity { Table = table, NetId = netId });

    public string Href(Entity entity) => Href(entity.Table, entity.NetId ?? 0);

    /// <summary>
    /// A row's page, under the slug its type is addressed by. A row of a non-public table has no
    /// page, and the empty string is the answer rather than a path with the table's shipped name in
    /// it: a caller that renders this links nowhere instead of publishing the name.
    /// </summary>
    public string Href(string? table, int netId) =>
        PublicTypes.SlugOf(table) is { } slug
            ? $"/entity/{slug}/{netId.ToString(CultureInfo.InvariantCulture)}"
            : string.Empty;

    /// <summary>The tooltip address of a row, under the same slug and on the same terms.</summary>
    public string Tip(Entity entity)
    {
        if (PublicTypes.SlugOf(entity.Table) is not { } slug) return string.Empty;

        var where = $"/api/tooltip/{slug}/{(entity.NetId ?? 0).ToString(CultureInfo.InvariantCulture)}";

        return Reading(entity) is { } hero ? $"{where}?hero={Uri.EscapeDataString(hero)}" : where;
    }

    /// <summary>
    /// The hero this reading is drawn for, on the ids that read differently per hero, and null on
    /// the ids one address answers for outright.
    ///
    /// Asked of <see cref="EntityStore.Wearer"/> rather than worked out here, because the answer is
    /// what the tooltip endpoint will resolve the same address to and two readings of that question
    /// is how the two surfaces came to disagree.
    /// </summary>
    public string? Reading(Entity entity)
    {
        if (entity.Table is not { } table || entity.NetId is not { } netId) return null;
        if (store.All(table, netId).Count <= 1) return null;

        return store.Wearer(entity)?.Name;
    }

    public MarkupString Text(string? source, IReadOnlyDictionary<string, Placeholder>? tokenValues,
        IReadOnlyList<string>? tags)
    {
        if (source is null) return default;

        return new MarkupString(richText.Render(source, Resolved(tokenValues, tags)));
    }

    /// <summary>
    /// The same sentence with the same numbers in it and no markup, for the surfaces answering with
    /// data rather than with a page. Resolved through the one path the page resolves through, so a
    /// record and the page over it cannot say different things about one row.
    /// </summary>
    public string? PlainText(string? source, IReadOnlyDictionary<string, Placeholder>? tokenValues,
        IReadOnlyList<string>? tags) =>
        source is null ? null : richText.Plain(source, Resolved(tokenValues, tags));

    /// <summary>
    /// A row's tokens as far as the build alone settles them, which is the reading with no caller
    /// attached: no hero, no item level and no stat totals, so a coefficient stays the share it is
    /// rather than becoming a number for somebody.
    /// </summary>
    private IReadOnlyDictionary<string, Substitution>? Resolved(
        IReadOnlyDictionary<string, Placeholder>? tokenValues, IReadOnlyList<string>? tags) =>
        tokenValues is null ? null : placeholders.Resolve(tokenValues, new TooltipQuery(), null, tags);

    /// <summary>
    /// What one substitution prints as on those same terms, which is the string a caller will find
    /// standing in the text where the token was.
    /// </summary>
    public string Reading(Placeholder token, IReadOnlyList<string>? tags) =>
        placeholders.Reading(token, new TooltipQuery(), null, tags).Text;

    public string RichTextCss(string scope) => richText.Css(scope);

    public static string Number(double value) => Codex.Number(value);

    public IReadOnlyList<(string Label, string Value, string? Unit, string? Href, string? Tip)> Facts(Entity entity)
    {
        var facts = new List<(string, string, string?, string?, string?)>();

        foreach (var property in Scalars)
        {
            if (Drawn.Contains(property.Name) || Internal.Contains(property.Name)) continue;

            var value = property.GetValue(entity);
            if (value is null) continue;

            var text = value switch
            {
                bool flag => flag ? "Yes" : "No",
                double number => Number(number),
                int number => number.ToString(CultureInfo.InvariantCulture),
                _ => value.ToString() ?? string.Empty,
            };

            if (text.Length == 0) continue;

            var points = Points(entity, property.Name, value);

            facts.Add((Heading(property.Name), text, Unit(property.Name), points?.Href, points?.Tip));
        }

        return facts;
    }

    /// <summary>
    /// Where a numeric fact leads, on the two fields whose number is another row's id rather than a
    /// quantity. A gem node replaces a node of its own table; an augmentation is always a talent
    /// record, and the item and trait rows carrying one do not say so.
    /// </summary>
    private (string Href, string? Tip)? Points(Entity entity, string property, object value) => property switch
    {
        nameof(Entity.Augmentation) when value is int id => Addresses(TalentAsset, id),
        nameof(Entity.Replaces) when value is int id => Addresses(entity.Table, id),
        _ => null,
    };

    /// <summary>
    /// Where a pointer to another row leads, or null when it leads into a table no reader can open.
    /// A caller draws plain text rather than a link in that case, which is what stops an address
    /// from being built out of a shipped table name.
    /// </summary>
    public (string Href, string? Tip)? Addresses(string? table, int netId)
    {
        if (!PublicTypes.IsPublic(table)) return null;

        return (Href(table, netId), store.All(table!, netId) is { Count: > 0 } readings ? Tip(readings[0]) : null);
    }

    private static readonly PropertyInfo[] Properties = typeof(Entity)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance);

    private static readonly PropertyInfo[] Scalars =
        [.. Properties.Where(p => IsScalar(Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType))];

    private static bool IsScalar(Type type) =>
        type == typeof(string) || type.IsPrimitive || type == typeof(decimal);

    /// <summary>
    /// Fields the page has a section of its own for, so a fact would repeat them.
    ///
    /// <see cref="Entity.Table"/> belongs here rather than below because the page does state it, as
    /// the public label its slug is addressed by; the shipped spelling behind that label is what
    /// never reaches a reader, and <see cref="PublicTypes"/> is where that is enforced.
    /// </summary>
    private static readonly HashSet<string> Drawn = new(StringComparer.Ordinal)
    {
        nameof(Entity.Table), nameof(Entity.NetId), nameof(Entity.DisplayName),
        nameof(Entity.Description), nameof(Entity.Icon),
    };

    /// <summary>
    /// Fields whose value is a shipped spelling, which is never drawn. Measured over the export,
    /// every value each of these carries is internal and none has a printed form to fall back on:
    /// <see cref="Entity.RuleSet"/> is 9 values on 2,204 items, all of the Default_NoGemSlot kind;
    /// <see cref="Entity.TalentSlot"/> is 19 values on 215 talents, all spelled Talent.ID.Talent15;
    /// <see cref="Entity.TraitCategoryTag"/> is 4 values on 38 traits, all spelled
    /// ItemTrait.TraitCategory.Heroic, and <see cref="Entity.TraitCategory"/> beside it already
    /// carries the word the game prints for the same fact; and <see cref="Entity.GemAttribute"/> is
    /// 6 values on the 6 gem roots, one per root and every one of them the root's own name run
    /// together with Power, so Amethyst holds AmethystPower and Topaz TopazPower.
    ///
    /// <see cref="Entity.Name"/> is here for the same reason and is the plainest case of it: it is
    /// the row's gameplay tag, which is the one spelling this whole catalogue is built to keep out.
    /// A row with no display name is named by <see cref="PublicTypes.NameOf"/> with its noun and its
    /// number instead, so the tag is not a fallback either.
    ///
    /// The test is not whether a field is internal, because an internal number is exactly what a
    /// catalogue like this exists to state and the tuning panels below print thousands of them. It
    /// is whether the value is a spelling and the fact it states is already drawn beside it: the
    /// attribute a gem root accumulates into says the root's own name back, and each trait carries
    /// the printed category beside the tag for it.
    ///
    /// The tag lists are out of reach here rather than named, because <see cref="Scalars"/> walks
    /// single values and a list is not one. <see cref="Entity.GrantedTags"/> is the one that would
    /// tempt a panel of its own, since a talent's entire content is that list, and it stays out on
    /// the test above: every entry is a gameplay tag, the build prints a word for none of them, and
    /// a talent already draws its text and its tuning. The same holds for the tag a
    /// <see cref="Entity.TalentGates"/> entry carries, which is why a gated group is drawn as
    /// conditional rather than under the tag that gates it. They stay backend, where the
    /// cross-references that need them read them.
    ///
    /// Separate from <see cref="Drawn"/> because the reason differs and the fix does too: a drawn
    /// field moves if the page is relaid out, and one of these can only ever be left out.
    /// </summary>
    private static readonly HashSet<string> Internal = new(StringComparer.Ordinal)
    {
        nameof(Entity.Name), nameof(Entity.RuleSet), nameof(Entity.TalentSlot),
        nameof(Entity.TraitCategoryTag), nameof(Entity.GemAttribute),
    };

    /// <summary>
    /// The table a talent's mechanics and text live in. An item's or a trait's augmentation number
    /// is an id into it and carries nothing saying so, so the table is what turns the number into
    /// an address and the heading below into a word.
    /// </summary>
    private const string TalentAsset = "CRCharacterAugmentationAsset";

    /// <summary>
    /// What a fact is headed with. The export names a field after the thing it reads, which for the
    /// augmentation pointer is the asset class this app publishes as a talent, so the pointer is
    /// headed with the same word its destination is headed with.
    /// </summary>
    private static string Heading(string property) => property switch
    {
        nameof(Entity.Augmentation) => PublicTypes.Label(TalentAsset),
        _ => Codex.Spaced(property),
    };

    private static string? Unit(string property) => property switch
    {
        nameof(Entity.CastTime) or nameof(Entity.ChannelTime) or nameof(Entity.ChannelTick)
            or nameof(Entity.Cooldown) or nameof(Entity.Duration) or nameof(Entity.Period) => "s",
        _ => null,
    };

    /// <summary>
    /// Every tuning number a dictionary of shipped rows holds, under the words it tunes.
    ///
    /// Three fields carry this shape and all three come through here: an entity's own
    /// <see cref="Entity.Constants"/>, the rows a <see cref="Placeholder"/> works its number out
    /// from, and each group of <see cref="Entity.Talents"/>. Measured over the rows of the export
    /// that is 4,399 dictionaries holding 9,844 keys, 5,249 of them distinct, and the granted
    /// abilities and legendary stat lines nested inside those rows bring more again, because each
    /// of them draws a panel of its own.
    ///
    /// A number reached by two paths is listed once. 72 keys are the fully qualified spelling of a
    /// row already listed under a shorter tail at the same value, which is a second way in to one
    /// number rather than a second number; the 21 pairs sharing a tail and disagreeing on the value
    /// are two rows and both stay. The comparison is on the printed value rather than on the shipped
    /// one because that is what a reader would see repeated, and because a rank-scaled row is four
    /// numbers rather than one.
    ///
    /// Export order is kept. It is the order the extractor walked the asset in, which puts a row
    /// beside the rows it works with, and no order worth having can be recovered from the labels.
    /// </summary>
    public IReadOnlyList<TuningRow> Tuning(IReadOnlyDictionary<string, object?>? rows)
    {
        if (rows is not { Count: > 0 }) return [];

        var read = rows
            .Select(row => (Path: Knob(row.Key), Value: Constant(row.Value)))
            .Where(row => row.Path.Length > 0)
            .ToList();

        var listed = new List<TuningRow>(read.Count);

        foreach (var row in read)
        {
            var repeats = read.Any(other =>
                other.Path.Length < row.Path.Length &&
                row.Path.EndsWith("." + other.Path, StringComparison.Ordinal) &&
                other.Value == row.Value);

            if (repeats) continue;

            var label = Spaced(row.Path);

            // Two assets stating a row of the same name at the same setting reduce to one label with
            // neither key a tail of the other, so the test above leaves both and the line is drawn
            // twice. One dictionary in the export is in that state: ability 64 reaches
            // ChannelMultiTargetHeal's tick interval through a boss and through the healer
            // archetype, both at 1, and what a reader would meet is the same sentence twice.
            if (listed.Any(other => other.Label == label && other.Value == row.Value)) continue;

            listed.Add(new TuningRow(label, row.Value, row.Value == Absent));
        }

        return listed;
    }

    /// <summary>
    /// A tuning key as a reader reads it, for the callers holding a key rather than a dictionary:
    /// the group names of <see cref="Entity.Talents"/> are keys of the same kind and are drawn as
    /// headings over the rows they hold.
    /// </summary>
    public string TuningName(string key) => Spaced(Knob(key));

    /// <summary>
    /// A knob as it is drawn: dotted segments read as a path and the underscore that joins two words
    /// read as the space it stands for. Applied to a knob rather than to a key, because taking the
    /// owner off is not idempotent: Knockback_Immune leads its own knob and would be taken for an
    /// owner a second time round.
    /// </summary>
    private static string Spaced(string knob) => Codex.Spaced(knob.Replace('_', ' '));

    /// <summary>
    /// The knob a tuning key names, with the asset that owns it taken off the front and every hero
    /// and stat in it named as the game names them.
    ///
    /// A key is a dotted path whose leading segments are the owning asset's own spelling, so
    /// SG_EchoingSpiritGhost.Explode.Radius is one asset's explosion radius and what a reader came
    /// for is the rest of it. The asset's name is the one part that cannot be drawn, and it is the
    /// one part written with punctuation: 260 of the 264 underscored segments in the export are an
    /// upper case class prefix and its asset, PR_Shark and UB_BossArchbishop and the like. The four
    /// that are not are Fire_Major, Fire_Minor and Knockback_Immune, which are two words joined by
    /// an underscore rather than an asset and which sit past the owner, and
    /// Shared_PolymorphChickenSmall, which is an owner and is dropped for leading its key. So an
    /// asset-shaped segment is dropped wherever it sits and the first segment is dropped for
    /// carrying an underscore at all, which between them leave every one of the 5,249 keys with
    /// something to say and leave no asset name drawn.
    ///
    /// 818 of those keys are one segment and no dots at all, AbilityAddedCritChance and
    /// ActivationDelay and AbsorbIntellectCoefficient, each of them a row named outright with no
    /// owner in front of it. Nothing is taken off them, because the rule takes an owner off rather
    /// than a first segment: a key with nothing but a knob in it is already the knob.
    ///
    /// A hero is renamed rather than dropped. Which hero a row is for is data a reader wants and the
    /// codename is merely how the build spells it, so Firemage reads Ardeos. Dropping a leading one
    /// would also be false as often as it was tidy: CAA_Talent_Ink_PassiveTalent4 reaches
    /// Firemage.FireBombs.ResourcePerOrb, a row that is Ardeos's and not Tariq's.
    /// </summary>
    private string Knob(string key)
    {
        var segments = key.Split('.');
        var kept = new List<string>(segments.Length);

        for (var at = 0; at < segments.Length; at++)
        {
            var segment = segments[at];

            if (NamesAnAsset(segment) || (at == 0 && segment.Contains('_', StringComparison.Ordinal)))
            {
                continue;
            }

            kept.Add(Renamed(segment));
        }

        // Nothing in the export is an asset name and nothing else, so this is a guard rather than a
        // path anything takes. Silence is the answer rather than the key, on the same terms the rest
        // of the catalogue draws nothing rather than an internal spelling: a caller drops the row.
        return kept.Count > 0 ? string.Join('.', kept) : string.Empty;
    }

    /// <summary>
    /// Whether a key segment is the build's spelling of an asset rather than words. Two or more
    /// capitals and then an underscore is how every one of the 260 asset segments in the export is
    /// written, and is how none of the four other underscored segments is.
    /// </summary>
    private static bool NamesAnAsset(string segment)
    {
        var underscore = segment.IndexOf('_');
        if (underscore < 2) return false;

        for (var at = 0; at < underscore; at++)
        {
            if (!char.IsUpper(segment[at])) return false;
        }

        return true;
    }

    /// <summary>
    /// A segment with every hero codename in it swapped for the name the game prints, and every stat
    /// it names printed as the game prints that stat.
    ///
    /// Whole words are matched, split at the capital that starts each one, because a codename is as
    /// often part of a compound as it is a segment of its own: Perk.MaxHealthBasedAbsorb.VigorMosse
    /// reads Vigour Sylvie and MaxDistanceFromMosse reads Max Distance From Sylvie. Of the 3,443
    /// distinct segments the keys and the talent group names are built from, 16 carry a codename as
    /// a word and every one of them means the hero, so the corpus holds no word that merely looks
    /// like one.
    ///
    /// One occurrence is not a word. JS_NPCRime glues the codename to the capital before it, so the
    /// split never offers Rime to the roster and the internal spelling would stand: that segment
    /// names an asset and <see cref="Knob"/> has already dropped it whole before this is asked, and
    /// it is the only segment in the corpus in that state.
    ///
    /// A stat is matched over a run of those words rather than over one of them, because a stat key
    /// is itself written as a compound: CritChance is two words here and IncreasedDotCritChance is
    /// five. A segment that is nothing but a key names the stat outright and prints the shipped word
    /// whatever that word is, which is what puts a tuning row and the Scales off pills three panels
    /// above it in the same vocabulary. A run inside a longer compound goes through
    /// <see cref="stats"/>, which is the narrower reading and says why.
    /// </summary>
    private string Renamed(string segment)
    {
        if (store.StatDisplay(segment)?.DisplayName is { Length: > 0 } named) return named;

        var words = Words(segment);
        var built = new StringBuilder(segment.Length + 8);

        for (var at = 0; at < words.Length; at++)
        {
            string? printed = null;
            var run = words.Length;

            // Longest run first, so AddedWeaponCritChance reads the stat rather than stopping at a
            // shorter key that starts in the same word.
            for (; run > at; run--)
            {
                if (stats.TryGetValue(string.Concat(words[at..run]), out printed)) break;
            }

            if (printed is not null)
            {
                built.Append(printed);
                at = run - 1;
                continue;
            }

            built.Append(heroes.TryGetValue(words[at], out var hero) ? hero : words[at]);
        }

        return built.ToString();
    }

    /// <summary>
    /// The words a segment is written from, which is the same break <see cref="Codex.Spaced"/> draws
    /// when it prints one: a capital that follows a lower case letter or a digit starts a word, and a
    /// run of capitals is one word. Both a codename and a stat key are matched on these boundaries,
    /// so neither can be found inside a word that merely ends in one.
    /// </summary>
    private static string[] Words(string segment)
    {
        var words = new List<string>();
        var start = 0;

        for (var at = 1; at <= segment.Length; at++)
        {
            if (at < segment.Length && !(char.IsUpper(segment[at]) && !char.IsUpper(segment[at - 1]))) continue;

            words.Add(segment[start..at]);
            start = at;
        }

        return [.. words];
    }

    /// <summary>
    /// The name the game prints for each hero codename, which is the only spelling of a hero a
    /// reader ever meets. Read off the roster rather than listed, so a hero the build adds is named
    /// without being named here. Four of the twelve are printed under their codename already.
    /// </summary>
    private readonly IReadOnlyDictionary<string, string> heroes = store.Vocabulary.Heroes
        .ToDictionary(hero => hero.Codename, hero => hero.Name, StringComparer.Ordinal);

    /// <summary>
    /// The word the game prints for a stat, for the stats whose word replaces the key's own spelling
    /// rather than adding to it. This is what a knob's label reads a stat through when the key is
    /// only part of it.
    ///
    /// The test is whether the printed name, with its spaces closed up, still holds the key. 4 of the
    /// 24 shipped rows fail it and are restated: CritChance reads Critical Strike, BonusArmor reads
    /// Armor, FlatHealth reads Health and FlatMana reads Mana. 3 rows pass it, and passing means the
    /// printed name qualifies the key rather than replacing it, which reads as the same word twice
    /// inside a compound already carrying it: Mana reads Max Mana, so ManaCost would print "Max Mana
    /// Cost" and MaxManaFraction "Max Max Mana Fraction", and HealthRegen reads Health Regen Rate, so
    /// HealthRegenRateMultiplier would print "Health Regen Rate Rate Multiplier". The remaining 17
    /// rows print the key's own words, so they read the same either way. A key with no row at all is
    /// left to <see cref="Codex.Spaced"/>, as every other tuning word is.
    ///
    /// Measured over the export that restates 79 of the 5,547 distinct keys and talent group names,
    /// reached 294 times: 76 of them name crit chance and 3 are the bare HealthRegen segment. Not one
    /// of the 9,771 tuning rows the corpus draws prints "Crit Chance" now, and 217 print Critical
    /// Strike.
    ///
    /// No row is lost to it. A label is what <see cref="Tuning"/> drops a duplicate on, so restating
    /// one key in another's spelling could collapse two rows into one, and the corpus was counted for
    /// it: all 4,792 tuning dictionaries in the export, the ones nested inside a granted ability or a
    /// legendary stat line included, list exactly as many rows after this as before.
    ///
    /// What is not reached is the build's own prose. CriticalChanceHasteExpertiseAndSpiritBonus,
    /// CritRatingIncrease and AddedCritStrikeChance are designers writing the stat their own way
    /// rather than naming the key, and they already read as words; matching them would need a list of
    /// spellings written here, which is the one thing this file derives instead.
    /// </summary>
    private readonly IReadOnlyDictionary<string, string> stats = store.Vocabulary.StatCalculation.Attributes
        .Where(row => row.Value.DisplayName is { Length: > 0 } printed &&
            !printed.Replace(" ", string.Empty, StringComparison.Ordinal)
                .Contains(row.Key, StringComparison.OrdinalIgnoreCase))
        .ToDictionary(row => row.Key, row => row.Value.DisplayName!, StringComparer.Ordinal);

    /// <summary>
    /// What a shipped tuning value reads as: one number, the four a rank-scaled row holds, or the
    /// sentence for a row the asset names and the shipped tables do not define.
    /// </summary>
    public static string Constant(object? value) => value switch
    {
        null => Absent,
        JsonElement { ValueKind: JsonValueKind.Array } array =>
            string.Join(" / ", array.EnumerateArray().Select(item => Constant(item))),
        JsonElement { ValueKind: JsonValueKind.Number } number => Number(number.GetDouble()),
        JsonElement { ValueKind: JsonValueKind.Null } => Absent,
        JsonElement element => element.ToString(),
        double number => Number(number),
        _ => value.ToString() ?? string.Empty,
    };

    /// <summary>What a row reads as where the asset names one the shipped tables do not define.</summary>
    private const string Absent = "not in the build";

    /// <summary>
    /// Every substitution an entity's text spends, resolved as far as the export states and numbered
    /// in the order the sentence spends them.
    ///
    /// 207 rows carry substitutions, 373 in all. The number is what a reader can use: a token's own
    /// key is a spelling rather than a label, so entry 1 is simply the first number the description
    /// works out. A token the text never spends sorts last, under its key, which is where the 9 of
    /// the 373 the export carries without spending belong.
    /// </summary>
    public IReadOnlyList<TokenReading> Substitutions(Entity entity)
    {
        var read = new List<TokenReading>();

        foreach (var token in Spent(Spends(entity), entity.Placeholders))
        {
            var (reading, resolved) = Reads(token);

            read.Add(new TokenReading(read.Count + 1, reading, resolved, Working(token), Tuning(token.Constants)));
        }

        return read;
    }

    /// <summary>
    /// A row's substitutions in the order its text spends them, which is the order every surface
    /// numbering them numbers by.
    ///
    /// Derived here rather than at each caller because the ordinal is the whole of a token's public
    /// identity: the key it is filed under is a spelling, so an entry is tied to the number in the
    /// sentence by its position and nothing else, and the entry page's panel and the record a caller
    /// fetches have to be counting the same way for the tie to hold.
    /// </summary>
    public IEnumerable<Placeholder> Spent(string text, IReadOnlyDictionary<string, Placeholder>? tokens) =>
        tokens is not { Count: > 0 }
            ? []
            : tokens
                .OrderBy(spent => Spends(text, spent.Key))
                .ThenBy(spent => spent.Key, StringComparer.Ordinal)
                .Select(spent => spent.Value);

    /// <summary>
    /// Everything of a row a substitution can be spent in, as one string. A detail line spends
    /// tokens as readily as the description does, and a token spent only there sorts by where it
    /// falls rather than after everything.
    /// </summary>
    public static string Spends(Entity entity) =>
        string.Join('\n', new[] { entity.Description }.Concat(entity.Detail ?? []).OfType<string>());

    /// <summary>
    /// Where a description first spends a token, or past the end of it where the text never does.
    /// Matched on the same terms the renderer substitutes on, so the order a reader is given is the
    /// order the numbers actually appear in.
    /// </summary>
    private static int Spends(string text, string name)
    {
        var at = text.IndexOf($"{{{name}}}", StringComparison.Ordinal);

        return at < 0 ? int.MaxValue : at;
    }

    /// <summary>
    /// What one token works out to, and whether the build settles it at all.
    ///
    /// The coefficient reading is the export's own and is what the game itself shows before a
    /// character is attached to the tooltip. 36 of the 164 coefficients a row of the export carries
    /// name the stand-in rather than a stat, which is not an attribute and has no printed word: the
    /// hero fills that slot, so the sentence says so instead of running a placeholder through the
    /// attribute table. One coefficient names no attribute at all, which is the sentence above it.
    /// </summary>
    private (string Reading, bool Resolved) Reads(Placeholder token)
    {
        if (token.Text is { } word) return (word, true);

        if (token.Coefficient is { } coefficient)
        {
            // Read through decimal for the reason the resolver does: 48 of the shipped coefficients
            // are values a double cannot hold exactly, and taking one to a percentage as a double
            // prints digits the build never contained.
            var share = ((decimal)coefficient * 100m).Normalize().ToString(CultureInfo.InvariantCulture);

            var of = token.Attribute switch
            {
                null => "the attribute the row captures",
                Relative => "whichever primary stat the hero uses",
                { } attribute => store.StatName(attribute),
            };

            return ($"{share}% of {of}", true);
        }

        if (token.Value is { } value)
        {
            // A share is carried as the fraction and shown as the percentage, which is the reading
            // the resolver gives it: {VoidTouchAccum} is 0.1 in the export and the game's own
            // sentence says 10% of all damage you deal. Printing the fraction with a sign on it
            // would be a hundredth of the number the player is shown.
            if (token.Percent == true || token.Format == Share)
            {
                return (((decimal)value * 100m).Normalize().ToString(CultureInfo.InvariantCulture) + "%", true);
            }

            var number = Number(value);

            return (token.Unit is { } unit ? $"{number} {unit}" : number, true);
        }

        // Through the resolver's own reading of an unsettled token, so the sentence under the entry
        // and the marker standing in the text above it are the same sentence rather than two.
        var unresolved = Placeholders.Unresolved(token);

        return (unresolved.Title ?? unresolved.Text, false);
    }

    /// <summary>
    /// The attribute name that stands in for whichever stat the wearer's hero uses. Spelled here as
    /// well as in <see cref="Placeholders"/> because this reads the field the resolver resolves: a
    /// value it treats as a placeholder is one this must not print as a stat.
    /// </summary>
    private const string Relative = "PrimaryAttribute";

    /// <summary>
    /// The arithmetic around a token's number, as one line: the band it rolls in, what its format
    /// does to it, whether it speaks its own damage school, and the attributes it grows with.
    ///
    /// The band is stated as the range rather than as the spread, because the spread is the total
    /// width of the band and half of it falls either side of the mean, which is the reading a
    /// number on its own invites getting wrong. A spread of nought is a band of nothing and says the
    /// number is exact, which is a different fact from the field being absent; one shipped token
    /// says it.
    /// </summary>
    private string Working(Placeholder token)
    {
        var parts = new List<string>(4);

        if (token.Spread is { } spread)
        {
            parts.Add(spread == 0
                ? "rolls exactly, with no band"
                : $"rolls between {Number(100 - spread * 50)}% and {Number(100 + spread * 50)}% of the mean");
        }

        if (token.Format is { } format) parts.Add(Printed(format));

        // The percent flag is already spoken by the sign on a token that prints a number outright,
        // so it is said here only for the 9 that carry the flag with no number to put it on.
        if (token.Value is null && token.Percent == true) parts.Add("shown as a share");

        if (token.ShowDamageType == true) parts.Add("speaks its damage school");

        // Read through the catalogue's own reading of a scaling list, so the words here are the
        // words the pills above the panel carry for the same attributes.
        if (codex.Scaling(token.ScalesOff).ToArray() is { Length: > 0 } scaling)
        {
            parts.Add("scales off " + string.Join(", ", scaling));
        }

        return string.Join(" · ", parts);
    }

    /// <summary>
    /// What a token's print format does to its number, in words. The three the export carries are
    /// all the build names: number on 52 tokens, percent on 33 and delta on 25.
    /// </summary>
    private static string Printed(string format) => format switch
    {
        Plain => "printed as it stands",
        Share => "printed as a share",
        Excess => "printed as its excess over 1",
        _ => $"printed as {Codex.Spaced(format)}",
    };

    /// <summary>
    /// The three shipped print formats a gem or set bonus argument names. Spelled out because two of
    /// them change the number rather than only describing it, and <see cref="Reads"/> has to act on
    /// the same word this sentence is written from.
    /// </summary>
    private const string Plain = "number";

    private const string Share = "percent";

    private const string Excess = "delta";
}

/// <summary>
/// One shipped tuning number as a reader meets it: what it tunes, in words, and what it is set to.
///
/// The row's shipped name is not carried. Two rows are told apart by their labels, which are derived
/// to differ wherever the rows do: nothing is taken off a key but the spelling of the asset that
/// owns it.
/// </summary>
public sealed record TuningRow(string Label, string Value, bool Absent);

/// <summary>
/// One substitution of a description as a reader meets it, numbered rather than named.
///
/// The token's own key is not carried. 221 of the 237 distinct keys in the export fail the test
/// <see cref="PublicTypes.DisplayNameOf"/> puts a shipped name to, and read as StrCoeff50 and
/// HemoStrikeTickDmg; the 16 that pass are CDR, amount, movespeed, percentage and twelve more. So
/// the key is a spelling and the ordinal is what ties an entry to the sentence it is spent in.
/// </summary>
public sealed record TokenReading(
    int Ordinal, string Reading, bool Resolved, string Working, IReadOnlyList<TuningRow> Tuning);
