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
        app.MapCurrency();
        app.MapGem();
        app.MapItem();
        app.MapTalentSlot();
        app.MapTrait();
        app.MapWeaponPower();
    }
}
