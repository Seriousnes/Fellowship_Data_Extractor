using System.ComponentModel;
using System.Reflection;

namespace Fellowship.Codex.Models;

public static class CodexKeys
{
    public static string Label<TKey>(TKey member) where TKey : struct, Enum => Labels<TKey>.ByMember[member];

    private static class Labels<TKey> where TKey : struct, Enum
    {
        public static readonly IReadOnlyDictionary<TKey, string> ByMember =
            Enum.GetValues<TKey>().ToDictionary(
                member => member,
                member => typeof(TKey).GetField(member.ToString())!.GetCustomAttribute<DescriptionAttribute>()?.Description
                    ?? throw new InvalidOperationException(
                        $"{typeof(TKey).Name}.{member} has no Description, so the rail has no label to print for it."));
    }
}
