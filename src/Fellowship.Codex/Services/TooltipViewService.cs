using System.Globalization;

using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;

using Microsoft.AspNetCore.Components;

using Fellowship.Codex.Models;

namespace Fellowship.Codex.Services;

public interface ITooltipViewService
{
    IChromeAssetService Chrome { get; }

    BlessingEntry Blessings { get; }

    RarityEntry? Rarity(EntityDocument document, TooltipQuery query);

    string Variables(RarityEntry? rarity);

    string? Texture(EntityDocument document, RarityEntry? rarity);

    string? Ornament(RarityEntry? rarity);

    string RarityName(EntityDocument document, RarityEntry rarity);

    int? Level(EntityDocument document, TooltipQuery query);

    string? Temper(EntityDocument document, TooltipQuery query, RarityEntry? rarity, int? level);

    IReadOnlyList<TooltipView.Fact> Facts(EntityDocument document, TooltipQuery query);

    IReadOnlyList<TooltipView.Fact> Facts(
        EntityDocument document, TooltipQuery query, TooltipView.Overview overview);

    TooltipView.Overview Overview(EntityDocument document, TooltipQuery query);

    string? Potential(EntityDocument document, TooltipQuery query, RarityEntry? rarity, int? level);

    IReadOnlyList<TooltipView.ModifierLine> Modifiers(
        ItemDocument document, TooltipQuery query, RarityEntry? rarity);

    IReadOnlyList<StatLine> Rolls(ItemDocument document, TooltipQuery query, RarityEntry? rarity);

    MarkupString Text(
        string source,
        IReadOnlyList<Placeholder>? substitutions,
        TooltipQuery query,
        HeroEntry? hero,
        IReadOnlyList<string>? tags);

    string Style();

    string ModalStyle();

    bool DrawsState(EntityDocument document);

    EntityDocument? Granted(EntityDocument document);

    IReadOnlyList<EntityDocument> LegendaryStats(EntityDocument document);
}

public sealed class TooltipViewService(
    IEntityStoreService store,
    IDesignTokenService tokens,
    IRichTextService renderer,
    IPlaceholderService placeholders,
    IChromeAssetService chrome,
    IWebHostEnvironment environment) : ITooltipViewService
{
    private const string Sheet = "css/tooltip.css";

    private const string ModalSheet = "css/modal.css";

    private readonly string sheet = Compose(environment, chrome, renderer);

    private readonly string modal = Read(environment, ModalSheet, "Styles/modal.scss", "Components/Tooltip/_Modal.scss");

    public IChromeAssetService Chrome => chrome;

    public BlessingEntry Blessings => store.Settings.Blessings;

    public RarityEntry? Rarity(EntityDocument document, TooltipQuery query)
    {
        var tier = document switch
        {
            ItemDocument { Rarity: { } only } => (int?)only,
            ItemDocument { Rarities: { Count: > 0 } rarities } => (int)rarities.Max(),
            CurrencyDocument { Rarity: { } coin } => (int)coin,
            GemDocument gem => (int)gem.Rarity,
            _ => null,
        };

        return store.Rarity(query.Rarity, document is GemDocument)
            ?? (tier is { } t ? store.Settings.Rarities.FirstOrDefault(r => r.Tier == t) : null);
    }

    public string Variables(RarityEntry? rarity)
    {
        var colour = rarity is null ? null : tokens.Rarity(rarity.Name);

        return
            tokens.CssVariables() +
            "--fx-frame:" + (chrome.Frame(rarity?.Name) is { } frame ? $"url({frame})" : "none") +
            ";--fx-header:" + (rarity is null ? "none" : $"url({chrome.Header(rarity.Name)})") +
            ";--fx-window:url(" + (rarity is null ? chrome.RoundedWindow : chrome.Window) +
            ");--fx-title:" + (colour ?? "var(--core-text-primary)") +
            ";";
    }

    public string? Texture(EntityDocument document, RarityEntry? rarity)
    {
        var icon = rarity is not null &&
                   document is ItemDocument { IconByRarity: { } byRarity } &&
                   byRarity.GetValueOrDefault(rarity.Name) is { } texture
            ? texture
            : DocumentFields.Text(document, nameof(ItemDocument.Icon));

        return chrome.Texture(icon);
    }

    public string? Ornament(RarityEntry? rarity) => rarity is null ? null : chrome.Ornament(rarity.Name);

    public string RarityName(EntityDocument document, RarityEntry rarity) =>
        document is GemDocument ? rarity.GemDisplayName : rarity.DisplayName;

    public int? Level(EntityDocument document, TooltipQuery query) => document switch
    {
        ItemDocument { Stats: { Count: > 0 } } => query.ItemLevel ?? store.DropItemLevel(document),
        ItemDocument => query.ItemLevel,
        _ => null,
    };

    public string? Temper(EntityDocument document, TooltipQuery query, RarityEntry? rarity, int? level)
    {
        if (document is not ItemDocument) return null;

        if (query.Upgrades is not { } upgrades) return null;

        var ceiling = query.MaxUpgrades
            ?? Tempering.Ceiling(store.Settings.MaxTempers, rarity?.Name, level);

        return ceiling is { } max
            ? $"{upgrades.ToString(CultureInfo.InvariantCulture)} / {max.ToString(CultureInfo.InvariantCulture)}"
            : upgrades.ToString(CultureInfo.InvariantCulture);
    }

    public IReadOnlyList<TooltipView.Fact> Facts(EntityDocument document, TooltipQuery query) =>
        Facts(document, query, overviewed: false);

    public IReadOnlyList<TooltipView.Fact> Facts(
        EntityDocument document, TooltipQuery query, TooltipView.Overview overview) =>
        Facts(document, query, overviewed: overview.Stated);

    private IReadOnlyList<TooltipView.Fact> Facts(
        EntityDocument document, TooltipQuery query, bool overviewed)
    {
        var facts = new List<TooltipView.Fact>();

        void Add(string label, double? value, string? unit = null, string? title = null)
        {
            if (value is not { } number) return;

            facts.Add(new TooltipView.Fact(label, unit is null ? Number(number) : Number(number) + unit, title));
        }

        var (cast, hastened) = Hastened(document, DocumentFields.Number(document, nameof(AbilityDocument.CastTime)), query);

        if (!overviewed)
        {
            Add("Cast", cast, "s", hastened);
            Add("Cooldown", DocumentFields.Number(document, nameof(AbilityDocument.Cooldown)), "s");
            Add("Range", DocumentFields.Number(document, nameof(AbilityDocument.Range)));
        }

        Add("Channel", DocumentFields.Number(document, nameof(AbilityDocument.ChannelTime)), "s");
        Add("Charges", DocumentFields.Number(document, nameof(AbilityDocument.Charges)));
        Add("Radius", DocumentFields.Number(document, nameof(AbilityDocument.Radius)));
        Add("Duration", DocumentFields.Number(document, nameof(AbilityDocument.Duration)), "s");

        if (!overviewed && DocumentFields.Number(document, nameof(AbilityDocument.Cost)) is { } cost)
        {
            var resource = DocumentFields.Text(document, nameof(AbilityDocument.Resource));

            facts.Add(new TooltipView.Fact(
                "Cost", resource is { } spent ? $"{Number(cost)} {spent}" : Number(cost), null));
        }

        foreach (var gain in FellowshipCodex.Generates(document))
        {
            facts.Add(new TooltipView.Fact("Generates", Generated(gain), gain.Source));
        }

        facts.AddRange(Menacing(document, query));

        return facts;
    }

    private static string Generated(ResourceGain gain)
    {
        var pool = gain.ResourceName ?? gain.Resource;

        var magnitude = gain.Amount is { } amount
            ? gain.AmountOnCritical is { } critical
                ? $"{Number(amount)} or {Number(critical)} on a critical strike "
                : $"{Number(amount)} "
            : gain.ShareOfMaximum is { } share
                ? $"{Number(share * 100)}% of maximum "
                : string.Empty;

        var trigger = gain.Per is { } per ? $" per {FellowshipCodex.Spaced(per).ToLowerInvariant()}" : string.Empty;
        var chance = gain.Chance is { } odds ? $", {Number(odds * 100)}% of the time" : string.Empty;

        return $"{magnitude}{pool}{trigger}{chance}";
    }

    public TooltipView.Overview Overview(EntityDocument document, TooltipQuery query)
    {
        if (document is not AbilityDocument) return TooltipView.Overview.None;

        var casting = new List<TooltipView.Reading>(2);
        var spending = new List<TooltipView.Reading>(2);
        var text = TooltipView.AbilityText(store.Settings);

        var (cast, hastened) = Hastened(
            document, DocumentFields.Number(document, nameof(AbilityDocument.CastTime)), query);

        casting.Add(cast is { } seconds
            ? new TooltipView.Reading(Number(seconds), text.CastUnit, text.Cast) { Title = hastened }
            : new TooltipView.Reading(null, null, text.Instant));

        if (DocumentFields.Number(document, nameof(AbilityDocument.Range)) is { } range)
        {
            var title = $"{Number(range)} unreal units, at " +
                $"{text.UnitsPerYard.ToString(CultureInfo.InvariantCulture)} to the yard";

            casting.Add(Distance.Self(range)
                ? new TooltipView.Reading(null, null, text.Self)
                : Distance.Melee(range, text)
                    ? new TooltipView.Reading(null, null, text.MeleeRange) { Title = title }
                    : new TooltipView.Reading(
                        Distance.Yards(range, text).Normalize().ToString(CultureInfo.InvariantCulture),
                        text.RangeUnit,
                        text.Range) { Title = title });
        }

        if (DocumentFields.Number(document, nameof(AbilityDocument.Cost)) is { } cost)
        {
            spending.Add(new TooltipView.Reading(
                Number(cost), null, DocumentFields.Text(document, nameof(AbilityDocument.Resource)))
            {
                Spent = true,
            });
        }

        if (DocumentFields.Number(document, nameof(AbilityDocument.Cooldown)) is { } cooldown)
        {
            spending.Add(new TooltipView.Reading(Number(cooldown), text.CooldownUnit, text.Cooldown));
        }

        return new TooltipView.Overview(casting, spending);
    }

    private static string Number(double value) =>
        Math.Round((decimal)value, 2).Normalize().ToString(CultureInfo.InvariantCulture);

    private IEnumerable<TooltipView.Fact> Menacing(EntityDocument document, TooltipQuery query)
    {
        if (store.Menacing(document) is not { } creature) yield break;

        var step = Difficulties.At(store.Settings, store.Difficulty(query.Difficulty?.ToString()));

        if (step is null) yield break;

        if (document is CreatureDocument &&
            Difficulties.Health(creature, store.Settings, step.Difficulty) is { } health)
        {
            yield return new TooltipView.Fact(
                "Health",
                FellowshipCodex.Magnitude((decimal)health),
                $"{FellowshipCodex.Number(creature.Health!.Value)} at the first difficulty, at {step.Label}");
        }

        if (document is not AbilityDocument { Coefficients: { Count: > 0 } stated }) yield break;

        foreach (var coefficient in stated)
        {
            if (Difficulties.Damage(coefficient, creature, store.Settings, step.Difficulty) is not { } band)
            {
                continue;
            }

            var label = coefficient.Part is { Length: > 0 } part ? $"Damage ({part})" : "Damage";

            var reading = band.Low == band.High
                ? FellowshipCodex.Magnitude(band.Mean)
                : $"{FellowshipCodex.Magnitude(band.Low)} to {FellowshipCodex.Magnitude(band.High)}";

            yield return new TooltipView.Fact(
                label,
                reading,
                $"{FellowshipCodex.Number(coefficient.Value * 100)}% of {coefficient.Attribute} at {step.Label}");
        }
    }

    private static (double? Value, string? Title) Hastened(EntityDocument document, double? value, TooltipQuery query)
    {
        if (value is not { } seconds || CastTimeScalesWithHaste(document) is not true) return (value, null);
        if (query.Haste is not { } haste || haste <= 0) return (value, null);

        var text = (haste * 100m).Normalize().ToString(CultureInfo.InvariantCulture);

        return (
            (double)((decimal)seconds / (1m + haste)),
            $"{seconds.ToString(CultureInfo.InvariantCulture)}s at no haste, " +
            $"divided by {(1m + haste).Normalize().ToString(CultureInfo.InvariantCulture)} for {text}% haste");
    }

    private static bool? CastTimeScalesWithHaste(EntityDocument document) => document switch
    {
        AbilityDocument ability => ability.CastTimeScalesWithHaste,
        RelicPowerDocument relic => relic.CastTimeScalesWithHaste,
        WeaponPowerDocument weapon => weapon.CastTimeScalesWithHaste,
        _ => null,
    };

    public string? Potential(EntityDocument document, TooltipQuery query, RarityEntry? rarity, int? level)
    {
        var power = query.PotentialPower
            ?? (document is ItemDocument item
                ? ItemPower.Of(item, store.Settings, rarity, query, level)
                : null);

        return power?.Normalize().ToString(CultureInfo.InvariantCulture);
    }

    public IReadOnlyList<TooltipView.ModifierLine> Modifiers(
        ItemDocument document, TooltipQuery query, RarityEntry? rarity)
    {
        var lines = new List<TooltipView.ModifierLine>(query.Modifiers.Count);

        var wearer = TooltipView.Wearer(document, query);
        var level = query.ItemLevel ?? store.DropItemLevel(document);

        foreach (var modifier in query.Modifiers)
        {
            var type = TooltipView.TypeOf(modifier.Kind, store.Settings);
            var row = type is TooltipView.TraitType or EntityType.Enchant
                ? TooltipView.ItemTrait(store, modifier.Id, wearer)
                : store.All(type, modifier.Id).FirstOrDefault();

            lines.Add(new TooltipView.ModifierLine(
                modifier.Kind,
                ItemCraft.Defining(modifier.Kind, row, store.Settings),
                TooltipView.Header(modifier.Kind, store.Settings),
                row is null ? modifier.Id.ToString(CultureInfo.InvariantCulture) : PublicTypes.NameOf(row),
                type switch
                {
                    EntityType.Enchant => TooltipView.Plus(EnchantRanks.RowValue(store.Settings, level)),
                    TooltipView.TraitType => null,
                    EntityType.Blessing => TooltipView.GlyphRowValue,
                    _ => TooltipView.Plus(modifier.Amount),
                })
            {
                Tone = row is GemTypeDocument gem ? tokens.Gem(gem.Type.ToString()) : null,
            });
        }

        for (var rolled = lines.Count; rolled < RandomRolls(document, rarity); rolled++)
        {
            lines.Add(TooltipView.Empty(store.Settings));
        }

        return lines;
    }

    private int RandomRolls(ItemDocument document, RarityEntry? rarity)
    {
        if (rarity is null || document.RuleSet is not { } ruleSet) return 0;
        if (!store.Settings.StatCalculation.RarityAdditions.TryGetValue(ruleSet, out var byTier)) return 0;

        return byTier.TryGetValue(rarity.Tier, out var added) ? added.RandomRolls : 0;
    }

    public IReadOnlyList<StatLine> Rolls(ItemDocument document, TooltipQuery query, RarityEntry? rarity)
    {
        var level = query.ItemLevel ?? store.DropItemLevel(document);

        return StatMagnitudes.For(
            document,
            store.Settings.StatCalculation,
            store.PrimaryAttributes,
            rarity,
            level,
            query,
            [.. Enchanted(query).SelectMany(enchant => EnchantRanks.Granted(enchant, store.Settings, level))]);
    }

    private IEnumerable<EnchantDocument> Enchanted(TooltipQuery query) =>
        query.Modifiers
            .Where(modifier => TooltipView.TypeOf(modifier.Kind, store.Settings) == EntityType.Enchant)
            .Select(modifier => modifier.Id)
            .Concat(query.ImbuedTraits.Select(attuned => attuned.Id))
            .Select(id => TooltipView.ItemTrait(store, id, query.Hero))
            .OfType<EnchantDocument>();

    public MarkupString Text(
        string source,
        IReadOnlyList<Placeholder>? substitutions,
        TooltipQuery query,
        HeroEntry? hero,
        IReadOnlyList<string>? tags) =>
        new(renderer.Render(
            source,
            substitutions is not { Count: > 0 } ? null : placeholders.Resolve(Keyed(substitutions), query, hero, tags)));

    private static IReadOnlyDictionary<string, Placeholder> Keyed(IReadOnlyList<Placeholder> tokens)
    {
        var keyed = new Dictionary<string, Placeholder>(tokens.Count, StringComparer.Ordinal);
        foreach (var token in tokens)
        {
            if (token.Key is { } key) keyed[key] = token;
        }

        return keyed;
    }

    public string Style() => sheet;

    public string ModalStyle() => modal;

    private static string Compose(
        IWebHostEnvironment environment, IChromeAssetService chrome, IRichTextService renderer)
    {
        return
            $$"""
            .{{TooltipView.Root}}{--fx-window:url({{chrome.Window}});
            --fx-thin:url({{chrome.ThinWindow}});--fx-fill:url({{chrome.WindowFill}});
            --fx-divider:url({{chrome.GoldDivider}});--fx-rule:url({{chrome.SimpleDivider}});
            --fx-subheader:url({{chrome.Header(null)}});
            --fx-modplate:url({{chrome.ModifierPlate}});--fx-moddecor:url({{chrome.ModifierDecor}});
            {{Heading("ability", chrome.AbilityBadge)}}{{Heading("legendary", chrome.LegendaryBadge)}}}

            """ +
            Read(environment, Sheet, "Styles/tooltip.scss", "Components/Tooltip/_Tooltip.razor.scss") +
            renderer.Css(TooltipView.Root);
    }

    private static string Read(IWebHostEnvironment environment, string sheet, string source, string partial)
    {
        var file = environment.WebRootFileProvider.GetFileInfo(sheet);

        if (!file.Exists || file.Length == 0)
        {
            throw new InvalidOperationException(
                $"{sheet} is missing from the web root. Sass writes it from {source} on build. " +
                $"That source pulls in {partial}.");
        }

        using var reading = new StreamReader(file.CreateReadStream());

        return reading.ReadToEnd();
    }

    private static string Heading(string section, Badge badge) =>
        badge.Colour is { } hex ? $"--fx-{section}:{hex};" : string.Empty;

    public bool DrawsState(EntityDocument document) => document is BlessingDocument or ItemTraitDocument;

    public EntityDocument? Granted(EntityDocument document) =>
        document is ItemDocument { Grants: { } power } && store.All(power.Type, power.Id) is { Count: > 0 } readings
            ? readings[0]
            : null;

    public IReadOnlyList<EntityDocument> LegendaryStats(EntityDocument document) =>
        document is ItemDocument { LegendaryStats: { Count: > 0 } rolls }
            ? [.. rolls.SelectMany(roll => store.All(roll.Type, roll.Id))]
            : [];
}
