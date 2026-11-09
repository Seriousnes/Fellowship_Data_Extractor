using Microsoft.AspNetCore.Components;

using Fellowship.Codex.Components.Cx;
using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;

namespace Fellowship.Codex.Components.Browse;

/// <summary>
/// One sortable column for each measure in the comparisons of the list. The columns are a fragment so that they
/// register with the table in the same pass as the other columns.
/// </summary>
public static class ComparedColumns
{
    public static RenderFragment For<T>(BrowseView view, IEnumerable<T> rows) where T : EntityDocument => builder =>
    {
        foreach (var measure in view.Compared)
        {
            builder.OpenComponent<Column<T>>(0);
            builder.SetKey(measure.Key);
            builder.AddAttribute(1, nameof(Column<T>.Head), measure.Label);
            builder.AddAttribute(2, nameof(Column<T>.Key), (SortColumn?)SortColumn.Of(measure));
            builder.AddAttribute(3, nameof(Column<T>.Unit), measure.Unit);
            builder.AddAttribute(4, nameof(Column<T>.Numeric), true);
            builder.AddAttribute(5, nameof(Column<T>.Value), (Func<T, object?>)(row => measure.Value(row)));
            builder.CloseComponent();
        }
    };
}
