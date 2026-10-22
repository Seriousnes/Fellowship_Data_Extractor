using Fellowship.SDK.Client;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public static class TooltipRequests
{
    public static TooltipQuery Read(
        AbilityTooltipRequest? request, IEntityStoreService store, out string? error) =>
        Read(request?.Stats, request?.ShowIcon, request?.Mobile, store, out error) with
        {
            Hero = StatQuery.NullIfEmpty(request?.Hero),
            Difficulty = request?.Difficulty,
        };

    public static TooltipQuery Read(BlessingTooltipRequest? request, out string? error) =>
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
        EffectTooltipRequest? request, IEntityStoreService store, out string? error) =>
        Read(request?.Stats, request?.ShowIcon, request?.Mobile, store, out error);

    public static TooltipQuery Read(GemTooltipRequest? request, out string? error) =>
        Read(request?.ShowIcon, request?.Mobile, out error) with
        {
            Rarity = StatQuery.NullIfEmpty(request?.Rarity),
        };

    public static TooltipQuery Read(GemTraitTooltipRequest? request, out string? error) =>
        Read(request?.ShowIcon, request?.Mobile, out error);

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
            ImbuedTraits = request?.ImbuedTraits ?? [],
            AllocatedTraits = request?.AllocatedTraits,
            HasImbuedTraits = request?.HasImbuedTraits,
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

    public static TooltipQuery Read(TraitTooltipRequest? request, out string? error) =>
        Read(request?.ShowIcon, request?.Mobile, out error) with
        {
            State = StatQuery.NullIfEmpty(request?.State),
            Hero = StatQuery.NullIfEmpty(request?.Hero),
            EquippedTier = request?.EquippedRank,
        };

    public static TooltipQuery Read(TraitPowerTooltipRequest? request, out string? error) =>
        Read(request?.ShowIcon, request?.Mobile, out error);

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
