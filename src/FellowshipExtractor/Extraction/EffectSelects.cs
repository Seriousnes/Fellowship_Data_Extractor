using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;

using Fellowship.SDK.Models;

using FellowshipExtractor.Blueprints;

namespace FellowshipExtractor.Extraction;

/// <summary>A Select node between effects: its tag test where the index is one, and each case's effect by asset name.</summary>
public sealed record EffectSelectRead(string? Tag, string? Character, IReadOnlyList<EffectSelectCaseRead> Cases);

/// <summary>A case of a Select node: its index value, and its effect by asset name or the Select node its spec is made from.</summary>
public sealed record EffectSelectCaseRead(string Value, string? Effect, EffectSelectRead? Select);

/// <summary>
/// The Select nodes of an ability's graph, and of the graphs of the actors it spawns, with a gameplay effect class or an
/// effect spec for each index value. A Select compiles to an <c>EX_SwitchValue</c>: an index term and one term for each
/// index value. A case's term is an effect class, a spec made from one with <c>FellowshipMakeEffectSpec</c>, or a
/// variable with either. An actor's variable has the value the spawning ability sets with
/// <c>SetStructurePropertyByName</c>.
/// </summary>
public static class EffectSelects
{
    private const string SpecHandle = "GameplayEffectSpecHandle";
    private const string MakeSpec = "MakeEffectSpec";
    private const string PropertySetterPrefix = "Set";
    private const string PropertySetterSuffix = "PropertyByName";
    private const string TagTest = "HasMatchingGameplayTag";
    private const string TargetDataFromActor = "AbilityTargetDataFromActor";
    private const string ApplyToken = "Apply";
    private const string InstigatorGetter = "GetInstigator";
    private const string InstigatorField = "Instigator";
    private const int MaxDepth = 24;

    private static readonly HashSet<string> OwnerGetters = new(StringComparer.Ordinal)
    {
        "GetOwningCRCharacterFromActorInfo",
        "GetAvatarActorFromActorInfo",
        "GetOwningActorFromActorInfo",
    };

    private sealed record Body(UStruct Function, KismetExpression[] Code);

    private sealed record Scope(UObject Cdo, IReadOnlyList<Body> Bodies, Scope? Spawner);

    private abstract record Option
    {
        public sealed record Effect(string Name) : Option;

        public sealed record Empty : Option;

        public sealed record Nested(EffectSelectRead Select) : Option;

        public sealed record Unresolved : Option;
    }

    private sealed record Reader(AbstractFileProvider Provider, TypeMappings Mappings, IReadOnlySet<string> Effects)
    {
        /// <summary>The Select nodes a case of another Select reads its effect from.</summary>
        public HashSet<EX_SwitchValue> Nested { get; } = new(ReferenceEqualityComparer.Instance);
    }

    public static IReadOnlyList<EffectSelectRead> Of(
        UObject? cdo, AbstractFileProvider provider, TypeMappings mappings, IReadOnlySet<string> effects)
    {
        if (cdo is null) return [];

        var reader = new Reader(provider, mappings, effects);
        var ability = new Scope(cdo, Bodies(cdo, provider), null);
        var scopes = new List<Scope> { ability };

        foreach (var path in ClassDefaults.Chain(cdo, provider).SelectMany(RowSinks.SpawnedPaths).Distinct(StringComparer.Ordinal))
        {
            if (ClassDefaults.Load(path, provider) is { } actor) scopes.Add(new Scope(actor, Bodies(actor, provider), ability));
        }

        var read = new List<(EX_SwitchValue Node, EffectSelectRead Select)>();

        foreach (var scope in scopes)
        {
            foreach (var body in scope.Bodies)
            {
                for (var at = 0; at < body.Code.Length; at++)
                {
                    foreach (var node in BytecodeGates.Descendants(body.Code[at], 0).OfType<EX_SwitchValue>())
                    {
                        if (Read(node, at, body, scope, reader) is { } select) read.Add((node, select));
                    }
                }
            }
        }

        var selects = new List<EffectSelectRead>();

        foreach (var (node, select) in read)
        {
            if (reader.Nested.Contains(node)) continue;
            if (!selects.Any(held => Same(held, select))) selects.Add(select);
        }

        return selects;
    }

    private static EffectSelectRead? Read(EX_SwitchValue select, int at, Body body, Scope scope, Reader reader)
    {
        var options = (select.Cases ?? [])
            .Select(option => (Term: option.CaseIndexValueTerm, Option: Effect(option.CaseTerm, at, body, scope, reader, 0)))
            .ToArray();

        if (!options.Any(entry => entry.Option is Option.Effect or Option.Nested)) return null;

        var graph = $"{scope.Cdo.Owner?.Name}:{body.Function.Name}";

        if (options.Any(entry => entry.Option is Option.Unresolved))
        {
            throw new InvalidOperationException(
                $"{graph} has a Select with an effect for one case and a case {nameof(EffectSelects)} reads no effect " +
                $"from: {KismetPrinter.Expression(select)}.");
        }

        var members = Enumerated(select.IndexTerm, body) is { } enumerated ? EnumOrdinals.Members(reader.Mappings, enumerated) : null;
        var cases = options
            .Select(entry => new EffectSelectCaseRead(
                CaseValue(entry.Term, members, graph),
                (entry.Option as Option.Effect)?.Name,
                (entry.Option as Option.Nested)?.Select))
            .ToArray();

        var (index, from) = Followed(select.IndexTerm, at, body, 0);

        if (index is not EX_Context tested || RowSinks.Invoking(tested) is not { } call || RowSinks.Calling(call) != TagTest)
        {
            return new EffectSelectRead(null, null, cases);
        }

        var tags = BytecodeGates.Descendants(call, 0)
            .OfType<EX_NameConst>()
            .Where(name => !name.Value.IsNone)
            .Select(name => name.Value.Text)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (tags is not [var tag])
        {
            throw new InvalidOperationException(
                $"{graph} has a Select between effects on a {TagTest} call with no literal tag: {KismetPrinter.Expression(tested)}.");
        }

        var character = Character(tested.ObjectExpression, from, body, scope, at)
            ?? throw new InvalidOperationException(
                $"{graph} has a Select between effects on the tag {tag} of a character that is neither the ability's " +
                $"owner nor the target of the application: {KismetPrinter.Expression(tested)}.");

        return new EffectSelectRead(tag, character, cases);
    }

    private static string CaseValue(KismetExpression? term, IReadOnlyDictionary<long, string>? members, string graph)
    {
        long? ordinal = term switch
        {
            EX_True => null,
            EX_False => null,
            EX_ByteConst value => value.Value,
            EX_IntConst value => value.Value,
            EX_IntZero => 0,
            EX_IntOne => 1,
            _ => throw new InvalidOperationException(
                $"{graph} has a Select between effects with the case value {KismetPrinter.Expression(term)}, and " +
                $"{nameof(EffectSelects)} reads a boolean or an ordinal."),
        };

        if (ordinal is not { } stated) return term is EX_True ? "true" : "false";
        if (members is null) return stated.ToString(System.Globalization.CultureInfo.InvariantCulture);

        return members.TryGetValue(stated, out var member)
            ? member
            : throw new InvalidOperationException($"{graph} has a Select case {stated}, and the index's enum has no member for it.");
    }

    /// <summary>The enum of the index term's local, from the function's properties.</summary>
    private static string? Enumerated(KismetExpression? index, Body body)
    {
        if (DynamicTextFloats.Name(index) is not { } name) return null;

        foreach (var child in body.Function.ChildProperties ?? [])
        {
            if (!child.Name.Text.Equals(name, StringComparison.Ordinal)) continue;

            return child switch
            {
                FEnumProperty { Enum.Name: { Length: > 0 } named } => named,
                FByteProperty { Enum.Name: { Length: > 0 } named } => named,
                _ => null,
            };
        }

        return null;
    }

    private static Option Effect(KismetExpression? term, int at, Body body, Scope scope, Reader reader, int depth)
    {
        if (term is null || depth > MaxDepth) return new Option.Unresolved();

        switch (term)
        {
            case EX_ObjectConst constant:
                return PropertyWalk.AsString(constant.Value) is { } path && Named(path, reader) is { } named
                    ? new Option.Effect(named)
                    : new Option.Unresolved();

            case EX_StructConst structure:
                return structure.Struct?.Name == SpecHandle ? new Option.Empty() : new Option.Unresolved();

            case EX_SwitchValue nested:
                if (Read(nested, at, body, scope, reader) is not { } select) return new Option.Unresolved();

                reader.Nested.Add(nested);

                return new Option.Nested(select);

            case EX_LocalVariable local when DynamicTextFloats.Name(local) is { } name:
                return Reaching(body, name, at) is var (value, from)
                    ? Effect(value, from, body, scope, reader, depth + 1)
                    : new Option.Unresolved();

            case EX_InstanceVariable variable when DynamicTextFloats.Name(variable) is { } property:
                return Held(property, scope, reader, depth + 1);
        }

        if (RowSinks.Invoking(term) is { } call && RowSinks.Calling(call) is { } called &&
            called.Contains(MakeSpec, StringComparison.Ordinal) && RowSinks.Parameters(call) is { Count: > 0 } parameters)
        {
            return Effect(parameters[0], at, body, scope, reader, depth + 1);
        }

        return new Option.Unresolved();
    }

    /// <summary>
    /// The effect of an instance variable: the one effect the scope's graphs assign to it or the spawning ability sets
    /// on it by name, and otherwise the class default.
    /// </summary>
    private static Option Held(string property, Scope scope, Reader reader, int depth)
    {
        var assigned = new List<Option>();

        foreach (var body in scope.Bodies)
        {
            for (var at = 0; at < body.Code.Length; at++)
            {
                if (Assigned(body.Code[at]) is not var (variable, value)) continue;
                if (variable is not EX_InstanceVariable || DynamicTextFloats.Name(variable) != property) continue;

                assigned.Add(Effect(value, at, body, scope, reader, depth + 1));
            }
        }

        foreach (var body in scope.Spawner?.Bodies ?? [])
        {
            for (var at = 0; at < body.Code.Length; at++)
            {
                foreach (var reached in BytecodeGates.Descendants(body.Code[at], 0))
                {
                    if (RowSinks.Invoking(reached) is not { } call || RowSinks.Calling(call) is not { } called) continue;
                    if (!called.StartsWith(PropertySetterPrefix, StringComparison.Ordinal)) continue;
                    if (!called.EndsWith(PropertySetterSuffix, StringComparison.Ordinal)) continue;

                    var parameters = RowSinks.Parameters(call);

                    if (parameters.Count < 3) continue;
                    if (parameters[1] is not EX_NameConst named || named.Value.Text != property) continue;

                    assigned.Add(Effect(parameters[2], at, body, scope.Spawner!, reader, depth + 1));
                }
            }
        }

        var distinct = assigned.Distinct().ToArray();

        if (distinct.Length == 1) return distinct[0];
        if (distinct.Length > 1) return new Option.Unresolved();

        return PropertyWalk.AsString(ClassDefaults.Inherited(scope.Cdo, property, reader.Provider)?.GenericValue) is { } path &&
               Named(path, reader) is { } effect
            ? new Option.Effect(effect)
            : new Option.Unresolved();
    }

    /// <summary>
    /// <see cref="EffectSelect.Owner"/> for a subject read from the ability's actor info or from the spawned actor's
    /// instigator, and <see cref="EffectSelect.Target"/> for a subject the Select's statement applies the effect to.
    /// </summary>
    private static string? Character(KismetExpression? subject, int at, Body body, Scope scope, int statement)
    {
        var root = Root(subject, at, body, 0);

        if (root is null) return null;

        if (RowSinks.Invoking(root) is { } getter && RowSinks.Calling(getter) is { } called)
        {
            return OwnerGetters.Contains(called) || scope.Spawner is not null && called == InstigatorGetter
                ? EffectSelect.Owner
                : null;
        }

        if (root is EX_InstanceVariable)
        {
            return scope.Spawner is not null && DynamicTextFloats.Name(root) == InstigatorField ? EffectSelect.Owner : null;
        }

        if (root is not EX_LocalVariable || DynamicTextFloats.Name(root) is not { } local) return null;

        foreach (var reached in BytecodeGates.Descendants(body.Code[statement], 0))
        {
            if (RowSinks.Invoking(reached) is not { } call || RowSinks.Calling(call) is not { } applying) continue;
            if (!applying.Contains(ApplyToken, StringComparison.Ordinal)) continue;

            foreach (var parameter in RowSinks.Parameters(call))
            {
                if (parameter is EX_SwitchValue) continue;

                var applied = Root(parameter, statement, body, 0);

                if (applied is EX_LocalVariable && DynamicTextFloats.Name(applied) == local) return EffectSelect.Target;
            }
        }

        return null;
    }

    /// <summary>The expression a character is read from: through locals, casts and <c>AbilityTargetDataFromActor</c>.</summary>
    private static KismetExpression? Root(KismetExpression? expression, int at, Body body, int depth)
    {
        if (expression is null || depth > MaxDepth) return expression;

        switch (expression)
        {
            case EX_LocalVariable local when DynamicTextFloats.Name(local) is { } name && Reaching(body, name, at) is var (value, from):
                return Root(value, from, body, depth + 1);
            case EX_CastBase cast:
                return Root(cast.Target, at, body, depth + 1);
            case EX_Cast cast:
                return Root(cast.Target, at, body, depth + 1);
            case EX_InterfaceContext context:
                return Root(context.InterfaceValue, at, body, depth + 1);
        }

        if (RowSinks.Invoking(expression) is { } call && RowSinks.Calling(call) == TargetDataFromActor &&
            RowSinks.Parameters(call) is { Count: > 0 } parameters)
        {
            return Root(parameters[0], at, body, depth + 1);
        }

        return expression;
    }

    /// <summary>The expression a term has at a statement, with each local replaced by the value assigned to it.</summary>
    private static (KismetExpression? Expression, int At) Followed(KismetExpression? term, int at, Body body, int depth)
    {
        if (depth <= MaxDepth && term is EX_LocalVariable local && DynamicTextFloats.Name(local) is { } name &&
            Reaching(body, name, at) is var (value, from))
        {
            return Followed(value, from, body, depth + 1);
        }

        return (term, at);
    }

    /// <summary>The nearest assignment to a local before a statement, or the one assignment of the body after it.</summary>
    private static (KismetExpression Value, int At)? Reaching(Body body, string local, int at)
    {
        for (var from = Math.Min(at, body.Code.Length) - 1; from >= 0; from--)
        {
            if (Assigns(body.Code[from], local) is { } value) return (value, from);
        }

        (KismetExpression Value, int At)? later = null;

        for (var from = at + 1; from < body.Code.Length; from++)
        {
            if (Assigns(body.Code[from], local) is not { } value) continue;
            if (later is not null) return null;

            later = (value, from);
        }

        return later;
    }

    private static KismetExpression? Assigns(KismetExpression statement, string local) =>
        Assigned(statement) is var (variable, value) && variable is EX_LocalVariable && DynamicTextFloats.Name(variable) == local
            ? value
            : null;

    private static (KismetExpression Variable, KismetExpression Value)? Assigned(KismetExpression statement) =>
        statement switch
        {
            EX_Let { Variable: { } variable, Assignment: { } value } => (variable, value),
            EX_LetObj { Variable: { } variable, Assignment: { } value } => (variable, value),
            EX_LetBool { Variable: { } variable, Assignment: { } value } => (variable, value),
            EX_LetBase { Variable: { } variable, Assignment: { } value } => (variable, value),
            _ => null,
        };

    private static IReadOnlyList<Body> Bodies(UObject cdo, AbstractFileProvider provider)
    {
        var bodies = new List<Body>();

        foreach (var holder in ClassDefaults.Chain(cdo, provider))
        {
            foreach (var export in holder.Owner?.GetExports() ?? [])
            {
                if (export is UStruct { ScriptBytecode: { Length: > 0 } code } function) bodies.Add(new Body(function, code));
            }
        }

        return bodies;
    }

    private static string? Named(string path, Reader reader)
    {
        var name = RowSinks.Leaf(path);

        return reader.Effects.Contains(name) ? name : null;
    }

    private static bool Same(EffectSelectRead? held, EffectSelectRead? read)
    {
        if (held is null || read is null) return held is null && read is null;

        return held.Tag == read.Tag && held.Character == read.Character && held.Cases.Count == read.Cases.Count &&
               held.Cases.Zip(read.Cases).All(pair =>
                   pair.First.Value == pair.Second.Value && pair.First.Effect == pair.Second.Effect &&
                   Same(pair.First.Select, pair.Second.Select));
    }
}
