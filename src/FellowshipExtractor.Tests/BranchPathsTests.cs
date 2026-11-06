using System.Runtime.CompilerServices;

using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Kismet;

using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

/// <summary>A walk ends inside its state cap on branches the caller ignores and on loops.</summary>
public sealed class BranchPathsTests
{
    private const int Offsets = 2;

    /// <summary>An expression with no archive to read it from, because a walk reads only its fields.</summary>
    private static T Expression<T>(Action<T> set)
        where T : KismetExpression
    {
        var expression = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

        set(expression);

        return expression;
    }

    private static BranchPaths Paths(params KismetExpression[] statements)
    {
        for (var at = 0; at < statements.Length; at++) statements[at].StatementIndex = at * Offsets;

        return new BranchPaths(new UStruct { ScriptBytecode = statements });
    }

    private static EX_JumpIfNot Branch(int targetStatement) =>
        Expression<EX_JumpIfNot>(branch =>
        {
            branch.CodeOffset = (uint)(targetStatement * Offsets);
            branch.BooleanExpression = Expression<EX_True>(condition => condition.StatementIndex = targetStatement);
        });

    /// <summary>Forty branches in a row. Told apart, the paths through them double at each one.</summary>
    private static KismetExpression[] Diamonds(int branches)
    {
        var statements = Enumerable.Range(0, branches).Select(at => (KismetExpression)Branch(at + 1)).ToList();

        statements.Add(Expression<EX_Return>(_ => { }));

        return [.. statements];
    }

    [Fact]
    public void PathsDifferingInAnIgnoredBranchJoin()
    {
        var paths = Paths(Diamonds(40));

        Assert.Equal(41, paths.From(0, _ => false).Count());
        Assert.False(paths.Truncated);
    }

    [Fact]
    public void ARelevantBranchInALoopRecordsItsSideOnce()
    {
        var paths = Paths(Branch(2), Expression<EX_Jump>(jump => jump.CodeOffset = 0), Expression<EX_Return>(_ => { }));

        var taken = paths.From(0, _ => true).ToArray();

        Assert.False(paths.Truncated);
        Assert.All(taken, step => Assert.True(step.Taken.Count <= 2));
    }

    [Fact]
    public void ALoopPushingOneAddressEnds()
    {
        var paths = Paths(Expression<EX_PushExecutionFlow>(push => push.PushingAddress = 6), Branch(3), Expression<EX_Jump>(jump => jump.CodeOffset = 0), Expression<EX_Return>(_ => { }));

        paths.From(0, _ => false).ToArray();

        Assert.False(paths.Truncated);
    }

    [Fact]
    public void AWalkPastTheCapIsReported()
    {
        var paths = Paths(Diamonds(40));

        paths.From(0, _ => true).ToArray();

        Assert.True(paths.Truncated);
    }
}
