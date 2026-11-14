using System.Collections.ObjectModel;
using BG3PakViewer.Shared.Models;

namespace BG3PakViewer.Services;

/// <summary>
///     Builds the package tree shown in the main window from the paths of the loaded package.
/// </summary>
/// <remarks>
///     Pure projection: it neither reads nor owns the package, so the tree can be built on a worker
///     thread and published from the UI thread. A null path list stands for "no package is loaded".
/// </remarks>
internal static class PackageTreeBuilder
{
    /// <summary>
    ///     Builds the tree, keeping only the paths matching the tree search query when one is given.
    /// </summary>
    /// <param name="filePaths">Package paths, or null when no package is loaded.</param>
    /// <param name="searchQuery">The tree search box query.</param>
    /// <returns>The tree, or null when no package is loaded.</returns>
    public static ObservableCollection<PackageEntry>? Build(IEnumerable<string>? filePaths,
        string? searchQuery = null)
    {
        if (filePaths is null) return null;

        var paths = string.IsNullOrWhiteSpace(searchQuery)
            ? filePaths
            : filePaths.Where(path => path.Contains(searchQuery, StringComparison.OrdinalIgnoreCase));
        return PackageEntry.BuildTree(paths);
    }
}
