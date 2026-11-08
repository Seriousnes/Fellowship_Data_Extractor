using System.Net;
using System.Text.Json.Serialization;

namespace Fellowship.Codex;

/// <summary>
/// Checks a Turnstile token against siteverify. A token passes only when siteverify reports success for
/// the expected action and an allowed hostname. A network error, a timeout or a non-OK status fails.
/// </summary>
public sealed class TurnstileVerification(HttpClient http, TurnstileSettings settings, ILogger<TurnstileVerification> logger)
{
    public const string ClientName = "turnstile";

    public const string SiteVerify = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

    /// <summary>The form field the widget writes its token to.</summary>
    public const string ResponseField = "cf-turnstile-response";

    public const int MaxTokenLength = 2048;

    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public static bool WellFormed(string? token) =>
        !string.IsNullOrEmpty(token) && token.Length <= MaxTokenLength;

    public async Task<bool> PassesAsync(string? token, string action, IPAddress? remote, CancellationToken cancellation)
    {
        if (!WellFormed(token) || settings.SecretKey is null || settings.Hostnames.Count == 0) return false;

        List<KeyValuePair<string, string>> form =
        [
            new("secret", settings.SecretKey),
            new("response", token!),
        ];

        if (remote is not null) form.Add(new("remoteip", remote.ToString()));

        try
        {
            using var content = new FormUrlEncodedContent(form);
            using var response = await http.PostAsync(SiteVerify, content, cancellation);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Turnstile siteverify returned {Status}.", (int)response.StatusCode);

                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<SiteVerifyResult>(cancellation);

            return result is { Success: true } &&
                   string.Equals(result.Action, action, StringComparison.Ordinal) &&
                   result.Hostname is { } hostname &&
                   settings.Hostnames.Contains(hostname);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            logger.LogWarning(exception, "Turnstile siteverify failed.");

            return false;
        }
    }

    private sealed record SiteVerifyResult(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("action")] string? Action,
        [property: JsonPropertyName("hostname")] string? Hostname);
}
