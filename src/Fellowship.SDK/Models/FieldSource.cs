namespace Fellowship.SDK.Models;

/// <summary>
/// Ability constants rows a document's scalar field is set from. <see cref="Kind"/> has the vocabulary of
/// <see cref="ResourceGain.SourceKind"/>. A <c>ConstantsRow</c> field has the value of its one row.
/// </summary>
public sealed record FieldSource(string Kind, IReadOnlyList<string> Rows)
{
    /// <summary>Keys of the document's <c>tuning</c> for <see cref="Rows"/>.</summary>
    public IReadOnlyList<string>? Tuning { get; init; }
}
