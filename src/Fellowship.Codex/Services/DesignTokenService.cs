using System.Text;

using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;

namespace Fellowship.Codex.Services;

public interface IDesignTokenService
{
    IReadOnlyDictionary<string, string> Colors { get; }

    IReadOnlyDictionary<string, TextStyle> TextStyles { get; }

    string? Color(string name);

    string? Rarity(string internalName);

    string? Hero(string name);

    string? Gem(string type);

    string? Role(string role);

    string CssVariables();
}

public sealed class DesignTokenService(SettingsFile settings) : IDesignTokenService
{
    private readonly IReadOnlyDictionary<string, string> colors = settings.Colors;

    private readonly IReadOnlyDictionary<string, string> rarityRows = settings.RarityColorRows;

    private readonly IReadOnlySet<string> unshown = Unshown(settings);

    public IReadOnlyDictionary<string, string> Colors => colors;

    public IReadOnlyDictionary<string, TextStyle> TextStyles => settings.TextStyles;

    public string? Color(string name) => colors.TryGetValue(name, out var value) ? '#' + value : null;

    public string? Rarity(string internalName) =>
        rarityRows.TryGetValue(internalName, out var row) ? Color(row) : null;

    public string? Hero(string name) => Color(HeroKey(name));

    public string? Gem(string type) => Color($"game.game.gem-{type.ToLowerInvariant()}");

    public string? Role(string role) => Color($"game.role-{role.ToLowerInvariant()}");

    public string CssVariables()
    {
        var css = new StringBuilder();

        foreach (var (name, value) in colors.OrderBy(c => c.Key, StringComparer.Ordinal))
        {
            if (unshown.Contains(name)) continue;

            css.Append("--").Append(Variable(name)).Append(":#").Append(value).Append(';');
        }

        return css.ToString();
    }

    private static IReadOnlySet<string> Unshown(SettingsFile settings)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rung in settings.Rarities)
        {
            if (string.Equals(rung.Name, rung.DisplayName, StringComparison.OrdinalIgnoreCase)) continue;

            if (settings.RarityColorRows.TryGetValue(rung.Name, out var row)) keys.Add(row);
        }

        return keys;
    }

    private static string HeroKey(string name) => $"game.game.hero-{name.ToLowerInvariant()}";

    public static string Variable(string name) => name.Replace('.', '-');
}
