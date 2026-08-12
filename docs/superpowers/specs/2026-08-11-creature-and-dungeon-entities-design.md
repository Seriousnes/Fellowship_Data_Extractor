# Creature and dungeon entities

**Goal:** A creature says how much health it has, what it is worth to kill, what rank it is, what
abilities it uses and which dungeons it is found in. A dungeon is an entity of its own with an id, a
name, a page and an API resource, so anything that names a dungeon links to it. `/creature/{id}` and
`/dungeon/{id}` read those facts off the exported documents, which the extractor computes once from
the assets that declare them.

## What the assets state

Measured on 2026-08-11 against `output/fellowship.usmap`, `output/files.txt`,
`output/ability-constants.jsonl` and the dumped assets under `output/dump/fellowship`.

### The join key is `CharacterID`

Every creature blueprint carries a `CharacterID` string, `VV_IcebiteReaver` on
`BP_NPC_VV_IcebiteReaver`. That string is the namespace for the creature's tuning rows in
`AC_<Dungeon>.csv` and the namespace for its attributes in `CT_CharacterBaseAttributes`. The
`CRAICharacter` tag table keys on the asset name, so the asset is the bridge between the two.

### Health, kill score and spirit points are curve rows

`CT_CharacterBaseAttributes` holds 3,094 rows keyed `<CharacterID>.<AttributeSet>.<Attribute>` over
281 namespaces:

| Row | Namespaces | Icebite Reaver |
| --- | --- | --- |
| `CRNpcAttributeSet.KillScore` | 256 | 5 |
| `CRNpcAttributeSet.SpiritPointValue` | 247 | 5 |
| `BasicAttributeSet.Armor` | 238 | 0 |
| `BasicAttributeSet.Resist` | 238 | 0 |
| `Custom.DefaultBaseHealthMultiplier` | 198 | 4.291 |
| `BasicAttributeSet.BaseHealth` | 80 | none |

**The two health rows are disjoint.** No namespace carries both, so health is the explicit
`BasicAttributeSet.BaseHealth` where one exists and `Default.BasicAttributeSet.BaseHealth` times
`Custom.DefaultBaseHealthMultiplier` otherwise. `Default.BasicAttributeSet.BaseHealth` is 2,999, so
the Icebite Reaver is 12,871 and CM_BossAina ships 32,000 outright.

`CT_CharacterScalingBaseAttributes` holds `Default.BasicAttributeSet.DifficultyHealthScale` and
`Default.BasicAttributeSet.DifficultyDamageScale`, each 151 keys covering difficulty 1 to 151.
Health runs 1.0 to 1,106.64 and damage runs 1.0 to 763.83. Only `Hatchling` and `TargetDummy` carry
a scaling row of their own, so every other creature reads the `Default` pair.

### A creature's abilities are its behaviour component's phases

`BehaviorComponentClass` on the creature names an `AIBC_*` asset whose `Phases` map is keyed by an
`AIPhase` gameplay tag. Each phase's `Abilities` array names an `AbilityClass` and states
`ShowOnUI`, which is the game's own answer for which abilities a player-facing card lists:
`GA_VV_IcebiteReaver_MeleeAutoAttack` is false, Slam and Haste Buff are true. `PassiveGameplayEffects`
on the creature and `LifetimePassiveAbilities` and `LifetimePassiveEffects` on the component hold
the rest. `AbilityOwners` already walks all of this to build the ability to creature map.

### Rank is a character tag

`CharacterTags` carries `ActorTypeTags.Boss` on 35 creatures, `ActorTypeTags.Elite` on 45,
`ActorTypeTags.Enemy` on 191 and `ActorTypeTags.FriendlyNPC` on 4. `CountTowardsDungeonScore` is a
bool that defaults true and reads false on ghosts and boss adds.

### A creature is found where its actor is placed

A dungeon's `.umap` packages hold the creature blueprints as placed actors. Trash and elites sit in
`<LevelName>_Enemies.umap`, bosses in the persistent `<LevelName>.umap`:
`VikingVillageAttack_Enemies` places 12 distinct creature classes and `VikingVillageAttack` places
Chilgar, the Frost Troll, both Kaldursons and the Viking Mystic.

The export map entry's `ObjectName` is the reliable identifier. It is a plain name-table read, it
names the class the actor was placed from (`BP_NPC_VV_BlackburnSlaver_C_6`), and it needs no export
deserialization. Reading it directly, rather than through a resolved class, is what the reader does.

### Dungeons are already a tag table

`Level.ID.csv` holds 31 tags with net ids, `Level.ID.FrozenKingdom` is 7 and `Level.ID.MoorCastle`
is 29. `DT_LevelInfo` holds 15 `CRLevelInfoRow` rows keyed by those tags, and the `LI_*`
`CRLevelInfo` assets hold one each for the GameFeature dungeons. Both carry `DisplayName`,
`LevelDescription`, `BannerTexture`, `BackgroundTexture`, `MaxPlayers`, `DungeonQualifierTime`,
`RequiredDungeonScoreToComplete`, `MinimumHeroRankRequired` and `LevelName`. `LevelName` is the
package name of the dungeon's persistent map, so it locates the umaps that belong to the dungeon.
`DungeonNames` already reads both sources; the export already prints these names on item drop
sources, including "Cithrel's Fall", "Silken Hollow" and "Ruins of Regath".

## Documents

`Fellowship.SDK/Documents/CreatureDocument.cs`:

```csharp
public sealed class CreatureDocument : EntityDocument
{
    public string? Icon { get; init; }
    public CreatureRank? Rank { get; init; }
    public double? Health { get; init; }
    public double? Armor { get; init; }
    public double? Resist { get; init; }
    public int? KillScore { get; init; }
    public int? SpiritPoints { get; init; }
    public bool? CountsTowardScore { get; init; }
    public IReadOnlyList<CreatureAbility>? Abilities { get; init; }
    public IReadOnlyList<CreatureAbility>? Passives { get; init; }
    public IReadOnlyList<EntityReference>? FoundIn { get; init; }
}
```

`Fellowship.SDK/Documents/DungeonDocument.cs`:

```csharp
public sealed class DungeonDocument : EntityDocument
{
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public string? Background { get; init; }
    public int? MaxPlayers { get; init; }
    public double? QualifierTime { get; init; }
    public int? RequiredScore { get; init; }
    public int? MinimumHeroRank { get; init; }
}
```

`Fellowship.SDK/Models`:

```csharp
public enum CreatureRank { Enemy, Elite, Boss, Friendly }

public sealed record CreatureAbility(EntityReference Ability, string Phase, bool Shown);

public sealed record ItemSource(SourceKind Kind, string Name)
{
    public EntityReference? Dungeon { get; init; }
}
```

`EntityType` gains `Dungeon`. `Health` carries the value at difficulty 1 and `QualifierTime` is in
seconds. `SettingsFile` gains `DifficultyScaling` holding the two 151 point curves, so any
difficulty is computable from a creature's health and the curve together.

A dungeon's creature list is derived in the Api by scanning the store, so the creature to dungeon
fact is stored once, on the creature.

## Every reference names an entity

A document that points at another entity carries an `EntityReference`, which states the type, the
id and the name. Nothing points at another entity by a name alone, and nothing points by an id
whose type the reader has to work out.

| Field | Carries |
| --- | --- |
| `AbilityDocument.Owner` | `EntityReference` |
| `EffectDocument.Owner`, `AppliedBy`, `NameBorrowedFrom`, `ArtBorrowedFrom` | `EntityReference` |
| `ItemDocument.SetBonus` | `IReadOnlyList<EntityReference>` |
| `ItemDocument.Grants` | `EntityReference` |
| `ItemDocument.LegendaryStats` | `IReadOnlyList<EntityReference>` |
| `ItemDocument.Sources[].Dungeon` | `EntityReference` |
| `TraitDocument.Power`, `SetBonusDocument.Power` | `EntityReference` |
| `TalentSlotDocument.Talent` | `EntityReference` |
| `GemTraitDocument.Replaces` | `EntityReference` |
| `CreatureDocument.FoundIn`, `Abilities[].Ability`, `Passives[].Ability` | `EntityReference` |

The extractor resolves each type, which it knows from the mapping that produced the reference.
`DocumentProjection` already does this for an ability's and an effect's owner through its
`TypeLookup`, and the same lookup answers the rest.

`IEntityStoreService.GrantedPower` is retired: a granted power's type arrives on the document, so
the Api reads it rather than trying `RelicPower`, `WeaponPower`, `LegendaryPower` and `Talent` in
turn and taking whichever holds an id.

Every reference the API emits is a `PublicReference` carrying `type`, `id`, `name` and `resource`,
an absolute `{baseurl}/api/{slug}/{id}` built from the request:

```json
"appliedBy": {
  "type": "ability", "id": 1273, "name": "Apocalypse",
  "resource": "https://host/api/ability/1273"
}
```

`PublicEntity` emits `owner`, `appliedBy`, `nameBorrowedFrom`, `artBorrowedFrom`, `setBonus`,
`grants`, `legendaryStats`, `power`, `talent`, `replaces`, `foundIn`, `abilities` and `passives` in
that shape.

## Extraction

`Tooltip/CreatureStats.cs` loads `CT_CharacterBaseAttributes` and
`CT_CharacterScalingBaseAttributes`, indexes their rows by namespace and attribute, answers
`Health`, `Armor`, `Resist`, `KillScore` and `SpiritPoints` for a `CharacterID`, and exposes the two
difficulty curves for `settings.json`. It logs how many creatures resolve neither health row.

`Tooltip/CreatureLocations.cs` takes the dungeons `DungeonNames` resolved, finds each one's
persistent umap by `LevelName`, and reads every umap under that package's folder. For each package
it reads `ExportMap[].ObjectName` through the package name map, strips the trailing `_<n>`, and
keeps the names the `CRAICharacter` tag table holds. It logs dungeons resolved, umaps read and
creatures placed in none.

`Tooltip/DungeonNames.cs` keeps the id, description, art, player count, qualifier time, required
score and hero rank it already reads past, and gains lookups by level tag and by `LevelName`.

`Tooltip/AbilityOwners.cs` keeps the forward map from the walk it already performs, carrying each
ability's phase tag and `ShowOnUI` alongside the creature.

`Tooltip/TypeResolver.cs` maps the `Level.ID` table to `EntityType.Dungeon`.
`Tooltip/TooltipResolver.cs` fills the new `Entity` fields for `CRAICharacter` and `Level.ID` tags,
and `Tooltip/ItemSources.cs` carries the dungeon's tag alongside the name it already resolves.
`Tooltip/DocumentProjection.cs` projects both new documents and turns every set bonus name, granted
power, legendary stat, trait power, talent and replaced gem trait into an `EntityReference` through
the `TypeLookup` it already holds.

## Api and UI

`Endpoints/Dungeon/` gains `DungeonGroup`, `DungeonRecordEndpoint` and `DungeonTooltipEndpoint`
matching the shape every other type uses, registered from `ApiEndpoints`. `PublicTypes` gains the
`Dungeon` row in the `Characters` group and places it in `Tabbed`. `PublicEntity` carries the
creature and dungeon fields and a dungeon's creature list.

`Components/Browse/Dungeons.razor` lists dungeons with their qualifier time, required score and
creature count. `Components/Browse/Creatures.razor` fills its Location column and gains a Rank
column. `CodexService` answers `CreatureDocument.FoundIn` from the existing `source` facet, gains a
`rank` facet, and gains `location` and `rank` sortings.

`/creature/{id}` shows health, armour, resist, kill score, spirit points, rank and score
participation through `CodexViewService.Facts`, plus two sections on the `Detail` composition: the
abilities as links grouped by phase with the ones the game hides marked, and the dungeons as links.
`/dungeon/{id}` shows the description, player count, qualifier time, required score and hero rank,
and lists every creature found there with its rank and kill score.

## Verification

Re-run `dotnet run --project src/FellowshipExtractor -- export-all --output output/export` and
confirm:

- Every creature with a `CharacterID` reports health and kill score, or is counted in the reader's
  log.
- Every `abilities[].ability.id` and `passives[].ability.id` resolves to a document in the export.
- Every `foundIn[].id` resolves to a dungeon document, and every dungeon document's name already
  appears among the item drop source names the previous export wrote.
- `grep -c '/Game/\|/Script/' entities.jsonl` is 0.
- Every reference in the export resolves: each `{ type, id }` pair names a document that exists, and
  the type each one states matches what the previous export's Api resolved the bare id to.
- The Icebite Reaver record reads health 12871, killScore 5, spiritPoints 5, rank Elite, three
  abilities with the melee auto attack marked hidden, and Ransack of Drakheim in `foundIn`.
- The four existing export invariants hold: every tuning row still reachable, every captured
  attribute still in a `scalesOff`, every written token matched by a placeholder, every icon naming
  a file that exists.

Tests: `FellowshipExtractor.Tests` covers the health branch on both shapes, the umap object name
read, the phase and `ShowOnUI` walk, and the `Level.ID` type resolution.
`Fellowship.Codex.Tests` covers the dungeon endpoints, the `resource` link on every reference
shape, and the creature and dungeon pages rendering their lists.
