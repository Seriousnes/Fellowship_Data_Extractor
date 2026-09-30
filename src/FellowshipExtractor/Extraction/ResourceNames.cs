using System.Globalization;
using System.Text;

using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

public sealed class ResourceNames(ResourcePools pools, HeroNames heroes)
{
    private const int MostWords = 2;

    private const string Joiner = " and ";

    private static readonly char[] Breaks = ['.', ';', ',', '!', '?', '\r', '\n'];

    private static readonly string[] Verbs =
    [
        "generate", "generates", "generating", "gain", "gains", "gaining", "grant", "grants", "granting",
        "replenish", "replenishes", "restore", "restores", "refund", "refunds", "regain", "regains",
    ];

    private static readonly string[] Filler = ["of", "a", "an", "the", "your", "max", "maximum"];

    private static readonly string[] Triggers = ["each", "per"];

    private Dictionary<(string Hero, string Channel), string>? _stated;

    private readonly Dictionary<(string Hero, string Channel), Dictionary<string, int>> _byHero = new();

    private readonly Dictionary<string, Dictionary<string, int>> _byChannel = new(StringComparer.Ordinal);

    private readonly Dictionary<string, HashSet<string>> _heroesOf = new(StringComparer.Ordinal);

    private readonly Dictionary<(string Channel, string Source), HashSet<string>> _triggers = new();

    private Dictionary<(string Hero, string Channel), string>? _perHero;

    private Dictionary<string, string>? _shared;

    public void Learn(IReadOnlyList<Entity> entities)
    {
        _byHero.Clear();
        _byChannel.Clear();
        _heroesOf.Clear();
        _triggers.Clear();

        var learned = new HashSet<(string Description, string Channel, double? Amount, double? Share, string? Hero)>();

        foreach (var entity in entities)
        {
            if (entity.Generates is not { Count: > 0 } gains) continue;
            if (entity.Description is not { Length: > 0 } description) continue;

            var segments = Segments(description);

            foreach (var gain in gains)
            {
                var hero = Owning(gain, entity.OwningHeroes) is [{ } only] ? only : null;

                if (!learned.Add((description, gain.Resource, gain.Amount, gain.ShareOfMaximum, hero))) continue;

                Triggering(segments, gain);

                foreach (var printed in Printed(segments, gain))
                {
                    Counting(_byChannel, gain.Resource)[printed] =
                        Counting(_byChannel, gain.Resource).GetValueOrDefault(printed) + 1;

                    if (hero is null) continue;

                    if (!_heroesOf.TryGetValue(gain.Resource, out var claiming))
                    {
                        _heroesOf[gain.Resource] = claiming = new(StringComparer.Ordinal);
                    }

                    claiming.Add(hero);

                    var counted = Counting(_byHero, (hero, gain.Resource));
                    counted[printed] = counted.GetValueOrDefault(printed) + 1;
                }
            }
        }

        _perHero = null;
        _shared = null;
    }

    public string? Of(ResourceGain gain, IReadOnlyList<string>? owningHeroes) =>
        Of(Owning(gain, owningHeroes), gain.Resource);

    public string? Of(IReadOnlyList<string>? claiming, string channel)
    {
        if (claiming is not { Count: > 0 }) return Shared().GetValueOrDefault(channel);

        var named = claiming
            .Select(hero => Stated().GetValueOrDefault((hero, channel))
                            ?? PerHero().GetValueOrDefault((hero, channel))
                            ?? Shared().GetValueOrDefault(channel))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return named is [{ } settled] ? settled : null;
    }

    public bool Prints(Entity entity, ResourceGain gain) =>
        entity.Description is { Length: > 0 } description && Printed(Segments(description), gain).Any();

    public bool Names(Entity entity, ResourceGain gain, IReadOnlyList<string>? owningHeroes)
    {
        if (entity.Description is not { Length: > 0 } description) return false;
        if (Of(gain, owningHeroes) is not { Length: > 0 } noun) return false;
        if (Owning(gain, null) is not [{ } hero]) return false;
        if (!(owningHeroes ?? []).Contains(hero, StringComparer.OrdinalIgnoreCase)) return false;

        return Printed(Segments(description), gain)
            .Any(printed => printed.Equals(noun, StringComparison.OrdinalIgnoreCase));
    }

    private IReadOnlyList<string>? Owning(ResourceGain gain, IReadOnlyList<string>? owningHeroes)
    {
        var codenames = (gain.Source ?? string.Empty)
            .Split(Joiner, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(row => row.Split('.')[0])
            .Where(segment => heroes.Codenames.Contains(segment, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (codenames is not [{ } only]) return owningHeroes;

        return heroes.Shipped(only) is { } shipped ? [shipped] : owningHeroes;
    }

    public string? Triggering(ResourceGain gain)
    {
        if (gain.Per is not null || gain.Source is not { } source) return null;
        if (!_triggers.TryGetValue((gain.Resource, source), out var found)) return null;

        return found.Count == 1 ? found.First() : null;
    }

    private void Triggering(IReadOnlyList<string> segments, ResourceGain gain)
    {
        if (gain.Source is not { } source) return;

        foreach (var token in Tokens(gain))
        {
            foreach (var segment in segments)
            {
                foreach (var trigger in Each(segment, token))
                {
                    if (!_triggers.TryGetValue((gain.Resource, source), out var found))
                    {
                        _triggers[(gain.Resource, source)] = found = new(StringComparer.Ordinal);
                    }

                    found.Add(trigger);
                }
            }
        }
    }

    private static IEnumerable<string> Each(string segment, string token)
    {
        for (var at = segment.IndexOf(token, StringComparison.Ordinal); at >= 0;
             at = segment.IndexOf(token, at + 1, StringComparison.Ordinal))
        {
            if (at > 0 && (char.IsAsciiDigit(segment[at - 1]) || segment[at - 1] == '.')) continue;
            if (!Generating(segment[..at])) continue;

            var words = segment[(at + token.Length)..]
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            for (var word = 0; word < words.Length - 1; word++)
            {
                if (!Triggers.Contains(words[word], StringComparer.OrdinalIgnoreCase)) continue;
                if (!words[word + 1].All(char.IsAsciiLetter)) continue;

                yield return char.ToUpperInvariant(words[word + 1][0]) + words[word + 1][1..].ToLowerInvariant();
            }
        }
    }

    public IEnumerable<string> Lines()
    {
        foreach (var (key, stated) in Stated().OrderBy(entry => entry.Key.Hero, StringComparer.Ordinal)
                     .ThenBy(entry => entry.Key.Channel, StringComparer.Ordinal))
        {
            yield return $"{key.Hero} {key.Channel} is stated as {stated} by the hero's own mechanics, " +
                         $"whose ceiling sentence matches {ResourcePools.CeilingPrefix}{key.Channel}";
        }

        foreach (var (key, printed) in PerHero().OrderBy(entry => entry.Key.Hero, StringComparer.Ordinal)
                     .ThenBy(entry => entry.Key.Channel, StringComparer.Ordinal))
        {
            yield return $"{key.Hero} {key.Channel} prints as {printed}";
        }

        foreach (var (channel, printed) in Shared().OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            yield return $"every hero prints {channel} as {printed}";
        }
    }

    public IEnumerable<string> Contested()
    {
        foreach (var (channel, counted) in _byChannel.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (Shared().ContainsKey(channel)) continue;

            var printed = counted
                .OrderByDescending(entry => entry.Value)
                .ThenBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => $"{entry.Key} x{entry.Value}");

            yield return $"{channel} is printed as {string.Join(", ", printed)}";
        }
    }

    public IEnumerable<string> Unnamed() =>
        pools.Heroes
            .SelectMany(hero => pools.Channels.Select(channel => (Hero: hero, Channel: channel)))
            .Where(pair => pools.Ceiling(pair.Hero, pair.Channel) is not null)
            .Where(pair => Of([pair.Hero], pair.Channel) is null)
            .OrderBy(pair => pair.Hero, StringComparer.Ordinal)
            .ThenBy(pair => pair.Channel, StringComparer.Ordinal)
            .Select(pair => $"{pair.Hero} {pair.Channel} is named by no shipped sentence");

    private static Dictionary<string, int> Counting<TKey>(
        Dictionary<TKey, Dictionary<string, int>> counted, TKey key)
        where TKey : notnull
    {
        if (!counted.TryGetValue(key, out var printed)) counted[key] = printed = new(StringComparer.Ordinal);

        return printed;
    }

    private Dictionary<(string Hero, string Channel), string> Stated()
    {
        if (_stated is not null) return _stated;

        _stated = [];

        foreach (var profile in heroes.Profiles)
        {
            if (profile.Mechanics is not { Length: > 0 } mechanics) continue;

            foreach (var segment in Segments(mechanics))
            {
                if (Capped(segment) is not var (held, noun)) continue;

                var channels = pools.Channels
                    .Where(channel => pools.Ceiling(profile.ShippedName, channel) == held)
                    .ToArray();

                if (channels is [{ } only]) _stated.TryAdd((profile.ShippedName, only), noun);
            }
        }

        return _stated;
    }

    private static (double Held, string Noun)? Capped(string segment)
    {
        var words = segment.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (var at = 0; at + 2 < words.Length; at++)
        {
            if (!words[at].Equals(ResourcePools.CeilingPrefix, StringComparison.OrdinalIgnoreCase)) continue;

            if (!double.TryParse(
                    words[at + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var held)) continue;

            if (Noun(string.Join(' ', words[(at + 2)..])) is not { } printed) continue;

            return (held, printed);
        }

        return null;
    }

    private Dictionary<(string Hero, string Channel), string> PerHero() =>
        _perHero ??= Leading(_byHero);

    private Dictionary<string, string> Shared()
    {
        if (_shared is not null) return _shared;

        _shared = [];

        foreach (var (channel, counted) in _byChannel)
        {
            if (_heroesOf.GetValueOrDefault(channel) is not { Count: > 0 } claiming) continue;
            if (claiming.Count == 1 && !pools.Universal.Contains(channel)) continue;
            if (counted.Keys.Select(Head).Distinct(StringComparer.Ordinal).Count() != 1) continue;
            if (Leading(counted) is not { } printed) continue;

            _shared[channel] = printed;
        }

        return _shared;
    }

    private static Dictionary<TKey, string> Leading<TKey>(Dictionary<TKey, Dictionary<string, int>> counted)
        where TKey : notnull
    {
        var settled = new Dictionary<TKey, string>();

        foreach (var (key, printed) in counted)
        {
            if (Leading(printed) is { } named) settled[key] = named;
        }

        return settled;
    }

    private static string Head(string printed) => printed.Split(' ')[0];

    private static string? Leading(Dictionary<string, int> counted)
    {
        var most = counted.Values.Max();
        var leading = counted.Where(entry => entry.Value == most).Select(entry => entry.Key).ToArray();

        return leading is [{ } only] ? only : null;
    }

    private static IReadOnlyList<string> Segments(string description)
    {
        var stripped = new StringBuilder(description.Length);
        var inside = false;

        foreach (var character in description)
        {
            if (character == '<') { inside = true; continue; }
            if (character == '>') { inside = false; continue; }
            if (!inside) stripped.Append(character);
        }

        var text = stripped.ToString();
        var segments = new List<string>();
        var start = 0;

        for (var at = 0; at <= text.Length; at++)
        {
            if (at < text.Length && !Ends(text, at)) continue;

            var segment = text[start..at].Trim();

            if (segment.Length > 0) segments.Add(segment);

            start = at + 1;
        }

        return segments;
    }

    private static bool Ends(string text, int at)
    {
        if (!Breaks.Contains(text[at])) return false;
        if (at == 0 || at + 1 == text.Length) return true;

        return !char.IsAsciiDigit(text[at - 1]) || !char.IsAsciiDigit(text[at + 1]);
    }

    private static IEnumerable<string> Printed(IReadOnlyList<string> segments, ResourceGain gain)
    {
        foreach (var token in Tokens(gain))
        {
            foreach (var segment in segments)
            {
                foreach (var printed in Beside(segment, token)) yield return printed;
            }
        }
    }

    private static IEnumerable<string> Tokens(ResourceGain gain)
    {
        if (gain.Amount is { } amount) yield return amount.ToString(CultureInfo.InvariantCulture);

        if (gain.ShareOfMaximum is { } share)
        {
            yield return Math.Round(share * 100, 4).ToString(CultureInfo.InvariantCulture);
        }
    }

    private static IEnumerable<string> Beside(string segment, string token)
    {
        for (var at = segment.IndexOf(token, StringComparison.Ordinal); at >= 0;
             at = segment.IndexOf(token, at + 1, StringComparison.Ordinal))
        {
            if (at > 0 && (char.IsAsciiDigit(segment[at - 1]) || segment[at - 1] == '.')) continue;
            if (!Generating(segment[..at])) continue;

            var after = at + token.Length;

            if (after < segment.Length && segment[after] == '%') after++;
            else if (after < segment.Length && (char.IsAsciiDigit(segment[after]) || segment[after] is '.' or '%'))
            {
                continue;
            }

            if (Noun(segment[after..]) is { } printed) yield return printed;
        }
    }

    private static bool Generating(string before)
    {
        var words = before.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return words.Any(word => Verbs.Contains(word, StringComparer.OrdinalIgnoreCase));
    }

    private static string? Noun(string after)
    {
        var words = after.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var at = 0;

        while (at < words.Length && Filler.Contains(words[at], StringComparer.OrdinalIgnoreCase)) at++;

        var noun = new List<string>();

        for (; at < words.Length && noun.Count < MostWords; at++)
        {
            if (!Capitalised(words[at])) break;

            noun.Add(words[at]);
        }

        return noun.Count == 0 ? null : string.Join(' ', noun);
    }

    private static bool Capitalised(string word) =>
        word.Length > 1 && char.IsAsciiLetterUpper(word[0]) && word[1..].All(char.IsAsciiLetterLower);
}
