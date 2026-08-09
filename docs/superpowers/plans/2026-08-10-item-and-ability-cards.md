# Item and ability cards implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Draw an item, an ability or effect, and everything else through cards of their own, dispatched from one switch expression in `Tooltip.razor`, writing byte-identical markup to what the tooltip writes today.

**Architecture:** `Tooltip.razor` becomes a dispatcher: a switch expression names a card type, `DynamicComponent` renders it, and `CascadingValue` carries the document. Each card owns its whole window and builds its own ordered `List<RenderFragment>`, which a loop separates with `<hr class="rule">`. The block machinery in `TooltipViewService` is deleted once every guard it held lives in the card that needs it.

**Tech Stack:** .NET 10, Blazor Server components, bunit 2.8.6, xunit.v3, Sass via SassCompiler.

## Global Constraints

- **The app must not be running.** `FellowshipExtractor.Api.exe` and Visual Studio hold `Fellowship.SDK.dll` in `src/FellowshipExtractor.Api/bin`, and every build fails at the copy step with MSB3027 while they do. Stop the app before Task 1.
- **Byte-identical output.** No card gets a root class of its own, no SCSS partial is added, and `Styles/tooltip.scss` keeps its three `@use` lines.
- **No comments.** Do not add `//`, `/* */`, XML doc comments or `<!-- -->` to any file in this plan.
- **No em dashes or en dashes** in code, markup, commit messages or UI text.
- **Never `git stash`.** The worktree is isolated; there is nothing to stash around.
- Work in `G:\source\Fellowship_Data_Extractor.item-and-ability-cards` on branch `item-and-ability-cards`, based on `9d38d2e`.
- Every card injects `ITooltipViewService`, and reads `Root`, `Fact`, `TraitLine` and `Magnitude` from the static `TooltipView`.
- Tests derive their inputs from `export.Store`, never from a hardcoded id.
- The dispatcher shape in Task 5 and the `@<TipFacts … />` template syntax in Tasks 2 to 4 were both compiled against this worktree before this plan was written, with no warnings. If either fails to build, the cause is in the surrounding edit, not the syntax.

---

### Task 1: Draw the baseline

**Files:**
- Create: none. This task writes only to a temp folder outside the repo.

**Interfaces:**
- Consumes: `TooltipDumpTests` as it already exists at `9d38d2e`.
- Produces: `%TEMP%\fx-tips\before`, the markup every later task is measured against.

- [ ] **Step 1: Confirm the tree is clean and on the right commit**

Run:
```powershell
git -C G:\source\Fellowship_Data_Extractor.item-and-ability-cards log --oneline -1
git -C G:\source\Fellowship_Data_Extractor.item-and-ability-cards status --short
```
Expected: the spec revision commit on top of `9d38d2e`, and no modified files.

- [ ] **Step 2: Draw the baseline**

Run:
```powershell
Set-Location G:\source\Fellowship_Data_Extractor.item-and-ability-cards
Remove-Item -Recurse -Force "$env:TEMP\fx-tips" -ErrorAction SilentlyContinue
$env:FELLOWSHIP_TIP_DUMP = "$env:TEMP\fx-tips\before"
dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~TooltipDumpTests" --nologo
```
Expected: PASS. If it fails with MSB3027 the app is still running; stop it and repeat.

- [ ] **Step 3: Confirm the baseline is not empty**

Run:
```powershell
(Get-ChildItem "$env:TEMP\fx-tips\before" -Filter *.html).Count
```
Expected: a count in the hundreds. If it is 0, the corpus did not load; check that `output` in the worktree is a junction to `G:\source\Fellowship_Data_Extractor\output` and that `output\export\entities.jsonl` exists.

- [ ] **Step 4: Run the whole suite green before touching anything**

Run:
```powershell
Set-Location G:\source\Fellowship_Data_Extractor.item-and-ability-cards
Remove-Item Env:\FELLOWSHIP_TIP_DUMP -ErrorAction SilentlyContinue
dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --nologo
```
Expected: PASS. Record the passing test count; every later task must match or exceed it.

---

### Task 2: Item.razor

**Files:**
- Create: `src/FellowshipExtractor.Api/Components/Tooltip/Item.razor`
- Test: `src/FellowshipExtractor.Api.Tests/ItemCardTests.cs`

**Interfaces:**
- Consumes: `ITooltipViewService` (`Rarity`, `Art`, `Ornament`, `Level`, `Temper`, `Style`, `Facts`, `Rolls`, `Traits`, `Potential`, `Granted`, `LegendaryStats`, `Variables`), the static `TooltipView.Root`, and the `Tip*` components.
- Produces: `FellowshipExtractor.Api.Components.Tooltip.Item`, a component taking `[CascadingParameter] EntityDocument Shown`, `[Parameter] TooltipQuery Query` and `[Parameter] HeroEntry? Hero`. Task 5 names `typeof(Item)` in the dispatch.

- [ ] **Step 1: Write the failing test**

Create `src/FellowshipExtractor.Api.Tests/ItemCardTests.cs`:

```csharp
using System.Globalization;

using FellowshipExtractor.Api.Components.Tooltip;

namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class ItemCardTests : BunitContext
{
    private readonly Export export;

    public ItemCardTests(Export export)
    {
        this.export = export;
        Services.AddSingleton(export.Tooltip);
        Services.AddSingleton(export.View);
    }

    private IEnumerable<ItemDocument> Items => export.Store.Entities.OfType<ItemDocument>();

    private IRenderedComponent<Item> Draw(ItemDocument document, TooltipQuery query) =>
        Render<Item>(p => p
            .AddCascadingValue((EntityDocument)document)
            .Add(x => x.Query, query));

    [Fact]
    public void An_item_draws_its_art_beside_the_card()
    {
        var worn = Items.First(row => row.Icon is not null || row.IconByRarity is { Count: > 0 });

        var drawn = Draw(worn, new TooltipQuery());

        Assert.NotEmpty(drawn.FindAll($".{TooltipView.Root} > figure.slot > img.art"));
        Assert.NotNull(drawn.Find($".{TooltipView.Root} > .card > .head > .title"));
    }

    [Fact]
    public void The_rung_asks_for_the_rarity_chrome_and_its_absence_asks_for_the_plain_window()
    {
        var rung = export.Store.Vocabulary.Rarities[^1];
        var asked = new TooltipQuery { Rarity = rung.Tier.ToString(CultureInfo.InvariantCulture) };

        var coloured = Draw(Items.First(), asked);
        var root = coloured.Find($".{TooltipView.Root}");

        Assert.DoesNotContain("plain", root.ClassList, StringComparer.Ordinal);
        Assert.NotEmpty(coloured.FindAll(".card > .head > img.orn"));
        Assert.NotEmpty(coloured.FindAll(".lines .rar"));
    }

    [Fact]
    public void A_granted_power_draws_the_ability_group_under_its_name()
    {
        var granting = Items.First(row =>
            row.GrantsId is not null && export.Tooltip.Granted(row) is not null);

        var drawn = Draw(granting, new TooltipQuery());

        var badge = drawn.Find(".lines .sec.ability span:last-child").TextContent;

        Assert.StartsWith("Ability", badge, StringComparison.Ordinal);
    }

    [Fact]
    public void Legendary_stats_and_a_set_bonus_each_draw_a_group_of_their_own()
    {
        var rolled = Items.First(row => export.Tooltip.LegendaryStats(row).Count > 0);
        Assert.NotEmpty(Draw(rolled, new TooltipQuery()).FindAll(".lines .sec.legendary"));

        var setted = Items.First(row => row.SetBonus is { Count: > 0 });
        Assert.NotEmpty(Draw(setted, new TooltipQuery()).FindAll(".lines .sec.setbonus"));
    }

    [Fact]
    public void A_rule_sits_between_every_pair_of_groups_and_never_above_the_first()
    {
        var drawn = Draw(Items.First(row => row.SetBonus is { Count: > 0 }), new TooltipQuery());

        var lines = drawn.Find(".lines");
        var children = lines.Children;

        Assert.NotEqual("HR", children[0].TagName, StringComparer.OrdinalIgnoreCase);

        var rules = children.Count(child => string.Equals(child.TagName, "HR", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(children.Length - rules - 1, rules);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:
```powershell
Set-Location G:\source\Fellowship_Data_Extractor.item-and-ability-cards
dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~ItemCardTests" --nologo
```
Expected: FAIL to build, with `CS0246: The type or namespace name 'Item' could not be found`.

- [ ] **Step 3: Write the card**

Create `src/FellowshipExtractor.Api/Components/Tooltip/Item.razor`:

```razor
@inject ITooltipViewService Ui

<div class="@TooltipView.Root @(Rung is null ? "plain" : null)" style="@Ui.Variables(Rung)">
    <style>@Sheet</style>
    @if (Art is { } art)
    {
        <figure class="slot"><img class="art" alt="" src="@art"></figure>
    }
    <div class="card">
        <header class="head">
            @if (Ui.Ornament(Rung) is { } ornament)
            {
                <img class="orn" alt="" src="@ornament">
            }

            <h3 class="title">@PublicTypes.NameOf(Entity)</h3>
        </header>
        @if (groups.Count > 0)
        {
            <div class="lines">
                @for (var at = 0; at < groups.Count; at++)
                {
                    @if (at > 0) { <hr class="rule"> }
                    @groups[at]
                }
            </div>
        }
    </div>
</div>

@code {
    [CascadingParameter] public EntityDocument Shown { get; set; } = default!;

    [Parameter] public TooltipQuery Query { get; set; } = new();

    [Parameter] public HeroEntry? Hero { get; set; }

    private ItemDocument Entity => (ItemDocument)Shown;

    private RarityEntry? Rung;

    private string? Art;

    private int? Level;

    private string? Temper;

    private string? Named;

    private MarkupString Sheet;

    private EntityDocument? granted;

    private IReadOnlyList<TooltipView.Fact> facts = [];

    private IReadOnlyList<StatLine> rolls = [];

    private IReadOnlyList<TooltipView.TraitLine> traits = [];

    private IReadOnlyList<RenderFragment> groups = [];

    protected override void OnParametersSet()
    {
        Rung = Ui.Rarity(Entity, Query);
        Art = Ui.Art(Entity, Rung);
        Level = Ui.Level(Entity, Query);
        Temper = Ui.Temper(Query, Rung, Level);
        Sheet = new MarkupString(Ui.Style());
        facts = Ui.Facts(Entity, Query);
        rolls = Ui.Rolls(Entity, Query, Rung);
        traits = Ui.Traits(Query);
        granted = Ui.Granted(Entity);
        Named = granted is null ? null : PublicTypes.DisplayNameOf(granted);

        groups = Groups();
    }

    private List<RenderFragment> Groups()
    {
        var drawn = new List<RenderFragment>(8);

        if (Identity)
        {
            drawn.Add(@<TipIdentity Entity="Entity" Query="Query" Rung="Rung" Level="Level" Temper="@Temper" />);
        }

        if (facts.Count > 0) drawn.Add(@<TipFacts Items="facts" />);

        if (Ui.Potential(Query) is not null || rolls.Count > 0)
        {
            drawn.Add(@<TipRolls Potential="@Ui.Potential(Query)" Lines="rolls" />);
        }

        if (traits.Count > 0 || Query.AllocatedTraits is not null || Query.MaxTraits is not null)
        {
            drawn.Add(@<TipTraits Lines="traits" Allocated="Query.AllocatedTraits" Total="Query.MaxTraits" />);
        }

        if (Entity.Description is not null)
        {
            drawn.Add(@<TipProse Entity="Entity" Query="Query" Hero="Hero" />);
        }

        if (Grants)
        {
            drawn.Add(@<TipGranted Entity="Entity" Query="Query" Hero="Hero" Named="@Named" />);
        }

        if (Ui.LegendaryStats(Entity).Count > 0)
        {
            drawn.Add(@<TipLegendaries Entity="Entity" Query="Query" Hero="Hero" />);
        }

        if (Entity.SetBonus is { Count: > 0 }) drawn.Add(@<TipSets Entity="Entity" />);

        return drawn;
    }

    private bool Identity =>
        Entity.ArmorType is not null || Entity.Slot is not null || Level is not null ||
        Rung is not null || Temper is not null ||
        Entity.GemSlot == true || Entity.UniqueEquipped == true;

    private bool Grants =>
        granted is { } power &&
        (power.Name ?? DocumentFields.Text(power, nameof(AbilityDocument.Description))) is not null;
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run:
```powershell
Set-Location G:\source\Fellowship_Data_Extractor.item-and-ability-cards
dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~ItemCardTests" --nologo
```
Expected: PASS, 5 tests.

- [ ] **Step 5: Commit**

```bash
git add src/FellowshipExtractor.Api/Components/Tooltip/Item.razor src/FellowshipExtractor.Api.Tests/ItemCardTests.cs
git commit -m "Draw an item on a card of its own"
```

---

### Task 3: Ability.razor

**Files:**
- Create: `src/FellowshipExtractor.Api/Components/Tooltip/Ability.razor`
- Test: `src/FellowshipExtractor.Api.Tests/AbilityCardTests.cs`

**Interfaces:**
- Consumes: the same `ITooltipViewService` members Task 2 consumes, less `Granted` and `LegendaryStats`.
- Produces: `FellowshipExtractor.Api.Components.Tooltip.Ability`, taking `[CascadingParameter] EntityDocument Shown`, `[Parameter] TooltipQuery Query`, `[Parameter] HeroEntry? Hero`. It draws both `AbilityDocument` and `EffectDocument`. Task 5 names `typeof(Ability)`.

- [ ] **Step 1: Write the failing test**

Create `src/FellowshipExtractor.Api.Tests/AbilityCardTests.cs`:

```csharp
using System.Globalization;

using FellowshipExtractor.Api.Components.Tooltip;

namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class AbilityCardTests : BunitContext
{
    private readonly Export export;

    public AbilityCardTests(Export export)
    {
        this.export = export;
        Services.AddSingleton(export.Tooltip);
        Services.AddSingleton(export.View);
    }

    private IRenderedComponent<Ability> Draw(EntityDocument document, TooltipQuery query) =>
        Render<Ability>(p => p
            .AddCascadingValue(document)
            .Add(x => x.Query, query));

    private AbilityDocument Spell => export.Store.Entities.OfType<AbilityDocument>()
        .First(row => row.Description is not null && row.Cooldown is not null);

    private EffectDocument Aura => export.Store.Entities.OfType<EffectDocument>()
        .First(row => row.Description is not null);

    [Fact]
    public void An_ability_and_an_effect_draw_the_same_card()
    {
        Assert.NotNull(Draw(Spell, new TooltipQuery()).Find($".{TooltipView.Root} > .card"));
        Assert.NotNull(Draw(Aura, new TooltipQuery()).Find($".{TooltipView.Root} > .card"));
    }

    [Fact]
    public void The_facts_row_carries_the_numbers_the_document_states()
    {
        var drawn = Draw(Spell, new TooltipQuery());

        var labels = drawn.FindAll(".lines .row .k").Select(cell => cell.TextContent).ToArray();

        Assert.Contains("Cooldown", labels, StringComparer.Ordinal);
    }

    [Fact]
    public void A_bare_ability_draws_the_plain_window_and_no_identity_group()
    {
        var drawn = Draw(Spell, new TooltipQuery());
        var root = drawn.Find($".{TooltipView.Root}");

        Assert.Contains("plain", root.ClassList, StringComparer.Ordinal);
        Assert.Empty(drawn.FindAll(".lines .rar"));
        Assert.Empty(drawn.FindAll(".card > .head > img.orn"));
    }

    [Fact]
    public void A_rarity_in_the_query_puts_the_rarity_chrome_on_an_ability()
    {
        var rung = export.Store.Vocabulary.Rarities[^1];

        var drawn = Draw(Spell, new TooltipQuery
        {
            Rarity = rung.Tier.ToString(CultureInfo.InvariantCulture),
        });

        Assert.DoesNotContain("plain", drawn.Find($".{TooltipView.Root}").ClassList, StringComparer.Ordinal);
        Assert.NotEmpty(drawn.FindAll(".card > .head > img.orn"));
        Assert.NotEmpty(drawn.FindAll(".lines .rar"));
    }

    [Fact]
    public void The_prose_reads_the_description_in_the_styles_the_build_ships()
    {
        var styled = export.Store.Entities.OfType<AbilityDocument>().First(row =>
            row.Description is { } text && text.Contains("<rt.", StringComparison.Ordinal));

        var prose = Draw(styled, new TooltipQuery()).Find(".lines .desc");

        Assert.NotEmpty(prose.QuerySelectorAll("span.rt"));
        Assert.DoesNotContain("<rt.", prose.TextContent, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:
```powershell
Set-Location G:\source\Fellowship_Data_Extractor.item-and-ability-cards
dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~AbilityCardTests" --nologo
```
Expected: FAIL to build, with `CS0246: The type or namespace name 'Ability' could not be found`.

- [ ] **Step 3: Write the card**

Create `src/FellowshipExtractor.Api/Components/Tooltip/Ability.razor`:

```razor
@inject ITooltipViewService Ui

<div class="@TooltipView.Root @(Rung is null ? "plain" : null)" style="@Ui.Variables(Rung)">
    <style>@Sheet</style>
    @if (Art is { } art)
    {
        <figure class="slot"><img class="art" alt="" src="@art"></figure>
    }
    <div class="card">
        <header class="head">
            @if (Ui.Ornament(Rung) is { } ornament)
            {
                <img class="orn" alt="" src="@ornament">
            }

            <h3 class="title">@PublicTypes.NameOf(Entity)</h3>
        </header>
        @if (groups.Count > 0)
        {
            <div class="lines">
                @for (var at = 0; at < groups.Count; at++)
                {
                    @if (at > 0) { <hr class="rule"> }
                    @groups[at]
                }
            </div>
        }
    </div>
</div>

@code {
    [CascadingParameter] public EntityDocument Shown { get; set; } = default!;

    [Parameter] public TooltipQuery Query { get; set; } = new();

    [Parameter] public HeroEntry? Hero { get; set; }

    private EntityDocument Entity => Shown;

    private RarityEntry? Rung;

    private string? Art;

    private int? Level;

    private string? Temper;

    private MarkupString Sheet;

    private IReadOnlyList<TooltipView.Fact> facts = [];

    private IReadOnlyList<TooltipView.TraitLine> traits = [];

    private IReadOnlyList<RenderFragment> groups = [];

    protected override void OnParametersSet()
    {
        Rung = Ui.Rarity(Entity, Query);
        Art = Ui.Art(Entity, Rung);
        Level = Ui.Level(Entity, Query);
        Temper = Ui.Temper(Query, Rung, Level);
        Sheet = new MarkupString(Ui.Style());
        facts = Ui.Facts(Entity, Query);
        traits = Ui.Traits(Query);

        groups = Groups();
    }

    private List<RenderFragment> Groups()
    {
        var drawn = new List<RenderFragment>(5);

        if (Level is not null || Rung is not null || Temper is not null)
        {
            drawn.Add(@<TipIdentity Entity="Entity" Query="Query" Rung="Rung" Level="Level" Temper="@Temper" />);
        }

        if (facts.Count > 0) drawn.Add(@<TipFacts Items="facts" />);

        if (Ui.Potential(Query) is not null)
        {
            drawn.Add(@<TipRolls Potential="@Ui.Potential(Query)" />);
        }

        if (traits.Count > 0 || Query.AllocatedTraits is not null || Query.MaxTraits is not null)
        {
            drawn.Add(@<TipTraits Lines="traits" Allocated="Query.AllocatedTraits" Total="Query.MaxTraits" />);
        }

        if (Prose) drawn.Add(@<TipProse Entity="Entity" Query="Query" Hero="Hero" />);

        return drawn;
    }

    private bool Prose =>
        DocumentFields.Text(Entity, nameof(AbilityDocument.Description)) is not null ||
        DocumentFields.Words(Entity, nameof(AbilityDocument.Detail)) is { Count: > 0 };
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run:
```powershell
Set-Location G:\source\Fellowship_Data_Extractor.item-and-ability-cards
dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~AbilityCardTests" --nologo
```
Expected: PASS, 5 tests.

- [ ] **Step 5: Commit**

```bash
git add src/FellowshipExtractor.Api/Components/Tooltip/Ability.razor src/FellowshipExtractor.Api.Tests/AbilityCardTests.cs
git commit -m "Draw an ability and an effect on one card"
```

---

### Task 4: Generic.razor

**Files:**
- Create: `src/FellowshipExtractor.Api/Components/Tooltip/Generic.razor`
- Test: `src/FellowshipExtractor.Api.Tests/GenericCardTests.cs`

**Interfaces:**
- Consumes: the same `ITooltipViewService` members Task 3 consumes, plus nothing new.
- Produces: `FellowshipExtractor.Api.Components.Tooltip.Generic`, taking `[CascadingParameter] EntityDocument Shown`, `[Parameter] TooltipQuery Query`, `[Parameter] HeroEntry? Hero`. Task 5 names `typeof(Generic)` as the fall-through.

- [ ] **Step 1: Write the failing test**

Create `src/FellowshipExtractor.Api.Tests/GenericCardTests.cs`:

```csharp
using FellowshipExtractor.Api.Components.Tooltip;

namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class GenericCardTests : BunitContext
{
    private readonly Export export;

    public GenericCardTests(Export export)
    {
        this.export = export;
        Services.AddSingleton(export.Tooltip);
        Services.AddSingleton(export.View);
    }

    private IRenderedComponent<Generic> Draw(EntityDocument document, TooltipQuery query) =>
        Render<Generic>(p => p
            .AddCascadingValue(document)
            .Add(x => x.Query, query));

    [Fact]
    public void A_gem_draws_its_kind_in_the_identity_group()
    {
        var gem = export.Store.Entities.OfType<GemDocument>().First();

        Assert.NotEmpty(Draw(gem, new TooltipQuery()).FindAll(".lines .kind"));
    }

    [Fact]
    public void A_currency_carries_the_rarity_chrome_its_own_rung_asks_for()
    {
        var coin = export.Store.Entities.OfType<CurrencyDocument>().FirstOrDefault(row => row.Rarity is not null);

        Assert.SkipWhen(coin is null, "The build carries no currency with a rarity.");

        Assert.DoesNotContain("plain", Draw(coin!, new TooltipQuery()).Find($".{TooltipView.Root}").ClassList, StringComparer.Ordinal);
    }

    [Fact]
    public void A_gem_type_draws_its_overflow_wording_in_a_group_of_its_own()
    {
        var overflowing = export.Store.Entities.OfType<GemTypeDocument>()
            .FirstOrDefault(row => row.OverflowDescription is not null);

        Assert.SkipWhen(overflowing is null, "The build carries no gem type with overflow wording.");

        var drawn = Draw(overflowing!, new TooltipQuery());

        Assert.Contains(
            "Past Max Power",
            drawn.Find(".lines").TextContent,
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_power_the_query_says_nothing_about_draws_only_the_groups_it_carries()
    {
        var power = export.Store.Entities.OfType<RelicPowerDocument>().First();

        var drawn = Draw(power, new TooltipQuery());

        Assert.Empty(drawn.FindAll(".lines .stats"));
        Assert.Empty(drawn.FindAll(".lines .sec.setbonus"));
        Assert.Empty(drawn.FindAll(".lines .sec.legendary"));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:
```powershell
Set-Location G:\source\Fellowship_Data_Extractor.item-and-ability-cards
dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~GenericCardTests" --nologo
```
Expected: FAIL to build, with `CS0246: The type or namespace name 'Generic' could not be found`.

- [ ] **Step 3: Write the card**

Create `src/FellowshipExtractor.Api/Components/Tooltip/Generic.razor`:

```razor
@inject ITooltipViewService Ui

<div class="@TooltipView.Root @(Rung is null ? "plain" : null)" style="@Ui.Variables(Rung)">
    <style>@Sheet</style>
    @if (Art is { } art)
    {
        <figure class="slot"><img class="art" alt="" src="@art"></figure>
    }
    <div class="card">
        <header class="head">
            @if (Ui.Ornament(Rung) is { } ornament)
            {
                <img class="orn" alt="" src="@ornament">
            }

            <h3 class="title">@PublicTypes.NameOf(Entity)</h3>
        </header>
        @if (groups.Count > 0)
        {
            <div class="lines">
                @for (var at = 0; at < groups.Count; at++)
                {
                    @if (at > 0) { <hr class="rule"> }
                    @groups[at]
                }
            </div>
        }
    </div>
</div>

@code {
    [CascadingParameter] public EntityDocument Shown { get; set; } = default!;

    [Parameter] public TooltipQuery Query { get; set; } = new();

    [Parameter] public HeroEntry? Hero { get; set; }

    private EntityDocument Entity => Shown;

    private RarityEntry? Rung;

    private string? Art;

    private int? Level;

    private string? Temper;

    private MarkupString Sheet;

    private IReadOnlyList<TooltipView.Fact> facts = [];

    private IReadOnlyList<TooltipView.TraitLine> traits = [];

    private IReadOnlyList<RenderFragment> groups = [];

    protected override void OnParametersSet()
    {
        Rung = Ui.Rarity(Entity, Query);
        Art = Ui.Art(Entity, Rung);
        Level = Ui.Level(Entity, Query);
        Temper = Ui.Temper(Query, Rung, Level);
        Sheet = new MarkupString(Ui.Style());
        facts = Ui.Facts(Entity, Query);
        traits = Ui.Traits(Query);

        groups = Groups();
    }

    private List<RenderFragment> Groups()
    {
        var drawn = new List<RenderFragment>(6);

        if (Kind || Level is not null || Rung is not null || Temper is not null)
        {
            drawn.Add(@<TipIdentity Entity="Entity" Query="Query" Rung="Rung" Level="Level" Temper="@Temper" />);
        }

        if (facts.Count > 0) drawn.Add(@<TipFacts Items="facts" />);

        if (Ui.Potential(Query) is not null)
        {
            drawn.Add(@<TipRolls Potential="@Ui.Potential(Query)" />);
        }

        if (traits.Count > 0 || Query.AllocatedTraits is not null || Query.MaxTraits is not null)
        {
            drawn.Add(@<TipTraits Lines="traits" Allocated="Query.AllocatedTraits" Total="Query.MaxTraits" />);
        }

        if (Prose) drawn.Add(@<TipProse Entity="Entity" Query="Query" Hero="Hero" />);

        if (Entity is GemTypeDocument { OverflowDescription: not null })
        {
            drawn.Add(@<TipOverflow Entity="Entity" Query="Query" Hero="Hero" />);
        }

        return drawn;
    }

    private bool Kind => Entity is GemDocument or GemTraitDocument or GemTypeDocument;

    private bool Prose =>
        DocumentFields.Text(Entity, nameof(AbilityDocument.Description)) is not null ||
        DocumentFields.Words(Entity, nameof(AbilityDocument.Detail)) is { Count: > 0 };
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run:
```powershell
Set-Location G:\source\Fellowship_Data_Extractor.item-and-ability-cards
dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~GenericCardTests" --nologo
```
Expected: PASS, 4 tests (some may report as skipped if the build carries no such row).

- [ ] **Step 5: Commit**

```bash
git add src/FellowshipExtractor.Api/Components/Tooltip/Generic.razor src/FellowshipExtractor.Api.Tests/GenericCardTests.cs
git commit -m "Draw every remaining type on the generic card"
```

---

### Task 5: The dispatch

This is the switchover. Until it lands the three new cards are unreachable; after it lands `Tooltip.razor` renders nothing itself.

**Files:**
- Modify: `src/FellowshipExtractor.Api/Components/Tooltip/Tooltip.razor` (whole file)
- Modify: `src/FellowshipExtractor.Api/Components/Tooltip/Blessing.razor:23`
- Modify: `src/FellowshipExtractor.Api/Components/Tooltip/Trait.razor:23`
- Test: `src/FellowshipExtractor.Api.Tests/CardTests.cs`

**Interfaces:**
- Consumes: `Item`, `Ability`, `Generic` from Tasks 2 to 4, and the existing `Blessing` and `Trait`.
- Produces: `Tooltip` keeps its three parameters, so `EntityPage.razor` and every `Endpoints/<Type>/<Type>TooltipEndpoint.cs` are untouched.

- [ ] **Step 1: Write the failing test**

Create `src/FellowshipExtractor.Api.Tests/CardTests.cs`:

```csharp
using FellowshipExtractor.Api.Components.Tooltip;

namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class CardTests : BunitContext
{
    private readonly Export export;

    public CardTests(Export export)
    {
        this.export = export;
        Services.AddSingleton(export.Tooltip);
        Services.AddSingleton(export.View);
    }

    private IRenderedComponent<Tooltip> Draw(EntityDocument document) =>
        Render<Tooltip>(p => p.Add(x => x.Entity, document).Add(x => x.Query, new TooltipQuery()));

    private EntityDocument? FirstOf(EntityType type) => export.Store.Entities
        .Where(row => EntityTypes.TypeOf(row) == type)
        .OrderBy(row => row.Id)
        .FirstOrDefault();

    [Fact]
    public void Every_type_the_build_carries_is_drawn_by_exactly_one_card()
    {
        foreach (var type in Enum.GetValues<EntityType>())
        {
            if (FirstOf(type) is not { } document) continue;

            var drawn = Draw(document);

            var cards =
                drawn.FindComponents<Item>().Count +
                drawn.FindComponents<Ability>().Count +
                drawn.FindComponents<Generic>().Count +
                drawn.FindComponents<Blessing>().Count +
                drawn.FindComponents<Trait>().Count;

            Assert.True(cards == 1, $"{EntityTypes.Slug(type)} drew {cards} cards.");
            Assert.NotNull(drawn.Find($".{TooltipView.Root}"));
        }
    }

    [Fact]
    public void The_type_names_the_card()
    {
        Assert.Single(Draw(export.Store.Entities.OfType<ItemDocument>().First()).FindComponents<Item>());
        Assert.Single(Draw(export.Store.Entities.OfType<AbilityDocument>().First()).FindComponents<Ability>());
        Assert.Single(Draw(export.Store.Entities.OfType<EffectDocument>().First()).FindComponents<Ability>());
        Assert.Single(Draw(export.Store.Entities.OfType<BlessingDocument>().First()).FindComponents<Blessing>());
        Assert.Single(Draw(export.Store.Entities.OfType<TraitDocument>().First()).FindComponents<Trait>());
        Assert.Single(Draw(export.Store.Entities.OfType<GemDocument>().First()).FindComponents<Generic>());
    }

    [Fact]
    public void The_dispatcher_adds_no_element_of_its_own()
    {
        var markup = Draw(export.Store.Entities.OfType<ItemDocument>().First()).Markup.TrimStart();

        Assert.StartsWith("<div class=\"fx-tip", markup, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:
```powershell
Set-Location G:\source\Fellowship_Data_Extractor.item-and-ability-cards
dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~CardTests" --nologo
```
Expected: FAIL. `Every_type_the_build_carries_is_drawn_by_exactly_one_card` reports 0 cards for an item, because `Tooltip.razor` still draws the rump itself.

- [ ] **Step 3: Replace Tooltip.razor**

Replace the whole of `src/FellowshipExtractor.Api/Components/Tooltip/Tooltip.razor` with:

```razor
<CascadingValue Value="Entity">
    <DynamicComponent Type="GetTooltipComponent(Entity)" Parameters="Knobs" />
</CascadingValue>

@code {
    [Parameter, EditorRequired] public EntityDocument Entity { get; set; } = default!;

    [Parameter] public TooltipQuery Query { get; set; } = new();

    [Parameter] public HeroEntry? Hero { get; set; }

    private Dictionary<string, object?> Knobs =>
        new(StringComparer.Ordinal) { ["Query"] = Query, ["Hero"] = Hero };

    private static Type GetTooltipComponent(EntityDocument document) => document switch
    {
        BlessingDocument => typeof(Blessing),
        TraitDocument => typeof(Trait),
        ItemDocument => typeof(Item),
        AbilityDocument or EffectDocument => typeof(Ability),
        _ => typeof(Generic),
    };
}
```

- [ ] **Step 4: Move Blessing.razor onto the cascade**

In `src/FellowshipExtractor.Api/Components/Tooltip/Blessing.razor`, replace this line:

```csharp
    [Parameter, EditorRequired] public BlessingDocument Entity { get; set; } = default!;
```

with:

```csharp
    [CascadingParameter] public EntityDocument Shown { get; set; } = default!;

    private BlessingDocument Entity => (BlessingDocument)Shown;
```

Leave every other line of the file alone.

- [ ] **Step 5: Move Trait.razor onto the cascade**

In `src/FellowshipExtractor.Api/Components/Tooltip/Trait.razor`, replace this line:

```csharp
    [Parameter, EditorRequired] public TraitDocument Entity { get; set; } = default!;
```

with:

```csharp
    [CascadingParameter] public EntityDocument Shown { get; set; } = default!;

    private TraitDocument Entity => (TraitDocument)Shown;
```

Leave every other line of the file alone.

- [ ] **Step 6: Run the whole suite**

Run:
```powershell
Set-Location G:\source\Fellowship_Data_Extractor.item-and-ability-cards
dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --nologo
```
Expected: PASS, at the Task 1 count plus the tests added in Tasks 2 to 5. `TooltipTests`, `InsetArtTests`, `StateTests`, `SheetTests`, `BlessingCardTests`, `TraitCardTests` and `WindowChromeTests` must all still pass without being edited. If one of them fails, the card is not byte-identical; fix the card, not the test.

- [ ] **Step 7: Draw the after dump and diff it**

Run:
```powershell
Set-Location G:\source\Fellowship_Data_Extractor.item-and-ability-cards
$env:FELLOWSHIP_TIP_DUMP = "$env:TEMP\fx-tips\after"
dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --filter "FullyQualifiedName~TooltipDumpTests" --nologo
Remove-Item Env:\FELLOWSHIP_TIP_DUMP
Compare-Object `
  (Get-ChildItem "$env:TEMP\fx-tips\before" -Filter *.html | ForEach-Object { $_.Name + "|" + (Get-FileHash $_.FullName -Algorithm SHA256).Hash }) `
  (Get-ChildItem "$env:TEMP\fx-tips\after"  -Filter *.html | ForEach-Object { $_.Name + "|" + (Get-FileHash $_.FullName -Algorithm SHA256).Hash })
```
Expected: no output at all. `Compare-Object` printing nothing is the proof this was an extraction.

If it prints rows, diff one offending pair to see what moved:
```powershell
$name = "<the file name from the Compare-Object output>"
git diff --no-index "$env:TEMP\fx-tips\before\$name" "$env:TEMP\fx-tips\after\$name"
```
Fix the card until the comparison is silent. Do not edit `TooltipDumpTests` to make it agree.

- [ ] **Step 8: Commit**

```bash
git add src/FellowshipExtractor.Api/Components/Tooltip/Tooltip.razor src/FellowshipExtractor.Api/Components/Tooltip/Blessing.razor src/FellowshipExtractor.Api/Components/Tooltip/Trait.razor src/FellowshipExtractor.Api.Tests/CardTests.cs
git commit -m "Name the card in one switch and let the document cascade"
```

---

### Task 6: Retire the block machinery

**Files:**
- Modify: `src/FellowshipExtractor.Api/Services/TooltipViewService.cs` (the `Blocks` member of `ITooltipViewService`, the `Blocks` method, the private `HasKind`, the `Rolls` signature)
- Modify: `src/FellowshipExtractor.Api/Models/TooltipView.cs` (the `Block` enum)

**Interfaces:**
- Consumes: nothing new.
- Produces: `ITooltipViewService.Rolls(ItemDocument document, TooltipQuery query, RarityEntry? rarity)`. No later task depends on this.

- [ ] **Step 1: Confirm nothing outside the service still reads them**

Run:
```powershell
Set-Location G:\source\Fellowship_Data_Extractor.item-and-ability-cards
Select-String -Path src -Include *.cs,*.razor -Pattern "\.Blocks\(|TooltipView\.Block\b|HasKind" -Recurse |
  Where-Object { $_.Path -notmatch "\\obj\\|\\bin\\" }
```
Expected: only lines inside `src/FellowshipExtractor.Api/Services/TooltipViewService.cs` and the `Block` declaration in `src/FellowshipExtractor.Api/Models/TooltipView.cs`. If anything else appears, stop and report it rather than deleting.

- [ ] **Step 2: Confirm Rolls has exactly one caller**

Run:
```powershell
Set-Location G:\source\Fellowship_Data_Extractor.item-and-ability-cards
Select-String -Path src -Include *.cs,*.razor -Pattern "Ui\.Rolls\(|\.Rolls\(" -Recurse |
  Where-Object { $_.Path -notmatch "\\obj\\|\\bin\\" }
```
Expected: the interface declaration, the implementation, and `Components/Tooltip/Item.razor`. If `Generic.razor` or `Ability.razor` appear, they were written wrong in Tasks 3 or 4; fix those first.

- [ ] **Step 3: Delete the Block enum**

In `src/FellowshipExtractor.Api/Models/TooltipView.cs`, delete this line and the blank line above it:

```csharp
    public enum Block { Identity, Facts, Rolls, Traits, Prose, Overflow, Granted, Legendaries, Sets }
```

- [ ] **Step 4: Delete Blocks from the interface**

In `src/FellowshipExtractor.Api/Services/TooltipViewService.cs`, delete this declaration from `ITooltipViewService`:

```csharp
    IReadOnlyList<TooltipView.Block> Blocks(
        EntityDocument document,
        TooltipQuery query,
        RarityEntry? rarity,
        IReadOnlyList<TooltipView.Fact> facts,
        IReadOnlyList<StatLine> rolls,
        IReadOnlyList<TooltipView.TraitLine> traits);
```

and change the `Rolls` declaration from:

```csharp
    IReadOnlyList<StatLine> Rolls(EntityDocument document, TooltipQuery query, RarityEntry? rarity);
```

to:

```csharp
    IReadOnlyList<StatLine> Rolls(ItemDocument document, TooltipQuery query, RarityEntry? rarity);
```

- [ ] **Step 5: Delete Blocks and HasKind from the service**

In the same file, delete the whole `public IReadOnlyList<TooltipView.Block> Blocks(...)` method body and the `private static bool HasKind(EntityDocument document) => ...` member.

Then change the implementation of `Rolls` from:

```csharp
    public IReadOnlyList<StatLine> Rolls(EntityDocument document, TooltipQuery query, RarityEntry? rarity) =>
        document is ItemDocument item
            ? StatMagnitudes.For(
                item,
                store.Vocabulary.StatCalculation,
                store.PrimaryAttributes,
                rarity,
                query.ItemLevel ?? store.DropItemLevel(item),
                query)
            : [];
```

to:

```csharp
    public IReadOnlyList<StatLine> Rolls(ItemDocument document, TooltipQuery query, RarityEntry? rarity) =>
        StatMagnitudes.For(
            document,
            store.Vocabulary.StatCalculation,
            store.PrimaryAttributes,
            rarity,
            query.ItemLevel ?? store.DropItemLevel(document),
            query);
```

Leave `DrawsState`, `Granted` and `LegendaryStats` exactly as they are. `EntityPage.razor:68` reads `DrawsState`.

- [ ] **Step 6: Run the whole suite**

Run:
```powershell
Set-Location G:\source\Fellowship_Data_Extractor.item-and-ability-cards
dotnet test src/FellowshipExtractor.Api.Tests/FellowshipExtractor.Api.Tests.csproj --nologo
```
Expected: PASS, the same count as Task 5 Step 6. A deletion that changes a test result means something still depended on it.

- [ ] **Step 7: Commit**

```bash
git add src/FellowshipExtractor.Api/Services/TooltipViewService.cs src/FellowshipExtractor.Api/Models/TooltipView.cs
git commit -m "Retire the block list the cards now state themselves"
```

---

### Task 7: Watch the card follow a knob

A dump is drawn by `HtmlRenderer` in one pass and says nothing about the interactive page. Every group renders at the same sequence number inside the loop, so adding and removing one moves a different component type through that position. This task is the only evidence that still holds.

**Files:**
- Create: none.

**Interfaces:**
- Consumes: the finished branch.
- Produces: a statement in the handoff of what was seen, or of what could not be confirmed.

- [ ] **Step 1: Start the app from the worktree**

Run in a tracked background shell:
```powershell
Set-Location G:\source\Fellowship_Data_Extractor.item-and-ability-cards
dotnet run --project src/FellowshipExtractor.Api/FellowshipExtractor.Api.csproj
```
Wait for the listening URL in the output.

- [ ] **Step 2: Open an item's entity page**

Navigate to `/entity/item/<id>`, using an id the dump wrote a file for, and confirm the tooltip draws with its rarity frame, stat rolls and name.

- [ ] **Step 3: Toggle a knob that adds a group**

In the `Knobs` panel, set `Upgrades` so the `Tempered X / Y` line appears, then clear it so the line goes away. Then do the same with a trait, which adds and removes the whole `Imbued Traits` group.

Expected: the card redraws each time, the rule count changes with the group count, and no group is left stale or duplicated.

- [ ] **Step 4: Stop the app**

Stop the background shell. Leave nothing running.

- [ ] **Step 5: Report**

State in the handoff what was toggled and what was seen. If the page could not be reached, say so plainly rather than implying it was checked.

---

## Self-review

**Spec coverage**

| Spec section | Task |
| --- | --- |
| The dispatch | 5 |
| The document each card reads | 2, 3, 4, 5 |
| The card | 2, 3, 4 |
| Item.razor | 2 |
| Ability.razor | 3 |
| Generic.razor | 4 |
| The service and the static | 6 |
| The styling | Global constraint: no root class, no partial, no `@use` |
| Tests | 2, 3, 4, 5 |
| Verification | 1, 5 (Step 7), 7 |

**Type consistency**

`Shown` is the cascading parameter on all five cards. `Entity` is the private typed reading in every card. `Groups()` returns `List<RenderFragment>` and the field is `groups`, of type `IReadOnlyList<RenderFragment>`, in all three new cards. `GetTooltipComponent` is named identically in the spec and in Task 5. `Rolls` takes `ItemDocument` from Task 6 onward and only `Item.razor` calls it.
