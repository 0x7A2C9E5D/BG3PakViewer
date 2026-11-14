using System.Runtime.CompilerServices;
using Syncfusion.UI.Xaml.Grid;

namespace BG3PakViewer.Shared.Extensions;

/// <summary>
///     Releasing a grid exactly once, however many behaviors are torn down with it.
/// </summary>
public static class SfDataGridExtensions
{
    private static readonly object Marker = new();
    private static readonly ConditionalWeakTable<SfDataGrid, object> Released = [];

    /// <summary>
    ///     Disposes the search helper and the grid, ignoring later calls for the same grid.
    /// </summary>
    /// <param name="grid">The grid that is being torn down.</param>
    /// <remarks>
    ///     Unloading a grid runs the <c>Unloaded</c> handler of every behavior attached to it, and more
    ///     than one behavior releases the grid. <see cref="SfDataGrid.Dispose" /> is not idempotent, so
    ///     the second release throws inside the disposed internals.
    /// </remarks>
    public static void DisposeOnce(this SfDataGrid grid)
    {
        if (!Released.TryAdd(grid, Marker)) return;
        grid.SearchHelper.Dispose();
        grid.Dispose();
    }
}