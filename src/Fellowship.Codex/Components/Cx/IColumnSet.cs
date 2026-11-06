namespace Fellowship.Codex.Components.Cx;

/// <summary>A component that a <see cref="Column{T}" /> registers with.</summary>
public interface IColumnSet<T>
{
    void Add(Column<T> column);

    void Remove(Column<T> column);
}
