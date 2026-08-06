using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Azure.Storage.Blobs;
using Fellowship.SDK.Documents;
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

    /// <summary>
    /// The colour a row's name is drawn in: a gem tier's or an item's rarity, failing that a gem
    /// type's own colour, failing that the hero it is for where it is for exactly one, and failing
    /// all three the ordinary text colour, muted for a row printing its noun and id rather than a
    /// name of its own.
    ///
    /// The rarity and gem lookups read the enum member's own name, which is the ladder's filed word
    /// rather than the word a page prints: <see cref="DesignTokens.Rarity"/> and
    /// <see cref="DesignTokens.Gem"/> both key their palette by that filed spelling, on the same
    /// terms <see cref="Codex"/>'s own facet colours do.
    /// </summary>
    public string NameColour(EntityDocument document)
    {
        Rarity? rarity = document switch
        {
            Item { Rarity: { } value } => value,
            GemTier tier => tier.Rarity,
            _ => null,
        };

        if (rarity is { } rung && tokens.Rarity(rung.ToString()) is { } shade) return Readable(shade)!;

        GemTypeName? gem = document switch
        {
            GemType type => type.Gem,
            GemTier tier => tier.Gem,
            GemTrait trait => trait.Gem,
            _ => null,
        };

        if (gem is { } stone && tokens.Gem(stone.ToString()) is { } colour) return Readable(colour)!;

        var tint = document switch
        {
            Item { Heroes: [{ } hero] } => HeroTint(hero),
            TalentSlot slot => HeroTint(slot.Hero),
            _ => null,
        };

        if (tint is not null) return Readable(tint)!;

        // The muted tone is what a row named only by its noun and id is drawn in, so it follows the
        // same reading of "named" the heading does: a row whose shipped name is an asset path prints
        // as "Effect 2812" and is drawn as the unnamed row it now is.
        return PublicTypes.DisplayNameOf(document) is null
            ? tokens.Color("core.text-secondary") ?? "#909BA6"
            : tokens.Color("core.text-primary") ?? "#EAE3DB";
    }

    private string? HeroTint(Hero hero) =>
        store.Hero(hero.ToString())?.Codename is { } codename ? tokens.Hero(codename) : null;

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

    public string Name(EntityDocument document) => PublicTypes.NameOf(document);

    /// <summary>
    /// The public name of a row known only by its address, for the <see cref="EntityReference"/>s a
    /// name or an icon is borrowed from: the extractor never writes a reference's own
    /// <see cref="EntityReference.Name"/>, so the row itself is read off the store and named as it is
    /// named everywhere else.
    ///
    /// Every reference the export carries addresses a real row - measured over the corpus, all 2,570
    /// of the borrowed-name and borrowed-art references on the 3,261 effects resolve - so the first
    /// reading is taken outright rather than defended against an address nothing in the build writes.
    /// </summary>
    public string Name(EntityType type, int id) => PublicTypes.NameOf(store.All(type, id)[0]);

    public string Href(EntityDocument document) => Href(EntityTypes.TypeOf(document), document.Id);

    /// <summary>A row's page, under the slug its type is addressed by.</summary>
    public string Href(EntityType type, int id) =>
        $"/entity/{EntityTypes.Slug(type)}/{id.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>The tooltip address of a row, under the same slug and on the same terms.</summary>
    public string Tip(EntityDocument document)
    {
        var where = $"/api/tooltip/{EntityTypes.Slug(EntityTypes.TypeOf(document))}/" +
            document.Id.ToString(CultureInfo.InvariantCulture);

        return Reading(document) is { } hero ? $"{where}?hero={Uri.EscapeDataString(hero)}" : where;
    }

    /// <summary>
    /// The hero this reading is drawn for, on the ids that read differently per hero, and null on
    /// the ids one address answers for outright.
    ///
    /// Asked of <see cref="EntityStore.Wearer"/> rather than worked out here, because the answer is
    /// what the tooltip endpoint will resolve the same address to and two readings of that question
    /// is how the two surfaces came to disagree.
    /// </summary>
    public string? Reading(EntityDocument document) =>
        store.All(EntityTypes.TypeOf(document), document.Id).Count <= 1 ? null : store.Wearer(document)?.Name;

    public MarkupString Text(string? source, IReadOnlyList<Placeholder>? tokens, IReadOnlyList<string>? tags)
    {
        if (source is null) return default;

        return new MarkupString(richText.Render(source, Resolved(tokens, tags)));
    }

    /// <summary>
    /// The same sentence with the same numbers in it and no markup, for the surfaces answering with
    /// data rather than with a page. Resolved through the one path the page resolves through, so a
    /// record and the page over it cannot say different things about one row.
    /// </summary>
    public string? PlainText(string? source, IReadOnlyList<Placeholder>? tokens, IReadOnlyList<string>? tags) =>
        source is null ? null : richText.Plain(source, Resolved(tokens, tags));

    /// <summary>
    /// A row's tokens as far as the build alone settles them, which is the reading with no caller
    /// attached: no hero, no item level and no stat totals, so a coefficient stays the share it is
    /// rather than becoming a number for somebody.
    ///
    /// <see cref="Placeholders.Resolve"/> still reads a key-to-token map, because the renderer finds a
    /// substitution by the name between a sentence's own <c>{Token}</c> braces; the document carries
    /// the list instead, each entry naming its own key, so the map is built here rather than kept.
    /// </summary>
    private IReadOnlyDictionary<string, Substitution>? Resolved(
        IReadOnlyList<Placeholder>? tokens, IReadOnlyList<string>? tags) =>
        tokens is not { Count: > 0 } ? null : placeholders.Resolve(Keyed(tokens), new TooltipQuery(), null, tags);

    /// <summary>
    /// A row's tokens keyed by their own name, for <see cref="Placeholders.Resolve"/>. Built with a
    /// loop rather than <c>ToDictionary</c> so a key one row spent twice, if the build ever ships one,
    /// overwrites rather than throws: measured over the export, no document's own list repeats a key
    /// today, but nothing here depends on that staying true.
    /// </summary>
    private static IReadOnlyDictionary<string, Placeholder> Keyed(IReadOnlyList<Placeholder> tokens)
    {
        var keyed = new Dictionary<string, Placeholder>(tokens.Count, StringComparer.Ordinal);
        foreach (var token in tokens)
        {
            if (token.Key is { } key) keyed[key] = token;
        }

        return keyed;
    }

    /// <summary>
    /// A row's own placeholder list, already ordered by the text that spends it, read off whichever
    /// of the seven document types carrying one actually declares this row's field. Public so a
    /// caller holding an arbitrary <see cref="EntityDocument"/> - a results row, a nested grant - has
    /// one fan-out to read rather than a copy of this switch of its own.
    /// </summary>
    public static IReadOnlyList<Placeholder> PlaceholdersOf(EntityDocument document) => document switch
    {
        Ability ability => ability.Placeholders ?? [],
        Effect effect => effect.Placeholders ?? [],
        Finesse finesse => finesse.Placeholders ?? [],
        GemTrait trait => trait.Placeholders ?? [],
        Talent talent => talent.Placeholders ?? [],
        TraitPower power => power.Placeholders ?? [],
        WeaponPower power => power.Placeholders ?? [],
        _ => [],
    };

    /// <summary>
    /// What one substitution prints as on those same terms, which is the string a caller will find
    /// standing in the text where the token was.
    /// </summary>
    public string Reading(Placeholder token, IReadOnlyList<string>? tags) =>
        placeholders.Reading(token, new TooltipQuery(), null, tags).Text;

    public string RichTextCss(string scope) => richText.Css(scope);

    public static string Number(double value) => Codex.Number(value);

    /// <summary>
    /// Every scalar fact one row states, in the order its own document class declares them, each
    /// already formatted the way a reader would read it.
    ///
    /// Reflected off the document's own runtime type rather than off one shared shape: a document
    /// carries no field its type never fills, so an <see cref="Item"/> and an <see cref="Ability"/>
    /// state entirely different facts here and neither is padded with the other's nulls.
    /// </summary>
    public IReadOnlyList<(string Label, string Value, string? Unit, string? Href, string? Tip)> Facts(
        EntityDocument document)
    {
        var facts = new List<(string, string, string?, string?, string?)>();

        foreach (var property in Scalars(document))
        {
            if (Drawn.Contains(property.Name) || Hidden(document, property.Name)) continue;

            var value = property.GetValue(document);
            if (value is null) continue;

            var text = value switch
            {
                bool flag => flag ? "Yes" : "No",
                double number => Number(number),
                int number => number.ToString(CultureInfo.InvariantCulture),
                _ => value.ToString() ?? string.Empty,
            };

            if (text.Length == 0) continue;

            var points = Points(document, property.Name, value);

            facts.Add((Heading(document, property.Name), text, Unit(property.Name), points?.Href, points?.Tip));
        }

        return facts;
    }

    /// <summary>
    /// Where a numeric fact leads, on the four fields whose number is another row's id rather than a
    /// quantity: a trait's or a set bonus's power, a talent slot's talent, and the weaker gem node a
    /// ladder's upper half replaces.
    /// </summary>
    private (string Href, string? Tip)? Points(EntityDocument document, string property, object value) =>
        document switch
        {
            Trait when property == nameof(Trait.PowerId) && value is int id => Addresses(EntityType.TraitPower, id),
            SetBonus when property == nameof(SetBonus.PowerId) && value is int id =>
                Addresses(EntityType.SetBonusPower, id),
            TalentSlot when property == nameof(TalentSlot.TalentId) && value is int id =>
                Addresses(EntityType.Talent, id),
            GemTrait when property == nameof(GemTrait.ReplacesId) && value is int id =>
                Addresses(EntityType.GemTrait, id),
            _ => null,
        };

    /// <summary>Where a pointer to another row leads, and that row's own tooltip address beside it.</summary>
    public (string Href, string? Tip)? Addresses(EntityType type, int id) =>
        (Href(type, id), store.All(type, id) is { Count: > 0 } readings ? Tip(readings[0]) : null);

    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> ScalarsByType = new();

    /// <summary>
    /// A document's own scalar properties, cached by its runtime type so the reflection runs once per
    /// document class rather than once per row.
    /// </summary>
    private static PropertyInfo[] Scalars(EntityDocument document) =>
        ScalarsByType.GetOrAdd(document.GetType(), type =>
        [
            .. type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => IsScalar(Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType)),
        ]);

    private static bool IsScalar(Type type) =>
        type == typeof(string) || type.IsPrimitive || type == typeof(decimal) || type.IsEnum;

    /// <summary>
    /// Fields the page has a section of its own for, so a fact would repeat them: every document's
    /// own <see cref="EntityDocument.Id"/> and <see cref="EntityDocument.Name"/>, and the
    /// description and the icon wherever a document class states them.
    ///
    /// <see cref="GemType.OverflowDescription"/> belongs here for a different reason than
    /// <see cref="Ability.Description"/> does: it is not a field the layout moved but the shipped
    /// sentence itself, {Token} braces and style tags intact, the same string
    /// <see cref="PublicEntity.Description"/>'s own remarks say must never be handed over. It is
    /// drawn through <see cref="PublicEntity.GemOverflow"/>'s resolved reading instead, so a fact row
    /// would only ever repeat it in the one spelling a reader must not see.
    /// </summary>
    private static readonly HashSet<string> Drawn = new(StringComparer.Ordinal)
    {
        nameof(EntityDocument.Id), nameof(EntityDocument.Name),
        nameof(Ability.Description), nameof(Item.Icon), nameof(GemType.OverflowDescription),
    };

    /// <summary>
    /// Fields whose value is a shipped spelling or a ladder's filed word rather than the word a page
    /// prints. <see cref="GemType.Attribute"/> is the root's own name run together with Power on all
    /// 6 gem roots, Amethyst holding AmethystPower; <see cref="Item.Rarity"/> and
    /// <see cref="GemTier.Rarity"/> are enum members named for the rung's filed word, which the
    /// ladder joins by ordinal to two different printed words rather than stating one itself.
    /// </summary>
    private static bool Hidden(EntityDocument document, string property) => document switch
    {
        GemType => property == nameof(GemType.Attribute),
        Item => property == nameof(Item.Rarity),
        GemTier => property == nameof(GemTier.Rarity),
        _ => false,
    };

    /// <summary>
    /// What a fact is headed with: the type a pointer leads to, for the four fields <see cref="Points"/>
    /// addresses, and the field's own name spaced out for the rest.
    /// </summary>
    private static string Heading(EntityDocument document, string property) => document switch
    {
        Trait when property == nameof(Trait.PowerId) => PublicTypes.Label(EntityType.TraitPower),
        SetBonus when property == nameof(SetBonus.PowerId) => PublicTypes.Label(EntityType.SetBonusPower),
        TalentSlot when property == nameof(TalentSlot.TalentId) => PublicTypes.Label(EntityType.Talent),
        GemTrait when property == nameof(GemTrait.ReplacesId) => PublicTypes.Label(EntityType.GemTrait),
        _ => Codex.Spaced(property),
    };

    private static string? Unit(string property) => property switch
    {
        nameof(Ability.CastTime) or nameof(Ability.ChannelTime) or nameof(Ability.ChannelTick)
            or nameof(Ability.Cooldown) or nameof(Ability.Duration) or nameof(Ability.Period) => "s",
        _ => null,
    };

    /// <summary>
    /// The word a rarity rung prints for one row: the item column of the ladder for an
    /// <see cref="Item"/>, the gem column for a <see cref="GemTier"/>, joined on the tier ordinal the
    /// <see cref="Rarity"/> enum's own members carry as their value. Null on every other document type
    /// and on the rows of either type that carry no rarity at all.
    /// </summary>
    public string? Rarity(EntityDocument document)
    {
        Rarity? rung = document switch
        {
            Item { Rarity: { } value } => value,
            GemTier tier => tier.Rarity,
            _ => null,
        };

        if (rung is not { } tier2) return null;

        var entry = store.Vocabulary.Rarities.FirstOrDefault(r => r.Tier == (int)tier2);

        return entry is null ? tier2.ToString() : document is GemTier ? entry.GemDisplayName : entry.DisplayName;
    }

    /// <summary>
    /// Every tuning number a dictionary of shipped rows holds, under the words it tunes.
    ///
    /// One field carries this shape today: the rows a <see cref="Placeholder"/> works its number out
    /// from.
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
    /// the group names of a talent's tuning are keys of the same kind and are drawn as headings over
    /// the rows they hold.
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
    /// one part written with punctuation: an upper case class prefix and its asset, PR_Shark and
    /// UB_BossArchbishop and the like. Fire_Major, Fire_Minor and Knockback_Immune are two words
    /// joined by an underscore rather than an asset and sit past the owner, and
    /// Shared_PolymorphChickenSmall is an owner and is dropped for leading its key. So an
    /// asset-shaped segment is dropped wherever it sits and the first segment is dropped for
    /// carrying an underscore at all, which between them leave every keyed row with something to say
    /// and leave no asset name drawn.
    ///
    /// A key with one segment and no dots at all - AbilityAddedCritChance, ActivationDelay,
    /// AbsorbIntellectCoefficient - is a row named outright with no owner in front of it. Nothing is
    /// taken off them, because the rule takes an owner off rather than a first segment: a key with
    /// nothing but a knob in it is already the knob.
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
    /// capitals and then an underscore is how every asset segment the export carries is written, and
    /// is how none of the underscored segments that are not one is written.
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
    /// reads Vigour Sylvie and MaxDistanceFromMosse reads Max Distance From Sylvie. Every segment
    /// carrying a codename as a whole word means the hero, so the corpus holds no word that merely
    /// looks like one.
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
    /// in the order the document's own <see cref="Placeholders"/> list already gives them - the order
    /// the sentence spends them, worked out once at extraction rather than derived again here.
    /// </summary>
    public IReadOnlyList<TokenReading> Substitutions(EntityDocument document)
    {
        var tokens = PlaceholdersOf(document);
        var read = new List<TokenReading>(tokens.Count);

        foreach (var token in tokens)
        {
            var (reading, resolved) = Reads(token);

            read.Add(new TokenReading(read.Count + 1, reading, resolved, Working(token), Tuning(token.Constants)));
        }

        return read;
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
    /// all the build names: number, percent and delta.
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
/// The token's own key is not carried. Most of the distinct keys the export carries fail the test
/// <see cref="PublicTypes.DisplayNameOf"/> puts a shipped name to, and read as StrCoeff50 and
/// HemoStrikeTickDmg; a few pass, such as CDR, amount, movespeed and percentage. So the key is a
/// spelling and the ordinal is what ties an entry to the sentence it is spent in.
/// </summary>
public sealed record TokenReading(
    int Ordinal, string Reading, bool Resolved, string Working, IReadOnlyList<TuningRow> Tuning);
