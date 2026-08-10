using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;

namespace FellowshipExtractor.Tooltip;

internal static class RaritySelect
{
    private const int MaxDepth = 12;

    public static IReadOnlyDictionary<string, string> Read(
        AbstractFileProvider provider,
        RarityNames rarities,
        string asset,
        string function,
        Func<KismetExpression, string?> read,
        string absence)
    {
        var locals = new Dictionary<string, KismetExpression>(StringComparer.Ordinal);

        foreach (var statement in Bytecode(provider, asset, function, absence))
        {
            foreach (var chosen in Selects(statement, 0))
            {
                if (Branches(chosen, locals, read) is { Count: > 0 } found)
                {
                    return Named(found, rarities, asset, function);
                }
            }

            Bind(statement, locals);
        }

        throw new InvalidOperationException(
            $"{asset}.{function} selects nothing over the rarity ladder. {absence}");
    }

    private static KismetExpression[] Bytecode(
        AbstractFileProvider provider, string asset, string function, string absence)
    {
        var file = provider.Files.Values.FirstOrDefault(candidate =>
            candidate.Path.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase) &&
            Path.GetFileNameWithoutExtension(candidate.Path).Equals(asset, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"No {asset} package in the build. {absence}");

        foreach (var export in provider.LoadPackage(file).GetExports())
        {
            if (export is UStruct { ScriptBytecode: { Length: > 0 } bytecode } &&
                export.Name.Equals(function, StringComparison.Ordinal))
            {
                return bytecode;
            }
        }

        throw new InvalidOperationException($"{asset} compiles no {function}. {absence}");
    }

    private static Dictionary<string, string> Named(
        Dictionary<int, string> byTier, RarityNames rarities, string asset, string function)
    {
        var named = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (tier, value) in byTier)
        {
            if (rarities.Label(tier) is not { } name)
            {
                throw new InvalidOperationException(
                    $"{asset}.{function} answers rarity {tier}, which no loot rule set row names, so the table it " +
                    "fills would key on nothing.");
            }

            named[name] = value;
        }

        return named;
    }

    private static Dictionary<int, string> Branches(
        EX_SwitchValue chosen,
        IReadOnlyDictionary<string, KismetExpression> locals,
        Func<KismetExpression, string?> read)
    {
        var found = new Dictionary<int, string>();

        foreach (var branch in chosen.Cases)
        {
            if (Ordinal(branch.CaseIndexValueTerm) is not { } tier) continue;
            if (Bound(branch.CaseTerm, locals, 0) is not { } term) continue;
            if (read(term) is { } value) found[tier] = value;
        }

        return found;
    }

    private static int? Ordinal(KismetExpression? term) => term switch
    {
        EX_ByteConst value => value.Value,
        EX_IntConst value => value.Value,
        _ => null,
    };

    private static KismetExpression? Bound(
        KismetExpression? term, IReadOnlyDictionary<string, KismetExpression> locals, int depth)
    {
        if (term is null || depth > MaxDepth) return null;

        return DynamicTextFloats.Name(term) is { } name && locals.TryGetValue(name, out var assigned)
            ? Bound(assigned, locals, depth + 1)
            : term;
    }

    private static void Bind(KismetExpression statement, Dictionary<string, KismetExpression> locals)
    {
        var assignment = statement switch
        {
            EX_Let let => (Variable: let.Variable, Value: let.Assignment),
            EX_LetObj let => (Variable: let.Variable, Value: let.Assignment),
            EX_LetBool let => (Variable: let.Variable, Value: let.Assignment),
            _ => default,
        };

        if (assignment.Variable is null || assignment.Value is null) return;

        if (DynamicTextFloats.Name(assignment.Variable) is { } name) locals[name] = assignment.Value;
    }

    private static IEnumerable<EX_SwitchValue> Selects(KismetExpression? expression, int depth)
    {
        if (expression is null || depth > MaxDepth) yield break;

        if (expression is EX_SwitchValue chosen) yield return chosen;

        foreach (var child in Children(expression))
        {
            foreach (var nested in Selects(child, depth + 1)) yield return nested;
        }
    }

    private static IEnumerable<KismetExpression?> Children(KismetExpression expression) => expression switch
    {
        EX_Let let => [let.Assignment],
        EX_LetObj let => [let.Assignment],
        EX_LetBool let => [let.Assignment],
        EX_LetValueOnPersistentFrame let => [let.AssignmentExpression],
        EX_Context context => [context.ContextExpression],
        EX_Cast cast => [cast.Target],
        EX_Return returned => [returned.ReturnExpression],
        EX_FinalFunction call => call.Parameters,
        EX_VirtualFunction call => call.Parameters,
        _ => [],
    };
}
