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

}
