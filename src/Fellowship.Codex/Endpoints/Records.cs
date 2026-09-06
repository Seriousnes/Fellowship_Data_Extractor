using Fellowship.SDK.Documents;
using Fellowship.SDK.Records;

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
        where TRecord : EntityRecord
    {
        var type = EntityRecords.TypeOf(typeof(TRecord));
        var request = StatQuery.NullIfEmpty(hero);

        if (request is not null && store.Hero(request) is null)
        {
            return Lookups.NoSuchHero(request, store.Settings.Heroes);
        }

        var outcome = store.Resolve(type, id, request, out var document, out var wearer);

        return outcome == Lookup.Found
            ? Results.Ok(RecordProjection.Of<TRecord>(document!, view, Origins.Of(http)))
            : Lookups.Problem(outcome, type, id, wearer, store.All(type, id));
    }
}
