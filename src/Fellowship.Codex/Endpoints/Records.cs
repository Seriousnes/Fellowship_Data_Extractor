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
        var asked = StatQuery.Given(hero);

        if (asked is not null && store.Hero(asked) is null)
        {
            return Lookups.NoSuchHero(asked, store.Settings.Heroes);
        }

        var outcome = store.Resolve(type, id, asked, out var document, out var wearer);

        return outcome == Lookup.Found
            ? Results.Ok(RecordProjection.Of<TRecord>(document!, view, Origins.Of(http)))
            : Lookups.Problem(outcome, type, id, wearer, store.All(type, id));
    }
}
