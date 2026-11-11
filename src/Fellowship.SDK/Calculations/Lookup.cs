namespace Fellowship.SDK.Calculations;

/// <summary>Outcome of resolving a type, an id and a hero to one reading.</summary>
public enum Lookup
{
    Found,
    NoSuchId,

    NeedsHero,

    NotForThatHero,
}

/// <summary>A reading the codex API refuses, with its problem title and detail.</summary>
public sealed record ReadingRefusal(string Title, string Detail);

/// <summary>Thrown for a reading the codex API refuses.</summary>
public sealed class RefusedReadingException(ReadingRefusal refusal) : ArgumentException(refusal.Detail)
{
    public ReadingRefusal Refusal { get; } = refusal;
}
