// Reads records of one hero from the export, and nothing else.
//
//   dotnet run tools/hero-record.cs -- <Hero> <type> <id> [field ...]
//       The record of that $type and id, with only the named fields; with no fields, its key names. A record whose
//       "heroes" names other heroes and not <Hero> prints no content.
//   dotnet run tools/hero-record.cs -- <Hero> find <name prefix>
//       $type, id and name of each record whose "heroes" contains <Hero> and whose name starts with the prefix
//       (case-insensitive).
//
// The export is the highest output/export-<build> with both entities.jsonl and settings.json, found from the current
// directory upwards.
using System.Text.Json.Nodes;

if (args.Length < 3)
{
    Console.Error.WriteLine("usage: hero-record <Hero> <type> <id> [field ...] | hero-record <Hero> find <name prefix>");
    return 2;
}

string hero = args[0];
string exportFile = FindExport();

if (args[1] == "find")
{
    string prefix = string.Join(' ', args.Skip(2));
    foreach (var record in Records(exportFile))
    {
        if (!NamesHero(record, hero)) continue;
        string name = record["name"]?.GetValue<string>() ?? "";
        if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
        Console.WriteLine(new JsonObject
        {
            ["$type"] = record["$type"]?.DeepClone(),
            ["id"] = record["id"]?.DeepClone(),
            ["name"] = name,
        }.ToJsonString());
    }
    return 0;
}

string type = args[1];
if (!long.TryParse(args[2], out long id))
{
    Console.Error.WriteLine($"'{args[2]}' is not an id.");
    return 2;
}
string[] fields = args.Skip(3).ToArray();

foreach (var record in Records(exportFile))
{
    if (record["$type"]?.GetValue<string>() != type || record["id"]?.GetValue<long>() != id) continue;
    if (record["heroes"] is JsonArray heroes && heroes.Count > 0 && !NamesHero(record, hero))
    {
        Console.WriteLine($"{type} {id} is not {hero}'s.");
        return 1;
    }
    Console.WriteLine(fields.Length == 0
        ? new JsonArray(record.Select(entry => (JsonNode?)entry.Key).ToArray()).ToJsonString()
        : new JsonObject(record.Where(entry => fields.Contains(entry.Key))
            .Select(entry => KeyValuePair.Create(entry.Key, entry.Value?.DeepClone()))).ToJsonString());
    return 0;
}
Console.WriteLine($"{type} {id} is not in the export.");
return 1;

static bool NamesHero(JsonObject record, string hero) =>
    record["heroes"] is JsonArray heroes
    && heroes.Any(node => string.Equals(node?.GetValue<string>(), hero, StringComparison.OrdinalIgnoreCase));

static IEnumerable<JsonObject> Records(string exportFile)
{
    foreach (string line in File.ReadLines(exportFile))
    {
        if (line.Length == 0) continue;
        if (JsonNode.Parse(line) is JsonObject record) yield return record;
    }
}

static string FindExport()
{
    for (var directory = new DirectoryInfo(Environment.CurrentDirectory); directory != null; directory = directory.Parent)
    {
        var output = new DirectoryInfo(Path.Combine(directory.FullName, "output"));
        if (!output.Exists) continue;
        var export = output.GetDirectories("export-*")
            .Where(build => File.Exists(Path.Combine(build.FullName, "entities.jsonl"))
                && File.Exists(Path.Combine(build.FullName, "settings.json")))
            .OrderByDescending(build => long.TryParse(build.Name["export-".Length..], out long number) ? number : 0)
            .FirstOrDefault();
        if (export != null) return Path.Combine(export.FullName, "entities.jsonl");
    }
    throw new DirectoryNotFoundException("No output/export-<build> with entities.jsonl and settings.json above this folder.");
}
