# Item and ability tooltip design

**Goal:** An item, and an ability or effect, are each drawn by a card of its own beside
`Blessing.razor` and `Trait.razor`. `Tooltip.razor` becomes the one place that pairs a document with
the card that draws it, and every card writes the same bytes the tooltip writes today.

## The dispatch

`Tooltip.razor` keeps `Entity`, `Query` and `Hero` as its parameters, so `EntityPage.razor` and the
`/api/tooltip/{type}/{id}`, `/api/item/{id}` and `/api/ability/{id}` endpoints reach it unchanged.
It names the card in a switch expression over the document:

```csharp
private static Type GetTooltipComponent(EntityDocument document) => document switch
{
    BlessingDocument => typeof(Blessing),
    TraitDocument => typeof(Trait),
    ItemDocument => typeof(Item),
    AbilityDocument or EffectDocument => typeof(Ability),
    _ => typeof(Generic),
};
```

and renders it:

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
}
```

`/api/ability/1234` and `/api/effect/1234` stay separate routes over separate types. Only the card
they land on is shared.

`CascadingValue` and `DynamicComponent` each render what they wrap and nothing of their own, so the
served fragment still opens on the card's own root element.

`Tooltip.razor` holds nothing else: no rarity, art, level, temper, facts, rolls, traits, granted
name or sheet, and no `OnParametersSet`.

## The document each card reads

Every card takes the cascaded document and names the type it draws:

```csharp
[CascadingParameter] public EntityDocument Shown { get; set; } = default!;

[Parameter] public TooltipQuery Query { get; set; } = new();

[Parameter] public HeroEntry? Hero { get; set; }

private ItemDocument Entity => (ItemDocument)Shown;
```

`CascadingValue<EntityDocument>` supplies by its declared type, so `Shown` is the base type and each
card states its own. `GetTooltipComponent` is the only thing that pairs a document with a card, so
it is the only place that has to agree with these casts, and `CardTests` renders one document of
every type through `Tooltip` to hold it to that.

`Blessing.razor` and `Trait.razor` move to this shape. Their markup is untouched, because `Entity`
still reads as `BlessingDocument` and `TraitDocument`. `Ability.razor` reads
`private EntityDocument Entity => Shown;`, because it draws two document types.

One dictionary reaches whichever card is named, so all five declare `Query` and `Hero`.

The `Tip*` components the groups name are unchanged, and each keeps taking the `EntityDocument`,
`TooltipQuery` and `HeroEntry?` it takes today.

## The card

Each card owns its whole window, the way `Blessing.razor` and `Trait.razor` do:

```razor
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
```

`<hr class="rule">` sits between drawn groups, so each card builds its groups as an ordered
`List<RenderFragment>` in `OnParametersSet` and the loop puts the rules in. The list is the card's
own; nothing is shared between cards but the `Tip*` components the fragments name.

```csharp
private List<RenderFragment> Groups()
{
    var groups = new List<RenderFragment>(8);

    if (Identity) groups.Add(@<TipIdentity Entity="Entity" Query="Query" Rung="Rung" Level="Level" Temper="@Temper" />);
    if (facts.Count > 0) groups.Add(@<TipFacts Items="facts" />);

    return groups;
}
```

Each card computes in `OnParametersSet` only what its own groups ask for, and sets
`Sheet = new MarkupString(Ui.Style())`.

## Item.razor

`src/FellowshipExtractor.Api/Components/Tooltip/Item.razor`, beside `Blessing.razor` and
`Trait.razor`. It takes an `ItemDocument`, so every guard reads a typed property.

| group | guard |
| --- | --- |
| `TipIdentity` | `ArmorType is not null`, `Slot is not null`, `Level is not null`, `Rung is not null`, `Temper is not null`, `GemSlot == true`, `UniqueEquipped == true` |
| `TipFacts` | `facts.Count > 0` |
| `TipRolls` | `Ui.Potential(Query) is not null \|\| rolls.Count > 0` |
| `TipTraits` | `traits.Count > 0 \|\| Query.AllocatedTraits is not null \|\| Query.MaxTraits is not null` |
| `TipProse` | `Description is not null` |
| `TipGranted` | `Ui.Granted(Entity)` carries a `Name` or a description |
| `TipLegendaries` | `Ui.LegendaryStats(Entity).Count > 0` |
| `TipSets` | `SetBonus is { Count: > 0 }` |

`Ui.Granted(Entity)` is read once into a field, which both the `TipGranted` guard and the `Named`
the badge prints are taken from.

## Ability.razor

`src/FellowshipExtractor.Api/Components/Tooltip/Ability.razor` draws an `AbilityDocument` or an
`EffectDocument` and carries five groups: `TipIdentity`, `TipFacts`, `TipRolls`, `TipTraits`,
`TipProse`.

`TipIdentity` and `TipRolls` are asked for by the query rather than by the document: an ability
carries no slot, kind, gem socket or stat roll, so those two are drawn when `?itemLevel=`,
`?rarity=`, `?upgrades=` or `?potentialPower=` says so. `/api/ability/1234?rarity=epic` draws the
rarity frame, header and ornament for that rung, because `Rung` is `Ui.Rarity(Entity, Query)` and
the root carries `plain` only while that reads null.

The `TipProse` guard reads `DocumentFields.Text(Shown, nameof(AbilityDocument.Description))` and
`DocumentFields.Words(Shown, nameof(AbilityDocument.Detail))`, which is how `TipProse` itself reads
the document, so the guard and the drawing cannot disagree. `EffectDocument` carries no `Detail`,
which those reads answer for.

## Generic.razor

`src/FellowshipExtractor.Api/Components/Tooltip/Generic.razor` draws every type without a card of
its own: companion pets, creatures, currencies, gems, gem traits, gem types, legendary powers,
legendary stats, mounts, mount powers, relic powers, set bonuses, set bonus powers, talents, talent
slots, trait powers and weapon powers. Its name is the build's, from
`ui/tooltips/UW_GenericItemTooltip`, the widget that stands beside the specialised
`UW_ItemTooltip`.

It carries six groups: `TipIdentity`, `TipFacts`, `TipRolls`, `TipTraits`, `TipProse`,
`TipOverflow`.

- `TipIdentity` is drawn for a `GemDocument`, `GemTraitDocument` or `GemTypeDocument`, which carry a
  kind, and for any document the query gives a level, a rarity or a temper to. A currency and a gem
  carry a rarity of their own, so `Rung` is `Ui.Rarity(Entity, Query)` and the rarity chrome stays
  on this card.
- `TipRolls` is drawn for `Ui.Potential(Query)` alone.
- `TipOverflow` is drawn for a `GemTypeDocument` carrying an `OverflowDescription`.

## TooltipView

`TooltipView` keeps `Rarity`, `Variables`, `Art`, `Ornament`, `Rung`, `Level`, `Temper`, `Facts`,
`Potential`, `Traits`, `Rolls`, `Magnitude`, `Text`, `Style`, `Granted` and `LegendaryStats`, which
the cards ask it for.

`Block`, `Blocks` and `HasKind` go, and the guards they held are the ones each card now states.
`Rolls` narrows from `EntityDocument` to `ItemDocument`, `Item.razor` being its one caller.
`Granted` and `LegendaryStats` keep taking an `EntityDocument`, because `Sections/Grants.razor` and
`Sections/Legendaries.razor` call them with one.

## The styling

The cards carry the class names the sheet already styles and no root class of their own, so
`Components/Tooltip/_Tooltip.razor.scss` and `Styles/tooltip.scss` are untouched.

## Tests

- `CardTests` asserts `Tooltip` draws each type through the card that names it: an item through
  `Item`, an ability and an effect through `Ability`, a blessing through `Blessing`, a trait through
  `Trait`, and one document of every remaining type through `Generic`. The list of types it walks is
  `Enum.GetValues<EntityType>()`, so a type added later is covered without the test being edited.
- `ItemCardTests` asserts the card draws its art in `figure.slot` beside the window, that the rarity
  frame, header and ornament are asked for by the rung, that an item carrying a granted power draws
  the ability badge with that power's name, that its legendary stats and set bonus each draw their
  own group, and that a rule sits between every pair of groups and never above the first.
- `AbilityCardTests` asserts an ability and an effect draw the same card, that the facts row carries
  the cast, cooldown and cost the document states, that a bare ability draws no identity group, and
  that `?rarity=` puts the rarity chrome on it.
- `TooltipTests`, `InsetArtTests`, `StateTests`, `SheetTests`, `BlessingCardTests` and
  `TraitCardTests` render `Tooltip` and pass unchanged.

## Verification

Hand-written assertions confirm the cards exist. The evidence that nothing changed is a diff of the
served markup over the corpus.

`TooltipDumpTests` writes one file per case to the folder `FELLOWSHIP_TIP_DUMP` names, and is
skipped when that variable is unset. Every input is read from the store rather than written into the
test: the first three ids of each `EntityType`, ordered by id, each drawn under a bare query and
under `?rarity=`, `?itemLevel=&upgrades=`, `?traits=`, `?potentialPower=` and `?hero=`, whose values
come from `store.Vocabulary` and the first trait id the build carries.

Run it on `a74e348`, the commit this work starts from, run it again on the finished branch, and diff
the two folders. An empty diff is what makes this an extraction: no texture is composited
differently, so the screenshot pass the chrome work needs is not owed here. Any file that does
differ is either a defect or a change that has to be argued for on its own.
