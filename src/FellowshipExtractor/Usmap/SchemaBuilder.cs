namespace FellowshipExtractor.Usmap;

public sealed record SchemaProperty(
    string Name,
    EPropertyGenFlags Type,
    ushort ArrayDim,
    IReadOnlyList<SchemaProperty> Inners)
{
    /// <summary>For Struct and Enum properties, the referenced type resolved from its Z_Construct pointer.</summary>
    public string? StructOrEnumName { get; init; }
}

public sealed record Schema(string Name, IReadOnlyList<SchemaProperty> Properties);

/// <summary>
/// Turns a raw PropPointers[] run into the schema a usmap describes.
///
/// UHT emits container element properties as their own records placed immediately before the
/// container that owns them, so a TArray contributes two entries and a TMap three. A usmap
/// records the container once with its element types nested, so the run has to be folded back
/// into a tree before it can be compared or written.
/// </summary>
public static class SchemaBuilder
{
    /// <summary>How many preceding records each container kind consumes as its element types.</summary>
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

            // UHT emits a map's value property before its key property, but usmap and every
            // reader expect key first. Graded against a fixture usmap this was 153 of 191
            // shared map properties reading reversed; the other 38 have matching key and
            // value types, so the swap is invisible there.
            if (record.Type == EPropertyGenFlags.Map && inners.Count == 2)
            {
                (inners[0], inners[1]) = (inners[1], inners[0]);
            }

            var carriesType = record.Type
                is EPropertyGenFlags.Struct or EPropertyGenFlags.Enum or EPropertyGenFlags.Byte;
            var referenced = carriesType && record.TypeFuncVa != 0
                ? resolveType?.Invoke(record.TypeFuncVa)
                : null;

            // TEnumAsByte<E> is generated as a Byte property carrying an enum function. usmap
            // spells that as an EnumProperty with a ByteProperty inner, and reading it as a bare
            // byte consumes the wrong width.
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

    /// <summary>usmap's property type names, as CUE4Parse spells them.</summary>
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
