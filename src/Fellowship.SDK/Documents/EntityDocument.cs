namespace Fellowship.SDK.Documents;

/// <summary>
/// One row as a reader meets it. The runtime class is the row's type, so nothing here restates it.
///
/// Two fields, because two are what every type has. A row the extractor cannot read yet carries the
/// id and nothing else, which is what makes the extraction worklist countable.
/// </summary>
public abstract class EntityDocument
{
    public required int Id { get; init; }

    /// <summary>The name a reader is shown, and null where the build prints none they would read.</summary>
    public string? Name { get; init; }
}
