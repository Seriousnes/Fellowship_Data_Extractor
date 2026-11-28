using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;

using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

/// <summary>
/// The <c>CRAbilityTask_DamageModificationTask</c> calls of an ability's graph. Shipping exe 26320967: the factory
/// 0x1451115E0 takes <c>(OwningAbility, IncomingDamageCharacter, OutgoingDamageCharacter, BlockOnTaskOwnerTag,
/// CapturedSourceTagsRequirements, Prio)</c>, activation 0x14510F960 puts the task's handler 0x145114290 in the
/// incoming character's listener map (+0xC90) and the outgoing character's (+0xC78) under <c>Prio</c>, and the damage
/// driver calls the listeners of both maps on the hit's Health damage. <c>docs/damage-absorption.md</c> has the hooks.
/// </summary>
public static class DamageModificationTasks
{
    private const string Task = "DamageModificationTask";
    private const string OwningCharacterGetter = "GetOwningCRCharacterFromActorInfo";
    private const string PrioEnum = "EDamageExecutionPreApplicationPrioLevel";

    private const int IncomingParameter = 1;
    private const int OutgoingParameter = 2;
    private const int BlockTagParameter = 3;
    private const int PrioParameter = 5;

    public static IReadOnlyList<DamageModificationTask> Of(UObject? cdo, TypeMappings mappings)
    {
        var tasks = new List<DamageModificationTask>();

        foreach (var export in cdo?.Owner?.GetExports() ?? [])
        {
            if (export is not UStruct { ScriptBytecode: { Length: > 0 } bytecode }) continue;

            var locals = Locals(bytecode);

            foreach (var statement in bytecode)
            {
                foreach (var call in BytecodeGates.Descendants(statement, 0))
                {
                    if (RowSinks.Calling(call) != Task) continue;

                    var parameters = RowSinks.Parameters(call);

                    if (parameters.Count <= PrioParameter)
                    {
                        throw new InvalidOperationException(
                            $"{export.GetPathName()} calls {Task} with {parameters.Count} parameters, and the native " +
                            $"factory takes {PrioParameter + 1}.");
                    }

                    tasks.Add(new DamageModificationTask(
                        IncomingDamageCharacter: Character(parameters[IncomingParameter], locals, export),
                        OutgoingDamageCharacter: Character(parameters[OutgoingParameter], locals, export),
                        BlockOnTaskOwnerTag: Tag(parameters[BlockTagParameter]),
                        Prio: Prio(parameters[PrioParameter], mappings, export)));
                }
            }
        }

        return tasks;
    }

    private static string? Character(
        KismetExpression parameter, IReadOnlyDictionary<string, List<KismetExpression>> locals, UObject graph)
    {
        if (parameter is EX_NoObject) return null;

        var assigned = DynamicTextFloats.Name(parameter) is { } local && locals.TryGetValue(local, out var values)
            ? values
            : [parameter];

        if (assigned.Count > 0 && assigned.All(value => RowSinks.Calling(value) == OwningCharacterGetter))
        {
            return DamageModificationTask.OwningCharacter;
        }

        throw new InvalidOperationException(
            $"{graph.GetPathName()} passes {Task} a character other than {OwningCharacterGetter}'s, and " +
            $"{nameof(DamageModificationTasks)} has no name for it.");
    }

    private static string? Tag(KismetExpression parameter) =>
        BytecodeGates.Descendants(parameter, 0)
            .OfType<EX_NameConst>()
            .Where(name => !name.Value.IsNone)
            .Select(name => name.Value.Text)
            .FirstOrDefault();

    private static string Prio(KismetExpression parameter, TypeMappings mappings, UObject graph)
    {
        long? ordinal = parameter switch
        {
            EX_ByteConst value => value.Value,
            EX_IntConst value => value.Value,
            EX_IntZero => 0,
            EX_IntOne => 1,
            _ => null,
        };

        if (ordinal is { } stated && EnumOrdinals.Members(mappings, PrioEnum).TryGetValue(stated, out var member))
        {
            return member;
        }

        throw new InvalidOperationException(
            $"{graph.GetPathName()} passes {Task} a Prio that is no literal member of {PrioEnum}.");
    }

    private static Dictionary<string, List<KismetExpression>> Locals(KismetExpression[] bytecode)
    {
        var locals = new Dictionary<string, List<KismetExpression>>(StringComparer.Ordinal);

        foreach (var statement in bytecode)
        {
            var (variable, assigned) = statement switch
            {
                EX_LetObj let => (let.Variable, let.Assignment),
                EX_Let let => (let.Variable, let.Assignment),
                _ => (null, null),
            };

            if (variable is null || assigned is null || DynamicTextFloats.Name(variable) is not { } local) continue;

            if (!locals.TryGetValue(local, out var held)) locals[local] = held = [];
            held.Add(assigned);
        }

        return locals;
    }
}
