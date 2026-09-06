using Fellowship.SDK.Client;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public static class TooltipRequests
{
    public static TooltipQuery Read(
        TooltipRequest? request, IEntityStoreService store, out string? error)
    {
        var query = StatQuery.Read(request?.Stats, store.Stats, out error);

        var randomStats = error is null
            ? StatQuery.Rolled(
                request?.RandomStats,
                store.Stats,
                store.Settings.StatCalculation.RandomStatPool,
                out error)
            : [];

        return query with
        {
            ShowIcon = request?.ShowIcon == true,
            Mobile = request?.Mobile == true,
            Hero = StatQuery.NullIfEmpty(request?.Hero),
            Difficulty = request?.Difficulty,
            State = StatQuery.NullIfEmpty(request?.State),
            EquippedTier = request?.EquippedTier,
            Rarity = StatQuery.NullIfEmpty(request?.Rarity),
            ItemLevel = request?.ItemLevel,
            Upgrades = request?.Upgrades,
            MaxUpgrades = request?.MaxUpgrades,
            NoSocket = request?.NoSocket == true,
            Gem = request?.Gem,
            PotentialPower = request?.PotentialPower,
            Armor = request?.Armor,
            RandomStats = randomStats,
            Modifiers = request?.Modifiers ?? [],
            ImbuedTraits = request?.ImbuedTraits ?? [],
            AllocatedTraits = request?.AllocatedTraits,
            HasImbuedTraits = request?.HasImbuedTraits,
        };
    }
}
