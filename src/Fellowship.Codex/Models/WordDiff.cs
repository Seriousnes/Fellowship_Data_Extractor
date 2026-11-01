using System.Text.RegularExpressions;

namespace Fellowship.Codex.Models;

public enum WordChange
{
    Kept,
    Removed,
    Added,
}

public readonly record struct DiffedWord(string Text, WordChange Change);

public static partial class WordDiff
{
    public static bool Reads(string older, string? newer) =>
        newer is not null && Words().IsMatch(older) && Words().IsMatch(newer);

    public static IReadOnlyList<DiffedWord> Between(string older, string newer)
    {
        var before = Tokens(older);
        var after = Tokens(newer);

        var common = new int[before.Length + 1, after.Length + 1];

        for (var was = before.Length - 1; was >= 0; was--)
        {
            for (var now = after.Length - 1; now >= 0; now--)
            {
                common[was, now] = before[was] == after[now]
                    ? common[was + 1, now + 1] + 1
                    : Math.Max(common[was + 1, now], common[was, now + 1]);
            }
        }

        var words = new List<DiffedWord>(before.Length + after.Length);
        int at = 0, to = 0;

        while (at < before.Length || to < after.Length)
        {
            if (at < before.Length && to < after.Length && before[at] == after[to])
            {
                Append(words, before[at++], WordChange.Kept);
                to++;
            }
            else if (to < after.Length && (at == before.Length || common[at, to + 1] >= common[at + 1, to]))
            {
                Append(words, after[to++], WordChange.Added);
            }
            else
            {
                Append(words, before[at++], WordChange.Removed);
            }
        }

        return words;
    }

    private static void Append(List<DiffedWord> words, string text, WordChange change)
    {
        if (words.Count > 0 && words[^1].Change == change)
        {
            words[^1] = words[^1] with { Text = words[^1].Text + text };
        }
        else
        {
            words.Add(new DiffedWord(text, change));
        }
    }

    private static string[] Tokens(string text) => [.. Token().Matches(text).Select(match => match.Value)];

    [GeneratedRegex(@"\S+\s*|\s+")]
    private static partial Regex Token();

    [GeneratedRegex(@"\S\s+\S")]
    private static partial Regex Words();
}
