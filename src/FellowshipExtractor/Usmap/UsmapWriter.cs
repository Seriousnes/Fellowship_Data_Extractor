using System.Text;

namespace FellowshipExtractor.Usmap;

public sealed record UsmapSchema(string Name, string? SuperName, IReadOnlyList<SchemaProperty> Properties);

public sealed record UsmapEnum(string Name, IReadOnlyList<string> Values);

/// <summary>
/// Writes the .usmap container, version 0 (Initial), uncompressed.
///
/// Uncompressed on purpose: CUE4Parse reads it either way, and a plain file can be inspected
/// byte by byte when a schema comes out wrong.
/// </summary>
public sealed class UsmapWriter
{
    private const ushort Magic = 0x30C4;
    private const byte VersionInitial = 0;
    private const byte CompressionNone = 0;

    private readonly List<string> _names = [];
    private readonly Dictionary<string, int> _nameIndex = new(StringComparer.Ordinal);

    private int Name(string value)
    {
        if (_nameIndex.TryGetValue(value, out var index)) return index;
        index = _names.Count;
        _names.Add(value);
        _nameIndex[value] = index;
        return index;
    }

    public void Write(string path, IReadOnlyList<UsmapSchema> schemas, IReadOnlyList<UsmapEnum> enums)
    {
        // The name table is built while serialising, so the body is staged first.
        using var bodyStream = new MemoryStream();
        using var body = new BinaryWriter(bodyStream, Encoding.ASCII, leaveOpen: true);

        using var enumStream = new MemoryStream();
        using (var enumWriter = new BinaryWriter(enumStream, Encoding.ASCII, leaveOpen: true))
        {
            enumWriter.Write(enums.Count);
            foreach (var e in enums)
            {
                enumWriter.Write(Name(e.Name));
                enumWriter.Write((byte)Math.Min(e.Values.Count, 255));
                foreach (var value in e.Values.Take(255)) enumWriter.Write(Name(value));
            }
        }

        using var schemaStream = new MemoryStream();
        using (var schemaWriter = new BinaryWriter(schemaStream, Encoding.ASCII, leaveOpen: true))
        {
            schemaWriter.Write(schemas.Count);
            foreach (var schema in schemas)
            {
                schemaWriter.Write(Name(schema.Name));
                schemaWriter.Write(schema.SuperName is null ? -1 : Name(schema.SuperName));

                var flattened = Flatten(schema.Properties);
                schemaWriter.Write((ushort)flattened.Sum(p => Math.Max(1, (int)p.Property.ArrayDim)));
                schemaWriter.Write((ushort)flattened.Count);

                foreach (var (property, index) in flattened)
                {
                    schemaWriter.Write((ushort)index);
                    schemaWriter.Write((byte)Math.Max(1, (int)property.ArrayDim));
                    schemaWriter.Write(Name(property.Name));
                    WriteType(schemaWriter, property);
                }
            }
        }

        using var output = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(output, Encoding.ASCII);

        using var nameStream = new MemoryStream();
        using (var nameWriter = new BinaryWriter(nameStream, Encoding.ASCII, leaveOpen: true))
        {
            nameWriter.Write(_names.Count);
            foreach (var name in _names)
            {
                var bytes = Encoding.UTF8.GetBytes(name);
                nameWriter.Write((byte)Math.Min(bytes.Length, 255));
                nameWriter.Write(bytes, 0, Math.Min(bytes.Length, 255));
            }
        }

        var payload = new MemoryStream();
        nameStream.Position = 0; nameStream.CopyTo(payload);
        enumStream.Position = 0; enumStream.CopyTo(payload);
        schemaStream.Position = 0; schemaStream.CopyTo(payload);
        var bytes2 = payload.ToArray();

        writer.Write(Magic);
        writer.Write(VersionInitial);
        writer.Write(CompressionNone);
        writer.Write((uint)bytes2.Length);
        writer.Write((uint)bytes2.Length);
        writer.Write(bytes2);
    }

    /// <summary>Schema entries carry their index, which is the position in the declared order.</summary>
    private static List<(SchemaProperty Property, int Index)> Flatten(IReadOnlyList<SchemaProperty> properties)
    {
        var result = new List<(SchemaProperty, int)>(properties.Count);
        var index = 0;
        foreach (var property in properties)
        {
            result.Add((property, index));
            index += Math.Max(1, (int)property.ArrayDim);
        }
        return result;
    }

    private void WriteType(BinaryWriter writer, SchemaProperty property)
    {
        var type = UsmapType(property.Type);
        writer.Write((byte)type);

        switch (type)
        {
            case UsmapPropertyType.EnumProperty:
                // The underlying integer property, then the enum's name.
                if (property.Inners.Count > 0) WriteType(writer, property.Inners[0]);
                else writer.Write((byte)UsmapPropertyType.ByteProperty);
                writer.Write(property.StructOrEnumName is { } enumName ? Name(enumName) : -1);
                break;

            case UsmapPropertyType.StructProperty:
                writer.Write(property.StructOrEnumName is { } structName ? Name(structName) : -1);
                break;

            case UsmapPropertyType.ArrayProperty:
            case UsmapPropertyType.SetProperty:
            case UsmapPropertyType.OptionalProperty:
                WriteInnerOrUnknown(writer, property, 0);
                break;

            case UsmapPropertyType.MapProperty:
                WriteInnerOrUnknown(writer, property, 0);
                WriteInnerOrUnknown(writer, property, 1);
                break;
        }
    }

    private void WriteInnerOrUnknown(BinaryWriter writer, SchemaProperty property, int index)
    {
        if (index < property.Inners.Count) WriteType(writer, property.Inners[index]);
        else writer.Write((byte)UsmapPropertyType.Unknown);
    }

    public static UsmapPropertyType UsmapType(EPropertyGenFlags type) => type switch
    {
        EPropertyGenFlags.Byte => UsmapPropertyType.ByteProperty,
        EPropertyGenFlags.Int8 => UsmapPropertyType.Int8Property,
        EPropertyGenFlags.Int16 => UsmapPropertyType.Int16Property,
        EPropertyGenFlags.Int => UsmapPropertyType.IntProperty,
        EPropertyGenFlags.Int64 => UsmapPropertyType.Int64Property,
        EPropertyGenFlags.UInt16 => UsmapPropertyType.UInt16Property,
        EPropertyGenFlags.UInt32 => UsmapPropertyType.UInt32Property,
        EPropertyGenFlags.UInt64 => UsmapPropertyType.UInt64Property,
        EPropertyGenFlags.UnsizedInt => UsmapPropertyType.Int64Property,
        EPropertyGenFlags.UnsizedUInt => UsmapPropertyType.UInt64Property,
        EPropertyGenFlags.Float => UsmapPropertyType.FloatProperty,
        EPropertyGenFlags.Double => UsmapPropertyType.DoubleProperty,
        EPropertyGenFlags.LargeWorldCoordinatesReal => UsmapPropertyType.DoubleProperty,
        EPropertyGenFlags.Bool => UsmapPropertyType.BoolProperty,
        EPropertyGenFlags.SoftClass => UsmapPropertyType.SoftClassProperty,
        EPropertyGenFlags.WeakObject => UsmapPropertyType.WeakObjectProperty,
        EPropertyGenFlags.LazyObject => UsmapPropertyType.LazyObjectProperty,
        EPropertyGenFlags.SoftObject => UsmapPropertyType.SoftObjectProperty,
        EPropertyGenFlags.Class => UsmapPropertyType.ClassProperty,
        EPropertyGenFlags.Object => UsmapPropertyType.ObjectProperty,
        EPropertyGenFlags.Interface => UsmapPropertyType.InterfaceProperty,
        EPropertyGenFlags.Name => UsmapPropertyType.NameProperty,
        EPropertyGenFlags.Str => UsmapPropertyType.StrProperty,
        EPropertyGenFlags.Array => UsmapPropertyType.ArrayProperty,
        EPropertyGenFlags.Map => UsmapPropertyType.MapProperty,
        EPropertyGenFlags.Set => UsmapPropertyType.SetProperty,
        EPropertyGenFlags.Struct => UsmapPropertyType.StructProperty,
        EPropertyGenFlags.Delegate => UsmapPropertyType.DelegateProperty,
        EPropertyGenFlags.InlineMulticastDelegate => UsmapPropertyType.MulticastInlineDelegateProperty,
        EPropertyGenFlags.SparseMulticastDelegate => UsmapPropertyType.MulticastDelegateProperty,
        EPropertyGenFlags.Text => UsmapPropertyType.TextProperty,
        EPropertyGenFlags.Enum => UsmapPropertyType.EnumProperty,
        EPropertyGenFlags.FieldPath => UsmapPropertyType.FieldPathProperty,
        EPropertyGenFlags.Optional => UsmapPropertyType.OptionalProperty,
        _ => UsmapPropertyType.Unknown,
    };
}

public enum UsmapPropertyType : byte
{
    ByteProperty = 0, BoolProperty = 1, IntProperty = 2, FloatProperty = 3, ObjectProperty = 4,
    NameProperty = 5, DelegateProperty = 6, DoubleProperty = 7, ArrayProperty = 8,
    StructProperty = 9, StrProperty = 10, TextProperty = 11, InterfaceProperty = 12,
    MulticastDelegateProperty = 13, WeakObjectProperty = 14, LazyObjectProperty = 15,
    AssetObjectProperty = 16, SoftObjectProperty = 17, UInt64Property = 18, UInt32Property = 19,
    UInt16Property = 20, Int64Property = 21, Int16Property = 22, Int8Property = 23,
    MapProperty = 24, SetProperty = 25, EnumProperty = 26, FieldPathProperty = 27,
    OptionalProperty = 28, Utf8StrProperty = 29, AnsiStrProperty = 30, ClassProperty = 31,
    MulticastInlineDelegateProperty = 32, SoftClassProperty = 33,
    Unknown = 0xFF,
}
