namespace FellowshipExtractor.Usmap;

public sealed record SchemaProperty(
    string Name,
    EPropertyGenFlags Type,
    ushort ArrayDim,
    IReadOnlyList<SchemaProperty> Inners)
{
    public string? StructOrEnumName { get; init; }
}

public sealed record Schema(string Name, IReadOnlyList<SchemaProperty> Properties);

public static class SchemaBuilder
{
    private static int InnerCount(EPropertyGenFlags type) => type switch
    {
        EPropertyGenFlags.Array => 1,
        EPropertyGenFlags.Set => 1,
        EPropertyGenFlags.Optional => 1,
        EPropertyGenFlags.Map => 2,
        EPropertyGenFlags.Enum => 1,
        _ => 0,
    };

    public static Schema Build(string name, IReadOnlyList<PropertyRecord> run, Func<ulong, string?>? resolveType = null)
    {
        var stack = new List<SchemaProperty>();

        foreach (var record in run)
        {
            var needed = InnerCount(record.Type);
            var inners = new List<SchemaProperty>();

            for (var i = 0; i < needed && stack.Count > 0; i++)
            {
                inners.Insert(0, stack[^1]);
                stack.RemoveAt(stack.Count - 1);
            }

            if (record.Type == EPropertyGenFlags.Map && inners.Count == 2)
            {
                (inners[0], inners[1]) = (inners[1], inners[0]);
            }

            var carriesType = record.Type
                is EPropertyGenFlags.Struct or EPropertyGenFlags.Enum or EPropertyGenFlags.Byte;
            var referenced = carriesType && record.TypeFuncVa != 0
                ? resolveType?.Invoke(record.TypeFuncVa)
                : null;

            if (record.Type == EPropertyGenFlags.Byte && referenced is not null)
            {
                stack.Add(new SchemaProperty(record.Name, EPropertyGenFlags.Enum, record.ArrayDim,
                    [new SchemaProperty(record.Name, EPropertyGenFlags.Byte, 1, [])])
                {
                    StructOrEnumName = referenced,
                });
                continue;
            }

            stack.Add(new SchemaProperty(record.Name, record.Type, record.ArrayDim, inners)
            {
                StructOrEnumName = referenced,
            });
        }

        return new Schema(name, stack);
    }

    public static string TypeName(EPropertyGenFlags type) => type switch
    {
        EPropertyGenFlags.Byte => "ByteProperty",
        EPropertyGenFlags.Int8 => "Int8Property",
        EPropertyGenFlags.Int16 => "Int16Property",
        EPropertyGenFlags.Int => "IntProperty",
        EPropertyGenFlags.Int64 => "Int64Property",
        EPropertyGenFlags.UInt16 => "UInt16Property",
        EPropertyGenFlags.UInt32 => "UInt32Property",
        EPropertyGenFlags.UInt64 => "UInt64Property",
        EPropertyGenFlags.Float => "FloatProperty",
        EPropertyGenFlags.Double => "DoubleProperty",
        EPropertyGenFlags.LargeWorldCoordinatesReal => "DoubleProperty",
        EPropertyGenFlags.Bool => "BoolProperty",
        EPropertyGenFlags.SoftClass => "SoftClassProperty",
        EPropertyGenFlags.WeakObject => "WeakObjectProperty",
        EPropertyGenFlags.LazyObject => "LazyObjectProperty",
        EPropertyGenFlags.SoftObject => "SoftObjectProperty",
        EPropertyGenFlags.Class => "ClassProperty",
        EPropertyGenFlags.Object => "ObjectProperty",
        EPropertyGenFlags.Interface => "InterfaceProperty",
        EPropertyGenFlags.Name => "NameProperty",
        EPropertyGenFlags.Str => "StrProperty",
        EPropertyGenFlags.Array => "ArrayProperty",
        EPropertyGenFlags.Map => "MapProperty",
        EPropertyGenFlags.Set => "SetProperty",
        EPropertyGenFlags.Struct => "StructProperty",
        EPropertyGenFlags.Delegate => "DelegateProperty",
        EPropertyGenFlags.InlineMulticastDelegate => "MulticastInlineDelegateProperty",
        EPropertyGenFlags.SparseMulticastDelegate => "MulticastSparseDelegateProperty",
        EPropertyGenFlags.Text => "TextProperty",
        EPropertyGenFlags.Enum => "EnumProperty",
        EPropertyGenFlags.FieldPath => "FieldPathProperty",
        EPropertyGenFlags.Optional => "OptionalProperty",
        EPropertyGenFlags.VerseValue => "VerseValueProperty",
        _ => "UnknownProperty",
    };
}
