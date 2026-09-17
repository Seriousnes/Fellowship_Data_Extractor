using System.Text.Json;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.UE4.Objects.Core.i18N;
using CUE4Parse.UE4.Objects.Engine.Curves;
using CUE4Parse.UE4.Objects.UObject;

namespace FellowshipExtractor;

public static class UeJson
{
    public static readonly JsonWriterOptions WriterOptions = new() { Indented = true };

    public static readonly Dictionary<string, int> StringifiedTypes = [];

    private static void RecordStringified(object value)
    {
        var key = value.GetType().FullName ?? value.GetType().Name;
        StringifiedTypes[key] = StringifiedTypes.GetValueOrDefault(key) + 1;
    }

    public static JsonElement ToElement(IPropertyHolder holder)
    {
        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteObject(writer, holder);
        }
        return JsonDocument.Parse(buffer.ToArray()).RootElement.Clone();
    }

    public static void WriteObject(Utf8JsonWriter writer, IPropertyHolder holder)
    {
        writer.WriteStartObject();
        if (holder is UObject uobject)
        {
            writer.WriteString("$name", uobject.Name);
            writer.WriteString("$type", uobject.ExportType);
        }

        foreach (var property in holder.Properties)
        {
            writer.WritePropertyName(PropertyName(property));
            WriteValue(writer, property.Tag);
        }

        var rows = RowsOf(holder);
        if (rows is not null)
        {
            if (holder is UDataTable table) writer.WriteString("$rowStruct", table.RowStructName);
            writer.WritePropertyName("$rows");
            writer.WriteStartObject();
            foreach (var (rowName, row) in rows)
            {
                writer.WritePropertyName(rowName.Text);
                WriteObject(writer, row);
            }
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    public static IReadOnlyDictionary<FName, FStructFallback>? RowsOf(IPropertyHolder holder) => holder switch
    {
        UDataTable table => table.RowMap,
        UCurveTable curves => curves.RowMap,
        _ => null,
    };

    private static string PropertyName(FPropertyTag property) =>
        property.ArrayIndex > 0 ? $"{property.Name.Text}[{property.ArrayIndex}]" : property.Name.Text;

    public static void WriteValue(Utf8JsonWriter writer, FPropertyTagType? tag)
    {
        if (tag is null)
        {
            writer.WriteNullValue();
            return;
        }

        switch (tag)
        {
            case BoolProperty b:
                writer.WriteBooleanValue(b.Value);
                return;
            case ArrayProperty or SetProperty:
                WriteScriptArray(writer, tag.GenericValue as UScriptArray);
                return;
            case MapProperty:
                WriteScriptMap(writer, tag.GenericValue as UScriptMap);
                return;
            default:
                WriteScalar(writer, tag.GenericValue);
                return;
        }
    }

    private static void WriteScriptArray(Utf8JsonWriter writer, UScriptArray? array)
    {
        writer.WriteStartArray();
        foreach (var element in array?.Properties ?? [])
        {
            WriteValue(writer, element);
        }
        writer.WriteEndArray();
    }

    private static void WriteScriptMap(Utf8JsonWriter writer, UScriptMap? map)
    {
        writer.WriteStartArray();
        foreach (var pair in map?.Properties ?? [])
        {
            writer.WriteStartObject();
            writer.WritePropertyName("key");
            WriteValue(writer, pair.Key);
            writer.WritePropertyName("value");
            WriteValue(writer, pair.Value);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void WriteScalar(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                return;
            case string s:
                writer.WriteStringValue(s);
                return;
            case bool b:
                writer.WriteBooleanValue(b);
                return;
            case byte or sbyte or short or ushort or int or uint or long:
                writer.WriteNumberValue(Convert.ToInt64(value));
                return;
            case ulong u:
                writer.WriteNumberValue(u);
                return;
            case float f:
                writer.WriteNumberValue(f);
                return;
            case double d:
                writer.WriteNumberValue(d);
                return;
            case FName name:
                writer.WriteStringValue(name.Text);
                return;
            case FText text:
                writer.WriteStringValue(StringTables.Resolve(text) ?? "");
                return;
            case FPackageIndex index:
                writer.WriteStringValue(index.ResolvedObject?.GetPathName() ?? index.Name);
                return;
            case FScriptStruct scriptStruct:
                WriteStruct(writer, scriptStruct.StructType);
                return;
            case IPropertyHolder holder:
                WriteObject(writer, holder);
                return;
            default:
                RecordStringified(value);
                writer.WriteStringValue(value.ToString());
                return;
        }
    }

    private static void WriteStruct(Utf8JsonWriter writer, object? structType)
    {
        if (structType is IPropertyHolder holder)
        {
            WriteObject(writer, holder);
            return;
        }

        if (structType is null)
        {
            writer.WriteNullValue();
            return;
        }

        switch (structType)
        {
            case FSimpleCurveKey simple:
                writer.WriteStartObject();
                writer.WriteNumber("Time", simple.Time);
                writer.WriteNumber("Value", simple.Value);
                writer.WriteEndObject();
                return;

            case FRichCurveKey rich:
                writer.WriteStartObject();
                writer.WriteNumber("Time", rich.Time);
                writer.WriteNumber("Value", rich.Value);
                writer.WriteString("InterpMode", rich.InterpMode.ToString());
                writer.WriteString("TangentMode", rich.TangentMode.ToString());
                writer.WriteString("TangentWeightMode", rich.TangentWeightMode.ToString());
                writer.WriteNumber("ArriveTangent", rich.ArriveTangent);
                writer.WriteNumber("ArriveTangentWeight", rich.ArriveTangentWeight);
                writer.WriteNumber("LeaveTangent", rich.LeaveTangent);
                writer.WriteNumber("LeaveTangentWeight", rich.LeaveTangentWeight);
                writer.WriteEndObject();
                return;
        }

        RecordStringified(structType);
        writer.WriteStringValue(structType.ToString());
    }
}
