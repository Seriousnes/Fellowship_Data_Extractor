using Fellowship.SDK.Client;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public static class TooltipRequests
{
    public static TooltipQuery Read(
        AbilityTooltipRequest? request, IEntityStoreService store, out string? error)
    {
        var read = Read(request?.Stats, request?.ShowIcon, request?.Mobile, store, out error);

        var sources = error is null ? ChangedBy(request?.ChangedBy, store, out error) : [];

        return read with
        {
            Hero = StatQuery.NullIfEmpty(request?.Hero),
            Difficulty = request?.Difficulty,
            ChangedBy = sources,
        };
    }

    public static IReadOnlyList<ChangeSource> ChangedBy(
        IReadOnlyList<ChangeSource>? sources, IEntityStoreService store, out string? error)
    {
        error = null;

        if (sources is not { Count: > 0 }) return [];

        foreach (var source in sources)
        {
            if (store.All(source.Type, source.Id) is [_, ..]) continue;

            error = $"The build has no {PublicTypes.Noun(source.Type)} {source.Id} to change the tooltip.";
            return [];
        }

        return sources;
    }

    public static IReadOnlyList<ChangeSource> ChangedBy(string? written, IEntityStoreService store, out string? error)
    {
        if (ChangeSources.TryParse(written, out var parsed)) return ChangedBy(parsed.Values, store, out error);

        error = $"'{written}' is not a list of change sources, each written type:id with any held effect " +
            "after a '~', such as talent:38~effect:1376.";
        return [];
    }

    public static TooltipQuery Read(GlyphTooltipRequest? request, out string? error) =>
        Read(request?.ShowIcon, request?.Mobile, out error) with
        {
            EquippedTier = request?.EquippedTier,
        };

    public static TooltipQuery Read(CompanionPetTooltipRequest? request, out string? error) =>
        Read(request?.ShowIcon, request?.Mobile, out error);

    public static TooltipQuery Read(CreatureTooltipRequest? request, out string? error) =>
        Read(request?.ShowIcon, request?.Mobile, out error) with
        {
            Difficulty = request?.Difficulty,
        };

    public static TooltipQuery Read(CurrencyTooltipRequest? request, out string? error) =>
        Read(request?.ShowIcon, request?.Mobile, out error) with
        {
            Rarity = StatQuery.NullIfEmpty(request?.Rarity),
        };

    public static TooltipQuery Read(DungeonTooltipRequest? request, out string? error) =>
        Read(request?.ShowIcon, request?.Mobile, out error);

    public static TooltipQuery Read(
        EffectTooltipRequest? request, IEntityStoreService store, out string? error)
    {
        var read = Read(request?.Stats, request?.ShowIcon, request?.Mobile, store, out error);

        var sources = error is null ? ChangedBy(request?.ChangedBy, store, out error) : [];

        return read with
        {
            Hero = StatQuery.NullIfEmpty(request?.Hero),
            ChangedBy = sources,
        };
    }

    public static TooltipQuery Read(GemTooltipRequest? request, out string? error) =>
        Read(request?.ShowIcon, request?.Mobile, out error) with
        {
            Rarity = StatQuery.NullIfEmpty(request?.Rarity),
        };

    public static TooltipQuery Read(GemAbilityTooltipRequest? request, out string? error) =>
        Read(request?.ShowIcon, request?.Mobile, out error);

    public static TooltipQuery Read(EnchantTooltipRequest? request, out string? error) =>
        Augmented(request?.ShowIcon, request?.Mobile, request?.State, request?.RankSources, out error) with
        {
            Hero = StatQuery.NullIfEmpty(request?.Hero),
            ItemLevel = request?.ItemLevel,
        };

    public static TooltipQuery Read(
        ItemTooltipRequest? request, IEntityStoreService store, out string? error) =>
        Read(request?.Stats, request?.ShowIcon, request?.Mobile, store, out error) with
        {
            Hero = StatQuery.NullIfEmpty(request?.Hero),
            Rarity = StatQuery.NullIfEmpty(request?.Rarity),
            ItemLevel = request?.ItemLevel,
            Upgrades = request?.Upgrades,
            MaxUpgrades = request?.MaxUpgrades,
            NoSocket = request?.NoSocket == true,
            Gem = request?.Gem,
            PotentialPower = request?.PotentialPower,
            Armor = request?.Armor,
            Modifiers = request?.Modifiers ?? [],
            Attunements = request?.Attunements ?? [],
            AllocatedAttunements = request?.AllocatedAttunements,
            HasAttunements = request?.HasAttunements,
        };

    public static TooltipQuery Read(RelicPassiveTooltipRequest? request, out string? error) =>
        Read(request?.ShowIcon, request?.Mobile, out error);

    public static TooltipQuery Read(LegendaryPowerTooltipRequest? request, out string? error) =>
        Read(request?.ShowIcon, request?.Mobile, out error);

    public static TooltipQuery Read(MountTooltipRequest? request, out string? error) =>
        Read(request?.ShowIcon, request?.Mobile, out error);

    public static TooltipQuery Read(
        RelicPowerTooltipRequest? request, IEntityStoreService store, out string? error) =>
        Read(request?.Stats, request?.ShowIcon, request?.Mobile, store, out error);

    public static TooltipQuery Read(TalentTooltipRequest? request, out string? error) =>
        Read(request?.ShowIcon, request?.Mobile, out error);

    public static TooltipQuery Read(SigilTooltipRequest? request, out string? error) =>
        Augmented(request?.ShowIcon, request?.Mobile, request?.State, request?.RankSources, out error);

    public static TooltipQuery Read(AspectTooltipRequest? request, out string? error) =>
        Augmented(request?.ShowIcon, request?.Mobile, request?.State, request?.RankSources, out error);

    public static TooltipQuery Read(ProtectiveEnchantTooltipRequest? request, out string? error) =>
        Augmented(request?.ShowIcon, request?.Mobile, request?.State, request?.RankSources, out error);

    private static TooltipQuery Augmented(
        bool? showIcon, bool? mobile, string? state, string? rankSources, out string? error) =>
        Read(showIcon, mobile, out error) with
        {
            State = StatQuery.NullIfEmpty(state),
            RankSources = RankSources.Parse(rankSources),
        };

    public static TooltipQuery Read(
        WeaponPowerTooltipRequest? request, IEntityStoreService store, out string? error) =>
        Read(request?.Stats, request?.ShowIcon, request?.Mobile, store, out error) with
        {
            Hero = StatQuery.NullIfEmpty(request?.Hero),
        };

    private static TooltipQuery Read(
        IReadOnlyDictionary<string, StatRolls>? stats,
        bool? showIcon,
        bool? mobile,
        IEntityStoreService store,
        out string? error) =>
        StatQuery.Read(stats, store.Stats, out error) with
        {
            ShowIcon = showIcon == true,
            Mobile = mobile == true,
        };

    private static TooltipQuery Read(bool? showIcon, bool? mobile, out string? error)
    {
        error = null;

        return new TooltipQuery { ShowIcon = showIcon == true, Mobile = mobile == true };
    }
}
