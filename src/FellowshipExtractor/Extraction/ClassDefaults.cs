using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.UE4.Objects.UObject;

namespace FellowshipExtractor.Extraction;

public static class ClassDefaults
{
    private const string DefaultPrefix = "Default__";
    private const string ScriptRoot = "/Script/";
    private const int MaxDepth = 16;

    public static FPropertyTagType? Inherited(UObject cdo, string field, AbstractFileProvider provider)
    {
        var holder = cdo;

        for (var depth = 0; holder is not null && depth < MaxDepth; depth++)
        {
            var declared = holder.Properties.FirstOrDefault(
                property => property.Name.Text.Equals(field, StringComparison.OrdinalIgnoreCase));

            if (declared is not null) return declared.Tag;

            holder = Parent(holder, provider);
        }

        return null;
    }

    public static UObject? Load(string classPath, AbstractFileProvider provider)
    {
        if (classPath.StartsWith(ScriptRoot, StringComparison.Ordinal)) return null;
        if (!provider.TryLoadPackage(AbilityPresentationIndex.PackagePath(classPath), out var package)) return null;

        return package.GetExports().FirstOrDefault(export => export.Name.StartsWith(DefaultPrefix, StringComparison.Ordinal));
    }

    public static string? NativeParent(UObject cdo, AbstractFileProvider provider)
    {
        var holder = cdo;

        for (var depth = 0; holder is not null && depth < MaxDepth; depth++)
        {
            if (holder.Class?.Load() is not UStruct type) return null;
            if (type.SuperStruct?.ResolvedObject?.GetPathName() is not { } parent) return null;
            if (parent.StartsWith(ScriptRoot, StringComparison.Ordinal)) return parent;

            holder = Load(parent, provider);
        }

        return null;
    }

    private static UObject? Parent(UObject cdo, AbstractFileProvider provider) =>
        cdo.Class?.Load() is UStruct type && type.SuperStruct?.ResolvedObject?.GetPathName() is { } parent
            ? Load(parent, provider)
            : null;
}
