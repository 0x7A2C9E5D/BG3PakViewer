using System.ComponentModel;
using Microsoft.Extensions.DependencyModel;
using Syncfusion.Data;

namespace BG3PakViewer.Dialogs.Comparers;

internal class RuntimeLibraryVersionComparer : IComparer<object>, ISortDirection
{
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

    public ListSortDirection SortDirection { get; set; }
}