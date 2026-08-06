# Entity types, typed models and a Postgres store: design

Every entity type gets a document model of its own. The extractor decides what type a row is, fills
that type's model, and correlates everything a reader's answer depends on while it still has the
cooked build open. An output format writes the run: JSONL, or one Postgres table per type.

---

## 1. Goal

**A model per type.** Each type has a class carrying the fields that type has and no others, so the
shape of an item is not the shape of an ability, and a field that is always present is not optional.

**A table per type.** Backed by Postgres, each type is a table of its own under a view that the
surfaces spanning types read. Enumerated columns are enumerated types.

**Correlation happens in the extractor.** Everything a reader's answer depends on that does not
depend on the reader is worked out once, at extraction, and stored. The API stops deriving it per
request and reads what the extractor wrote.

Type is decided from what the build states. Talent means a hero's talent, and the eight other things
filed in the augmentation table are each named for what they are.

---

## 2. What moves into the extractor

The API derives, per request, a set of answers that are the same for every request. Each is a
correlation the extractor is better placed to make, because it holds the cooked build, the shipped
tables and every other row at once.

| Answer | Derived today by | Stored by the extractor as |
|---|---|---|
| The name a reader is shown | `PublicTypes.NameOf`, `DisplayNameOf`, `WrittenAsWords` | `Name` on the model |
| Whether a shipped name is a name or a build label | `PublicTypes.WrittenAsWords` | absence of `Name` |
| The damage schools a row is | `Codex.Schools` over the tag list | `Schools` |
| Art per rarity, keyed by the printed rung | `PublicEntity.Art` | `IconByRarity` |
| Who lent a row its name and art | `PublicReference.From` | `NameBorrowedFrom`, `ArtBorrowedFrom` |
| An attribute key as the word the game prints | `Codex.Scaling`, `EntityStore.StatName` | the printed word |
| The order a description spends its tokens | `CodexView.Spends` | `Placeholders` as an ordered list |
| Which rows a reader may meet | `PublicTypes.IsPublic` | the type itself |
| The term list the omnibox runs against | `SearchIndex` at startup | a table of terms and postings |

Every internal spelling the API strips is stripped at extraction instead: the gameplay tag, the
tuning row keys under `Constants` and `Talents`, `TalentGates`, `GrantedTags`, `TalentSlot`,
`RuleSet`, `TraitCategoryTag`, a rank run's `Key`, and `GemAttribute`. A model carries the reader's
reading, so there is no second reading to strip.

What stays in the API is what depends on the caller. `Placeholders` turns a coefficient into a number
only once a hero or a stat value is named, `StatMagnitudes` needs the item's level and rarity, and
`StatConversion` needs the rating the caller holds. `RichText` renders to HTML, which is a choice
about presentation rather than a fact about the build.

---

## 3. What a row is

### 3.1 The type resolver

`TooltipResolver` already loads every map this reads. Asked of a row in this order:

**A reference the build states outright.**

1. A `Talent.ID` slot points at the augmentation: **talent**. `TalentTrees` holds the map.
2. The row carries a gem ladder node: **gem trait**. `GemDefinitions` holds the map.
3. An `ItemTrait.ID` row points at it: **trait power**. `ItemTraits` holds the map.
4. An `ItemID.SetBonus` row points at it: **set bonus power**. `ItemSetBonuses` holds the map.

**The folder the asset ships in**, for the types nothing points at:

| Folder | Type |
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

**The shipped table**, for every table holding one type: `GameplayAbility` is an **ability**,
`GameplayEffect` an **effect**, `CRAICharacter` a **creature**, `ItemTrait.ID` a **trait**, and a
`Talent.ID` row carrying grid coordinates a **talent slot**.

A row no rule reaches has no type and is not written. That default keeps a type out of the store
until someone has decided what it is.

A reference is asked before a folder because a folder holds more than the assets of one type:
`/Game/Abilities/Talents/Mosse` ships 33 assets and 18 of them are the talents Mosse's slots point
at. Reading the folder alone counts 467 talents where the build has 216.

### 3.2 The 216th talent

215 talent slots point at an augmentation carrying the talent's text. `Talent.ID.Talent8`, Mara's
row 4 column 1, carries its own name and description instead: *Macabre Stratagem*. `TalentTrees`
already reads a talent this way, choosing the slot's own text where the slot names no augmentation,
so the talent written for that slot is built from the slot row.

### 3.3 What is not written

Four rules, each derived from the corpus:

- **An empty branch of the tag tree.** A tag another tag hangs beneath, carrying nothing itself, is a
  branch rather than an entity: `ItemID.HeroItem`, `ItemID.VisualCollection`, `Modifiers.Ascension`.
  Emptiness is required, because `ItemID.GemType.Ruby` is a branch with four gem tiers beneath it and
  a gem type in its own right.
- **A row no rule in 3.1 reaches.**
- **A type decided against**: visual collections, player and community customization, a creature
  carrying nothing, and a mount whose name pairs with no `CA_Mount` augmentation.
- **A shipped enumeration's null member.** A tag whose leaf segment is `None` or `Invalid` names the
  member an enumeration reserves for the absence of a thing rather than a thing:
  `ItemID.GemType.Invalid`, `ItemID.CompanionPet.None`, `ItemID.PlayerCustomization.Avatar.None`,
  `ItemID.PlayerCustomization.Banner.None` and `ItemID.PlayerCustomization.Title.None` are the five
  tags in the build that end that way. Emptiness is required here as well, so a row carrying
  something under a name ending in one of those two words is written.

A row of a type that is written but carrying nothing yet is written. `ItemID.HeroItem.BowGuy_CC_Back`
is an item whose name, stats and art the extractor does not read yet, and it is a row of the `item`
table with its document null. What those rows are missing is read in work of its own, which fills
columns and moves no row.

### 3.4 The types

| Type | Rows | | Type | Rows |
|---|---:|---|---|---:|
| item | 5,744 | | gem tier | 24 |
| effect | 3,261 | | set bonus | 23 |
| ability | 2,307 | | set bonus power | 15 |
| creature | 232 | | finesse | 14 |
| talent | 216 | | weapon power | 12 |
| talent slot | 216 | | relic power | 11 |
| mount | 96 | | companion pet | 9 |
| trait | 62 | | gem type | 6 |
| gem trait | 60 | | legendary stat | 3 |
| mount power | 54 | | | |
| trait power | 40 | | | |
| currency | 32 | | | |

12,437 rows over 21 types, from the 12,436 export rows that are written: `Talent.ID.Talent8` is one
export row and two entities, its slot and the talent the slot carries.

2,804 export rows are not written, under the four rules in 3.3:

| Not written | Rows |
|---|---:|
| **a type decided against** | **1,097** |
| &nbsp;&nbsp;visual collections | 733 |
| &nbsp;&nbsp;player and community customization | 215 |
| &nbsp;&nbsp;creatures carrying nothing | 106 |
| &nbsp;&nbsp;mounts pairing with no augmentation | 43 |
| **reached by no rule** | **895** |
| **an empty branch of the tag tree** | **807** |
| **a shipped enumeration's null member** | **5** |

The mount row and the mounts pairing with no augmentation row are counted over the tags the pairing
rule in 3.3 pairs and drops directly. The earlier figures counted the unpaired mounts across the whole
mount tag tree, branches included, rather than the paired ones the rule writes.

The 895 are two populations. 547 are every row of a segment or a table no rule in 3.1 names: hero
skins, hero abilities, cosmetic swatches, progression, visual trims, SDT entries, the 21 `Talent.ID`
rows carrying no coordinates, and the tag tables whose rows say nothing at all. 348 are augmentation
rows that no reference claims and whose folder names no type, of which 213 ship no asset the resolver can find and 126 sit
in a hero's talent folder under a name no slot points at.

Named rows are among them, and each is a row the game would print that this store does not hold. 37
sit in hero talent folders under names no slot claims, 30 are visual collections carrying a name, a
description and an icon - *Wildspring Oracle* (1792), *Deepwarden's Oath* (1793), *Abyssal Touch*
(2534) - and 2 are Gunde's legendary traits, *Bloody Mess* and *Bleeding Hearts*.

---

## 4. The models

A class per type over a base carrying the two fields every row has:

```csharp
public abstract class EntityDocument
{
    public required int Id { get; init; }

    /// <summary>The name a reader is shown, and null where the build prints none.</summary>
    public string? Name { get; init; }
}
```

The class is the type, so no property restates it.

Each type's class carries the fields its census shows that type has, and no others, with a field
every row of that type states declared as required:

```csharp
public sealed class GemTier : EntityDocument
{
    public required GemType Type { get; init; }
    public required int Tier { get; init; }
    public required int Power { get; init; }
    public required Rarity Rarity { get; init; }
}

public sealed class TalentSlot : EntityDocument
{
    public required Hero Hero { get; init; }
    public required int Row { get; init; }
    public required int Column { get; init; }
    public required int Cost { get; init; }
    public required int TalentId { get; init; }
}
```

`Item` carries `Slot`, `Stats`, `ArmorType`, `PrimaryStat`, `Heroes`, `SetBonus`, `IconByRarity`,
`GrantsId` and `LegendaryStatIds`. `Talent` carries its text, its placeholders and its granted tags.
`Ability` carries the times, the costs and the distances an ability states.

`Placeholder`, `StatSlot` and `RankRun` carry over and are used by the types that have them.

### 4.1 The enumerated fields

A field whose values are a closed set the build states is an enum, not a string:

```csharp
public enum ArmorType { Cloth, Leather, Mail, Plate }
public enum PrimaryStat { Agility, Intellect, Strength }
public enum Hero { Aeona, Ardeos, Elarion, Gunde, Helena, Mara, Meiko, Rime, Sylvie, Tariq, Vigour, Xavian }
public enum GemType { Amethyst, Diamond, Emerald, Ruby, Sapphire, Topaz }
```

Every member is a word the game prints. A hero is named by the name a player reads and never by the
codename their assets are filed under, which is the rule `EntityStore` already holds and the reason
`?hero=Bowguy` resolves nowhere.

The members are the build's rather than this repo's, so they are generated from `vocabulary.json`
into the extractor and committed, and one generator emits both these and the `CREATE TYPE` statements
in 6.1. A build that adds a hero is caught rather than absorbed: a test asserts each enum's members
are exactly the set the current vocabulary states, so the run fails and the generator is re-run.
Committing them is what breaks the circle, since the vocabulary the members come from is itself
something a run produces.

`Rarity` is generated the same way and is keyed by ordinal, because a rung is filed under one word,
prints another, and prints differently again on a gem.

### 4.2 Rows that were nested

An item's granted ability and a legendary's stat lines are rows rather than nested documents. Both
already resolve through `LoadAugmentation`, and `AugmentationIds.For` answers the netId of an
augmentation path, so the id an item carries is the augmentation's own and nothing is minted:

- `Item.GrantsId` is a `relic power` or `weapon power` id.
- `Item.LegendaryStatIds` are `legendary stat` ids. 111 nested lines across 37 legendary items name
  3 distinct augmentations, so the rows they become are 3.

---

## 5. Writing a run

```csharp
public interface IOutputFormat : IAsyncDisposable
{
    Task WriteAsync(EntityDocument entity, CancellationToken cancellationToken);
    Task WriteVocabularyAsync(VocabularyFile vocabulary, CancellationToken cancellationToken);
}
```

The model is the whole of what crosses, and its runtime class is its type, so a third format is one
class.

`--output-format jsonl` writes `entities.jsonl` and `vocabulary.json` to the paths they are written
to today. `--output-format postgres` writes the tables in section 6. `jsonl` is the default, so a run
needs a database only when one is asked for.

The JSONL a run writes is the models serialised, so it is not the file the project writes today. A
line carries no gameplay tag, an attribute reads as the word the game prints, and an item states
`grantsId` and `legendaryStatIds` where it used to nest a whole ability and three stat lines. The two
files are compared by the counts in 3.4 and by named ids rather than by diff.

---

## 6. Postgres

Postgres 18 in Docker, added to the AppHost beside the storage emulator and carrying the same
per-worktree treatment: the data volume and the compose stack are named for `stack.Name` and the port
is `Port(builder, "Ports:Postgres", stack.Offset, 5432)`, because every address an AppHost claims is
global to the machine and several worktrees of this repo run at once.

### 6.1 Enumerated types

The same members the C# enums in 4.1 carry, emitted by the same generator so the two cannot drift:

```sql
CREATE TYPE armor_type   AS ENUM ('Cloth', 'Leather', 'Mail', 'Plate');
CREATE TYPE primary_stat AS ENUM ('Agility', 'Intellect', 'Strength');
CREATE TYPE hero         AS ENUM ('Aeona', 'Ardeos', 'Elarion', 'Gunde', 'Helena', 'Mara',
                                  'Meiko', 'Rime', 'Sylvie', 'Tariq', 'Vigour', 'Xavian');
CREATE TYPE gem_type     AS ENUM ('Amethyst', 'Diamond', 'Emerald', 'Ruby', 'Sapphire', 'Topaz');
```

`rarity` is emitted the same way from the shipped ladder, keyed by ordinal rather than by word,
because a rung is filed under one word and prints another and prints differently again on a gem.

### 6.2 A table per type

The document is the row. Every column beside `id` is generated from it and stored, so a column and
the document it came from cannot disagree and a writer states each fact once.

`talent_slot` is the shape where every generated column is present on every row. All 216 rows state a
hero, a grid position and a cost, which is what lets the key be a primary key:

```sql
CREATE TABLE talent_slot (
    id       integer NOT NULL,
    document jsonb   NOT NULL,
    hero     hero     GENERATED ALWAYS AS ((document->>'hero')::hero)  STORED NOT NULL,
    "row"    smallint GENERATED ALWAYS AS ((document->>'row')::smallint)    STORED NOT NULL,
    "column" smallint GENERATED ALWAYS AS ((document->>'column')::smallint) STORED NOT NULL,
    cost     smallint GENERATED ALWAYS AS ((document->>'cost')::smallint)   STORED NOT NULL,
    talent   integer  GENERATED ALWAYS AS ((document->>'talentId')::integer) STORED NOT NULL,
    PRIMARY KEY (id, hero)
);
```

`item` is the type an id does not identify on its own, because one item reads as a different named
item per armour type, primary stat and hero:

```sql
CREATE TABLE item (
    id           integer NOT NULL,
    document     jsonb,
    name         text         GENERATED ALWAYS AS (document->>'name') STORED,
    armor_type   armor_type   GENERATED ALWAYS AS ((document->>'armorType')::armor_type)     STORED,
    primary_stat primary_stat GENERATED ALWAYS AS ((document->>'primaryStat')::primary_stat) STORED,
    hero         hero         GENERATED ALWAYS AS ((document->>'hero')::hero)                STORED,
    UNIQUE NULLS NOT DISTINCT (id, armor_type, primary_stat, hero)
);

CREATE TABLE ability (
    id       integer PRIMARY KEY,
    document jsonb,
    name     text GENERATED ALWAYS AS (document->>'name') STORED
);
```

`id` is the one column that is not a projection, because it is the only thing a row carrying no
document has.

Measured over the corpus, `item` and `talent_slot` are the only two types where an id carries more
than one row: 188 item ids and 19 slot ids. Every other type takes `id` as its primary key and
generates `name` alone.

**Why `item`'s three discriminators are nullable and its key is not a primary key.** A generated
column is null wherever the document does not state its path, and on `item` that is most rows: the
corpus states an armour type on 26% of them, a primary stat on 22% and a single hero on 35%. A
primary key admits no null, so the key is `UNIQUE NULLS NOT DISTINCT`, which is what makes two rows
absent the same discriminators collide rather than both being written. The alternative, a `None`
member in each enum, would put a value in the store that the build does not state.

`document` is null on a row carrying nothing yet, which makes the extraction worklist a query:

```sql
SELECT count(*) FROM item WHERE document IS NULL;
```

### 6.3 The view every cross-type surface reads

```sql
CREATE VIEW entity AS
    SELECT 'item' AS type, id, name, document FROM item
    UNION ALL
    SELECT 'effect', id, name, document FROM effect
    UNION ALL ...;
```

Browse, the omnibox, the front page counts and `Nearby` read the view. A page about one type reads
its table.

### 6.4 Vocabulary and search

```sql
CREATE TABLE vocabulary (section text PRIMARY KEY, document jsonb NOT NULL);
CREATE TABLE search_term (term text NOT NULL, type text NOT NULL, id integer NOT NULL, weight real NOT NULL);
```

One vocabulary row per section: `colors` (123 rows), `textStyles` (21), `printFormats` (20), `heroes`
(12), `rarities` (7), `statCalculation`. `VocabularyFile` deserialises from the assembled document as
it does from the file, including the case-insensitive rebuild `EntityStore`'s constructor performs:
jsonb preserves key case and does not fold it, and the corpus writes `rt.Absorb` against a table
filing every style lower case.

`search_term` is the term list `SearchIndex` builds at startup today, built at extraction instead.

---

## 7. The API and the Codex browser

`EntityStore` reads Postgres and stops deriving what section 2 moves. The rules its doc comments
record and that remain its own move rather than get rewritten: a hero resolves by the name the game
prints and by nothing else, and `Reads` keeps the first clause that answers for the eighteen
Back-slot cloaks.

`PublicTypes` keys on type. The types the augmentation table used to hide are addressable, and a slug
is its type's name hyphenated: `talent`, `gem-trait`, `trait-power`, `set-bonus-power`,
`weapon-power`, `relic-power`, `legendary-stat`, `mount-power`, `finesse`.

`PublicEntity` is the model serialised. The fields it strips are absent from the model, and its
`Type` carries the type's slug.

---

## 8. What the build states

Every number is measured over `output/export/entities.jsonl`, `vocabulary.json` and
`asset-registry.jsonl`.

**Talents.** 216 talent slots carry grid coordinates, 18 for each of the 12 heroes, and each names
exactly one hero. 215 point at an augmentation and each points at a different one; `Talent.ID.Talent8`
points at none and carries *Macabre Stratagem* itself. 21 further `Talent.ID` rows carry no
coordinates.

**Gems.** 6 gem types: Amethyst, Diamond, Emerald, Ruby, Sapphire and Topaz. 60 gem traits, 10 for
each type. 24 gem tiers, 4 for each type, and they are the only rows in the corpus stating a rarity.

**Enumerated columns.** The corpus spells 4 armour types, 3 primary stats and 12 heroes, and each set
is exactly the set `vocabulary.json` states.

**Augmentations.** 559 of the 772 augmentation tags reach an asset under `TooltipResolver.FindAsset`,
which matches a tag to an asset by primary asset name and then by asset name, and never by prefix.
The folder answers the type for every one the four references do not. Matching by prefix as well
would reach 3 further `CA_Mount` tags, which is the whole of the difference it makes.

**Keys.** 188 item ids and 19 talent slot ids carry more than one row. No id of any other type does.

**Grants.** 200 granted abilities and 111 legendary stat lines resolve to augmentation rows already
in the corpus, so promoting them mints no id.

**Rows written.** 12,436 of the 15,240 export rows are written, producing 12,437 entity rows.

---

## 9. Tests

**Types.** The resolver reaches the count in 3.4 for each of the 21 types. A talent is counted only
where a slot points at it, so a fixture adding an asset to a hero's talent folder does not raise the
talent count. `Talent.ID.Talent8` produces a talent named *Macabre Stratagem*. The 6 gem types are
named from the shipped table and a fixture renaming one keeps the count at 6.

**Drop rules.** `ItemID.GemType.Ruby` is written, being a branch that carries content;
`ItemID.HeroItem` is not, being a branch that carries none. A row of a written type carrying nothing
is written with a null document.

**Correlation.** Each answer in section 2's table is on the model: a row whose shipped name is a
build label carries no `Name`, a fire ability carries its schools, an effect that borrowed its name
carries the row it borrowed from, and a description spends its placeholders in the order the list
gives them. No model carries a gameplay tag, a tuning row key or an attribute in the build's
spelling.

**Keys.** Loading the corpus raises no duplicate key, and `NULLS NOT DISTINCT` rejects a second item
row absent the same three discriminators.

**Enums.** Every value the corpus spells for an enumerated field is a member of that enum. Each C#
enum's members are exactly the set the current `vocabulary.json` states, which is the test that fails
when the build adds a hero, and each Postgres enum's members are its C# enum's.

**Generated columns.** A column and the document it is generated from agree, over every row of every
type: writing a document and reading the column back gives what the model held. A row whose document
states no armour type has a null `armor_type` rather than a stand-in member.

**Formats.** A run under `--output-format jsonl` and a run under `--output-format postgres` describe
the same rows: each type's count agrees, and each row's document agrees field for field.

**Vocabulary.** `VocabularyFile` read back from the `vocabulary` table equals the one read from the
file, and `rt.Absorb` resolves against a table filing `rt.absorb`.

**The API.** The store answers the same tooltip for the same id and hero as it answers from the file
today, over every id in the corpus.

---

## 10. Order of work

1. The models and the type resolver, with the counts in 3.4 as its tests. `ExportAllCommand` writes
   through them and goes on writing JSONL, so the change is measurable before anything else moves.
2. The correlation in section 2, moved answer by answer, each leaving the API as it lands on a model.
3. `IOutputFormat`, with the JSONL writer behind it and `--output-format` selecting it.
4. Postgres in the AppHost, the schema in section 6, and the Postgres format.
5. The API and the Codex browser on Postgres.

Section 5 of `2026-08-05-codex-rarity-links-variants-and-kinds-design.md` reads a row's type from the
store rather than classifying it, once step 1 lands, and the slug table in its section 5.4 is the one
in section 7 here, whose slugs are the type names. Its sections 2, 3 and 4 depend on none of this.
