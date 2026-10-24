using Fellowship.SDK.Client;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public static class Records
{
    public static IResult One<TRecord>(
        int id,
        string? hero,
        HttpContext http,
        IEntityStoreService store,
        ICodexViewService view)
        where TRecord : EntityRecord =>
        One<TRecord>(id, new RecordReading { Hero = hero }, http, store, view, null);

    public static IResult One<TRecord>(
        int id,
        RecordReading reading,
        HttpContext http,
        IEntityStoreService store,
        ICodexViewService view,
        ITooltipViewService? tooltips)
        where TRecord : EntityRecord
    {
        var type = EntityRecords.TypeOf(typeof(TRecord));
        reading = reading with { Hero = StatQuery.NullIfEmpty(reading.Hero) };

        if (RecordReadings.Refused(reading, store) is { } refused) return refused;

        var outcome = store.Resolve(type, id, reading.Hero, out var document, out var wearer);

        if (outcome != Lookup.Found) return Lookups.Problem(outcome, type, id, wearer, store.All(type, id));

        var record = RecordProjection.Of<TRecord>(document!, view, Origins.Of(http));

        return Results.Ok(tooltips is null ? record : RecordReadings.Read(record, document!, reading, store, tooltips));
    }
}
