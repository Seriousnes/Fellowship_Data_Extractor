using System.Globalization;

using Fellowship.SDK.Documents;

using Microsoft.AspNetCore.Components;

using Fellowship.Codex.Models;

namespace Fellowship.Codex.Services;

public interface ITooltipViewService
{
    IChromeAssetService Chrome { get; }

    BlessingEntry Blessings { get; }

    RarityEntry? Rarity(EntityDocument document, TooltipQuery query);

    string Variables(RarityEntry? rarity);

    string? Art(EntityDocument document, RarityEntry? rarity);

    string? Ornament(RarityEntry? rarity);

    string Rung(EntityDocument document, RarityEntry rarity);

    int? Level(EntityDocument document, TooltipQuery query);

    string? Temper(TooltipQuery query, RarityEntry? rarity, int? level);

    IReadOnlyList<TooltipView.Fact> Facts(EntityDocument document, TooltipQuery query);

    string? Potential(TooltipQuery query);

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

    private readonly string sheet = Compose(environment, chrome, renderer);

    public IChromeAssetService Chrome => chrome;

    public BlessingEntry Blessings => store.Settings.Blessings;

    public RarityEntry? Rarity(EntityDocument document, TooltipQuery query)
    {
        var tier = document switch
        {
            ItemDocument { Rarity: { } rung } => (int?)rung,
            ItemDocument { Rarities: { Count: > 0 } rungs } => (int)rungs.Max(),
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

    public string? Art(EntityDocument document, RarityEntry? rarity)
    {
        var icon = rarity is not null &&
                   document is ItemDocument { IconByRarity: { } byRarity } &&
                   byRarity.GetValueOrDefault(rarity.Name) is { } art
            ? art
            : DocumentFields.Text(document, nameof(ItemDocument.Icon));

        return chrome.Art(icon);
    }

    public string? Ornament(RarityEntry? rarity) => rarity is null ? null : chrome.Ornament(rarity.Name);

    public string Rung(EntityDocument document, RarityEntry rarity) =>
        document is GemDocument ? rarity.GemDisplayName : rarity.DisplayName;

    public int? Level(EntityDocument document, TooltipQuery query) =>
        document is ItemDocument { Stats: { Count: > 0 } }
            ? query.ItemLevel ?? store.DropItemLevel(document)
            : query.ItemLevel;

    public string? Temper(TooltipQuery query, RarityEntry? rarity, int? level)
    {
        if (query.Upgrades is not { } upgrades) return null;

        var ceiling = query.MaxUpgrades
            ?? Tempering.Ceiling(store.Settings.MaxTempers, rarity?.Name, level);

        return ceiling is { } max
            ? $"{upgrades.ToString(CultureInfo.InvariantCulture)} / {max.ToString(CultureInfo.InvariantCulture)}"
            : upgrades.ToString(CultureInfo.InvariantCulture);
    }

    public IReadOnlyList<TooltipView.Fact> Facts(EntityDocument document, TooltipQuery query)
    {
        var facts = new List<TooltipView.Fact>();

        void Add(string label, double? value, string? unit = null, string? title = null)
        {
            if (value is not { } number) return;

            var text = Math.Round((decimal)number, 2).Normalize().ToString(CultureInfo.InvariantCulture);

            facts.Add(new TooltipView.Fact(label, unit is null ? text : text + unit, title));
        }

        var (cast, hastened) = Hastened(document, DocumentFields.Number(document, nameof(AbilityDocument.CastTime)), query);

        Add("Cast", cast, "s", hastened);
        Add("Channel", DocumentFields.Number(document, nameof(AbilityDocument.ChannelTime)), "s");
        Add("Cooldown", DocumentFields.Number(document, nameof(AbilityDocument.Cooldown)), "s");
        Add("Charges", DocumentFields.Number(document, nameof(AbilityDocument.Charges)));
        Add("Range", DocumentFields.Number(document, nameof(AbilityDocument.Range)));
        Add("Radius", DocumentFields.Number(document, nameof(AbilityDocument.Radius)));
        Add("Duration", DocumentFields.Number(document, nameof(AbilityDocument.Duration)), "s");

        if (DocumentFields.Number(document, nameof(AbilityDocument.Cost)) is { } cost)
        {
            var text = Math.Round((decimal)cost, 2).Normalize().ToString(CultureInfo.InvariantCulture);
            var resource = DocumentFields.Text(document, nameof(AbilityDocument.Resource));

            facts.Add(new TooltipView.Fact("Cost", resource is { } r ? $"{text} {r}" : text, null));
        }

        facts.AddRange(Menacing(document, query));

        return facts;
    }

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
        if (value is not { } seconds || ScalesWithHaste(document) is not true) return (value, null);
        if (query.Haste is not { } haste || haste <= 0) return (value, null);

        var text = (haste * 100m).Normalize().ToString(CultureInfo.InvariantCulture);

        return (
            (double)((decimal)seconds / (1m + haste)),
            $"{seconds.ToString(CultureInfo.InvariantCulture)}s at no haste, " +
            $"divided by {(1m + haste).Normalize().ToString(CultureInfo.InvariantCulture)} for {text}% haste");
    }

    private static bool? ScalesWithHaste(EntityDocument document) => document switch
    {
        AbilityDocument ability => ability.ScalesWithHaste,
        RelicPowerDocument relic => relic.ScalesWithHaste,
        WeaponPowerDocument weapon => weapon.ScalesWithHaste,
        _ => null,
    };

    public string? Potential(TooltipQuery query) =>
        query.PotentialPower is { } power
            ? power.Normalize().ToString(CultureInfo.InvariantCulture)
            : null;

    public IReadOnlyList<TooltipView.ModifierLine> Modifiers(
        ItemDocument document, TooltipQuery query, RarityEntry? rarity)
    {
        var lines = new List<TooltipView.ModifierLine>(query.Modifiers.Count);

        foreach (var modifier in query.Modifiers)
        {
            lines.Add(new TooltipView.ModifierLine(
                modifier.Kind,
                store.All(TooltipView.TypeOf(modifier.Kind), modifier.Id).Select(PublicTypes.NameOf).FirstOrDefault()
                    ?? modifier.Id.ToString(CultureInfo.InvariantCulture),
                modifier.Amount));
        }

        for (var rolled = lines.Count; rolled < RandomRolls(document, rarity); rolled++)
        {
            lines.Add(TooltipView.ModifierLine.Random);
        }

        return lines;
    }

    private int RandomRolls(ItemDocument document, RarityEntry? rarity)
    {
        if (rarity is null || document.RuleSet is not { } ruleSet) return 0;
        if (!store.Settings.StatCalculation.RarityAdditions.TryGetValue(ruleSet, out var byTier)) return 0;

        return byTier.TryGetValue(rarity.Tier, out var added) ? added.RandomRolls : 0;
    }

    public IReadOnlyList<StatLine> Rolls(ItemDocument document, TooltipQuery query, RarityEntry? rarity) =>
        StatMagnitudes.For(
            document,
            store.Settings.StatCalculation,
            store.PrimaryAttributes,
            rarity,
            query.ItemLevel ?? store.DropItemLevel(document),
            query);

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

    private static string Compose(
        IWebHostEnvironment environment, IChromeAssetService chrome, IRichTextService renderer)
    {
        var file = environment.WebRootFileProvider.GetFileInfo(Sheet);

        if (!file.Exists || file.Length == 0)
        {
            throw new InvalidOperationException(
                $"{Sheet} is missing from the web root, so the tooltip has no rules to carry. " +
                "Sass writes it from Styles/tooltip.scss on build, which pulls in " +
                "Components/Tooltip/_Tooltip.razor.scss.");
        }

        using var reading = new StreamReader(file.CreateReadStream());

        return
            $$"""
            .{{TooltipView.Root}}{--fx-window:url({{chrome.Window}});
            --fx-thin:url({{chrome.ThinWindow}});--fx-fill:url({{chrome.WindowFill}});
            --fx-divider:url({{chrome.GoldDivider}});--fx-subheader:url({{chrome.Header(null)}});
            --fx-modplate:url({{chrome.ModifierPlate}});--fx-moddecor:url({{chrome.ModifierDecor}});
            {{Heading("ability", chrome.AbilityBadge)}}{{Heading("legendary", chrome.LegendaryBadge)}}
            {{Heading("setbonus", chrome.SetBonusBadge)}}}

            """ +
            reading.ReadToEnd() +
            renderer.Css(TooltipView.Root);
    }

    private static string Heading(string section, Badge badge) =>
        badge.Colour is { } hex ? $"--fx-{section}:{hex};" : string.Empty;

    public bool DrawsState(EntityDocument document) => document is BlessingDocument or TraitDocument;

    public EntityDocument? Granted(EntityDocument document) =>
        document is ItemDocument { Grants: { } power } && store.All(power.Type, power.Id) is { Count: > 0 } readings
            ? readings[0]
            : null;

    public IReadOnlyList<EntityDocument> LegendaryStats(EntityDocument document) =>
        document is ItemDocument { LegendaryStats: { Count: > 0 } rolls }
            ? [.. rolls.SelectMany(roll => store.All(roll.Type, roll.Id))]
            : [];
}
