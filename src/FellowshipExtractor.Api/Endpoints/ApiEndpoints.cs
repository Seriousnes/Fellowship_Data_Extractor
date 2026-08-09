using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public static class ApiEndpoints
{
    public static void MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapAbility();
        app.MapBlessing();
        app.MapCompanionPet();
        app.MapCreature();
        app.MapCurrency();
        app.MapEffect();
        app.MapGem();
        app.MapGemTrait();
        app.MapGemType();
        app.MapItem();
        app.MapLegendaryPower();
        app.MapLegendaryStat();
        app.MapMount();
        app.MapMountPower();
        app.MapRelicPower();
        app.MapSetBonus();
        app.MapSetBonusPower();
        app.MapTalent();
        app.MapTalentSlot();
        app.MapTrait();
        app.MapTraitPower();
        app.MapWeaponPower();
    }
}
