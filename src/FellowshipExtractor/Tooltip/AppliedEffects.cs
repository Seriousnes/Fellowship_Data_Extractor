using System.Reflection;

using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;

namespace FellowshipExtractor.Tooltip;

public static class AppliedEffects
{
    private const string ClassSuffix = "_C";
    private const string ScriptRoot = "/Script/";
    private const int MaxDepth = 12;

    private static readonly Dictionary<Type, Func<object, object?>[]> Reachable = [];

    public static IReadOnlyList<string> Named(
        AbstractFileProvider provider, string objectPath, IReadOnlySet<string> wanted)
    {
        if (!provider.TryLoadPackage(AbilityPresentationIndex.PackagePath(objectPath), out var package))
        {
            return [];
        }

        UObject[] exports;
        try
        {
            exports = [.. package.GetExports()];
        }
        catch (Exception)
        {
            return [];
        }

        var named = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var export in exports)
        {
            foreach (var property in PropertyWalk.Walk(export))
            {
                if (Leaf(PropertyWalk.AsString(property.Value)) is not { } name) continue;
                if (!wanted.Contains(name)) continue;
                if (seen.Add(name)) named.Add(name);
            }

            if (export is not UStruct { ScriptBytecode: { Length: > 0 } bytecode }) continue;

            foreach (var statement in bytecode)
            {
                foreach (var constant in Constants(statement, 0))
                {
                    if (Leaf(constant) is not { } name) continue;
                    if (!wanted.Contains(name)) continue;
                    if (seen.Add(name)) named.Add(name);
                }
            }
        }

        return named;
    }

    private static IEnumerable<string> Constants(KismetExpression? expression, int depth)
    {
        if (expression is null || depth > MaxDepth) yield break;

        if (expression is EX_ObjectConst named && PropertyWalk.AsString(named.Value) is { } path)
        {
            yield return path;
        }

        foreach (var read in Reaching(expression.GetType()))
        {
            switch (read(expression))
            {
                case KismetExpression nested:
                    foreach (var found in Constants(nested, depth + 1)) yield return found;
                    break;

                case KismetExpression[] many:
                    foreach (var nested in many)
                    {
                        foreach (var found in Constants(nested, depth + 1)) yield return found;
                    }

                    break;
            }
        }
    }

    private static Func<object, object?>[] Reaching(Type type)
    {
        lock (Reachable)
        {
            if (Reachable.TryGetValue(type, out var cached)) return cached;

            var readers = new List<Func<object, object?>>();

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!Carries(field.FieldType)) continue;

                readers.Add(field.GetValue);
            }

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length > 0) continue;
                if (!Carries(property.PropertyType)) continue;

                readers.Add(property.GetValue);
            }

            return Reachable[type] = [.. readers];
        }
    }

    private static bool Carries(Type type) =>
        typeof(KismetExpression).IsAssignableFrom(type) || type == typeof(KismetExpression[]);

    private static string? Leaf(string? path)
    {
        if (path is null) return null;
        if (path.StartsWith(ScriptRoot, StringComparison.OrdinalIgnoreCase)) return null;

        var name = path.Contains('/') ? AbilityPresentationIndex.AssetName(path) : path;

        return name.EndsWith(ClassSuffix, StringComparison.Ordinal) ? name[..^ClassSuffix.Length] : name;
    }
}
