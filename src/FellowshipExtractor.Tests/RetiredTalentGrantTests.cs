using System.Text.Json.Nodes;

namespace FellowshipExtractor.Tests;

public sealed class RetiredTalentGrantTests
{
    private const int VigourSpiritedFortitudeOld = 2290;
    private const int VigourMagicWardOld = 1301;
    private const int SylvieSpiritedFortitudeOld = 1423;
    private const int SylvieMagicWardOld = 2064;
    private const int TheLake = 2853;

    private static JsonObject Effect(int id) =>
        Export.Loaded.Documents
            .Where(document => document.Type == EntityType.Effect && document.Id == id)
            .Select(document => document.Body)
            .FirstOrDefault()
        ?? throw new InvalidOperationException($"Effect {id} is not in the export. Either the id moved or the effect is gone.");

    [Theory]
    [InlineData(VigourSpiritedFortitudeOld)]
    [InlineData(VigourMagicWardOld)]
    [InlineData(SylvieSpiritedFortitudeOld)]
    [InlineData(SylvieMagicWardOld)]
    public void An_effect_only_a_disabled_talent_row_grants_is_disabled_in_game(int id) =>
        Assert.False(Effect(id)["enabledInGame"]!.GetValue<bool>());

    [Fact]
    public void An_effect_a_protective_enchant_grants_stays_enabled() =>
        Assert.Null(Effect(TheLake)["enabledInGame"]);
}
