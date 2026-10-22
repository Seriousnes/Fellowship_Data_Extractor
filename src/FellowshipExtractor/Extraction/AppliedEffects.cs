using System.Reflection;

using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.GameplayTags;
using CUE4Parse.UE4.Objects.UObject;

namespace FellowshipExtractor.Extraction;

public readonly record struct AppliedEffect(string Name, string? Hero);

public static class AppliedEffects
{
    private const string ClassSuffix = "_C";
    private const string ScriptRoot = "/Script/";
    private const string RelabelField = "LogAsADifferentAbility";
    private const int MaxDepth = 12;

    private static readonly Dictionary<Type, Func<object, object?>[]> Reachable = [];

    public static IReadOnlyList<AppliedEffect> Find(
        AbstractFileProvider provider,
        string objectPath,
        IReadOnlySet<string> wanted,
        IReadOnlyDictionary<string, string> codenames)
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

        var order = new List<string>();
        var byName = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        void Hold(string name, string? hero)
        {
            if (!byName.TryGetValue(name, out var already))
            {
                order.Add(name);
                byName[name] = hero;
                return;
            }

            if (already is null && hero is not null) byName[name] = hero;
        }

        foreach (var export in exports)
        {
            var properties = PropertyWalk.Walk(export).ToArray();
            var keyed = HeroKeyed(properties, codenames);

            foreach (var property in properties)
            {
                if (property.Name.Equals(RelabelField, StringComparison.Ordinal)) continue;
                if (Leaf(PropertyWalk.AsString(property.Value)) is not { } name) continue;
                if (!wanted.Contains(name)) continue;

                Hold(name, Keying(keyed, property.Path));
            }

            if (export is not UStruct { ScriptBytecode: { Length: > 0 } bytecode }) continue;

            foreach (var statement in bytecode)
            {
                foreach (var (constant, _) in Constants(statement, 0, false))
                {
                    if (Leaf(constant) is not { } name) continue;
                    if (!wanted.Contains(name)) continue;

                    Hold(name, null);
                }
            }
        }

        return [.. order.Select(name => new AppliedEffect(name, byName[name]))];
    }

    private static Dictionary<string, string?> HeroKeyed(
        IReadOnlyList<WalkedProperty> properties, IReadOnlyDictionary<string, string> codenames)
    {
        var keyed = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var property in properties)
        {
            if (Codename(property.Value, codenames) is not { } codename) continue;

            foreach (var element in Elements(property.Path))
            {
                if (keyed.TryGetValue(element, out var already))
                {
                    if (!codename.Equals(already, StringComparison.Ordinal)) keyed[element] = null;
                    continue;
                }

                keyed[element] = codename;
            }
        }

        return keyed;
    }

    private static string? Keying(Dictionary<string, string?> keyed, string path)
    {
        string? nearest = null;

        foreach (var element in Elements(path))
        {
            if (keyed.TryGetValue(element, out var codename) && codename is not null) nearest = codename;
        }

        return nearest;
    }

    private static IEnumerable<string> Elements(string path)
    {
        for (var at = path.IndexOf(']'); at >= 0; at = path.IndexOf(']', at + 1))
        {
            yield return path[..(at + 1)];
        }
    }

    private static string? Codename(object? value, IReadOnlyDictionary<string, string> codenames)
    {
        foreach (var tag in Tags(value))
        {
            foreach (var segment in tag.Split('.'))
            {
                if (codenames.TryGetValue(segment, out var canonical)) return canonical;
            }
        }

        return null;
    }

    private static IEnumerable<string> Tags(object? value)
    {
        if (PropertyWalk.AsTags(value) is { } listed) return listed;

        var inner = value is FScriptStruct wrapper ? wrapper.StructType : value;

        return inner switch
        {
            FGameplayTag tag => tag.TagName.IsNone ? [] : (string[])[tag.TagName.Text],
            IPropertyHolder holder when PropertyWalk.GetString(holder, TagNameField) is { } text => [text],
            _ => [],
        };
    }

    private const string TagNameField = "TagName";

    public static IReadOnlyList<(string Name, string Path)> Referenced(
        IPackage package, IReadOnlySet<string> wanted) => Walk(package, wanted, false);

    public static IReadOnlyList<(string Name, string Path)> Applying(
        IPackage package, IReadOnlySet<string> wanted) => Walk(package, wanted, true);

    private static IReadOnlyList<(string Name, string Path)> Walk(
        IPackage package, IReadOnlySet<string> wanted, bool applyingOnly)
    {
        UObject[] exports;
        try
        {
            exports = [.. package.GetExports()];
        }
        catch (Exception)
        {
            return [];
        }

        var names = new List<(string Name, string Path)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var export in exports)
        {
            foreach (var property in PropertyWalk.Walk(export))
            {
                Take(PropertyWalk.AsString(property.Value), wanted, seen, names);
            }

            if (export is not UStruct { ScriptBytecode: { Length: > 0 } bytecode }) continue;

            foreach (var statement in bytecode)
            {
                foreach (var (constant, applied) in Constants(statement, 0, false))
                {
                    if (applyingOnly && !applied) continue;

                    Take(constant, wanted, seen, names);
                }
            }
        }

        return names;
    }

    private static void Take(
        string? path, IReadOnlySet<string> wanted, HashSet<string> seen, List<(string Name, string Path)> names)
    {
        if (path is null) return;
        if (Leaf(path) is not { } name) return;
        if (!wanted.Contains(name)) return;
        if (seen.Add(name)) names.Add((name, path));
    }

    private static IEnumerable<(string Path, bool Applied)> Constants(
        KismetExpression? expression, int depth, bool applied)
    {
        if (expression is null || depth > MaxDepth) yield break;

        if (expression is EX_ObjectConst constant && PropertyWalk.AsString(constant.Value) is { } path)
        {
            yield return (path, applied);
        }

        var under = applied || Applies(expression);

        foreach (var read in Reaching(expression.GetType()))
        {
            switch (read(expression))
            {
                case KismetExpression nested:
                    foreach (var found in Constants(nested, depth + 1, under)) yield return found;
                    break;

                case KismetExpression[] many:
                    foreach (var nested in many)
                    {
                        foreach (var found in Constants(nested, depth + 1, under)) yield return found;
                    }

                    break;
            }
        }
    }

    private const string ApplyToken = "Apply";
    private const string SpecToken = "Spec";

    private static readonly string[] FunctionMembers = ["StackNode", "VirtualFunctionName"];

    private static readonly Dictionary<Type, Func<object, object?>[]> Calling = [];

    private static bool Applies(KismetExpression expression)
    {
        foreach (var read in Called(expression.GetType()))
        {
            var name = read(expression) switch
            {
                FPackageIndex index => index.ResolvedObject?.Name.Text,
                FName function => function.Text,
                _ => null,
            };

            if (name is null) continue;

            if (name.Contains(ApplyToken, StringComparison.OrdinalIgnoreCase) ||
                name.Contains(SpecToken, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static Func<object, object?>[] Called(Type type)
    {
        lock (Calling)
        {
            if (Calling.TryGetValue(type, out var cached)) return cached;

            var readers = new List<Func<object, object?>>();

            foreach (var member in FunctionMembers)
            {
                if (type.GetField(member, BindingFlags.Public | BindingFlags.Instance) is { } field)
                {
                    readers.Add(field.GetValue);
                    continue;
                }

                if (type.GetProperty(member, BindingFlags.Public | BindingFlags.Instance) is { } property &&
                    property.GetIndexParameters().Length == 0)
                {
                    readers.Add(property.GetValue);
                }
            }

            return Calling[type] = [.. readers];
        }
    }

    internal static Func<object, object?>[] Reaching(Type type)
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
