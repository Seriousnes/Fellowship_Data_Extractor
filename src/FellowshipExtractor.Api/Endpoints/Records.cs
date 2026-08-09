using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public static class Records
{
    public static IResult One(
        EntityType type, int id, string? hero, IEntityStoreService store, ICodexViewService view)
    {
        var asked = StatQuery.Given(hero);

        if (asked is not null && store.Hero(asked) is null)
        {
            return Lookups.NoSuchHero(asked, store.Settings.Heroes);
        }

        var outcome = store.Resolve(type, id, asked, out var document, out var wearer);

        return outcome == Lookup.Found
            ? Results.Ok(PublicEntity.From(document!, view))
            : Lookups.Problem(outcome, type, id, wearer, store.All(type, id));
    }
}
