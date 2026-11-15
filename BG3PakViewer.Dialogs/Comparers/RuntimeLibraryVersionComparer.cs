using System.ComponentModel;
using Microsoft.Extensions.DependencyModel;
using Syncfusion.Data;

namespace BG3PakViewer.Dialogs.Comparers;

/// <summary>
///     Comparer for runtime library versions.
/// </summary>
internal class RuntimeLibraryVersionComparer : IComparer<object>, ISortDirection
{
    /// <summary>
    ///     Compare two runtime library versions.
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    public int Compare(object? x, object? y)
    {
        var comparisonResult = CompareVersions(x as RuntimeLibrary, y as RuntimeLibrary);
        // The direction has to be applied to the fallback too: otherwise the versions that cannot be
        // parsed as a Version stay ascending while the rest of the column is descending.
        return SortDirection == ListSortDirection.Descending ? -comparisonResult : comparisonResult;
    }

    /// <summary>
    ///     The sort direction.
    /// </summary>
    public ListSortDirection SortDirection { get; set; }

    /// <summary>
    ///     Compares the versions of two runtime libraries.
    /// </summary>
    /// <param name="x">The first runtime library.</param>
    /// <param name="y">The second runtime library.</param>
    /// <returns>A negative value, zero or a positive value, in ascending order.</returns>
    private static int CompareVersions(RuntimeLibrary? x, RuntimeLibrary? y)
    {
        var versionStringX = x?.Version;
        var versionStringY = y?.Version;

        // A row without a runtime library or without a version has nothing to parse and sorts as the
        // smallest value; the grid does hand out null for a missing row.
        if (versionStringX is null || versionStringY is null)
            return string.Compare(versionStringX, versionStringY, StringComparison.Ordinal);

        // Versions such as "1.0.0-beta" are not a System.Version, so they fall back to the text order.
        return Version.TryParse(versionStringX, out var versionX)
               && Version.TryParse(versionStringY, out var versionY)
            ? versionX.CompareTo(versionY)
            : string.Compare(versionStringX, versionStringY, StringComparison.Ordinal);
    }
}
