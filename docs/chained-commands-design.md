# Chained commands

One extractor invocation runs several verbs in sequence, so regenerating the mappings and writing
the export is one line:

```powershell
dotnet run --project src/FellowshipExtractor -- usmap export-all --output output/export
```

A verb whose gate fails stops the run and takes its exit code out with it, so a chain never writes an
export from mappings it could not verify.

## Splitting the command line

A token starts a new step when it names a verb and is not the value of a valued flag. Everything
else joins the step it follows. Verb matching is an exact, case-insensitive match against the verb
set. Each step carries its verb at index 0, so every handler keeps the argument indices it reads
today.

`src/FellowshipExtractor/Commands/Verbs.cs`:

```csharp
using System.Collections.Frozen;

namespace FellowshipExtractor.Commands;

public static class Verbs
{
    public static readonly FrozenSet<string> All = new[]
    {
        "list", "cat", "raw", "registry", "dump", "tags", "constants", "locres", "index",
        "usmap-scan", "usmap", "usmap-diff", "usmap-array", "usmap-count", "usmap-debug",
        "probe", "texture", "textures", "functions", "tooltip", "export-all", "enums", "webdb",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
}
```

The dispatch switch in `Program.cs` is the canonical source of that set, and `VerbsTests` below
reads the switch back to hold the two together.

`src/FellowshipExtractor/Commands/CommandLine.cs`:

```csharp
namespace FellowshipExtractor.Commands;

public sealed record Step(string[] Tokens)
{
    public string Verb => Tokens[0].ToLowerInvariant();
}

public static class CommandLine
{
    public static Step[] Split(string[] args, IReadOnlySet<string> verbs, IReadOnlyList<string> valuedFlags)
    {
        if (args.Length == 0) return [new Step(["list"])];

        List<Step> steps = [];
        List<string> current = [];

        for (var at = 0; at < args.Length; at++)
        {
            var value = at > 0 && valuedFlags.Any(
                flag => args[at - 1].Equals(flag, StringComparison.OrdinalIgnoreCase));

            if (current.Count > 0 && !value && verbs.Contains(args[at]))
            {
                steps.Add(new Step([.. current]));
                current.Clear();
            }

            current.Add(args[at]);
        }

        steps.Add(new Step([.. current]));

        return [.. steps];
    }
}
```

A first token that names no verb still reaches the dispatch switch and still fails there as an
unknown command, because the first token never triggers a split.

## Running the steps

`Program.cs` keeps its single `ExtractorOptions`, its `valuedFlags`, and the whole body of its
dispatch switch. Three things change.

The top-level `command` local is replaced by the split, and the single call becomes a loop inside
the existing `MappingException` handler:

```csharp
var steps = CommandLine.Split(args, Verbs.All, valuedFlags);

Directory.CreateDirectory(options.ResolvedOutputDir);

try
{
    foreach (var step in steps)
    {
        var code = await RunAsync(step.Verb, step.Tokens);

        if (code != 0) return code;
    }

    return 0;
}
catch (MappingException)
{
    ...
    return 2;
}
```

`RunAsync` takes the step it runs:

```csharp
async Task<int> RunAsync(string command, string[] args)
```

Its body is unchanged. `args[0]` is still the verb, so `args[1]`, `args[2]` and `args[1..]` keep
naming what they name now.

The `usmap` case tests its gate:

```csharp
case "usmap":
    if (!await UsmapGenerateCommand.RunAsync(options)) return 1;
    break;
```

`ResolvedMappingsFile` reads `output/fellowship.usmap` from disk each time
`ProviderFactory.CreateAsync` asks for it, so a later step in the same run reads the mappings an
earlier `usmap` step wrote, with no environment set and nothing carried between steps.

With `FELLOWSHIP_USMAP` set, the chain regenerates the mappings and then reads the file that
variable names, which is the precedence `AUsmapNamedByTheEnvironmentWinsOverTheGeneratedOne` fixes.
Leave the variable unset for the regenerated mappings to be the ones the run reads.

## Gating the mappings

`ProviderFactory` throws `FileNotFoundException` naming an absent usmap before it mounts anything,
and `TooltipResolver` names the usmap when it is built without mappings. The gate covers the
remaining case: a usmap that exists and parses but resolves no schema for a type the extractor
depends on. That one is silent, because a missing schema reads its tables back empty instead of
throwing.

`UsmapGenerateCommand.RunAsync` returns `Task<bool>`: true when the mappings are usable, false when
they are not. The method stays synchronous, as it is today, so its signature becomes
`public static Task<bool> RunAsync(ExtractorOptions options)` and it returns through
`Task.FromResult`. Hoist the round-trip's type list to a field, since the gate and the report read
the same list:

```csharp
private static readonly string[] Required =
[
    "GameplayTag", "ScalableFloat", "CRMountData", "CRTalentData", "DataTable", "Object",
    "PrimaryDataAsset",
];
```

The round-trip block collects what it could not resolve and fails on it:

```csharp
try
{
    var parsed = new UsmapParser(target);
    var mappings = parsed.Mappings ?? throw new InvalidDataException("Round-trip produced no mappings.");
    Console.WriteLine($"round-trip OK: version={parsed.Version} schemas={mappings.Types.Count} enums={mappings.Enums.Count}");

    List<string> missing = [];

    foreach (var want in Required)
    {
        if (mappings.Types.TryGetValue(want, out var schema))
        {
            var names = Enumerable.Range(0, schema.PropertyCount)
                .Select(i => schema.TryGetValue(i, out var p) ? p!.Name : "?");
            Console.WriteLine($"  {want,-20} super={schema.SuperType ?? "(none)"} [{schema.PropertyCount}] {string.Join(", ", names)}");
        }
        else
        {
            Console.WriteLine($"  {want,-20} missing");
            missing.Add(want);
        }
    }

    if (missing.Count > 0)
    {
        Console.Error.WriteLine(
            $"{target} resolves no schema for {string.Join(", ", missing)}, so the tables those types " +
            "carry would read back empty. The mappings this run wrote are the ones on disk.");

        return Task.FromResult(false);
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"round-trip FAILED: {ex.GetType().Name}: {ex.Message}");

    return Task.FromResult(false);
}

Console.WriteLine($"\nWrote {target}, which the extractor reads with no environment set.");

return Task.FromResult(true);
```

The closing line names the written path rather than telling the reader to set `FELLOWSHIP_USMAP`,
because setting that variable is what makes a later run read something else.

## Tests

`src/FellowshipExtractor.Tests/CommandLineTests.cs` covers the split with no fixture and no game
build:

- No arguments gives one step whose verb is `list`.
- `export-all --output output/export` gives one step of three tokens.
- `usmap export-all --output output/export` gives two steps, the second holding three tokens.
- `usmap export-all --output raw` gives two steps, and the second step's last token is `raw`, since
  a valued flag's value is never read as a verb.
- `export-all --output output/export ItemID GameplayAbility` gives one step, since a table name
  names no verb.
- `tooltip ItemID 5224` gives one step of three tokens.
- `USMAP Export-All --output output/export` gives two steps, since verb matching ignores case.

`src/FellowshipExtractor.Tests/VerbsTests.cs` holds `Verbs.All` to the dispatch and to the build:

- Read `src/FellowshipExtractor/Program.cs` by walking parents of `AppContext.BaseDirectory` for
  that path, the walk `Export.Folder_()` already uses, then match
  `^\s+case "(?<verb>[a-z][a-z-]*)":` over it and assert the labels equal `Verbs.All` under
  `StringComparer.OrdinalIgnoreCase`, the comparer `Verbs.All` is built with. A verb added to the
  switch without being added to the set fails here.
- Assert no table `Export.Resolver.Tags` names is in `Verbs.All`, so a table can never be swallowed
  as the next step. This one reads the build, the same precondition the rest of the suite carries.

## Acceptance

- `dotnet run --project src/FellowshipExtractor -- usmap` prints `round-trip OK`, prints all seven
  required types with a super, and exits 0 on this build.
- With `output/export` deleted and `FELLOWSHIP_USMAP` unset,
  `dotnet run --project src/FellowshipExtractor -- usmap export-all --output output/export` exits 0
  and writes `data/entities.jsonl`, `data/settings.json` and `ui/`.
- `dotnet test src/FellowshipExtractor.slnx` is green.
