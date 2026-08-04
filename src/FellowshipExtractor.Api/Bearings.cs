using FellowshipExtractor.Tooltip;

namespace FellowshipExtractor.Api;

/// <summary>
/// What the entity page knows about the address it is answering, cascaded to the sections that draw
/// its own chrome. Every address in it is built once by the page: a nested row carries no table and
/// no id of its own, so a section that computed one would link nowhere on half the records it is
/// given.
/// </summary>
public sealed record Bearings
{
    /// <summary>The reading on screen, which is the asked-for hero's or the first one read.</summary>
    public Entity Shown { get; init; } = new();

    /// <summary>Every reading of this id, in the order the export was written.</summary>
    public IReadOnlyList<Entity> Readings { get; init; } = [];

    /// <summary>
    /// The hero whose reading is shown, or null when the tooltip is nobody's in particular. A hero
    /// asked for who cannot read this id is not one, so this is the hero of the reading drawn.
    /// </summary>
    public string? Reading { get; init; }

    /// <summary>The tooltip address, complete with any query the page wants carried into it.</summary>
    public string Address { get; init; } = string.Empty;

    /// <summary>This row's own page, which the version rosters hang their hero query off.</summary>
    public string Page { get; init; } = string.Empty;

    /// <summary>The browser, narrowed to this row's type.</summary>
    public string Under { get; init; } = string.Empty;

    /// <summary>The word this row's type is published under.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>What one row of this type is called, for a sentence that counts them.</summary>
    public string Noun { get; init; } = string.Empty;

    public int NetId { get; init; }

    /// <summary>Where else a reader can be sent from here, already labelled and counted.</summary>
    public IReadOnlyList<(string Label, string Href, int Count)> Related { get; init; } = [];
}
