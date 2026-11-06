using System.Net;
using System.Security.Cryptography;

namespace Fellowship.Codex.Usage;

/// <summary>Cause of a 5xx response.</summary>
public enum FailureReason
{
    Rejected,
    TimedOut,
    Exception,
    Other,
}

public static class FailureTag
{
    private static readonly object Key = new();

    public static void Mark(HttpContext http, FailureReason reason) => http.Items[Key] = reason;

    public static FailureReason Read(HttpContext http, bool threw) =>
        http.Items.TryGetValue(Key, out var reason) && reason is FailureReason tagged
            ? tagged
            : threw ? FailureReason.Exception : FailureReason.Other;
}

/// <summary>One 5xx response. Held in memory alone, because its address has the query string.</summary>
public sealed record FailedRequest(
    DateTimeOffset At,
    FailureReason Reason,
    int Status,
    string Method,
    UsageArea Area,
    string Route,
    string Address,
    Agent Agent,
    string Client,
    double Milliseconds,
    string? Exception);

/// <summary>The newest items added, up to a fixed capacity.</summary>
public sealed class Ring<T>(int capacity)
{
    private readonly Lock gate = new();

    private readonly T[] items = new T[capacity];

    private long added;

    public void Add(T item)
    {
        lock (gate) items[added++ % capacity] = item;
    }

    /// <summary>The items, newest first.</summary>
    public IReadOnlyList<T> Newest()
    {
        lock (gate)
        {
            var count = (int)Math.Min(added, capacity);
            var newest = new T[count];

            for (var at = 0; at < count; at++) newest[at] = items[(added - 1 - at) % capacity];

            return newest;
        }
    }
}

/// <summary>
/// Hashes an address with a salt held in memory and replaced each UTC day. One client has one tag for a
/// day, and the tag cannot be reversed once the salt is gone, so no address is stored.
/// </summary>
public sealed class ClientTags(TimeProvider time)
{
    private const int TagBytes = 4;

    private const int SaltBytes = 32;

    private readonly Lock gate = new();

    private byte[] salt = [];

    private long day = -1;

    public string Tag(IPAddress? address)
    {
        if (address is null) return string.Empty;

        return Convert.ToHexStringLower(
            HMACSHA256.HashData(Salt(), address.MapToIPv6().GetAddressBytes()).AsSpan(0, TagBytes));
    }

    private byte[] Salt()
    {
        var today = UsageCounter.Day(time.GetUtcNow());

        lock (gate)
        {
            if (day == today) return salt;

            day = today;
            salt = RandomNumberGenerator.GetBytes(SaltBytes);

            return salt;
        }
    }
}
