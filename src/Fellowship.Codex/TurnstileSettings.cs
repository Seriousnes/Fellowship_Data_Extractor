namespace Fellowship.Codex;

/// <summary>
/// Cloudflare Turnstile configuration, read from the <c>Turnstile</c> section. With a site key, the
/// catalogue filter gate's fixed page is a Turnstile challenge.
/// </summary>
public sealed record TurnstileSettings(string? SiteKey, string? SecretKey, IReadOnlySet<string> Hostnames)
{
    public const string SiteKeyKey = "Turnstile:SiteKey";

    public const string SecretKeyKey = "Turnstile:SecretKey";

    /// <summary>Comma-separated hostnames a siteverify result must name.</summary>
    public const string HostnamesKey = "Turnstile:Hostnames";

    private static readonly string[] LocalHostnames = ["localhost", "127.0.0.1"];

    public bool Challenge => SiteKey is not null;

    /// <summary>
    /// The settings in a configuration. A site key with no secret key or no hostnames throws, so a partial
    /// configuration fails at startup and never falls back to the script gate.
    /// </summary>
    public static TurnstileSettings From(IConfiguration configuration, IHostEnvironment environment)
    {
        var settings = new TurnstileSettings(
            Value(configuration, SiteKeyKey),
            Value(configuration, SecretKeyKey),
            (configuration[HostnamesKey] ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase));

        if (!settings.Challenge) return settings;

        if (settings.SecretKey is null || settings.Hostnames.Count == 0)
        {
            throw new InvalidOperationException($"{SiteKeyKey} needs {SecretKeyKey} and {HostnamesKey}.");
        }

        if (environment.IsProduction() && settings.Hostnames.Overlaps(LocalHostnames))
        {
            throw new InvalidOperationException($"{HostnamesKey} names a local hostname in production.");
        }

        return settings;
    }

    private static string? Value(IConfiguration configuration, string key) =>
        string.IsNullOrWhiteSpace(configuration[key]) ? null : configuration[key];
}
