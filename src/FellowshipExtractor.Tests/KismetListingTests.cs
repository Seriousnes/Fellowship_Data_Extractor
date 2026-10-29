using System.Runtime.CompilerServices;

using CUE4Parse.UE4.Kismet;

using FellowshipExtractor.Blueprints;
using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class KismetListingTests
{
    private const string AbilityTable = "GameplayAbility";
    private const int SkullCrusher = 1229;

    public static TheoryData<Type> ExpressionTypes()
    {
        var types = new TheoryData<Type>();

        foreach (var type in typeof(KismetExpression).Assembly.GetTypes()
                     .Where(type => typeof(KismetExpression).IsAssignableFrom(type) && !type.IsAbstract)
                     .OrderBy(type => type.Name, StringComparer.Ordinal))
        {
            types.Add(type);
        }

        return types;
    }

    [Theory]
    [MemberData(nameof(ExpressionTypes))]
    public void EveryExpressionTypePrintsWithoutPlaceholder(Type type)
    {
        var expression = (KismetExpression)RuntimeHelpers.GetUninitializedObject(type);

        var printed = KismetPrinter.Expression(expression);
        var listed = KismetListing.Structured([expression]);

        Assert.DoesNotContain(KismetPrinter.Unhandled, printed);
        Assert.All(listed, line => Assert.DoesNotContain(KismetPrinter.Unhandled, line));
    }

    [Fact]
    public void BranchWithElseBecomesIfElse()
    {
        KismetExpression[] statements =
        [
            JumpIfNot(0, 20),
            Text(5, "then"),
            Jump(10, 30),
            Text(20, "else"),
            Text(30, "after"),
            Return(40),
        ];

        Assert.Equal(
        [
            "     0  if (true) {",
            "     5      \"then\"",
            "    10  } else {",
            "    20      \"else\"",
            "        }",
            "    30  \"after\"",
            "    40  return",
        ], KismetListing.Structured(statements));
    }

    [Fact]
    public void BranchWithoutElseBecomesIf()
    {
        KismetExpression[] statements =
        [
            JumpIfNot(0, 10),
            Text(5, "then"),
            Text(10, "after"),
        ];

        Assert.Equal(
        [
            "     0  if (true) {",
            "     5      \"then\"",
            "        }",
            "    10  \"after\"",
        ], KismetListing.Structured(statements));
    }

    [Fact]
    public void JumpOntoElseJumpKeepsBranchWithoutElse()
    {
        KismetExpression[] statements =
        [
            JumpIfNot(0, 30),
            JumpIfNot(5, 20),
            Text(10, "inner"),
            Jump(20, 40),
            Text(30, "else"),
            Text(40, "after"),
        ];

        Assert.Equal(
        [
            "     0  if (true) {",
            "     5      if (true) {",
            "    10          \"inner\"",
            "            }",
            "    20      goto L40",
            "        }",
            "    30  \"else\"",
            "        L40:",
            "    40  \"after\"",
        ], KismetListing.Structured(statements));
    }

    [Fact]
    public void EntryIntoBranchKeepsLabelsAndGotos()
    {
        KismetExpression[] statements =
        [
            JumpIfNot(0, 20),
            Text(5, "then"),
            Jump(10, 30),
            Text(20, "else"),
            Text(30, "after"),
        ];

        var entries = new Dictionary<long, IReadOnlyList<string>> { [5] = ["ReceiveBeginPlay"] };

        Assert.Equal(
        [
            "     0  if (!true) goto L20",
            "        L5:  // entry from ReceiveBeginPlay",
            "     5  \"then\"",
            "    10  goto L30",
            "        L20:",
            "    20  \"else\"",
            "        L30:",
            "    30  \"after\"",
        ], KismetListing.Structured(statements, entries));
    }

    [Fact]
    public void FlatListingLabelsEveryTarget()
    {
        KismetExpression[] statements =
        [
            JumpIfNot(0, 10),
            Text(5, "then"),
            Text(10, "after"),
        ];

        Assert.Equal(
        [
            "     0  if (!true) goto L10",
            "     5  \"then\"",
            "        L10:",
            "    10  \"after\"",
        ], KismetListing.Flat(statements));
    }

    [Fact]
    public void PushThroughPopBecomesSequence()
    {
        KismetExpression[] statements =
        [
            Node<EX_PushExecutionFlow>(0, push => push.PushingAddress = 20),
            Text(5, "first"),
            Node<EX_PopExecutionFlow>(10, _ => { }),
            Text(20, "second"),
        ];

        Assert.Equal(
        [
            "     0  sequence then L20 {",
            "     5      \"first\"",
            "    10      pop_flow",
            "        }",
            "        L20:",
            "    20  \"second\"",
        ], KismetListing.Structured(statements));
    }

    [Fact]
    public void SkullCrusherBlueprintDecompiles()
    {
        var provider = Export.Provider;
        var tag = CsvTables.ReadTags(provider).Single(t => t.Table == AbilityTable && t.NetId == SkullCrusher);
        var package = BlueprintsCommand.Packages(provider)
            .Single(path => path[(path.LastIndexOf('/') + 1)..] == tag.Name);

        var folder = Path.Combine(Path.GetTempPath(), $"blueprints-{Guid.NewGuid():N}");

        try
        {
            var written = BlueprintsCommand.Write(provider, [package], folder);
            var listings = Directory.GetFiles(folder, "*.txt", SearchOption.AllDirectories)
                .ToDictionary(path => Path.GetFileNameWithoutExtension(path), File.ReadAllText);

            Assert.Equal(0, written.Failures);
            Assert.Single(Directory.GetFiles(folder, BlueprintsCommand.DefaultsFile, SearchOption.AllDirectories));
            Assert.All(listings.Values, text => Assert.DoesNotContain(KismetPrinter.Unhandled, text));

            var ubergraph = Assert.Single(listings, pair => pair.Key.StartsWith("ExecuteUbergraph_", StringComparison.Ordinal)).Value;
            Assert.Contains("FellowshipCommitHeroAbility(", ubergraph);
            Assert.Contains("// entry from K2_ActivateAbility", ubergraph);
            Assert.Contains("enters ExecuteUbergraph_", listings["K2_ActivateAbility"]);
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    private static T Node<T>(int offset, Action<T> set) where T : KismetExpression
    {
        var node = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
        node.StatementIndex = offset;
        set(node);
        return node;
    }

    private static EX_JumpIfNot JumpIfNot(int offset, uint target) =>
        Node<EX_JumpIfNot>(offset, jump =>
        {
            jump.CodeOffset = target;
            jump.BooleanExpression = Node<EX_True>(offset, _ => { });
        });

    private static EX_Jump Jump(int offset, uint target) => Node<EX_Jump>(offset, jump => jump.CodeOffset = target);

    private static EX_StringConst Text(int offset, string value) => Node<EX_StringConst>(offset, text => text.Value = value);

    private static EX_Return Return(int offset) => Node<EX_Return>(offset, _ => { });
}
