namespace Fellowship.Codex.Usage;

/// <summary>Usage configuration, read from the <c>Usage</c> and <c>GitHub</c> sections.</summary>
public sealed record UsageSettings(
    string? Database,
    int RetentionDays,
    string? ClientId,
    string? ClientSecret,
    IReadOnlySet<string> Owners)
{
    public const string DatabaseKey = "Usage:Database";

    public const string RetentionKey = "Usage:RetentionDays";

    public const string ClientIdKey = "GitHub:ClientId";

    public const string ClientSecretKey = "GitHub:ClientSecret";

    /// <summary>Comma-separated numeric GitHub user ids allowed to open the dashboard.</summary>
    public const string OwnersKey = "GitHub:Owners";

    private const int DefaultRetentionDays = 400;

    public bool Counting => Database is not null;

    public bool SignIn => ClientId is not null && ClientSecret is not null && Owners.Count > 0;

    /// <summary>Data Protection keys beside the database, so a sign-in outlives a redeploy.</summary>
    public string? Keys => Database is null ? null : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(Database))!, "keys");

    public static UsageSettings From(IConfiguration configuration) =>
        new(
            Value(configuration, DatabaseKey),
            int.TryParse(configuration[RetentionKey], out var days) && days > 0 ? days : DefaultRetentionDays,
            Value(configuration, ClientIdKey),
            Value(configuration, ClientSecretKey),
            (configuration[OwnersKey] ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.Ordinal));

    private static string? Value(IConfiguration configuration, string key) =>
        string.IsNullOrWhiteSpace(configuration[key]) ? null : configuration[key];
}
