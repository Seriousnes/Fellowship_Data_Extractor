using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public static class ApiEndpoints
{
    public static void MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapBrowsing();

        app.MapAbility();
        app.MapBlessing();
        app.MapCompanionPet();
        app.MapCreature();
        app.MapCurrency();
        app.MapDungeon();
        app.MapEffect();
        app.MapGem();
        app.MapGemTrait();
        app.MapItem();
        app.MapLegendaryPower();
        app.MapMount();
        app.MapRelicPower();
        app.MapSetBonus();
        app.MapSetBonusPower();
        app.MapTalent();
        app.MapTrait();
        app.MapTraitPower();
        app.MapWeaponPower();
    }
}
