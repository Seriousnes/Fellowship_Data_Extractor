using System.ComponentModel;
using System.Reflection;

namespace Fellowship.Codex.Models;

public static class CodexKeys
{
    public static string Key<TKey>(TKey member) where TKey : struct, Enum => Members<TKey>.Keys[member];

    public static string Label<TKey>(TKey member) where TKey : struct, Enum => Members<TKey>.Labels[member];

    public static bool TryParse<TKey>(string? key, out TKey member) where TKey : struct, Enum =>
        Members<TKey>.ByKey.TryGetValue(key ?? string.Empty, out member);

    private static class Members<TKey> where TKey : struct, Enum
    {
        public static readonly IReadOnlyDictionary<TKey, string> Keys =
            Enum.GetValues<TKey>().ToDictionary(member => member, member => member.ToString().ToLowerInvariant());

        public static readonly IReadOnlyDictionary<TKey, string> Labels =
            Enum.GetValues<TKey>().ToDictionary(
                member => member,
                member => typeof(TKey).GetField(member.ToString())!.GetCustomAttribute<DescriptionAttribute>()?.Description
                    ?? throw new InvalidOperationException(
                        $"{typeof(TKey).Name}.{member} has no Description, so the rail has no label to print for it."));

        public static readonly IReadOnlyDictionary<string, TKey> ByKey =
            Keys.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.OrdinalIgnoreCase);
    }
}
