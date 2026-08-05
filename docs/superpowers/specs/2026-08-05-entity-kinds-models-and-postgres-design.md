# Entity kinds, typed models and a Postgres store: design

The extractor resolves what each row is, writes it into a model of that kind, and hands it to an
output format. One format writes the JSONL file the project writes today; the other writes Postgres.
The API and the Codex browser read Postgres.

---

## 1. Goal

A row's kind is decided once, by the extractor, from what the build states. Talent means a hero's
talent. Each kind has a C# model carrying the fields that kind has, a Postgres table of its own, and
a row in the view every cross-kind surface reads. `--output-format` chooses where the run is written.

---

## 2. What a row is

### 2.1 The kind resolver

`TooltipResolver` already loads every map this reads. Asked of a row in this order:

**A reference the build states outright.**

1. A `Talent.ID` slot points at the augmentation: **talent**. `TalentTrees` holds the map.
2. The row carries a gem ladder node: **gem trait**. `GemDefinitions` holds the map.
3. An `ItemTrait.ID` row points at it: **trait power**. `ItemTraits` holds the map.
4. An `ItemID.SetBonus` row points at it: **set bonus power**. `ItemSetBonuses` holds the map.

**The folder the asset ships in**, for the kinds nothing points at:

| Folder | Kind |
|---|---|
| `/Game/Items/MountAugmentations` | mount power |
| `/Game/Items/RelicAugmentations` | relic power |
| `/Game/Abilities/RelicPowers/` | relic power |
| `/Game/Abilities/Weapons/` | weapon power |
| `/Game/Abilities/Finesses/` | finesse |
| `/Game/Abilities/LegendaryStats/` | legendary stat |

**The tag's own second segment**, for `ItemID`: `HeroItem` is an **item**, `Mount` a **mount**,
`Currency` a **currency**, `SetBonus` a **set bonus**, `CompanionPet` a **companion pet**, and
`GemType` a **gem tier** where the row carries a tier and a **gem type** where it does not.

**The shipped table**, for every table holding one kind: `GameplayAbility` is an **ability**,
`GameplayEffect` an **effect**, `CRAICharacter` a **creature**, `ItemTrait.ID` a **trait**, and a
`Talent.ID` row carrying grid coordinates a **talent slot**.

A row no rule reaches has no kind and is not written. That default is what keeps a kind out of the
store until someone has decided what it is.

A reference is asked before a folder because a folder holds more than the assets of one kind:
`/Game/Abilities/Talents/Mosse` ships 33 assets and 18 of them are the talents Mosse's slots point
at. Reading the folder alone counts 467 talents where the build has 216.

### 2.2 The 216th talent

215 talent slots point at an augmentation carrying the talent's text. `Talent.ID.Talent8`, Mara's
row 4 column 1, carries its own name and description instead: *Macabre Stratagem*. `TalentTrees`
already reads a talent this way, choosing the slot's own text where the slot names no augmentation,
so the talent written for that slot is built from the slot row.

### 2.3 What is not written

Three rules, each derived from the corpus:

- **An empty branch of the tag tree.** A tag another tag hangs beneath, carrying nothing itself, is a
  branch rather than an entity: `ItemID.HeroItem`, `ItemID.VisualCollection`, `Modifiers.Ascension`.
  Emptiness is required, because `ItemID.GemType.Ruby` is a branch with four gem tiers beneath it and
  a gem type in its own right.
- **A row no rule in 2.1 reaches.**
- **A kind decided against**: visual collections, player and community customization, a creature
  carrying nothing, and a mount whose name pairs with no `CA_Mount` augmentation.

A row of a kind that is written but carrying nothing yet is written. `ItemID.HeroItem.BowGuy_CC_Back`
is an item whose name, stats and art the extractor does not read yet, and it is a row of the `item`
table with its document null. What those rows are missing is read in work of its own, which fills
columns and moves no row.

### 2.4 The kinds

| Kind | Rows | | Kind | Rows |
|---|---:|---|---|---:|
| item | 5,744 | | gem tier | 24 |
| effect | 3,261 | | set bonus | 23 |
| ability | 2,307 | | set bonus power | 15 |
| creature | 232 | | finesse | 14 |
| talent | 216 | | weapon power | 12 |
| talent slot | 216 | | relic power | 11 |
| mount | 78 | | companion pet | 9 |
| trait | 62 | | gem type | 6 |
| gem trait | 60 | | legendary stat | 3 |
| mount power | 57 | | | |
| trait power | 40 | | | |
| currency | 32 | | | |

12,422 rows over 21 kinds, from the 12,421 export rows that are written: `Talent.ID.Talent8` is one
export row and two entities, its slot and the talent the slot carries.

2,814 export rows are not written, under the three rules in 2.3:

| Not written | Rows |
|---|---:|
| **a kind decided against** | **1,115** |
| &nbsp;&nbsp;visual collections | 733 |
| &nbsp;&nbsp;player and community customization | 215 |
| &nbsp;&nbsp;creatures carrying nothing | 106 |
| &nbsp;&nbsp;mounts pairing with no augmentation | 61 |
| **reached by no rule** | **892** |
| **an empty branch of the tag tree** | **807** |

The 892 are two populations. 547 are every row of a segment or a table no rule in 2.1 names: hero
skins, hero abilities, cosmetic swatches, progression, visual trims, SDT entries, the 21 `Talent.ID`
rows carrying no coordinates, and the tag tables whose rows say nothing at all. 345 are augmentation
rows that no reference claims and whose folder names no kind, of which 210 ship no asset and 126 sit
in a hero's talent folder under a name no slot points at.

Named rows are among them, and each is a row the game would print that this store does not hold. 37
sit in hero talent folders under names no slot claims, 30 are visual collections carrying a name, a
description and an icon - *Wildspring Oracle* (1792), *Deepwarden's Oath* (1793), *Abyssal Touch*
(2534) - and 2 are Gunde's legendary traits, *Bloody Mess* and *Bleeding Hearts*.

---

## 3. The models

A record per kind over a base carrying the three fields every row has:

```csharp
public abstract record EntityDocument
{
    public required string Kind { get; init; }
    public required int NetId { get; init; }
    public required string Name { get; init; }
}
```

Each kind's record carries the fields its census shows that kind has, and no others. `Item` carries
`Slot`, `Stats`, `RuleSet`, `ArmorType`, `PrimaryStat`, `IconByRarity` and `SetBonus`. `GemTier`
carries `GemType`, `GemTier`, `GemPower` and `Rarity`, all four required, because all 24 rows state
all four. `TalentSlot` carries one hero, `TalentRow`, `TalentColumn`, `TalentCost` and the id of its
talent. `Talent` carries `GrantedTags`, `TalentGates` and its text.

`Placeholder`, `StatSlot` and `RankRun` stay as they are and are used by the kinds that have them.

### 3.1 Rows that were nested

An item's granted ability and a legendary's stat lines are rows rather than nested documents. Both
already resolve through `LoadAugmentation`, and `AugmentationIds.For` answers the netId of an
augmentation path, so the id an item carries is the augmentation's own and nothing is minted:

- `Item.GrantsId` is a `relic-power` or `weapon-power` netId.
- `Item.LegendaryStatIds` are `legendary-stat` netIds. 111 nested lines across 37 legendary items
  name 3 distinct augmentations, so the rows they become are 3.

---

## 4. Writing a run

```csharp
public interface IOutputFormat : IAsyncDisposable
{
    Task WriteAsync(EntityDocument entity, CancellationToken cancellationToken);
    Task WriteVocabularyAsync(VocabularyFile vocabulary, CancellationToken cancellationToken);
}
```

The model and its kind are the whole of what crosses, so a third format is one class.

`--output-format jsonl` writes `entities.jsonl` and `vocabulary.json` to the paths they are written
to today. `--output-format postgres` writes the tables in section 5. `jsonl` is the default, so a run
needs a database only when one is asked for.

The JSONL a run writes is the models serialised, so it is not the file the project writes today. A
line states `kind` where it used to state `table`, it states no gameplay tag, and an item states
`grantsId` and `legendaryStatIds` where it used to nest a whole ability and three stat lines. The two
files are compared by the counts in 2.4 and by named ids rather than by diff.

`ExportAllCommand` keeps the duplicate-key guard it has, now per kind, and keeps the counts it
prints, now per kind.

---

## 5. Postgres

Postgres 18 in Docker, added to the AppHost beside the storage emulator and carrying the same
per-worktree treatment: the data volume and the compose stack are named for `stack.Name` and the port
is `Port(builder, "Ports:Postgres", stack.Offset, 5432)`, because every address an AppHost claims is
global to the machine and several worktrees of this repo run at once.

### 5.1 A table per kind

```sql
CREATE TABLE item (
    net_id       integer NOT NULL,
    armor_type   text    NOT NULL DEFAULT '',
    primary_stat text    NOT NULL DEFAULT '',
    hero         text    NOT NULL DEFAULT '',
    name         text    NOT NULL,
    document     jsonb,
    PRIMARY KEY (net_id, armor_type, primary_stat, hero)
);
```

`(net_id, armor_type, primary_stat, hero)` is unique inside every one of the 21 kinds, measured over
the corpus. It is the key `ExportAllCommand` already enforces, and every kind declares it so one
shape serves all of them.

`hero` is the sole hero a row is for, and the empty string on a row that is for several. That is the
rule the export's own key uses, and it is what makes the column a discriminator rather than a
summary: 42 items name a roster without reading differently per hero, and a column holding the first
of those names would split one item into rows that do not exist. The full roster a row names lives in
its document, which is where `Reads` and `Wearer` go on asking about it.

`document` is null on a row carrying nothing yet, which makes the extraction worklist a query:

```sql
SELECT count(*) FROM item WHERE document IS NULL;
```

### 5.2 The view every cross-kind surface reads

```sql
CREATE VIEW entity AS
    SELECT 'item' AS kind, net_id, armor_type, primary_stat, hero, name, document FROM item
    UNION ALL
    SELECT 'effect', net_id, armor_type, primary_stat, hero, name, document FROM effect
    UNION ALL ...;
```

Browse, the omnibox, the front page counts and `Nearby` read the view. A per-kind page reads its
table.

### 5.3 Vocabulary

```sql
CREATE TABLE vocabulary (section text PRIMARY KEY, document jsonb NOT NULL);
```

One row per section: `colors` (123 rows), `textStyles` (21), `printFormats` (20), `heroes` (12),
`rarities` (7), `statCalculation`. `VocabularyFile` deserialises from the assembled document exactly
as it does from the file, including the case-insensitive rebuild `EntityStore`'s constructor
performs: jsonb preserves key case and does not fold it, and the corpus writes `rt.Absorb` against a
table filing every style lower case.

---

## 6. The API and the Codex browser

`EntityStore` reads Postgres. The rules its doc comments record move rather than get rewritten: a
hero resolves by the name the game prints and by nothing else, `Reads` keeps the first clause that
answers for the eighteen Back-slot cloaks, and `WrittenAsWords` keeps deciding which shipped names a
reader is shown.

`PublicTypes` keys on kind. `PublicType.Table` becomes `PublicType.Kind`, `IsPublic`, `SlugOf`,
`Label` and `Noun` answer about a kind, and the augmentation table no longer needs partitioning
because the extractor has partitioned it. A slug is its kind's name hyphenated, so the kinds the
augmentation table used to hide are addressable: `talent`, `gem-traits`, `trait-powers`,
`set-bonus-powers`, `weapon-powers`, `relic-powers`, `legendary-stats`, `mount-powers`, `finesses`.

`PublicEntity` projects from a kind's model. The fields it rewrites and withholds it goes on
rewriting and withholding, and `PublicEntity.Type` carries the kind's slug.

---

## 7. What the build states

Every number is measured over `output/export/entities.jsonl`, `vocabulary.json` and
`asset-registry.jsonl`.

**Talents.** 216 talent slots carry grid coordinates, 18 for each of the 12 heroes, and each names
exactly one hero. 215 point at an augmentation and each points at a different one; `Talent.ID.Talent8`
points at none and carries *Macabre Stratagem* itself. 21 further `Talent.ID` rows carry no
coordinates.

**Gems.** 6 gem types: Amethyst, Diamond, Emerald, Ruby, Sapphire and Topaz. 60 gem traits, 10 for
each type. 24 gem tiers, 4 for each type, and they are the only rows in the corpus stating a rarity.

**Augmentations.** 562 of the 772 augmentation tags reach an asset, and the folder answers the kind
for every one the four references do not.

**Keys.** `(net_id, armor_type, primary_stat, hero)` is unique inside every kind.

**Grants.** 200 granted abilities and 111 legendary stat lines resolve to augmentation rows already
in the corpus, so promoting them mints no id.

**Rows written.** 12,421 of the 15,235 export rows are written, producing 12,422 entity rows.

---

## 8. Tests

**Kinds.** The resolver reaches the count in 2.4 for each of the 21 kinds. A talent is counted only
where a slot points at it, so a fixture adding an asset to a hero's talent folder does not raise the
talent count. `Talent.ID.Talent8` produces a talent named *Macabre Stratagem*. The 6 gem types are
named from the shipped table and a fixture renaming one keeps the count at 6.

**Drop rules.** `ItemID.GemType.Ruby` is written, being a branch that carries content;
`ItemID.HeroItem` is not, being a branch that carries none. A row of a written kind carrying nothing
is written with a null document.

**Keys.** Loading the corpus into each kind's table raises no duplicate key.

**Formats.** A run under `--output-format jsonl` and a run under `--output-format postgres` describe
the same rows: each kind's count agrees, and each row's document agrees field for field.

**Vocabulary.** `VocabularyFile` read back from the `vocabulary` table equals the one read from the
file, and `rt.Absorb` resolves against a table filing `rt.absorb`.

**The API.** The store answers the same tooltip for the same id and hero as it answers from the file
today, over every id in the corpus.

---

## 9. Order of work

1. The models and the kind resolver, with the counts in 2.4 as its tests. `ExportAllCommand` writes
   through them and goes on writing JSONL, so the change is measurable before anything else moves.
2. `IOutputFormat`, with the JSONL writer behind it and `--output-format` selecting it.
3. Postgres in the AppHost, the schema in section 5, and the Postgres format.
4. The API and the Codex browser on Postgres.

Section 5 of `2026-08-05-codex-rarity-links-variants-and-kinds-design.md` reads a row's kind from the
store rather than classifying it, once step 1 lands, and the slug table in its section 5.4 is the one
in section 6 here, whose slugs are the kind names. Its sections 2, 3 and 4 depend on none of this.
