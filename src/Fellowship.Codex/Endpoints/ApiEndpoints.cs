namespace Fellowship.Codex.Endpoints;

public static class ApiEndpoints
{
    public static void MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapBrowsing();
        app.MapExport();

        app.MapAbility();
        app.MapGlyph();
        app.MapCompanionPet();
        app.MapCreature();
        app.MapCurrency();
        app.MapDungeon();
        app.MapEffect();
        app.MapGem();
        app.MapEnchant();
        app.MapGemAbility();
        app.MapItem();
        app.MapLegendaryPower();
        app.MapDungeonAffix();
        app.MapRelicPassive();
        app.MapMount();
        app.MapRelicPower();
        app.MapTalent();
        app.MapSigil();
        app.MapAspect();
        app.MapProtectiveEnchant();
        app.MapWeaponPower();
    }
}
