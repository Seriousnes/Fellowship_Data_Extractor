namespace Fellowship.SDK.Models;

/// <summary>
/// A set-by-caller magnitude an ability's Blueprint assigns as <see cref="Base"/> plus <see cref="Per"/> for each unit
/// of <see cref="Count"/>. Hemorrhaging Strike assigns <c>SetByCaller.Duration</c> as 12 plus 3 for each combo point
/// spent.
/// </summary>
/// <param name="Magnitude">The set-by-caller tag the value is assigned to.</param>
/// <param name="Count">
/// The quantity counted: the attribute read for a spent resource, such as <c>ResourcesSecond</c>, or
/// <c>SourceLevel</c> for the item level of the ability's source.
/// </param>
public sealed record SetByCallerScaling(string Magnitude, string Count, double Base, double Per)
{
    /// <summary>The units of <see cref="Count"/> one <see cref="Per"/> is for, such as 15 item levels.</summary>
    public double? Every { get; init; }

    /// <summary>
    /// The magnitude at each count from 1 to the owning hero's maximum of the counted attribute, such as
    /// <c>MaxResourcesSecond</c>.
    /// </summary>
    public IReadOnlyList<double>? Values { get; init; }
}
