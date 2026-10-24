namespace Fellowship.SDK.Records;

public sealed record RecordPage<TRecord>(int Total, int Page, int Pages, IReadOnlyList<TRecord> Rows)
    where TRecord : EntityRecord;

public sealed record StatMagnitudeRecord(string? Stat, decimal Value, string Kind);

public sealed record GrantedStatRecord(string Stat, decimal Value);
