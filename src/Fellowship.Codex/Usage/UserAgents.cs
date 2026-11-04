namespace Fellowship.Codex.Usage;

/// <summary>
/// Browser family, major version and operating system of a User-Agent header. The header itself is
/// never stored.
/// </summary>
public readonly record struct Agent(string Browser, int Major, string Os, bool Mobile)
{
    public const string Other = "Other";

    public static readonly Agent Unknown = new(Other, 0, Other, false);
}

public static class UserAgents
{
    // Link-preview fetchers, in the order they are tested. Each names the product in its own header.
    private static readonly (string Token, string Browser)[] Previewers =
    [
        ("Discordbot", "Discordbot"),
        ("Twitterbot", "Twitterbot"),
        ("facebookexternalhit", "Facebook"),
        ("Slackbot", "Slackbot"),
        ("TelegramBot", "TelegramBot"),
        ("WhatsApp", "WhatsApp"),
        ("redditbot", "Redditbot"),
        ("LinkedInBot", "LinkedInBot"),
        ("Googlebot", "Googlebot"),
        ("bingbot", "Bingbot"),
    ];

    private static readonly string[] Robots = ["bot", "crawl", "spider", "slurp", "preview"];

    private static readonly (string Token, string Browser)[] Tools =
    [
        ("curl/", "curl"),
        ("Wget/", "Wget"),
        ("python-requests/", "python-requests"),
        ("python-httpx/", "python-httpx"),
        ("aiohttp/", "aiohttp"),
        ("Go-http-client/", "Go"),
        ("node-fetch", "node-fetch"),
        ("undici", "undici"),
        ("axios/", "axios"),
        ("okhttp/", "okhttp"),
        ("PostmanRuntime/", "Postman"),
        ("insomnia/", "Insomnia"),
        ("HeadlessChrome/", "HeadlessChrome"),
    ];

    // Browsers whose header also names Chrome or Safari, so they are tested before both.
    private static readonly (string Token, string Browser)[] Browsers =
    [
        ("Edg/", "Edge"),
        ("EdgA/", "Edge"),
        ("EdgiOS/", "Edge"),
        ("OPR/", "Opera"),
        ("SamsungBrowser/", "Samsung Internet"),
        ("YaBrowser/", "Yandex"),
        ("Vivaldi/", "Vivaldi"),
        ("FxiOS/", "Firefox"),
        ("Firefox/", "Firefox"),
        ("CriOS/", "Chrome"),
        ("Chrome/", "Chrome"),
    ];

    public static Agent Classify(string? header)
    {
        if (string.IsNullOrWhiteSpace(header)) return Agent.Unknown;

        var os = System(header);
        var mobile = header.Contains("Mobi", StringComparison.Ordinal) || os is "iOS" or "Android";

        foreach (var (token, browser) in Previewers)
        {
            if (header.Contains(token, StringComparison.OrdinalIgnoreCase)) return new Agent(browser, 0, os, mobile);
        }

        foreach (var (token, browser) in Tools)
        {
            if (header.Contains(token, StringComparison.OrdinalIgnoreCase)) return new Agent(browser, 0, os, mobile);
        }

        foreach (var robot in Robots)
        {
            if (header.Contains(robot, StringComparison.OrdinalIgnoreCase)) return new Agent("Bot", 0, os, mobile);
        }

        foreach (var (token, browser) in Browsers)
        {
            if (Version(header, token) is { } major) return new Agent(browser, major, os, mobile);
        }

        if (header.Contains("Safari/", StringComparison.Ordinal))
        {
            return new Agent("Safari", Version(header, "Version/") ?? 0, os, mobile);
        }

        return new Agent(Agent.Other, 0, os, mobile);
    }

    private static string System(string header) =>
        header switch
        {
            _ when header.Contains("iPhone", StringComparison.Ordinal) ||
                   header.Contains("iPad", StringComparison.Ordinal) ||
                   header.Contains("iPod", StringComparison.Ordinal) => "iOS",
            _ when header.Contains("Android", StringComparison.Ordinal) => "Android",
            _ when header.Contains("Windows", StringComparison.Ordinal) => "Windows",
            _ when header.Contains("CrOS", StringComparison.Ordinal) => "ChromeOS",
            _ when header.Contains("Mac OS X", StringComparison.Ordinal) ||
                   header.Contains("Macintosh", StringComparison.Ordinal) => "macOS",
            _ when header.Contains("Linux", StringComparison.Ordinal) => "Linux",
            _ => Agent.Other,
        };

    private static int? Version(string header, string token)
    {
        var at = header.IndexOf(token, StringComparison.Ordinal);

        if (at < 0) return null;

        var digits = header.AsSpan(at + token.Length);
        var length = 0;

        while (length < digits.Length && length < 4 && char.IsAsciiDigit(digits[length])) length++;

        return length > 0 ? int.Parse(digits[..length]) : 0;
    }
}
