using System.Text.Json;

namespace Fellowship.SDK.Client;

public static class QueryKeys
{
    public static string Key<TKey>(TKey member) where TKey : struct, Enum => Members<TKey>.Keys[member];

    public static bool TryParse<TKey>(string? key, out TKey member) where TKey : struct, Enum =>
        Members<TKey>.ByKey.TryGetValue(key ?? string.Empty, out member);

    private static class Members<TKey> where TKey : struct, Enum
    {
        public static readonly IReadOnlyDictionary<TKey, string> Keys =
            Enum.GetValues<TKey>().ToDictionary(member => member, member => JsonNamingPolicy.CamelCase.ConvertName(member.ToString()));

        public static readonly IReadOnlyDictionary<string, TKey> ByKey =
            Keys.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.OrdinalIgnoreCase);
    }
}
