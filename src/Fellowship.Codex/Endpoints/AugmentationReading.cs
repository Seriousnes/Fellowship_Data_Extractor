using System.ComponentModel;

using Fellowship.SDK.Client;

using Fellowship.Codex.Models;

namespace Fellowship.Codex.Endpoints;

public static class AugmentationReading
{
    public const string ItemLevel =
        "The item level of each item carrying the enchant. It sets the ranks each rank source adds and the " +
        "stats the enchant grants. Defaults to the highest item level. Any of itemLevel, rankSources and rank " +
        "adds equipped to the record.";

    public const string RankSources =
        "The gear slots of the equipped items carrying the augmentation, comma separated, each an item type " +
        "such as Head or Necklace. The equipped rank is the sum of their ranks, up to the max rank. Stating " +
        "it adds equipped to the record.";

    public const string Rank =
        "The equipped rank to read the augmentation at, from 1, up to its max rank. Stating it adds equipped " +
        "to the record. It takes the place of rankSources.";

    public static RecordReading Of(string? hero, int? itemLevel, string? rankSources, int? rank) => new()
    {
        Hero = StatQuery.NullIfEmpty(hero),
        ItemLevel = itemLevel,
        RankSources = Models.RankSources.Parse(StatQuery.NullIfEmpty(rankSources)),
        Rank = rank,
    };
}

public sealed record AugmentationRecordRequest
{
    [Description("The id.")]
    public int Id { get; init; }

    [Description(AugmentationReading.RankSources)]
    public string? RankSources { get; init; }

    [Description(AugmentationReading.Rank)]
    public int? Rank { get; init; }

    public RecordReading ToReading() => AugmentationReading.Of(null, null, RankSources, Rank);
}

public sealed record AugmentationListRequest : RecordListRequest
{
    [Description(AugmentationReading.RankSources)]
    public string? RankSources { get; init; }

    [Description(AugmentationReading.Rank)]
    public int? Rank { get; init; }

    public override RecordReading ToReading() => AugmentationReading.Of(null, null, RankSources, Rank);
}
