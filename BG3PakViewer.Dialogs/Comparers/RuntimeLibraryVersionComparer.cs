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
        var versionStringX = ((RuntimeLibrary)x!).Version;
        var versionStringY = ((RuntimeLibrary)y!).Version;

        try
        {
            var versionX = new Version(versionStringX);
            var versionY = new Version(versionStringY);

            var comparisonResult = versionX.CompareTo(versionY);
            return SortDirection == ListSortDirection.Descending
                ? -comparisonResult
                : comparisonResult;
        }
        catch (Exception)
        {
            return string.Compare(versionStringX, versionStringY,
                StringComparison.Ordinal);
        }
    }

    /// <summary>
    ///     The sort direction.
    /// </summary>
    public ListSortDirection SortDirection { get; set; }
}