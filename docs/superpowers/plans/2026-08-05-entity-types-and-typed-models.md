# Entity Types and Typed Models Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The extractor decides what type every row is, fills a model class of that type, and writes the run through an output format.

**Architecture:** A `TypeResolver` answers one question - what is this row - by asking the four reference maps `TooltipResolver` already holds, then the asset's folder, then the tag's own segments. `TooltipResolver.Resolve` keeps building the `Entity` it builds today; a projection turns that `Entity` plus its resolved type into a model class of that type. `ExportAllCommand` writes models through `IOutputFormat`, whose only implementation for now writes the JSONL the project writes today.

**Tech Stack:** C# 13 / .NET 10, xunit, System.Text.Json, CUE4Parse.

## Global Constraints

- Target framework `net10.0`, nullable enabled, implicit usings enabled.
- No polyfills for missing runtime functionality. Use the modern API directly.
- No em dashes or en dashes in code, comments or output.
- Never hardcode a list, id or data fact derivable from a canonical source. The canonical sources here are `output/export/vocabulary.json`, the tag tables, and the asset registry.
- Comments only where they carry significant value. Do not explain trivial code.
- Counts asserted in tests come from the spec `docs/superpowers/specs/2026-08-05-entity-types-models-and-postgres-design.md` section 3.4.

## Working Rhythm

**No commits.** Nothing in this plan commits. The working tree carries the whole change and the author commits when they choose.

**Build after every step.** Each step ends with `dotnet build src/FellowshipExtractor.slnx`, which must succeed before the next step starts. That is the only per-step verification.

**Test files are written but not run.** Tasks create the test files beside the code they cover, because a test written later against finished code tests what the code does rather than what it should do. They stay unrun until Task 11, which runs the suite once over the finished change.

**Two commands are not builds and are called out where they are needed.** `dotnet run --project src/FellowshipExtractor export-all` rewrites the export, and Task 11's test run reads it. Steps needing either say so.

## File Structure

| File | Responsibility |
|---|---|
| `src/FellowshipExtractor/Tooltip/EntityType.cs` | The closed set of types and the slug each carries |
| `src/FellowshipExtractor/Tooltip/TypeResolver.cs` | Deciding one row's type |
| `src/FellowshipExtractor/Tooltip/Documents/EntityDocument.cs` | The abstract base: `Id`, `Name` |
| `src/FellowshipExtractor/Tooltip/Documents/ItemDocuments.cs` | `Item`, `Mount`, `Currency`, `SetBonus`, `CompanionPet` |
| `src/FellowshipExtractor/Tooltip/Documents/AbilityDocuments.cs` | `Ability`, `Effect`, `Creature`, `EntityReference` |
| `src/FellowshipExtractor/Tooltip/Documents/TalentDocuments.cs` | `Talent`, `TalentSlot`, `Finesse` |
| `src/FellowshipExtractor/Tooltip/Documents/GemDocuments.cs` | `GemType`, `GemTier`, `GemTrait` |
| `src/FellowshipExtractor/Tooltip/Documents/PowerDocuments.cs` | `Trait`, `TraitPower`, `SetBonusPower`, `MountPower`, `RelicPower`, `WeaponPower`, `LegendaryStat` |
| `src/FellowshipExtractor/Tooltip/DocumentProjection.cs` | `Entity` plus an `EntityType` to a model class |
| `src/FellowshipExtractor/Tooltip/GeneratedEnums.g.cs` | Generator output: `ArmorType`, `PrimaryStat`, `Hero`, `GemTypeName`, `Rarity` |
| `src/FellowshipExtractor/Tooltip/EnumsCommand.cs` | Regenerates the file above and `enums.sql` |
| `src/FellowshipExtractor/Tooltip/VocabularyReader.cs` | Reads a vocabulary sidecar back |
| `src/FellowshipExtractor/Output/IOutputFormat.cs` | The write boundary |
| `src/FellowshipExtractor/Output/JsonlOutputFormat.cs` | Writes `entities.jsonl` and `vocabulary.json` |
| `src/FellowshipExtractor.Tests/EnumDriftTests.cs` | The enums still match the vocabulary |
| `src/FellowshipExtractor.Tests/TypeResolverTests.cs` | Per-type counts over the real export |
| `src/FellowshipExtractor.Tests/DocumentTests.cs` | Projection round-trips and required fields |
| `src/FellowshipExtractor.Tests/OutputFormatTests.cs` | What a JSONL line carries |

---

### Task 1: The enum generator and its committed output

**Files:**
- Create: `src/FellowshipExtractor/Tooltip/EnumsCommand.cs`
- Create: `src/FellowshipExtractor/Tooltip/VocabularyReader.cs`
- Create: `src/FellowshipExtractor/Tooltip/GeneratedEnums.g.cs` (generator output)
- Modify: `src/FellowshipExtractor.Api/EntityStore.cs` (`VocabularyFile`, `StatCalculationFile`)
- Modify: `src/FellowshipExtractor/Tooltip/GemDefinitions.cs`, `Vocabulary.cs`, `TooltipResolver.cs`, `Program.cs`
- Test: `src/FellowshipExtractor.Tests/EnumDriftTests.cs`

**Interfaces:**
- Consumes: `FellowshipExtractor.Api.VocabularyFile`, `HeroEntry(string Codename, string Name, string? ArmorType, string PrimaryStat)`, `RarityEntry(int Tier, string Name, string DisplayName, string GemDisplayName)`.
- Produces: `enum ArmorType`, `enum PrimaryStat`, `enum Hero`, `enum GemTypeName`, `enum Rarity` in namespace `FellowshipExtractor.Tooltip`. `EnumsCommand.RunAsync(ExtractorOptions)`, `EnumsCommand.Members(VocabularyFile)` returning `IReadOnlyDictionary<string, IReadOnlyList<string>>`, `EnumsCommand.Rarities(VocabularyFile)` returning `IReadOnlyList<(string Name, int Tier)>`. `VocabularyReader.Read(string path)`. `GemDefinitions.Types`.

- [ ] **Step 1: Declare the two vocabulary fields the generator reads**

`Vocabulary.WriteAsync` already writes `armorWeights`, but `StatCalculationFile` in `src/FellowshipExtractor.Api/EntityStore.cs` does not declare it, so it deserialises empty and `ArmorType` would be generated with no members. Add to `StatCalculationFile`, beside `SlotWeights`:

```csharp
    /// <summary>What each armour type is worth, keyed by the word the game prints.</summary>
    public IReadOnlyDictionary<string, double> ArmorWeights { get; init; } =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
```

Nothing writes `gemTypes` yet. Add to `VocabularyFile` in the same file:

```csharp
    /// <summary>The gem types the build ships, under the words it prints for them.</summary>
    public IReadOnlyList<string> GemTypes { get; init; } = [];
```

Build: `dotnet build src/FellowshipExtractor.slnx`

- [ ] **Step 2: Write the gem types into the sidecar**

`GemDefinitions` holds the types in a private `_byType`. Expose them, beside `TypeCount`:

```csharp
    /// <summary>The gem types the build ships, under the words it prints for them.</summary>
    public IReadOnlyList<string> Types => [.. _byType.Keys.Order(StringComparer.Ordinal)];
```

In `src/FellowshipExtractor/Tooltip/Vocabulary.cs`, add a `gemTypes` constructor parameter of `IReadOnlyList<string>`, store it as a public `GemTypes` property beside `RandomStatPool`, and add it to the anonymous object `WriteAsync` serialises, beside `rarities`:

```csharp
                gemTypes = GemTypes,
```

`TooltipResolver`'s constructor builds `Vocabulary` after `Gems`, so pass `Gems.Types` at that call.

Build: `dotnet build src/FellowshipExtractor.slnx`

- [ ] **Step 3: Write the vocabulary reader**

Create `src/FellowshipExtractor/Tooltip/VocabularyReader.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using FellowshipExtractor.Api;

namespace FellowshipExtractor.Tooltip;

/// <summary>Reads a vocabulary sidecar back. The extractor writes one and cannot otherwise read one.</summary>
public static class VocabularyReader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static VocabularyFile Read(string path) =>
        JsonSerializer.Deserialize<VocabularyFile>(File.ReadAllText(path), Options)
        ?? throw new InvalidOperationException($"{path} deserialised to nothing.");
}
```

Build: `dotnet build src/FellowshipExtractor.slnx`

- [ ] **Step 4: Write the generator**

Create `src/FellowshipExtractor/Tooltip/EnumsCommand.cs`:

```csharp
using System.Text;
using FellowshipExtractor.Api;

namespace FellowshipExtractor.Tooltip;

/// <summary>
/// The enumerated fields, emitted as C# and as Postgres from one reading of the vocabulary, so the
/// two can never state different members.
///
/// The output is committed rather than generated at build time because the vocabulary it reads is
/// itself something a run produces, and a build that cannot run the extractor still has to compile.
/// <see cref="Members"/> is what the drift test compares the committed file against.
/// </summary>
public static class EnumsCommand
{
    private const string FileName = "GeneratedEnums.g.cs";
    private const string SqlName = "enums.sql";

    /// <summary>
    /// Every bare enum and the members the vocabulary states for it. Sorted, because the vocabulary
    /// lists heroes in the build's own order and a member's position is a value a reader could come
    /// to depend on.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Members(VocabularyFile vocabulary) =>
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["ArmorType"] = Sorted(vocabulary.StatCalculation.ArmorWeights.Keys),
            ["PrimaryStat"] = Sorted(vocabulary.Heroes.Select(hero => hero.PrimaryStat)),
            ["Hero"] = Sorted(vocabulary.Heroes.Select(hero => hero.Name)),
            ["GemTypeName"] = Sorted(vocabulary.GemTypes),
        };

    /// <summary>
    /// The rarity ladder, the one enum whose members carry a value. A rung is filed under one word,
    /// prints another on an item and a third on a gem, so the ordinal is the only thing that names a
    /// rung once. The member is the filed word and the value is the shipped ordinal.
    /// </summary>
    public static IReadOnlyList<(string Name, int Tier)> Rarities(VocabularyFile vocabulary) =>
        [.. vocabulary.Rarities.OrderBy(rarity => rarity.Tier).Select(rarity => (rarity.Name, rarity.Tier))];

    private static IReadOnlyList<string> Sorted(IEnumerable<string> members) =>
        [.. members.Where(member => member.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    public static async Task RunAsync(ExtractorOptions options)
    {
        var folder = Path.Combine(options.ResolvedOutputDir, "export");
        var vocabulary = VocabularyReader.Read(Path.Combine(folder, Vocabulary.FileName));
        var members = Members(vocabulary);

        var source = new StringBuilder();
        source.AppendLine("// Generated by `dotnet run --project src/FellowshipExtractor enums`. Do not edit.");
        source.AppendLine();
        source.AppendLine("namespace FellowshipExtractor.Tooltip;");

        var sql = new StringBuilder();
        sql.AppendLine("-- Generated by `dotnet run --project src/FellowshipExtractor enums`. Do not edit.");

        foreach (var (name, values) in members)
        {
            source.AppendLine();
            source.AppendLine($"public enum {name}");
            source.AppendLine("{");
            foreach (var value in values) source.AppendLine($"    {value},");
            source.AppendLine("}");

            sql.AppendLine(
                $"CREATE TYPE {SnakeCase(name)} AS ENUM ({string.Join(", ", values.Select(v => $"'{v}'"))});");
        }

        var rarities = Rarities(vocabulary);
        source.AppendLine();
        source.AppendLine("public enum Rarity");
        source.AppendLine("{");
        foreach (var (member, tier) in rarities) source.AppendLine($"    {member} = {tier},");
        source.AppendLine("}");

        sql.AppendLine(
            $"CREATE TYPE rarity AS ENUM ({string.Join(", ", rarities.Select(r => $"'{r.Name}'"))});");

        var target = Path.Combine("src", "FellowshipExtractor", "Tooltip", FileName);
        await File.WriteAllTextAsync(target, source.ToString());
        await File.WriteAllTextAsync(Path.Combine(folder, SqlName), sql.ToString());

        Console.WriteLine($"wrote {target}");
        foreach (var (name, values) in members) Console.WriteLine($"  {name}: {values.Count} members");
        Console.WriteLine($"  Rarity: {rarities.Count} members");
    }

    private static string SnakeCase(string name)
    {
        var text = new StringBuilder();
        for (var at = 0; at < name.Length; at++)
        {
            if (at > 0 && char.IsUpper(name[at])) text.Append('_');
            text.Append(char.ToLowerInvariant(name[at]));
        }
        return text.ToString();
    }
}
```

Build: `dotnet build src/FellowshipExtractor.slnx`

- [ ] **Step 5: Add the CLI case**

In `src/FellowshipExtractor/Program.cs`, beside `case "export-all":`:

```csharp
    case "enums":
        await FellowshipExtractor.Tooltip.EnumsCommand.RunAsync(options);
        break;
```

And in the help text, beside the `export-all` line:

```
  enums                 regenerate the enumerated types from the exported vocabulary
```

Build: `dotnet build src/FellowshipExtractor.slnx`

- [ ] **Step 6: Run the export, then the generator**

Run: `dotnet run --project src/FellowshipExtractor export-all`
Run: `dotnet run --project src/FellowshipExtractor enums`

Expected: `wrote src/FellowshipExtractor/Tooltip/GeneratedEnums.g.cs`, with member counts 4, 3, 12, 6 and 7. If `ArmorType` reports 0, Step 1 did not take.

Build: `dotnet build src/FellowshipExtractor.slnx`

- [ ] **Step 7: Write the drift test file**

Create `src/FellowshipExtractor.Tests/EnumDriftTests.cs`. It is not run until Task 11.

```csharp
using FellowshipExtractor.Tooltip;

namespace FellowshipExtractor.Tests;

/// <summary>
/// The enums name words the build states. A build that adds a hero or renames an armour type has to
/// fail here rather than be absorbed, because every one of these members is committed source that
/// nothing else re-derives.
/// </summary>
public sealed class EnumDriftTests
{
    [Fact]
    public void MembersAreTheSetTheVocabularyStates()
    {
        var expected = EnumsCommand.Members(Export.Loaded.Vocabulary);

        Assert.Equal(expected["ArmorType"], Enum.GetNames<ArmorType>());
        Assert.Equal(expected["PrimaryStat"], Enum.GetNames<PrimaryStat>());
        Assert.Equal(expected["Hero"], Enum.GetNames<Hero>());
        Assert.Equal(expected["GemTypeName"], Enum.GetNames<GemTypeName>());
    }

    [Fact]
    public void ARarityMemberCarriesItsShippedOrdinal()
    {
        foreach (var (name, tier) in EnumsCommand.Rarities(Export.Loaded.Vocabulary))
        {
            Assert.Equal(tier, (int)Enum.Parse<Rarity>(name));
        }
    }

    [Fact]
    public void HeroesAreNamedAsThePlayerReadsThem()
    {
        // Bowguy is Elarion's codename. A codename that resolves is one a reader can find, which is
        // the failure EntityStore already closed for the hero query parameter.
        Assert.Contains("Elarion", Enum.GetNames<Hero>());
        Assert.DoesNotContain("Bowguy", Enum.GetNames<Hero>());
    }
}
```

Build: `dotnet build src/FellowshipExtractor.slnx`

---

### Task 2: The type set

**Files:**
- Create: `src/FellowshipExtractor/Tooltip/EntityType.cs`

**Interfaces:**
- Produces: `enum EntityType` with the 21 members named in the spec's 3.4, and `EntityTypes.Slug(EntityType)` returning the hyphenated lower-case name.

- [ ] **Step 1: Write the type set**

Create `src/FellowshipExtractor/Tooltip/EntityType.cs`:

```csharp
using System.Text;

namespace FellowshipExtractor.Tooltip;

/// <summary>
/// What a row is. The shipped tag table is not this: ItemID holds items, mounts, currencies, gem
/// types, gem tiers and set bonuses, and CRCharacterAugmentationAsset holds nine things of which a
/// hero's talent is one.
/// </summary>
public enum EntityType
{
    Ability,
    CompanionPet,
    Creature,
    Currency,
    Effect,
    Finesse,
    GemTier,
    GemTrait,
    GemType,
    Item,
    LegendaryStat,
    Mount,
    MountPower,
    RelicPower,
    SetBonus,
    SetBonusPower,
    Talent,
    TalentSlot,
    Trait,
    TraitPower,
    WeaponPower,
}

public static class EntityTypes
{
    /// <summary>
    /// The route segment a type is addressed by: its name, lower cased, with a hyphen where the name
    /// changes word. Derived rather than listed, so a type added to the enum is addressable without
    /// a second edit.
    /// </summary>
    public static string Slug(EntityType type)
    {
        var name = type.ToString();
        var slug = new StringBuilder();

        for (var at = 0; at < name.Length; at++)
        {
            if (at > 0 && char.IsUpper(name[at])) slug.Append('-');
            slug.Append(char.ToLowerInvariant(name[at]));
        }

        return slug.ToString();
    }
}
```

Build: `dotnet build src/FellowshipExtractor.slnx`

---

### Task 3: The type resolver

**Files:**
- Create: `src/FellowshipExtractor/Tooltip/TypeResolver.cs`
- Modify: `src/FellowshipExtractor/Tooltip/ItemTraits.cs`, `ItemSetBonuses.cs`, `TooltipResolver.cs`, `Entity.cs`

**Interfaces:**
- Consumes: `TagRecord(string Table, string Name, int NetId, int Flag)`, `TalentTrees.ForAugmentation(string)`, `GemDefinitions.ForAugmentation(string)`, `TalentTrees.SlotTable`, `TalentTrees.AugmentationTable`, `ItemTraits.Table`.
- Produces: `TypeResolver.Of(TagRecord tag, Entity entity, string? packageName)` returning `EntityType?`; `TooltipResolver.Types`; `ItemTraits.PointsAt(int)`; `ItemSetBonuses.PointsAt(int)`; `Entity.Type`.

- [ ] **Step 1: Add the two back-reference lookups**

`ItemTraits` and `ItemSetBonuses` are keyed by their own tag today and hold an `Augmentation` netId per row. Add the inverse to each.

In `src/FellowshipExtractor/Tooltip/ItemTraits.cs`, beside `ForTag`:

```csharp
    private readonly HashSet<int> _augmentations = [];

    /// <summary>Whether an augmentation id is the mechanics behind one of these rows.</summary>
    public bool PointsAt(int netId) => _augmentations.Contains(netId);
```

Fill `_augmentations` in the constructor from the same loop that fills `_byTag`, adding each row's `Augmentation` where it has one. Do the same in `src/FellowshipExtractor/Tooltip/ItemSetBonuses.cs`.

Build: `dotnet build src/FellowshipExtractor.slnx`

- [ ] **Step 2: Write the resolver**

Create `src/FellowshipExtractor/Tooltip/TypeResolver.cs`:

```csharp
namespace FellowshipExtractor.Tooltip;

/// <summary>
/// What type a row is, asked of the build rather than of the row's own spelling.
///
/// A reference is asked before a folder because a folder holds more than the assets of one type:
/// /Game/Abilities/Talents/Mosse ships 33 assets and 18 of them are the talents Mosse's slots point
/// at. Reading the folder alone counts 467 talents where the build has 216.
/// </summary>
public sealed class TypeResolver(
    TalentTrees talents,
    GemDefinitions gems,
    ItemTraits traits,
    ItemSetBonuses setBonuses)
{
    /// <summary>The folders that name a type, for the types no reference points at.</summary>
    private static readonly (string Folder, EntityType Type)[] Folders =
    [
        ("/Game/Items/MountAugmentations/", EntityType.MountPower),
        ("/Game/Items/RelicAugmentations/", EntityType.RelicPower),
        ("/Game/Abilities/RelicPowers/", EntityType.RelicPower),
        ("/Game/Abilities/Weapons/", EntityType.WeaponPower),
        ("/Game/Abilities/Finesses/", EntityType.Finesse),
        ("/Game/Abilities/LegendaryStats/", EntityType.LegendaryStat),
    ];

    /// <summary>What an ItemID tag's second segment names.</summary>
    private static readonly Dictionary<string, EntityType> ItemSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        ["HeroItem"] = EntityType.Item,
        ["Mount"] = EntityType.Mount,
        ["Currency"] = EntityType.Currency,
        ["SetBonus"] = EntityType.SetBonus,
        ["CompanionPet"] = EntityType.CompanionPet,
    };

    /// <summary>The tables holding exactly one type.</summary>
    private static readonly Dictionary<string, EntityType> Tables = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GameplayAbility"] = EntityType.Ability,
        ["GameplayEffect"] = EntityType.Effect,
        ["CRAICharacter"] = EntityType.Creature,
        [ItemTraits.Table] = EntityType.Trait,
    };

    /// <summary>The type a row is, or null where no rule reaches it and the row is not written.</summary>
    public EntityType? Of(TagRecord tag, Entity entity, string? packageName)
    {
        if (tag.Table.Equals(TalentTrees.AugmentationTable, StringComparison.OrdinalIgnoreCase))
        {
            return Augmentation(tag, entity, packageName);
        }

        if (tag.Table.Equals("ItemID", StringComparison.OrdinalIgnoreCase)) return Item(tag, entity);

        // A slot with no grid position is an id the trees never filled. 21 of the 237 are in that
        // state and the build places no talent at any of them.
        if (tag.Table.Equals(TalentTrees.SlotTable, StringComparison.OrdinalIgnoreCase))
        {
            return entity.TalentRow is null ? null : EntityType.TalentSlot;
        }

        return Tables.TryGetValue(tag.Table, out var table) ? table : null;
    }

    private EntityType? Augmentation(TagRecord tag, Entity entity, string? packageName)
    {
        if (talents.ForAugmentation(tag.Name) is not null) return EntityType.Talent;
        if (entity.GemNode is not null || gems.ForAugmentation(tag.Name) is not null) return EntityType.GemTrait;
        if (traits.PointsAt(tag.NetId)) return EntityType.TraitPower;
        if (setBonuses.PointsAt(tag.NetId)) return EntityType.SetBonusPower;

        if (packageName is null) return null;

        foreach (var (folder, type) in Folders)
        {
            if (packageName.Contains(folder, StringComparison.OrdinalIgnoreCase)) return type;
        }

        return null;
    }

    private static EntityType? Item(TagRecord tag, Entity entity)
    {
        var segments = tag.Name.Split('.');
        if (segments.Length < 2) return null;

        // The gem segment holds both the six types and their four tiers each, and a tier is the
        // thing that carries one.
        if (segments[1].Equals("GemType", StringComparison.OrdinalIgnoreCase))
        {
            if (entity.GemTier is not null) return EntityType.GemTier;
            return tag.Name.EndsWith(".Invalid", StringComparison.OrdinalIgnoreCase) ? null : EntityType.GemType;
        }

        return ItemSegments.TryGetValue(segments[1], out var type) ? type : null;
    }
}
```

`Dictionary.GetValueOrDefault` on a `Dictionary<string, EntityType>` yields the enum's zero member rather than null, which is why both lookups use `TryGetValue`.

Build: `dotnet build src/FellowshipExtractor.slnx`

- [ ] **Step 3: Record the type on the entity**

In `src/FellowshipExtractor/Tooltip/Entity.cs`, beside `Table`:

```csharp
    /// <summary>What this row is. Set on a top-level entity and absent from anything nested inside one.</summary>
    public EntityType? Type { get; init; }
```

Build: `dotnet build src/FellowshipExtractor.slnx`

- [ ] **Step 4: Expose and call the resolver**

In `src/FellowshipExtractor/Tooltip/TooltipResolver.cs`, beside the other public maps:

```csharp
    public TypeResolver Types { get; }
```

Assign it at the end of the constructor, after `Talents`, `Gems`, `Traits` and `SetBonuses` are built:

```csharp
        Types = new TypeResolver(Talents, Gems, Traits, SetBonuses);
```

In `Resolve`, immediately after the `if (tag.Table.Equals(TalentTrees.AugmentationTable, ...))` block that applies `WithTalent` and `WithGemNode`, and before the name and icon inheritance blocks:

```csharp
        entity = entity with { Type = Types.Of(tag, entity, asset?.PackageName.Text) };
```

`asset` is already in scope, from `var asset = FindAsset(tag.Name);` at the top of the method. The line sits after the gem and talent steps because two of the four references read what those steps fold onto the record.

Build: `dotnet build src/FellowshipExtractor.slnx`

---

### Task 4: The drop rules

**Files:**
- Modify: `src/FellowshipExtractor/Tooltip/ExportAllCommand.cs`

**Interfaces:**
- Consumes: `Entity.Type` from Task 3.
- Produces: `ExportAllCommand.Branches(IEnumerable<TagRecord>)` returning `IReadOnlySet<string>`; `ExportAllCommand.Written(Entity, IReadOnlySet<string>)` returning `bool`.

- [ ] **Step 1: Write the drop rules**

In `src/FellowshipExtractor/Tooltip/ExportAllCommand.cs`, replace `NullMembers`, `Content` and `NullMember` with:

```csharp
    /// <summary>
    /// Every tag that another tag hangs beneath. A branch is not an entity, so the 807 that also say
    /// nothing are not written; a branch that does say something is, which is what keeps the six gem
    /// types, each carrying four tiers beneath it.
    /// </summary>
    public static IReadOnlySet<string> Branches(IEnumerable<TagRecord> tags)
    {
        var branches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tag in tags)
        {
            var segments = tag.Name.Split('.');
            for (var at = 1; at < segments.Length; at++)
            {
                branches.Add(string.Join('.', segments[..at]));
            }
        }

        return branches;
    }

    /// <summary>
    /// Everything an entity can say beyond which tag it is, read off the record so a field added
    /// later counts without anyone remembering to list it.
    /// </summary>
    private static readonly PropertyInfo[] Content =
    [
        .. typeof(Entity).GetProperties()
            .Where(p => p.Name is not (nameof(Entity.Table) or nameof(Entity.NetId) or nameof(Entity.Name)
                or nameof(Entity.Type))),
    ];

    private static bool Says(Entity entity) => Array.Exists(Content, p => p.GetValue(entity) is not null);

    /// <summary>Whether a row reaches the store at all.</summary>
    public static bool Written(Entity entity, IReadOnlySet<string> branches) =>
        entity.Type is not null &&
        !(entity.Name is { } name && branches.Contains(name) && !Says(entity));
```

Build: `dotnet build src/FellowshipExtractor.slnx`

- [ ] **Step 2: Apply them in the export loop**

Before the loop in `RunAsync`:

```csharp
        var branches = Branches(resolver.Tags);
```

Replace the `if (NullMember(record))` block with:

```csharp
                if (!Written(record, branches))
                {
                    var reason = record.Type is null ? "no type" : "empty branch";
                    dropped[reason] = dropped.GetValueOrDefault(reason) + 1;
                    continue;
                }
```

Change `dropped` from `List<string>` to `SortedDictionary<string, int>` and print it as counts per reason. Change the duplicate-key guard to key on the type rather than the table:

```csharp
                var key = $"{record.Type}/{record.NetId}/{record.ArmorType}/{record.PrimaryStat}/{hero}";
```

Build: `dotnet build src/FellowshipExtractor.slnx`

---

### Task 5: The document base and the simple types

**Files:**
- Create: `src/FellowshipExtractor/Tooltip/Documents/EntityDocument.cs`
- Create: `src/FellowshipExtractor/Tooltip/Documents/AbilityDocuments.cs`

**Interfaces:**
- Produces: `abstract class EntityDocument { int Id; string? Name; }`, `Ability`, `Effect`, `Creature`, `record EntityReference(EntityType Type, int Id, string? Name)`.

- [ ] **Step 1: Write the base**

Create `src/FellowshipExtractor/Tooltip/Documents/EntityDocument.cs`:

```csharp
namespace FellowshipExtractor.Tooltip.Documents;

/// <summary>
/// One row as a reader meets it. The runtime class is the row's type, so nothing here restates it.
///
/// Two fields, because two are what every type has. A row the extractor cannot read yet carries the
/// id and nothing else, which is what makes the extraction worklist countable.
/// </summary>
public abstract class EntityDocument
{
    public required int Id { get; init; }

    /// <summary>The name a reader is shown, and null where the build prints none they would read.</summary>
    public string? Name { get; init; }
}
```

Build: `dotnet build src/FellowshipExtractor.slnx`

- [ ] **Step 2: Write the three simple types**

Create `src/FellowshipExtractor/Tooltip/Documents/AbilityDocuments.cs`:

```csharp
namespace FellowshipExtractor.Tooltip.Documents;

/// <summary>
/// Something a hero or a creature does. Every field is optional because an ability states the times,
/// costs and distances it has and no others: 18% of them state a cast time, 33% a range.
/// </summary>
public sealed class Ability : EntityDocument
{
    public string? Description { get; init; }
    public IReadOnlyList<string>? Detail { get; init; }
    public string? Icon { get; init; }

    public double? CastTime { get; init; }
    public double? ChannelTime { get; init; }
    public double? ChannelTick { get; init; }
    public double? Cooldown { get; init; }
    public double? Charges { get; init; }
    public double? Cost { get; init; }

    /// <summary>What the cost is paid in: Mana, Spirit, Fury, Burning Ember.</summary>
    public string? Resource { get; init; }

    public double? Range { get; init; }
    public double? Radius { get; init; }
    public double? Duration { get; init; }
    public double? Period { get; init; }

    /// <summary>Set where the ability states that its own times run faster with the wearer's haste.</summary>
    public bool? ScalesWithHaste { get; init; }

    public IReadOnlyList<string>? ScalesOff { get; init; }
    public IReadOnlyList<Placeholder>? Placeholders { get; init; }
    public IReadOnlyList<string>? Schools { get; init; }
}

/// <summary>Something an ability applies. Anonymous in the build unless an applier names it.</summary>
public sealed class Effect : EntityDocument
{
    public string? Description { get; init; }
    public string? Icon { get; init; }

    public double? Duration { get; init; }
    public double? Period { get; init; }
    public double? Cooldown { get; init; }
    public double? Range { get; init; }
    public double? Radius { get; init; }
    public double? Cost { get; init; }

    /// <summary>Set where the effect keeps its period fixed however much haste the wearer holds.</summary>
    public bool? TickrateIgnoresHaste { get; init; }

    public IReadOnlyList<string>? ScalesOff { get; init; }
    public IReadOnlyList<Placeholder>? Placeholders { get; init; }
    public IReadOnlyList<string>? Schools { get; init; }

    public EntityReference? NameBorrowedFrom { get; init; }
    public EntityReference? ArtBorrowedFrom { get; init; }
}

/// <summary>A non-player character.</summary>
public sealed class Creature : EntityDocument
{
    public string? Icon { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }
}

/// <summary>Another row, addressed the way a reader would ask for it again.</summary>
public sealed record EntityReference(EntityType Type, int Id, string? Name);
```

Build: `dotnet build src/FellowshipExtractor.slnx`

---

### Task 6: The item and gem documents

**Files:**
- Create: `src/FellowshipExtractor/Tooltip/Documents/ItemDocuments.cs`
- Create: `src/FellowshipExtractor/Tooltip/Documents/GemDocuments.cs`

**Interfaces:**
- Consumes: `EntityDocument` from Task 5; `ArmorType`, `PrimaryStat`, `Hero`, `GemTypeName`, `Rarity` from Task 1; `StatSlot`, `Placeholder` from `Entity.cs`.
- Produces: `Item`, `Mount`, `Currency`, `SetBonus`, `CompanionPet`, `GemType`, `GemTier`, `GemTrait`.

- [ ] **Step 1: Write the item documents**

Create `src/FellowshipExtractor/Tooltip/Documents/ItemDocuments.cs`:

```csharp
namespace FellowshipExtractor.Tooltip.Documents;

/// <summary>
/// Something a hero wears or carries. The three discriminators are what make one id several items:
/// id 5146 is a Webbed Robe in cloth, a Tribal Hauberk in leather and a Venom Pitted Chestpiece in
/// plate, and 188 ids read differently per hero this way.
/// </summary>
public sealed class Item : EntityDocument
{
    public ArmorType? ArmorType { get; init; }
    public PrimaryStat? PrimaryStat { get; init; }

    /// <summary>The sole hero this reading is for, and null where it is for several.</summary>
    public Hero? Hero { get; init; }

    /// <summary>Every hero the row is for, which is what a reader is shown.</summary>
    public IReadOnlyList<Hero>? Heroes { get; init; }

    public string? Slot { get; init; }
    public bool? GemSlot { get; init; }
    public bool? UniqueEquipped { get; init; }

    /// <summary>The rung every copy of this item is, and none where the drop decides it.</summary>
    public Rarity? Rarity { get; init; }

    public IReadOnlyList<string>? SetBonus { get; init; }
    public IReadOnlyList<StatSlot>? Stats { get; init; }

    public string? Description { get; init; }
    public string? Icon { get; init; }

    /// <summary>The art at each rarity it differs at, keyed by the word the game prints for the rung.</summary>
    public IReadOnlyDictionary<string, string>? IconByRarity { get; init; }

    /// <summary>The relic or weapon power this item grants.</summary>
    public int? GrantsId { get; init; }

    /// <summary>The legendary stat lines this item carries. 37 items carry three each.</summary>
    public IReadOnlyList<int>? LegendaryStatIds { get; init; }
}

/// <summary>A mount a player rides. Its mechanics live in the mount power it pairs with.</summary>
public sealed class Mount : EntityDocument
{
    public string? Description { get; init; }
    public string? Icon { get; init; }
}

public sealed class Currency : EntityDocument
{
    public string? Description { get; init; }
    public string? Icon { get; init; }
}

/// <summary>
/// A bonus for wearing several pieces of one set. Its mechanics live in the set bonus power it
/// points at, and 3 of the rows are gated to a role rather than offered to everyone.
/// </summary>
public sealed class SetBonus : EntityDocument
{
    public required int Pieces { get; init; }
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public int? PowerId { get; init; }
    public IReadOnlyList<string>? AllowedRoles { get; init; }
    public IReadOnlyList<PrimaryStat>? AllowedAttributes { get; init; }
}

public sealed class CompanionPet : EntityDocument
{
    public string? Description { get; init; }
    public string? Icon { get; init; }
}
```

`Item.Rarity` is not the `RuleSet` the design strips. A rule set is an internal spelling and goes;
the rung a reader is shown stays, and for 37 item rows the build fixes it. Those 37 are the rows
whose rule set is `Legendary`, they are exactly the rows carrying `legendaryStats`, and every other
item leaves its rarity to the drop. `StatMagnitudes` answers a request naming no item level at 315,
and at 360 for an item fixed at Legendary, so this field is what that answer reads. The design for it
is `docs/superpowers/specs/2026-08-05-item-level-rarity-and-tempering-design.md`.

Build: `dotnet build src/FellowshipExtractor.slnx`

- [ ] **Step 2: Write the gem documents**

Create `src/FellowshipExtractor/Tooltip/Documents/GemDocuments.cs`:

```csharp
namespace FellowshipExtractor.Tooltip.Documents;

/// <summary>
/// One of the six gem types. A socketed gem adds its tier's power to this type's running total, and
/// the type's ten-node ladder unlocks against that total.
/// </summary>
public sealed class GemType : EntityDocument
{
    public required GemTypeName Gem { get; init; }

    /// <summary>The attribute this type accumulates into.</summary>
    public required string Attribute { get; init; }

    public string? Icon { get; init; }

    /// <summary>What the type keeps giving past the top of its ladder.</summary>
    public string? OverflowDescription { get; init; }

    public IReadOnlyList<Placeholder>? OverflowPlaceholders { get; init; }
}

/// <summary>
/// One of a gem type's four sizes. All 24 state all four of these, which is why none is optional,
/// and they are the only rows in the corpus stating a rarity.
/// </summary>
public sealed class GemTier : EntityDocument
{
    public required GemTypeName Type { get; init; }

    /// <summary>Which of the four shipped sizes this is, 1 Small to 4 Flawless.</summary>
    public required int Tier { get; init; }

    /// <summary>The power a gem of this tier contributes to its type's total when socketed.</summary>
    public required int Power { get; init; }

    public required Rarity Rarity { get; init; }
    public string? Icon { get; init; }
}

/// <summary>One of the ten rungs on a gem type's ladder, unlocked by accumulated power.</summary>
public sealed class GemTrait : EntityDocument
{
    public required GemTypeName Type { get; init; }

    /// <summary>Where on its type's ten-step ladder this sits, 1 to 10.</summary>
    public required int Node { get; init; }

    public required int RequiredPower { get; init; }

    /// <summary>The weaker node this takes the place of, on the upper half of every ladder.</summary>
    public int? ReplacesId { get; init; }

    public string? Description { get; init; }
    public string? Icon { get; init; }
    public IReadOnlyList<Placeholder>? Placeholders { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }
}
```

Build: `dotnet build src/FellowshipExtractor.slnx`

---

### Task 7: The talent and power documents

**Files:**
- Create: `src/FellowshipExtractor/Tooltip/Documents/TalentDocuments.cs`
- Create: `src/FellowshipExtractor/Tooltip/Documents/PowerDocuments.cs`

**Interfaces:**
- Consumes: `EntityDocument` from Task 5; `Hero` from Task 1; `Placeholder`, `RankRun` from `Entity.cs`.
- Produces: `Talent`, `TalentSlot`, `Finesse`, `Trait`, `TraitPower`, `SetBonusPower`, `MountPower`, `RelicPower`, `WeaponPower`, `LegendaryStat`.

- [ ] **Step 1: Write the talent documents**

Create `src/FellowshipExtractor/Tooltip/Documents/TalentDocuments.cs`:

```csharp
namespace FellowshipExtractor.Tooltip.Documents;

/// <summary>
/// A hero's talent. 215 of the 216 live in an augmentation a slot points at; Macabre Stratagem
/// lives on the slot row itself, Mara's row 4 column 1.
/// </summary>
public sealed class Talent : EntityDocument
{
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public IReadOnlyList<Placeholder>? Placeholders { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }

    public double? Cooldown { get; init; }
    public double? Duration { get; init; }
    public double? Range { get; init; }
    public double? Radius { get; init; }
    public double? Cost { get; init; }
}

/// <summary>
/// One position in one hero's tree. 18 for each of the 12 heroes, each naming exactly one hero and
/// exactly one talent, which is why every field here is required.
/// </summary>
public sealed class TalentSlot : EntityDocument
{
    public required Hero Hero { get; init; }

    /// <summary>Where it sits in the grid, 1 to 6 down and 1 to 3 across.</summary>
    public required int Row { get; init; }

    public required int Column { get; init; }

    /// <summary>What it costs to take, 1 to 3.</summary>
    public required int Cost { get; init; }

    public required int TalentId { get; init; }
}

/// <summary>A finesse: The Herald, The Trickster, The Sinister. All 14 carry a name.</summary>
public sealed class Finesse : EntityDocument
{
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public IReadOnlyList<Placeholder>? Placeholders { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }
}
```

Build: `dotnet build src/FellowshipExtractor.slnx`

- [ ] **Step 2: Write the power documents**

Create `src/FellowshipExtractor/Tooltip/Documents/PowerDocuments.cs`:

```csharp
namespace FellowshipExtractor.Tooltip.Documents;

/// <summary>
/// A trait an item rolls. A weapon trait upgrades four times and a necklace trait once, and the
/// rank progressions its description prints are the only tie between the printed digits and the
/// numbers the game uses.
/// </summary>
public sealed class Trait : EntityDocument
{
    /// <summary>The category under the name the game prints. The Major tag ships as Master.</summary>
    public string? Category { get; init; }

    /// <summary>How far it upgrades: 4 on every weapon trait, 1 on every necklace trait.</summary>
    public int? MaxRank { get; init; }

    /// <summary>The weight it is rolled at against the others in its category.</summary>
    public int? RandomChance { get; init; }

    public int? PowerId { get; init; }
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public IReadOnlyList<RankRun>? RankRuns { get; init; }
}

/// <summary>The mechanics behind a trait.</summary>
public sealed class TraitPower : EntityDocument
{
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public double? Cooldown { get; init; }
    public double? Duration { get; init; }
    public IReadOnlyList<Placeholder>? Placeholders { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }
}

/// <summary>The mechanics behind a set bonus.</summary>
public sealed class SetBonusPower : EntityDocument
{
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public double? Cooldown { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }
}

/// <summary>What a mount gives its rider.</summary>
public sealed class MountPower : EntityDocument
{
    public string? Description { get; init; }
    public string? Icon { get; init; }
}

/// <summary>An ability a relic grants. 11 of them, all named.</summary>
public sealed class RelicPower : EntityDocument
{
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public double? CastTime { get; init; }
    public double? Cooldown { get; init; }
    public double? Duration { get; init; }
    public double? Range { get; init; }
    public bool? ScalesWithHaste { get; init; }
    public IReadOnlyList<string>? Schools { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }
}

/// <summary>An ability a weapon grants: Al'zerac's Shackle, Voidbringer's Touch. 12, all named.</summary>
public sealed class WeaponPower : EntityDocument
{
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public double? CastTime { get; init; }
    public double? ChannelTime { get; init; }
    public double? ChannelTick { get; init; }
    public double? Cooldown { get; init; }
    public double? Duration { get; init; }
    public double? Period { get; init; }
    public double? Range { get; init; }
    public double? Radius { get; init; }
    public bool? ScalesWithHaste { get; init; }
    public IReadOnlyList<string>? Schools { get; init; }
    public IReadOnlyList<Placeholder>? Placeholders { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }
}

/// <summary>A legendary's bonus stat line: Mote of Creation, Blood of the Undying.</summary>
public sealed class LegendaryStat : EntityDocument
{
    public string? Description { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }
}
```

Build: `dotnet build src/FellowshipExtractor.slnx`

---

### Task 8: The pointers, and the talent that lives on its slot

**Files:**
- Modify: `src/FellowshipExtractor/Tooltip/Entity.cs`, `TooltipResolver.cs`, `TalentTrees.cs`

**Interfaces:**
- Consumes: `AugmentationIds.For(string? assetPath)`.
- Produces: `Entity.GrantsId`, `Entity.LegendaryStatIds`, `Entity.SlotBorneTalent`.

- [ ] **Step 1: Add the three fields**

In `src/FellowshipExtractor/Tooltip/Entity.cs`, beside `Grants`:

```csharp
    /// <summary>
    /// The relic or weapon power this item grants, by id. The grant resolves through
    /// <c>LoadAugmentation</c> and every augmentation exports as a line of its own, so the reference
    /// is the whole of what an item needs to carry and the ability is not nested twice.
    /// </summary>
    public int? GrantsId { get; init; }

    /// <summary>The legendary stat lines this item carries, by id.</summary>
    public IReadOnlyList<int>? LegendaryStatIds { get; init; }

    /// <summary>
    /// Set on the one talent slot carrying its talent's text rather than pointing at an augmentation.
    /// Mara's row 4 column 1 is Macabre Stratagem, and without this the corpus holds 215 talents for
    /// 216 slots.
    /// </summary>
    public bool? SlotBorneTalent { get; init; }
```

Build: `dotnet build src/FellowshipExtractor.slnx`

- [ ] **Step 2: Record the ids as the grants resolve**

`ResolveGranted` and `LegendaryStats` both call `LoadAugmentation(assetPath, icons)` and have the path in hand. `AugmentationIds.For(path)` turns that path into the id. In `src/FellowshipExtractor/Tooltip/TooltipResolver.cs`, add beside `LegendaryStats`:

```csharp
    /// <summary>A legendary's bonus stats, by the id each augmentation exports under.</summary>
    private List<int> LegendaryStatIds(UObject cdo) =>
        [.. PropertyWalk.Walk(cdo)
            .Where(p => p.Path.StartsWith("LegendaryStats[", StringComparison.Ordinal))
            .Select(p => PropertyWalk.AsString(p.Value))
            .OfType<string>()
            .Select(_augmentationIds.For)
            .OfType<int>()];
```

In the `body with` initialiser, beside `LegendaryStats`:

```csharp
            LegendaryStatIds = cdo is null ? null : Empty(LegendaryStatIds(cdo)),
```

Have `ResolveGranted` also report the augmentation path it resolved, and set `GrantsId = _augmentationIds.For(path)` in the same initialiser. Keep `Grants` and `LegendaryStats` on `Entity` for now: `WebDbCommand` and the API still read them, and Plan 4 removes them.

`_augmentationIds` is built in the constructor already for `Gems`, `Traits` and `SetBonuses`. Promote it to a field if it is currently a local.

Build: `dotnet build src/FellowshipExtractor.slnx`

- [ ] **Step 3: Mark the slot-borne talent**

In `src/FellowshipExtractor/Tooltip/TalentTrees.cs`, `Expand` already chooses the slot's own text where `talent.Augmentation is null`. Set the flag in the same initialiser:

```csharp
                SlotBorneTalent = talent.Augmentation is null ? true : null,
```

Build: `dotnet build src/FellowshipExtractor.slnx`

---

### Task 9: The projection

**Files:**
- Create: `src/FellowshipExtractor/Tooltip/DocumentProjection.cs`

**Interfaces:**
- Consumes: every document class from Tasks 5 to 7, `Entity`, `EntityType`, `IconSource`.
- Produces: `DocumentProjection.From(Entity, TypeLookup)` returning `EntityDocument`; `DocumentProjection.TypeOf(EntityDocument)` returning `EntityType`; `DocumentProjection.Implied(Entity)` returning `EntityDocument?`; `DocumentProjection.Spaced(string)`; `delegate EntityType? TypeLookup(int netId)`.

- [ ] **Step 1: Write the conversions and the type map**

Create `src/FellowshipExtractor/Tooltip/DocumentProjection.cs`:

```csharp
using FellowshipExtractor.Tooltip.Documents;

namespace FellowshipExtractor.Tooltip;

/// <summary>
/// One resolved row as its type's document. The switch is exhaustive over <see cref="EntityType"/>,
/// so a type added to the enum stops compiling here until it has a document to become.
/// </summary>
public static class DocumentProjection
{
    /// <summary>The type of the row an id names, for the pointers a document carries.</summary>
    public delegate EntityType? TypeLookup(int netId);

    /// <summary>The type a document is, which is its runtime class.</summary>
    public static EntityType TypeOf(EntityDocument document) => document switch
    {
        Ability => EntityType.Ability,
        CompanionPet => EntityType.CompanionPet,
        Creature => EntityType.Creature,
        Currency => EntityType.Currency,
        Effect => EntityType.Effect,
        Finesse => EntityType.Finesse,
        GemTier => EntityType.GemTier,
        GemTrait => EntityType.GemTrait,
        Documents.GemType => EntityType.GemType,
        Item => EntityType.Item,
        LegendaryStat => EntityType.LegendaryStat,
        Mount => EntityType.Mount,
        MountPower => EntityType.MountPower,
        RelicPower => EntityType.RelicPower,
        Documents.SetBonus => EntityType.SetBonus,
        SetBonusPower => EntityType.SetBonusPower,
        Talent => EntityType.Talent,
        TalentSlot => EntityType.TalentSlot,
        Trait => EntityType.Trait,
        TraitPower => EntityType.TraitPower,
        WeaponPower => EntityType.WeaponPower,
        _ => throw new ArgumentOutOfRangeException(nameof(document), document.GetType().Name),
    };

    /// <summary>
    /// The extra document a row implies. One talent slot carries its talent's text rather than
    /// pointing at an augmentation, so that row is a slot and a talent both, and the talent takes
    /// the slot's own id.
    /// </summary>
    public static EntityDocument? Implied(Entity entity) =>
        entity is { Type: EntityType.TalentSlot, SlotBorneTalent: true }
            ? new Talent
            {
                Id = entity.NetId!.Value,
                Name = entity.DisplayName,
                Description = entity.Description,
                Icon = entity.Icon,
            }
            : null;

    /// <summary>
    /// The damage schools a row is, out of its tag list. The schools are the one family of these
    /// tags a player reads off a tooltip; Skill.Secondary and TrackCooldown are the build talking to
    /// itself and reach no reader.
    ///
    /// The rule is `Codex.Schools`, moved. The prefix carries no dot because Magic and Physical are
    /// each a tag in their own right beside Magic.Fire and Physical.Bleed.
    /// </summary>
    private static IReadOnlyList<string>? Schools(IReadOnlyList<string>? tags) =>
        (tags ?? [])
            .Where(tag => tag.StartsWith("Magic", StringComparison.Ordinal)
                       || tag.StartsWith("Physical", StringComparison.Ordinal))
            .Select(Spaced)
            .ToArray() is { Length: > 0 } schools
            ? schools
            : null;

    /// <summary>
    /// A shipped identifier as a reader reads it: dotted segments spaced out and runs of capitals
    /// broken up, so Magic.Fire prints as Magic / Fire and DamageScale as Damage Scale.
    /// `Codex.Spaced`, moved, because the extractor cannot reference the API.
    /// </summary>
    public static string Spaced(string value)
    {
        var text = string.Join(" / ", value.Split('.'));
        var spaced = new System.Text.StringBuilder(text.Length + 8);

        for (var at = 0; at < text.Length; at++)
        {
            if (at > 0 && char.IsUpper(text[at]) && (char.IsLower(text[at - 1]) || char.IsDigit(text[at - 1])))
            {
                spaced.Append(' ');
            }

            spaced.Append(text[at]);
        }

        return spaced.ToString();
    }

    /// <summary>
    /// A row's tokens as a list. The key was how a caller found a token in the sentence it
    /// substituted into, and 221 of the 237 distinct keys read like StrCoeff50. Plan 2 replaces the
    /// dictionary's own order with the order the description spends them.
    /// </summary>
    private static IReadOnlyList<Placeholder>? Spent(IReadOnlyDictionary<string, Placeholder>? tokens) =>
        tokens is { Count: > 0 } ? [.. tokens.Values] : null;

    /// <summary>Another row, addressed by its type rather than by the table it is filed in.</summary>
    private static EntityReference? Reference(IconSource? source, TypeLookup lookup) =>
        source is null || lookup(source.NetId) is not { } type
            ? null
            : new EntityReference(type, source.NetId, null);
}
```

Build: `dotnet build src/FellowshipExtractor.slnx`

- [ ] **Step 2: Write the projection switch**

Add `From` to the same class. Four arms are written out; the rest follow the table below.

```csharp
    public static EntityDocument From(Entity entity, TypeLookup lookup) => entity.Type switch
    {
        EntityType.Ability => new Ability
        {
            Id = entity.NetId!.Value,
            Name = entity.DisplayName,
            Description = entity.Description,
            Detail = entity.Detail,
            Icon = entity.Icon,
            CastTime = entity.CastTime,
            ChannelTime = entity.ChannelTime,
            ChannelTick = entity.ChannelTick,
            Cooldown = entity.Cooldown,
            Charges = entity.Charges,
            Cost = entity.Cost,
            Resource = entity.Resource,
            Range = entity.Range,
            Radius = entity.Radius,
            Duration = entity.Duration,
            Period = entity.Period,
            ScalesWithHaste = entity.ScalesWithHaste,
            ScalesOff = entity.ScalesOff,
            Placeholders = Spent(entity.Placeholders),
            Schools = Schools(entity.Tags),
        },

        EntityType.Item => new Item
        {
            Id = entity.NetId!.Value,
            Name = entity.DisplayName,
            ArmorType = entity.ArmorType is { } armor ? Enum.Parse<ArmorType>(armor) : null,
            PrimaryStat = entity.PrimaryStat is { } stat ? Enum.Parse<PrimaryStat>(stat) : null,
            Hero = entity.Heroes is { Count: 1 } only ? Enum.Parse<Hero>(only[0]) : null,
            Heroes = entity.Heroes?.Select(Enum.Parse<Hero>).ToArray(),
            Slot = entity.Slot,
            GemSlot = entity.GemSlot,
            UniqueEquipped = entity.UniqueEquipped,

            // A rule set naming a rung is the build fixing the item at it. Parsing through the
            // generated enum is what keeps that rung's name out of this file: Relic, Weapon and the
            // Default and SetBonus rule sets name no rung and parse to none.
            Rarity = Enum.TryParse<Rarity>(entity.RuleSet, out var fixedRarity) ? fixedRarity : null,

            SetBonus = entity.SetBonus,
            Stats = entity.Stats,
            Description = entity.Description,
            Icon = entity.Icon,
            IconByRarity = entity.IconByRarity,
            GrantsId = entity.GrantsId,
            LegendaryStatIds = entity.LegendaryStatIds,
        },

        EntityType.GemTier => new GemTier
        {
            Id = entity.NetId!.Value,
            Name = entity.DisplayName,
            Type = Enum.Parse<GemTypeName>(entity.GemType!),
            Tier = entity.GemTier!.Value,
            Power = entity.GemPower!.Value,
            Rarity = Enum.Parse<Rarity>(entity.Rarity!),
            Icon = entity.Icon,
        },

        EntityType.TalentSlot => new TalentSlot
        {
            Id = entity.NetId!.Value,
            Name = entity.DisplayName,
            Hero = Enum.Parse<Hero>(entity.Heroes!.Single()),
            Row = entity.TalentRow!.Value,
            Column = entity.TalentColumn!.Value,
            Cost = entity.TalentCost!.Value,

            // The slot-borne talent has no augmentation to point at, so it takes the slot's own id,
            // which is the id `Implied` gives the talent it makes from this row.
            TalentId = entity.Augmentation ?? entity.NetId!.Value,
        },

        // ...the remaining 17 arms, each following the table below...

        _ => throw new InvalidOperationException($"{entity.Name} has no type and should not have been projected."),
    };
```

Every remaining arm opens with `Id = entity.NetId!.Value, Name = entity.DisplayName,` and then this table, which is the whole of the mapping. A field named the same on both sides copies straight across, so only the ones that differ are listed.

| Type | `Entity` reads | Conversions |
|---|---|---|
| `Effect` | `Description`, `Icon`, `Duration`, `Period`, `Cooldown`, `Range`, `Radius`, `Cost`, `TickrateIgnoresHaste`, `ScalesOff`, `Placeholders`, `Tags`, `NameInheritedFrom`, `IconInheritedFrom` | `Placeholders` by `Spent`; `Tags` to `Schools`; the two `InheritedFrom` to `NameBorrowedFrom` and `ArtBorrowedFrom` by `Reference` |
| `Creature` | `Icon`, `ScalesOff` | |
| `Mount`, `Currency`, `CompanionPet`, `MountPower` | `Description`, `Icon` | |
| `SetBonus` | `SetCount` to `Pieces`, `Description`, `Icon`, `Augmentation` to `PowerId`, `AllowedRoles`, `AllowedAttributes` | `AllowedAttributes` by `Enum.Parse<PrimaryStat>` |
| `GemType` | `GemType` to `Gem`, `GemAttribute` to `Attribute`, `Icon`, `GemOverflow.Description` to `OverflowDescription`, `GemOverflow.Placeholders` to `OverflowPlaceholders` | `Gem` by `Enum.Parse<GemTypeName>`; the overflow placeholders by `Spent` |
| `GemTrait` | `GemType` to `Type`, `GemNode` to `Node`, `RequiredGemPower` to `RequiredPower`, `Replaces` to `ReplacesId`, `Description`, `Icon`, `Placeholders`, `ScalesOff` | `Type` by `Enum.Parse<GemTypeName>`; `Placeholders` by `Spent` |
| `Talent` | `Description`, `Icon`, `Placeholders`, `ScalesOff`, `Cooldown`, `Duration`, `Range`, `Radius`, `Cost` | `Placeholders` by `Spent` |
| `Finesse` | `Description`, `Icon`, `Placeholders`, `ScalesOff` | `Placeholders` by `Spent` |
| `Trait` | `TraitCategory` to `Category`, `MaxRank`, `RandomChance`, `Augmentation` to `PowerId`, `Description`, `Icon`, `RankRuns` | |
| `TraitPower` | `Description`, `Icon`, `Cooldown`, `Duration`, `Placeholders`, `ScalesOff` | `Placeholders` by `Spent` |
| `SetBonusPower` | `Description`, `Icon`, `Cooldown`, `ScalesOff` | |
| `RelicPower` | `Description`, `Icon`, `CastTime`, `Cooldown`, `Duration`, `Range`, `ScalesWithHaste`, `Tags`, `ScalesOff` | `Tags` to `Schools` |
| `WeaponPower` | `Description`, `Icon`, `CastTime`, `ChannelTime`, `ChannelTick`, `Cooldown`, `Duration`, `Period`, `Range`, `Radius`, `ScalesWithHaste`, `Tags`, `Placeholders`, `ScalesOff` | `Tags` to `Schools`; `Placeholders` by `Spent` |
| `LegendaryStat` | `Description`, `ScalesOff` | |

Build: `dotnet build src/FellowshipExtractor.slnx`

---

### Task 10: The output boundary

**Files:**
- Create: `src/FellowshipExtractor/Output/IOutputFormat.cs`, `JsonlOutputFormat.cs`
- Modify: `src/FellowshipExtractor/Tooltip/ExportAllCommand.cs`, `Program.cs`

**Interfaces:**
- Consumes: `EntityDocument`, `DocumentProjection`, `Vocabulary`.
- Produces: `IOutputFormat`; `JsonlOutputFormat(string folder)`; `ExportAllCommand.RunAsync(ExtractorOptions, string[], IOutputFormat)`.

- [ ] **Step 1: Write the interface**

Create `src/FellowshipExtractor/Output/IOutputFormat.cs`:

```csharp
using FellowshipExtractor.Tooltip;
using FellowshipExtractor.Tooltip.Documents;

namespace FellowshipExtractor.Output;

/// <summary>
/// Where a run is written. The document is the whole of what crosses and its runtime class is its
/// type, so a format added later is one class and no change here.
/// </summary>
public interface IOutputFormat : IAsyncDisposable
{
    Task WriteAsync(EntityDocument entity, CancellationToken cancellationToken);

    Task WriteVocabularyAsync(Vocabulary vocabulary, CancellationToken cancellationToken);
}
```

Build: `dotnet build src/FellowshipExtractor.slnx`

- [ ] **Step 2: Write the JSONL format**

Create `src/FellowshipExtractor/Output/JsonlOutputFormat.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FellowshipExtractor.Tooltip;
using FellowshipExtractor.Tooltip.Documents;

namespace FellowshipExtractor.Output;

/// <summary>
/// One line per row, in the folder the project writes to today. The line is the document with its
/// type beside it, which is what a reader needs to know which shape they are holding.
/// </summary>
public sealed class JsonlOutputFormat(string folder) : IOutputFormat
{
    public const string FileName = "entities.jsonl";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly StreamWriter _writer = Create(folder);

    private static StreamWriter Create(string folder)
    {
        Directory.CreateDirectory(folder);
        return new StreamWriter(Path.Combine(folder, FileName), append: false);
    }

    public async Task WriteAsync(EntityDocument entity, CancellationToken cancellationToken)
    {
        var type = EntityTypes.Slug(DocumentProjection.TypeOf(entity));
        var body = JsonSerializer.SerializeToNode(entity, entity.GetType(), Options)!.AsObject();

        // The type leads the line, because it is what tells a reader which of the twenty-one shapes
        // the rest of the line is.
        body.Insert(0, "type", type);

        await _writer.WriteLineAsync(body.ToJsonString(Options));
    }

    /// <summary>
    /// The sidecar, written by the vocabulary itself. It builds from the cooked tables and knows the
    /// shape it writes, so this format asks rather than restating it.
    /// </summary>
    public async Task WriteVocabularyAsync(Vocabulary vocabulary, CancellationToken cancellationToken) =>
        await vocabulary.WriteAsync(folder);

    public async ValueTask DisposeAsync()
    {
        await _writer.FlushAsync();
        await _writer.DisposeAsync();
    }
}
```

Build: `dotnet build src/FellowshipExtractor.slnx`

- [ ] **Step 3: Take the format as a parameter**

In `src/FellowshipExtractor/Tooltip/ExportAllCommand.cs`, change the signature to `RunAsync(ExtractorOptions options, string[] tables, IOutputFormat output)`.

The projection needs a `TypeLookup`, and a lookup over every row cannot be built while the rows are still being resolved. Resolve first, then write:

```csharp
        var branches = Branches(resolver.Tags);
        var resolved = new List<Entity>();

        foreach (var tag in selected)
        {
            foreach (var record in resolver.Resolve(tag, icons))
            {
                if (!Written(record, branches))
                {
                    var reason = record.Type is null ? "no type" : "empty branch";
                    dropped[reason] = dropped.GetValueOrDefault(reason) + 1;
                    continue;
                }

                // The duplicate-key guard from Task 4 stays here, before the row is kept.
                resolved.Add(record);
            }
        }

        var types = resolved
            .Where(row => row.NetId is not null)
            .GroupBy(row => row.NetId!.Value)
            .ToDictionary(group => group.Key, group => group.First().Type);

        DocumentProjection.TypeLookup lookup = netId => types.GetValueOrDefault(netId);

        foreach (var record in resolved)
        {
            await output.WriteAsync(DocumentProjection.From(record, lookup), default);
            written++;

            // One talent slot is a slot and a talent both, which is why 12,418 rows write 12,419.
            if (DocumentProjection.Implied(record) is { } implied)
            {
                await output.WriteAsync(implied, default);
                written++;
            }
        }

        await output.WriteVocabularyAsync(resolver.Vocabulary, default);
```

Build: `dotnet build src/FellowshipExtractor.slnx`

- [ ] **Step 4: Select the format from the command line**

In `src/FellowshipExtractor/Program.cs`, replace the `export-all` case:

```csharp
    case "export-all":
    {
        var format = Argument(args, "--output-format") ?? "jsonl";
        var folder = Path.Combine(options.ResolvedOutputDir, "export");

        await using IOutputFormat output = format switch
        {
            "jsonl" => new JsonlOutputFormat(folder),
            _ => throw new ArgumentException($"Unknown output format '{format}'. Formats: jsonl."),
        };

        await FellowshipExtractor.Tooltip.ExportAllCommand.RunAsync(
            options, [.. args[1..].Where(a => !a.StartsWith('-'))], output);
        break;
    }
```

Add the helper beside the other local functions at the bottom of the file:

```csharp
static string? Argument(string[] args, string name)
{
    var at = Array.FindIndex(args, a => a.Equals(name, StringComparison.OrdinalIgnoreCase));
    return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
}
```

And update the help line:

```
  export-all [table...] [--output-format jsonl]  every entity as its type's document
```

Build: `dotnet build src/FellowshipExtractor.slnx`

---

### Task 11: The tests

Everything above is written but unverified. This task adds the remaining test files and runs the suite once.

**Files:**
- Create: `src/FellowshipExtractor.Tests/TypeResolverTests.cs`, `DocumentTests.cs`, `OutputFormatTests.cs`
- Modify: `src/FellowshipExtractor.Tests/Export.cs`

- [ ] **Step 1: Give the tests a live resolver**

Add to `src/FellowshipExtractor.Tests/Export.cs`, beside `Loaded`:

```csharp
    private static readonly Lazy<TooltipResolver> ResolverInstance = new(
        () => new TooltipResolver(
            ProviderFactory.CreateAsync(new ExtractorOptions
            {
                MappingsFile = Environment.GetEnvironmentVariable("FELLOWSHIP_USMAP"),
                Game = ExtractorOptions.ParseGame(Environment.GetEnvironmentVariable("FELLOWSHIP_UE_VERSION")),
            }).GetAwaiter().GetResult()),
        isThreadSafe: true);

    /// <summary>
    /// A resolver over the cooked build, built once for the whole run. Mounting the containers is
    /// the expensive part of every one of these tests and the state it produces is read only.
    /// </summary>
    public static TooltipResolver Resolver => ResolverInstance.Value;
```

`Export.Loaded` deserialises `entities.jsonl` into `Entity`, which no longer matches the file the new format writes. Change its loader to read the document lines instead: parse each line's `type`, and keep the `id` and `name` plus the raw `JsonObject`, exposing them as `IReadOnlyList<(EntityType Type, int Id, string? Name, JsonObject Body)>` under a new `Documents` property. Leave `Vocabulary` as it is.

Build: `dotnet build src/FellowshipExtractor.slnx`

- [ ] **Step 2: Write the type resolver tests**

Create `src/FellowshipExtractor.Tests/TypeResolverTests.cs`:

```csharp
using FellowshipExtractor.Tooltip;

namespace FellowshipExtractor.Tests;

public sealed class TypeResolverTests
{
    private static readonly TooltipResolver Resolver = Export.Resolver;

    [Fact]
    public void TheTypeSetIsTheOneTheSpecNames() => Assert.Equal(21, Enum.GetValues<EntityType>().Length);

    [Theory]
    [InlineData(EntityType.Item, "item")]
    [InlineData(EntityType.GemTier, "gem-tier")]
    [InlineData(EntityType.SetBonusPower, "set-bonus-power")]
    [InlineData(EntityType.TalentSlot, "talent-slot")]
    public void ASlugIsTheTypeNameHyphenated(EntityType type, string slug) =>
        Assert.Equal(slug, EntityTypes.Slug(type));

    [Theory]
    [InlineData("/Game/Items/MountAugmentations/CA_Mount_Bear", EntityType.MountPower)]
    [InlineData("/Game/Items/RelicAugmentations/CA_RelicPower_Sanctuary", EntityType.RelicPower)]
    [InlineData("/Game/Abilities/RelicPowers/Portal/CA_RelicPower_Portal", EntityType.RelicPower)]
    [InlineData("/Game/Abilities/Weapons/ChainHealDamage/CA_Weapon_ChainHealDamage", EntityType.WeaponPower)]
    [InlineData("/Game/Abilities/Finesses/BasicToAoe/CAA_Finesse_BasicToAoe", EntityType.Finesse)]
    [InlineData("/Game/Abilities/LegendaryStats/X/CAA_LegendaryStat_X", EntityType.LegendaryStat)]
    [InlineData("/Game/Abilities/Talents/Mosse/CAA_Talent_Mosse_Talent1", null)]
    public void AFolderNamesTheTypeNothingPointsAt(string package, EntityType? expected)
    {
        var tag = new TagRecord("CRCharacterAugmentationAsset", "CA_Unclaimed", 999_999, 0);
        Assert.Equal(expected, Resolver.Types.Of(tag, new Entity(), package));
    }

    [Theory]
    [InlineData("ItemID.HeroItem.Dynamic.Chest.A_Crit", EntityType.Item)]
    [InlineData("ItemID.Mount.CatPanther", EntityType.Mount)]
    [InlineData("ItemID.Currency.Gold", EntityType.Currency)]
    [InlineData("ItemID.SetBonus.SetA_Percentage_HDT", EntityType.SetBonus)]
    [InlineData("ItemID.CompanionPet.Cat.Base", EntityType.CompanionPet)]
    [InlineData("ItemID.VisualCollection.Naked", null)]
    [InlineData("ItemID.PlayerCustomization.Avatar.ChiefRebel", null)]
    [InlineData("ItemID.HeroSkin.Warmaster_Default_Black_Back", null)]
    public void AnItemTagsSecondSegmentNamesItsType(string name, EntityType? expected)
    {
        var tag = new TagRecord("ItemID", name, 999_999, 0);
        Assert.Equal(expected, Resolver.Types.Of(tag, new Entity(), null));
    }

    [Fact]
    public void AGemTypeTagIsATierWhereItCarriesOne()
    {
        var tag = new TagRecord("ItemID", "ItemID.GemType.Ruby.Small", 999_999, 0);

        Assert.Equal(EntityType.GemTier, Resolver.Types.Of(tag, new Entity { GemTier = 1 }, null));
        Assert.Equal(EntityType.GemType, Resolver.Types.Of(tag, new Entity(), null));
    }

    [Theory]
    [InlineData("GameplayAbility", EntityType.Ability)]
    [InlineData("GameplayEffect", EntityType.Effect)]
    [InlineData("CRAICharacter", EntityType.Creature)]
    [InlineData("ItemTrait.ID", EntityType.Trait)]
    [InlineData("UIMaps", null)]
    [InlineData("Encounters", null)]
    public void AOneTypeTableIsItsOwnType(string table, EntityType? expected)
    {
        var tag = new TagRecord(table, "Whatever", 999_999, 0);
        Assert.Equal(expected, Resolver.Types.Of(tag, new Entity(), null));
    }

    [Fact]
    public void ATalentSlotIsARowCarryingCoordinates()
    {
        var tag = new TagRecord("Talent.ID", "Talent.ID.Talent8", 8, 0);

        Assert.Equal(EntityType.TalentSlot, Resolver.Types.Of(tag, new Entity { TalentRow = 4 }, null));
        Assert.Null(Resolver.Types.Of(tag, new Entity(), null));
    }

    /// <summary>
    /// A branch of the tag tree is only a non-entity where it also says nothing. ItemID.GemType.Ruby
    /// is a branch, its four tiers hanging off it, and a gem type in its own right.
    /// </summary>
    [Fact]
    public void AnEmptyBranchIsDroppedAndAFullOneIsNot()
    {
        var branches = ExportAllCommand.Branches(Resolver.Tags);

        Assert.Contains("ItemID.HeroItem", branches);
        Assert.Contains("ItemID.GemType.Ruby", branches);
    }

    /// <summary>
    /// The count for every type, against the spec's section 3.4. These move when the game moves,
    /// which is the point: a number changing here is the build changing rather than this code.
    /// </summary>
    [Theory]
    [InlineData(EntityType.Item, 5744)]
    [InlineData(EntityType.Effect, 3261)]
    [InlineData(EntityType.Ability, 2307)]
    [InlineData(EntityType.Creature, 232)]
    [InlineData(EntityType.Talent, 216)]
    [InlineData(EntityType.TalentSlot, 216)]
    [InlineData(EntityType.Mount, 78)]
    [InlineData(EntityType.Trait, 62)]
    [InlineData(EntityType.GemTrait, 60)]
    [InlineData(EntityType.MountPower, 54)]
    [InlineData(EntityType.TraitPower, 40)]
    [InlineData(EntityType.Currency, 32)]
    [InlineData(EntityType.GemTier, 24)]
    [InlineData(EntityType.SetBonus, 23)]
    [InlineData(EntityType.SetBonusPower, 15)]
    [InlineData(EntityType.Finesse, 14)]
    [InlineData(EntityType.WeaponPower, 12)]
    [InlineData(EntityType.RelicPower, 11)]
    [InlineData(EntityType.CompanionPet, 9)]
    [InlineData(EntityType.GemType, 6)]
    [InlineData(EntityType.LegendaryStat, 3)]
    public void TheCorpusHoldsTheseCounts(EntityType type, int expected) =>
        Assert.Equal(expected, Export.Loaded.Documents.Count(d => d.Type == type));

    [Fact]
    public void EveryHeroHasEighteenSlots()
    {
        var perHero = Export.Loaded.Documents
            .Where(d => d.Type == EntityType.TalentSlot)
            .GroupBy(d => d.Body["hero"]!.GetValue<string>())
            .ToArray();

        Assert.Equal(12, perHero.Length);
        Assert.All(perHero, hero => Assert.Equal(18, hero.Count()));
    }

    [Fact]
    public void SixtyGemTraitsTenPerType()
    {
        var traits = Export.Loaded.Documents.Where(d => d.Type == EntityType.GemTrait).ToArray();

        Assert.Equal(60, traits.Length);
        Assert.All(traits.GroupBy(t => t.Body["type"]!.GetValue<string>()), type => Assert.Equal(10, type.Count()));
    }

    /// <summary>
    /// Every slot names a talent. 215 point at an augmentation; Talent8 carries Macabre Stratagem
    /// itself, so the talent for that slot is the slot row read as one.
    /// </summary>
    [Fact]
    public void TheSlotBorneTalentIsTheTwoHundredAndSixteenth()
    {
        var talent = Export.Loaded.Documents.Single(
            d => d.Type == EntityType.Talent && d.Name == "Macabre Stratagem");

        Assert.Equal(8, talent.Id);
    }

    /// <summary>
    /// A grant and a legendary stat line are augmentation rows already in the corpus, so an item
    /// points at them by their own id and nothing is minted.
    /// </summary>
    [Fact]
    public void AnItemPointsAtAugmentationsThatExist()
    {
        var ids = Export.Loaded.Documents.Select(d => d.Id).ToHashSet();

        var legendary = Export.Loaded.Documents
            .Where(d => d.Type == EntityType.Item && d.Body["legendaryStatIds"] is not null)
            .ToArray();

        Assert.Equal(37, legendary.Length);
        Assert.All(legendary, item => Assert.All(
            item.Body["legendaryStatIds"]!.AsArray(),
            stat => Assert.Contains(stat!.GetValue<int>(), ids)));
    }

    [Fact]
    public void AnItemStatesARarityOnlyWhereTheBuildFixesIt()
    {
        var items = Export.Loaded.Documents.Where(d => d.Type == EntityType.Item).ToArray();
        var stated = items.Where(d => d.Body["rarity"] is not null).ToArray();

        Assert.Equal(37, stated.Length);
        Assert.All(stated, item => Assert.Equal("Legendary", item.Body["rarity"]!.GetValue<string>()));
        Assert.All(stated, item => Assert.NotNull(item.Body["legendaryStatIds"]));
    }
}
```

Build: `dotnet build src/FellowshipExtractor.slnx`

- [ ] **Step 3: Write the document and output tests**

Create `src/FellowshipExtractor.Tests/DocumentTests.cs`:

```csharp
using FellowshipExtractor.Tooltip;
using FellowshipExtractor.Tooltip.Documents;

namespace FellowshipExtractor.Tests;

public sealed class DocumentTests
{
    [Fact]
    public void ADocumentDoesNotRestateItsType()
    {
        // The class is the type. A property naming it would be a second answer that can disagree.
        Assert.DoesNotContain(
            typeof(EntityDocument).GetProperties(),
            property => property.Name is "Type" or "Kind");
    }

    /// <summary>
    /// A field every row of a type states is required. All 24 gem tiers state a type, a tier, a
    /// power and a rarity, and all 216 talent slots state a hero, a position and a cost.
    /// </summary>
    [Fact]
    public void AFieldEveryRowStatesIsRequired()
    {
        Assert.All(
            new[] { "Type", "Tier", "Power", "Rarity" },
            name => Assert.True(IsRequired(typeof(GemTier).GetProperty(name)!)));

        Assert.All(
            new[] { "Hero", "Row", "Column", "Cost", "TalentId" },
            name => Assert.True(IsRequired(typeof(TalentSlot).GetProperty(name)!)));
    }

    [Fact]
    public void SchoolsAreTheDamageTagsSpacedOut()
    {
        Assert.Equal("Magic / Fire", DocumentProjection.Spaced("Magic.Fire"));
        Assert.Equal("Physical / Bleed", DocumentProjection.Spaced("Physical.Bleed"));
        Assert.Equal("Damage Scale", DocumentProjection.Spaced("DamageScale"));
    }

    /// <summary>
    /// A slug addresses one type. Slug is derived from the member name, so two members differing
    /// only in where their capitals fall would collide and one of them would be unreachable.
    /// </summary>
    [Fact]
    public void EveryTypeHasASlugOfItsOwn()
    {
        var slugs = Enum.GetValues<EntityType>().Select(EntityTypes.Slug).ToArray();
        Assert.Equal(slugs.Length, slugs.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void ADocumentsRuntimeClassIsItsType()
    {
        Assert.Equal(EntityType.Ability, DocumentProjection.TypeOf(new Ability { Id = 1 }));
        Assert.Equal(EntityType.GemType, DocumentProjection.TypeOf(
            new GemType { Id = 1, Gem = GemTypeName.Ruby, Attribute = "RubyPower" }));
        Assert.Equal(EntityType.TalentSlot, DocumentProjection.TypeOf(
            new TalentSlot { Id = 8, Hero = Hero.Mara, Row = 4, Column = 1, Cost = 1, TalentId = 8 }));
    }

    private static bool IsRequired(System.Reflection.PropertyInfo property) =>
        property.GetCustomAttributes(typeof(System.Runtime.CompilerServices.RequiredMemberAttribute), false).Length > 0;
}
```

Create `src/FellowshipExtractor.Tests/OutputFormatTests.cs`:

```csharp
using System.Text.Json;
using FellowshipExtractor.Output;
using FellowshipExtractor.Tooltip.Documents;

namespace FellowshipExtractor.Tests;

public sealed class OutputFormatTests
{
    [Fact]
    public async Task AJsonlLineIsTheDocumentAndItsType()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;

        await using (var output = new JsonlOutputFormat(folder))
        {
            await output.WriteAsync(new Ability { Id = 1122, Name = "Stone Shield" }, default);
        }

        var line = (await File.ReadAllLinesAsync(Path.Combine(folder, "entities.jsonl")))[0];
        using var document = JsonDocument.Parse(line);

        Assert.Equal("ability", document.RootElement.GetProperty("type").GetString());
        Assert.Equal(1122, document.RootElement.GetProperty("id").GetInt32());
        Assert.Equal("Stone Shield", document.RootElement.GetProperty("name").GetString());
        Assert.False(document.RootElement.TryGetProperty("table", out _));
    }
}
```

Build: `dotnet build src/FellowshipExtractor.slnx`

- [ ] **Step 4: Run the export, then the suite**

Run: `dotnet run --project src/FellowshipExtractor export-all`
Expected: 12,419 entries written, and the first line of `output/export/entities.jsonl` carrying a `type` and no `table`.

Run: `dotnet run --project src/FellowshipExtractor enums`
Expected: unchanged output from Task 1 Step 6.

Run: `dotnet test src/FellowshipExtractor.Tests`
Expected: PASS.

A per-type count that disagrees is the finding, not a test to adjust. Reconcile it against the spec's section 3.4 before changing either number.

---

## Notes for whoever executes this

**The export the tests read is the one you just wrote.** `Export.Loaded` reads `output/export/entities.jsonl`, so Task 11 needs `dotnet run --project src/FellowshipExtractor export-all` to have run after Task 10. When a count test fails, re-run the export before believing the number.

**The API does not compile against the new export.** `EntityStore` reads `entities.jsonl` into `Entity` and will not load a document-shaped file. The API is Plan 4's work; expect it to be broken from Task 10 onward and do not patch it here.

**A document's placeholder list is not yet in a meaningful order.** `Spent` returns the dictionary's own values, and the order a description spends its tokens is what ties an entry to its number now that the sentence carries no key. Nothing reads the new JSONL until Plan 4, and Plan 2 supplies the order, so this is a gap rather than a regression.

**`FELLOWSHIP_USMAP` must point at a `.usmap` for this build** or every export run fails with `MappingException`. `Export.Resolver` mounts the cooked containers, which is slow: the first test touching it pays about a minute.
