using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;

using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

public static class AugmentationCategoryTexts
{
    public const string HeadingBlock = "Text_TraitCategory";
    public const string BandBlock = "TraitRankText";

    private const string SetTextFunction = "SetText";
    private const string HasTagFunction = "HasTag";
    private const string TagStruct = "GameplayTag";
    private const string TagsField = "TraitTags";

    public static IReadOnlyList<AugmentationCategoryText> Read(UObject[] exports, string widget)
    {
        var assigned = new Dictionary<string, KismetExpression>(StringComparer.Ordinal);
        var chosen = new Dictionary<string, List<(string? Tag, string Text)>>(StringComparer.Ordinal);

        foreach (var body in exports.OfType<UStruct>())
        {
            if (body.ScriptBytecode is not { Length: > 0 } bytecode) continue;

            assigned.Clear();

            foreach (var statement in bytecode)
            {
                switch (statement)
                {
                    case EX_Let let when DynamicTextFloats.Name(let.Variable) is { } local:
                        assigned[local] = let.Assignment;
                        break;
                    case EX_LetBool let when DynamicTextFloats.Name(let.Variable) is { } local:
                        assigned[local] = let.Assignment;
                        break;
                    case EX_Context { ContextExpression: EX_VirtualFunction call } context
                        when call.VirtualFunctionName.Text == SetTextFunction &&
                             DynamicTextFloats.Name(context.ObjectExpression) is HeadingBlock or BandBlock:
                    {
                        var block = DynamicTextFloats.Name(context.ObjectExpression)!;

                        if (chosen.ContainsKey(block))
                        {
                            throw new InvalidOperationException(
                                $"{widget} sets {block} more than once, so no one choice says what it prints.");
                        }

                        var branches = new List<(string? Tag, string Text)>();
                        Walk(call.Parameters.FirstOrDefault(), assigned, branches, widget, block);
                        chosen[block] = branches;
                        break;
                    }
                }
            }
        }

        if (!chosen.TryGetValue(HeadingBlock, out var headings) || !chosen.TryGetValue(BandBlock, out var bands))
        {
            throw new InvalidOperationException(
                $"{widget} sets no {HeadingBlock} or no {BandBlock}, so no augmentation category has a printed heading.");
        }

        var bandByTag = bands.ToDictionary(branch => branch.Tag ?? string.Empty, branch => branch.Text);

        if (bandByTag.Count != bands.Count || headings.Count != bands.Count ||
            headings.Any(heading => !bandByTag.ContainsKey(heading.Tag ?? string.Empty)))
        {
            throw new InvalidOperationException(
                $"{widget} chooses {HeadingBlock} and {BandBlock} on different augmentation categories, so a category " +
                "would have a heading and no band text or the reverse.");
        }

        return
        [
            .. headings.Select(heading =>
                new AugmentationCategoryText(heading.Tag, heading.Text, bandByTag[heading.Tag ?? string.Empty])),
        ];
    }

    private static void Walk(
        KismetExpression? term,
        IReadOnlyDictionary<string, KismetExpression> assigned,
        List<(string? Tag, string Text)> branches,
        string widget,
        string block)
    {
        term = Resolved(term, assigned);

        if (term is not EX_SwitchValue choice)
        {
            branches.Add((null, Printed(term, widget, block)));
            return;
        }

        var tag = Tag(choice.IndexTerm, assigned)
            ?? throw new InvalidOperationException(
                $"{widget} chooses {block} on something other than a {TagsField} tag test.");

        var taken = choice.Cases.Where(branch => branch.CaseIndexValueTerm is EX_True)
            .Select(branch => branch.CaseTerm).SingleOrDefault();
        var passed = choice.Cases.Where(branch => branch.CaseIndexValueTerm is EX_False)
            .Select(branch => branch.CaseTerm).SingleOrDefault();

        if (taken is null || passed is null)
        {
            throw new InvalidOperationException(
                $"{widget} chooses {block} on {tag} with no true and false branch.");
        }

        branches.Add((tag, Printed(Resolved(taken, assigned), widget, block)));
        Walk(passed, assigned, branches, widget, block);
    }

    private static KismetExpression? Resolved(
        KismetExpression? term, IReadOnlyDictionary<string, KismetExpression> assigned)
    {
        for (var depth = 0; depth < 8; depth++)
        {
            if (term is not EX_LocalVariable || DynamicTextFloats.Name(term) is not { } local ||
                !assigned.TryGetValue(local, out var value))
            {
                return term;
            }

            term = value;
        }

        return term;
    }

    private static string? Tag(KismetExpression? index, IReadOnlyDictionary<string, KismetExpression> assigned) =>
        Resolved(index, assigned) is EX_CallMath call &&
        call.StackNode.Name.Equals(HasTagFunction, StringComparison.Ordinal) &&
        DynamicTextFloats.Name(call.Parameters.ElementAtOrDefault(0)) == TagsField &&
        call.Parameters.ElementAtOrDefault(1) is EX_StructConst { Struct.Name: TagStruct } tag
            ? tag.Properties.OfType<EX_NameConst>().FirstOrDefault()?.Value.Text
            : null;

    private static string Printed(KismetExpression? term, string widget, string block) =>
        term is EX_TextConst { Value: { } text } &&
        text.TableIdString is EX_StringConst { Value: { } table } &&
        text.KeyString is EX_StringConst { Value: { } key } &&
        StringTables.Find(table, key) is { Length: > 0 } printed
            ? printed
            : throw new InvalidOperationException(
                $"{widget} sets {block} to something other than a string table text that resolves.");
}
